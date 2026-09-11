using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Phases;
using Rebellion.AI.Planners;
using Rebellion.AI.Planners.Demand;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Generation;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

public static partial class HeadlessSimulationRunner
{
    [Serializable]
    private sealed class SpecialForcesLifecycleSimulationSummary
    {
        public string TypeId;
        public int InitialCount;
        public int CreatedCount;
        public int RemovedCount;
        public int FinalCount;
        public int SampleCount;
        public double AverageAvailableCount;
        public double AverageMissionCount;
        public double AverageTransitCount;
        public double AverageBuildingCount;
        public int MinimumAvailableCount;
        public int MaximumAvailableCount;
        public int ZeroAvailableTicks;
    }

    private sealed class SpecialForcesLifecycleTracker
    {
        // Availability changes slowly relative to a mission lifecycle, so a 25-tick sample keeps
        // long-run utilization representative without adding a scene traversal to every tick.
        public const int SampleInterval = 25;

        private readonly Dictionary<string, TrackedSpecialForces> _units = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, SpecialForcesLifecycleCounts> _counts = new(
            StringComparer.Ordinal
        );
        private int _lastAvailabilitySampleTick = int.MinValue;

        /// <summary>
        /// Records the initial special-forces inventory and availability.
        /// </summary>
        /// <param name="game">The initial game state.</param>
        /// <param name="specialForces">The shared initial special-forces snapshot.</param>
        public void RecordInitialState(
            GameRoot game,
            IReadOnlyCollection<SpecialForces> specialForces
        )
        {
            _units.Clear();
            _counts.Clear();
            foreach (SpecialForces unit in specialForces)
            {
                TrackedSpecialForces tracked = TrackedSpecialForces.From(unit);
                _units[tracked.InstanceId] = tracked;
                GetCounts(tracked.OwnerInstanceId, tracked.TypeId).InitialCount++;
            }

            RecordAvailability(game, specialForces);
        }

        /// <summary>
        /// Records special-forces creation and removal from resolved lifecycle results.
        /// </summary>
        /// <param name="results">The resolved game results.</param>
        public void Record(IReadOnlyList<GameResult> results)
        {
            if (results == null)
                return;

            foreach (GameObjectDeployedResult result in results.OfType<GameObjectDeployedResult>())
            {
                if (
                    result.GameObject is not SpecialForces unit
                    || _units.ContainsKey(unit.InstanceID)
                )
                    continue;

                TrackedSpecialForces tracked = TrackedSpecialForces.From(unit);
                _units[tracked.InstanceId] = tracked;
                GetCounts(tracked.OwnerInstanceId, tracked.TypeId).CreatedCount++;
            }

            foreach (
                GameObjectDestroyedResult result in results.OfType<GameObjectDestroyedResult>()
            )
            {
                if (
                    result.DestroyedObject is not SpecialForces unit
                    || !_units.Remove(unit.InstanceID, out TrackedSpecialForces tracked)
                )
                    continue;

                GetCounts(tracked.OwnerInstanceId, tracked.TypeId).RemovedCount++;
            }
        }

        /// <summary>
        /// Samples special-forces availability at the documented coarse interval.
        /// </summary>
        /// <param name="game">The current game state.</param>
        public void RecordSample(GameRoot game)
        {
            RecordAvailability(game, game.GetSceneNodesByType<SpecialForces>().ToList());
        }

        /// <summary>
        /// Records the final availability sample when the simulation does not end on a regular
        /// sampling tick.
        /// </summary>
        /// <param name="game">The completed game state.</param>
        public void RecordFinalState(GameRoot game)
        {
            if (game.CurrentTick != _lastAvailabilitySampleTick)
                RecordAvailability(game, game.GetSceneNodesByType<SpecialForces>().ToList());
        }

        /// <summary>
        /// Builds lifecycle summaries for one faction in deterministic type order.
        /// </summary>
        /// <param name="factionId">The faction instance identifier.</param>
        /// <returns>The recorded lifecycle summaries.</returns>
        public SpecialForcesLifecycleSimulationSummary[] BuildSummary(string factionId)
        {
            return _counts
                .Where(entry => entry.Value.OwnerInstanceId == factionId)
                .OrderBy(entry => entry.Value.TypeId, StringComparer.Ordinal)
                .Select(entry => entry.Value.BuildSummary())
                .ToArray();
        }

        /// <summary>
        /// Samples the current state of every known special-forces type.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="specialForces">The shared current special-forces snapshot.</param>
        private void RecordAvailability(
            GameRoot game,
            IReadOnlyCollection<SpecialForces> specialForces
        )
        {
            _lastAvailabilitySampleTick = game.CurrentTick;
            foreach (Faction faction in game.GetFactions())
            {
                HashSet<string> availableIds = faction
                    .GetAvailableMissionParticipants()
                    .OfType<SpecialForces>()
                    .Select(unit => unit.InstanceID)
                    .ToHashSet(StringComparer.Ordinal);
                List<SpecialForces> factionUnits = specialForces
                    .Where(unit => unit.OwnerInstanceID == faction.InstanceID)
                    .ToList();

                IEnumerable<string> typeIds = faction
                    .GetUnlockedTechnologies(ManufacturingType.Troop)
                    .Select(technology => technology.GetReference())
                    .OfType<SpecialForces>()
                    .Select(template => template.GetTypeID())
                    .Concat(factionUnits.Select(unit => unit.GetTypeID()))
                    .Distinct(StringComparer.Ordinal);

                foreach (string typeId in typeIds)
                {
                    List<SpecialForces> units = factionUnits
                        .Where(unit => unit.GetTypeID() == typeId)
                        .ToList();
                    GetCounts(faction.InstanceID, typeId)
                        .RecordSample(
                            units.Count(unit => availableIds.Contains(unit.InstanceID)),
                            units.Count(unit => unit.IsOnMission()),
                            units.Count(unit => unit.Movement != null),
                            units.Count(unit =>
                                unit.ManufacturingStatus != ManufacturingStatus.Complete
                            ),
                            units.Count
                        );
                }
            }
        }

        /// <summary>
        /// Gets or creates the lifecycle counters for one faction and special-forces type.
        /// </summary>
        /// <param name="factionId">The owning faction instance identifier.</param>
        /// <param name="typeId">The special-forces type identifier.</param>
        /// <returns>The counters associated with the faction and unit type.</returns>
        private SpecialForcesLifecycleCounts GetCounts(string factionId, string typeId)
        {
            string key = $"{factionId ?? string.Empty}\u001f{typeId ?? string.Empty}";
            if (!_counts.TryGetValue(key, out SpecialForcesLifecycleCounts counts))
            {
                counts = new SpecialForcesLifecycleCounts(factionId, typeId);
                _counts[key] = counts;
            }

            return counts;
        }

        private sealed class SpecialForcesLifecycleCounts
        {
            public readonly string OwnerInstanceId;
            public readonly string TypeId;
            public int InitialCount;
            public int CreatedCount;
            public int RemovedCount;
            private int _sampleCount;
            private int _availableTotal;
            private int _missionTotal;
            private int _transitTotal;
            private int _buildingTotal;
            private int _finalCount;
            private int _minimumAvailable = int.MaxValue;
            private int _maximumAvailable;
            private int _zeroAvailableTicks;

            /// <summary>
            /// Creates lifecycle counters for one faction and special-forces type.
            /// </summary>
            /// <param name="ownerInstanceId">The owning faction instance identifier.</param>
            /// <param name="typeId">The special-forces type identifier.</param>
            public SpecialForcesLifecycleCounts(string ownerInstanceId, string typeId)
            {
                OwnerInstanceId = ownerInstanceId;
                TypeId = typeId;
            }

            /// <summary>
            /// Records one sampled distribution of the tracked special-forces type.
            /// </summary>
            /// <param name="available">The units available for new missions.</param>
            /// <param name="onMission">The units assigned to missions.</param>
            /// <param name="inTransit">The units currently traveling.</param>
            /// <param name="building">The units currently under construction.</param>
            /// <param name="total">The total units present in the sample.</param>
            public void RecordSample(
                int available,
                int onMission,
                int inTransit,
                int building,
                int total
            )
            {
                _sampleCount++;
                _availableTotal += available;
                _missionTotal += onMission;
                _transitTotal += inTransit;
                _buildingTotal += building;
                _finalCount = total;
                _minimumAvailable = Math.Min(_minimumAvailable, available);
                _maximumAvailable = Math.Max(_maximumAvailable, available);
                if (available == 0)
                    _zeroAvailableTicks++;
            }

            /// <summary>
            /// Builds the simulation summary represented by the accumulated samples.
            /// </summary>
            /// <returns>The completed lifecycle summary.</returns>
            public SpecialForcesLifecycleSimulationSummary BuildSummary()
            {
                return new SpecialForcesLifecycleSimulationSummary
                {
                    TypeId = TypeId,
                    InitialCount = InitialCount,
                    CreatedCount = CreatedCount,
                    RemovedCount = RemovedCount,
                    FinalCount = _finalCount,
                    SampleCount = _sampleCount,
                    AverageAvailableCount = Divide(_availableTotal, _sampleCount),
                    AverageMissionCount = Divide(_missionTotal, _sampleCount),
                    AverageTransitCount = Divide(_transitTotal, _sampleCount),
                    AverageBuildingCount = Divide(_buildingTotal, _sampleCount),
                    MinimumAvailableCount =
                        _minimumAvailable == int.MaxValue ? 0 : _minimumAvailable,
                    MaximumAvailableCount = _maximumAvailable,
                    ZeroAvailableTicks = _zeroAvailableTicks,
                };
            }

            /// <summary>
            /// Calculates a sampled average while handling an empty sample set.
            /// </summary>
            /// <param name="value">The accumulated sample value.</param>
            /// <param name="count">The number of samples.</param>
            /// <returns>The average value, or zero when no samples were recorded.</returns>
            private static double Divide(int value, int count) =>
                count == 0 ? 0 : (double)value / count;
        }

        private sealed class TrackedSpecialForces
        {
            public string InstanceId;
            public string OwnerInstanceId;
            public string TypeId;

            /// <summary>
            /// Captures the stable identity fields needed to track a special-forces unit.
            /// </summary>
            /// <param name="unit">The unit to snapshot.</param>
            /// <returns>The tracked identity snapshot.</returns>
            public static TrackedSpecialForces From(SpecialForces unit)
            {
                return new TrackedSpecialForces
                {
                    InstanceId = unit.InstanceID,
                    OwnerInstanceId = unit.OwnerInstanceID,
                    TypeId = unit.GetTypeID(),
                };
            }
        }
    }

}
