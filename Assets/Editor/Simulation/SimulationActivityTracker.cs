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
    private sealed class ActivityTracker
    {
        private readonly Dictionary<string, TrackedMission> _activeMissions = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, PlanetState> _planetStates = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, FactionActivity> _factionActivities = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, ResearchOrders> _researchOrders = new(
            StringComparer.Ordinal
        );

        public ISet<string> AbductionTargetIds =>
            _activeMissions
                .Values.Select(mission => mission.OfficerTargetId)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet(StringComparer.Ordinal);

        /// <summary>
        /// Records the missions, ownership, and research state at simulation start.
        /// </summary>
        /// <param name="game">The initial game state.</param>
        public void RecordInitialState(GameRoot game)
        {
            foreach (Mission mission in game.GetSceneNodesByType<Mission>())
            {
                TrackedMission trackedMission = TrackedMission.From(mission, game);
                trackedMission.InitialIntelTick = GetIntelTick(
                    game,
                    trackedMission.FactionId,
                    trackedMission.PlanetId
                );
                _activeMissions[mission.InstanceID] = trackedMission;
            }

            foreach (Planet planet in game.GetSceneNodesByType<Planet>())
                _planetStates[planet.InstanceID] = PlanetState.From(planet);

            foreach (Faction faction in game.GetFactions())
                _researchOrders[faction.InstanceID] = ResearchOrders.From(faction);
        }

        /// <summary>
        /// Records mission, ownership, and research changes for one simulation tick.
        /// </summary>
        /// <param name="game">The current game state.</param>
        public void RecordTick(GameRoot game)
        {
            RecordMissions(game);
            RecordPlanetOwnership(game);
            RecordResearch(game);
        }

        /// <summary>
        /// Builds the recorded strategic activity summary for one faction.
        /// </summary>
        /// <param name="faction">The faction to summarize.</param>
        /// <returns>The faction activity summary.</returns>
        public FactionActivitySummary BuildSummary(Faction faction)
        {
            FactionActivity activity = GetActivity(faction.InstanceID);
            ResearchOrders orders = ResearchOrders.From(faction);
            return new FactionActivitySummary
            {
                Missions = activity
                    .MissionCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new MissionActivitySummary
                    {
                        MissionTypeId = pair.Key,
                        Started = pair.Value.Started,
                        OfficerLedHostileStarted = pair.Value.OfficerLedHostileStarted,
                        OfficerLedHostileStartedWithDecoy =
                            pair.Value.OfficerLedHostileStartedWithDecoy,
                        OfficerLedHostileStartedWithSpecialForcesDecoy =
                            pair.Value.OfficerLedHostileStartedWithSpecialForcesDecoy,
                        Ended = pair.Value.Ended,
                        Active = _activeMissions.Values.Count(mission =>
                            mission.FactionId == faction.InstanceID
                            && mission.MissionTypeId == pair.Key
                        ),
                    })
                    .ToArray(),
                MissionTargets = activity
                    .MissionTargetCounts.Values.OrderBy(counts => counts.MissionTypeId)
                    .ThenBy(counts => counts.PlanetId)
                    .Select(counts => new MissionTargetActivitySummary
                    {
                        MissionTypeId = counts.MissionTypeId,
                        PlanetId = counts.PlanetId,
                        Started = counts.Started,
                        Ended = counts.Ended,
                        IntelRefreshes = counts.IntelRefreshes,
                        EarlyInterruptions = counts.EarlyInterruptions,
                        ArrivalInterruptions = counts.ArrivalInterruptions,
                        MinimumMainRating =
                            counts.MainRatingSamples > 0 ? counts.MinimumMainRating : 0,
                        MaximumMainRating = counts.MaximumMainRating,
                        AverageMainRating =
                            counts.MainRatingSamples > 0
                                ? (double)counts.MainRatingTotal / counts.MainRatingSamples
                                : 0,
                        Active = _activeMissions.Values.Count(mission =>
                            mission.FactionId == faction.InstanceID
                            && mission.MissionTypeId == counts.MissionTypeId
                            && mission.PlanetId == counts.PlanetId
                        ),
                    })
                    .ToArray(),
                SabotageTargets = activity
                    .SabotageTargetCounts.Values.OrderBy(counts => counts.PlanetId)
                    .ThenBy(counts => counts.TargetId)
                    .Select(counts => new SabotageTargetActivitySummary
                    {
                        TargetId = counts.TargetId,
                        PlanetId = counts.PlanetId,
                        TargetType = counts.TargetType,
                        Started = counts.Started,
                        Ended = counts.Ended,
                        Destroyed = counts.Destroyed,
                    })
                    .ToArray(),
                PlanetsAcquired = activity.PlanetsAcquired,
                PlanetsLost = activity.PlanetsLost,
                PlanetsColonized = activity.PlanetsColonized,
                ShipResearchAdvances = activity.ShipResearchAdvances,
                FacilityResearchAdvances = activity.FacilityResearchAdvances,
                TroopResearchAdvances = activity.TroopResearchAdvances,
                FinalShipResearchOrder = orders.Ship,
                FinalFacilityResearchOrder = orders.Facility,
                FinalTroopResearchOrder = orders.Troop,
            };
        }

        /// <summary>
        /// Records mission starts, completions, targets, and observed outcomes.
        /// </summary>
        /// <param name="game">The current game state.</param>
        private void RecordMissions(GameRoot game)
        {
            Dictionary<string, TrackedMission> currentMissions = _planetStates
                .Keys.Select(planetId => game.GetSceneNodeByInstanceID<Planet>(planetId))
                .Where(planet => planet != null)
                .SelectMany(planet => planet.GetChildren<Mission>())
                .ToDictionary(
                    mission => mission.InstanceID,
                    mission => TrackedMission.From(mission, game)
                );

            foreach (TrackedMission mission in currentMissions.Values)
            {
                if (
                    _activeMissions.TryGetValue(
                        mission.InstanceId,
                        out TrackedMission previousMission
                    )
                )
                    mission.InitialIntelTick = previousMission.InitialIntelTick;
                else
                    mission.InitialIntelTick = GetIntelTick(
                        game,
                        mission.FactionId,
                        mission.PlanetId
                    );
            }

            foreach (
                TrackedMission mission in currentMissions.Values.Where(mission =>
                    !_activeMissions.ContainsKey(mission.InstanceId)
                )
            )
            {
                MissionCounts missionCounts = GetMissionCounts(mission);
                missionCounts.Started++;
                if (mission.OfficerLedHostile)
                {
                    missionCounts.OfficerLedHostileStarted++;
                    if (mission.HasDecoy)
                        missionCounts.OfficerLedHostileStartedWithDecoy++;
                    if (mission.HasSpecialForcesDecoy)
                        missionCounts.OfficerLedHostileStartedWithSpecialForcesDecoy++;
                }
                MissionTargetCounts targetCounts = GetMissionTargetCounts(mission);
                targetCounts.Started++;
                targetCounts.MainRatingTotal += mission.MainRating;
                targetCounts.MainRatingSamples++;
                targetCounts.MinimumMainRating = Math.Min(
                    targetCounts.MinimumMainRating,
                    mission.MainRating
                );
                targetCounts.MaximumMainRating = Math.Max(
                    targetCounts.MaximumMainRating,
                    mission.MainRating
                );
                SabotageTargetCounts sabotageCounts = GetSabotageTargetCounts(mission);
                if (sabotageCounts != null)
                    sabotageCounts.Started++;
            }

            foreach (
                TrackedMission mission in _activeMissions.Values.Where(mission =>
                    !currentMissions.ContainsKey(mission.InstanceId)
                )
            )
            {
                GetMissionCounts(mission).Ended++;
                MissionTargetCounts targetCounts = GetMissionTargetCounts(mission);
                targetCounts.Ended++;
                RecordEspionageOutcome(game, mission, targetCounts);
                SabotageTargetCounts sabotageCounts = GetSabotageTargetCounts(mission);
                if (sabotageCounts == null)
                    continue;

                sabotageCounts.Ended++;
                if (game.GetSceneNodeByInstanceID<ISceneNode>(mission.TargetId) == null)
                    sabotageCounts.Destroyed++;
            }

            _activeMissions.Clear();
            foreach (KeyValuePair<string, TrackedMission> pair in currentMissions)
                _activeMissions[pair.Key] = pair.Value;
        }

        /// <summary>
        /// Classifies the observed outcome of a completed espionage mission.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="mission">The completed mission state.</param>
        /// <param name="counts">The target counters to update.</param>
        private void RecordEspionageOutcome(
            GameRoot game,
            TrackedMission mission,
            MissionTargetCounts counts
        )
        {
            if (mission.MissionTypeId != MissionTypeIDs.Espionage)
                return;

            if (GetIntelTick(game, mission.FactionId, mission.PlanetId) > mission.InitialIntelTick)
            {
                counts.IntelRefreshes++;
                return;
            }

            if (mission.WaitingForParticipants)
            {
                counts.ArrivalInterruptions++;
                return;
            }

            if (mission.CurrentProgress + 1 < mission.MaxProgress)
                counts.EarlyInterruptions++;
        }

        /// <summary>
        /// Gets the tick of the faction's latest intelligence snapshot for a planet.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="factionId">The observing faction instance identifier.</param>
        /// <param name="planetId">The observed planet instance identifier.</param>
        /// <returns>The snapshot tick, or -1 when no snapshot exists.</returns>
        private static int GetIntelTick(GameRoot game, string factionId, string planetId)
        {
            Faction faction = game.GetFactionByOwnerInstanceID(factionId);
            if (
                faction?.Fog?.PlanetToSector == null
                || !faction.Fog.PlanetToSector.TryGetValue(planetId, out string systemId)
                || !faction.Fog.Snapshots.TryGetValue(
                    systemId,
                    out PlanetSectorSnapshot systemSnapshot
                )
                || !systemSnapshot.Planets.TryGetValue(planetId, out PlanetSnapshot planetSnapshot)
            )
                return -1;

            return planetSnapshot.TickCaptured;
        }

        /// <summary>
        /// Records planet acquisition, loss, and colonization transitions.
        /// </summary>
        /// <param name="game">The current game state.</param>
        private void RecordPlanetOwnership(GameRoot game)
        {
            foreach (string planetId in _planetStates.Keys.ToList())
            {
                Planet planet = game.GetSceneNodeByInstanceID<Planet>(planetId);
                if (planet == null)
                    continue;

                PlanetState current = PlanetState.From(planet);
                if (_planetStates.TryGetValue(planet.InstanceID, out PlanetState previous))
                {
                    if (previous.OwnerId != current.OwnerId)
                    {
                        if (!string.IsNullOrEmpty(previous.OwnerId))
                            GetActivity(previous.OwnerId).PlanetsLost++;

                        if (!string.IsNullOrEmpty(current.OwnerId))
                            GetActivity(current.OwnerId).PlanetsAcquired++;
                    }

                    if (
                        !previous.IsColonized
                        && current.IsColonized
                        && !string.IsNullOrEmpty(current.OwnerId)
                    )
                        GetActivity(current.OwnerId).PlanetsColonized++;
                }

                _planetStates[planet.InstanceID] = current;
            }
        }

        /// <summary>
        /// Records research-order advances for every faction.
        /// </summary>
        /// <param name="game">The current game state.</param>
        private void RecordResearch(GameRoot game)
        {
            foreach (Faction faction in game.GetFactions())
            {
                ResearchOrders current = ResearchOrders.From(faction);
                if (_researchOrders.TryGetValue(faction.InstanceID, out ResearchOrders previous))
                {
                    FactionActivity activity = GetActivity(faction.InstanceID);
                    activity.ShipResearchAdvances += Math.Max(0, current.Ship - previous.Ship);
                    activity.FacilityResearchAdvances += Math.Max(
                        0,
                        current.Facility - previous.Facility
                    );
                    activity.TroopResearchAdvances += Math.Max(0, current.Troop - previous.Troop);
                }

                _researchOrders[faction.InstanceID] = current;
            }
        }

        /// <summary>
        /// Gets the aggregate counters for a tracked mission type.
        /// </summary>
        /// <param name="mission">The tracked mission.</param>
        /// <returns>The mission-type counters.</returns>
        private MissionCounts GetMissionCounts(TrackedMission mission)
        {
            FactionActivity activity = GetActivity(mission.FactionId);
            if (
                !activity.MissionCounts.TryGetValue(mission.MissionTypeId, out MissionCounts counts)
            )
            {
                counts = new MissionCounts();
                activity.MissionCounts[mission.MissionTypeId] = counts;
            }

            return counts;
        }

        /// <summary>
        /// Gets or creates the mutable activity counters for a faction.
        /// </summary>
        /// <param name="factionId">The faction instance identifier.</param>
        /// <returns>The faction activity counters.</returns>
        private FactionActivity GetActivity(string factionId)
        {
            string key = factionId ?? string.Empty;
            if (!_factionActivities.TryGetValue(key, out FactionActivity activity))
            {
                activity = new FactionActivity();
                _factionActivities[key] = activity;
            }

            return activity;
        }

        /// <summary>
        /// Gets or creates counters for a mission type and target planet.
        /// </summary>
        /// <param name="mission">The tracked mission.</param>
        /// <returns>The mission-target counters.</returns>
        private MissionTargetCounts GetMissionTargetCounts(TrackedMission mission)
        {
            FactionActivity activity = GetActivity(mission.FactionId);
            string key = $"{mission.MissionTypeId}\0{mission.PlanetId}";
            if (!activity.MissionTargetCounts.TryGetValue(key, out MissionTargetCounts counts))
            {
                counts = new MissionTargetCounts
                {
                    MissionTypeId = mission.MissionTypeId,
                    PlanetId = mission.PlanetId,
                };
                activity.MissionTargetCounts[key] = counts;
            }

            return counts;
        }

        /// <summary>
        /// Gets or creates counters for a sabotage target.
        /// </summary>
        /// <param name="mission">The tracked sabotage mission.</param>
        /// <returns>The sabotage-target counters, or null when the mission has no target.</returns>
        private SabotageTargetCounts GetSabotageTargetCounts(TrackedMission mission)
        {
            if (string.IsNullOrEmpty(mission.TargetId))
                return null;

            FactionActivity activity = GetActivity(mission.FactionId);
            if (
                !activity.SabotageTargetCounts.TryGetValue(
                    mission.TargetId,
                    out SabotageTargetCounts counts
                )
            )
            {
                counts = new SabotageTargetCounts
                {
                    TargetId = mission.TargetId,
                    PlanetId = mission.PlanetId,
                    TargetType = mission.TargetType,
                };
                activity.SabotageTargetCounts[mission.TargetId] = counts;
            }

            return counts;
        }

        private sealed class FactionActivity
        {
            public Dictionary<string, MissionCounts> MissionCounts { get; } =
                new(StringComparer.Ordinal);
            public Dictionary<string, MissionTargetCounts> MissionTargetCounts { get; } =
                new(StringComparer.Ordinal);
            public Dictionary<string, SabotageTargetCounts> SabotageTargetCounts { get; } =
                new(StringComparer.Ordinal);
            public int PlanetsAcquired;
            public int PlanetsLost;
            public int PlanetsColonized;
            public int ShipResearchAdvances;
            public int FacilityResearchAdvances;
            public int TroopResearchAdvances;
        }

        private sealed class MissionCounts
        {
            public int Started;
            public int OfficerLedHostileStarted;
            public int OfficerLedHostileStartedWithDecoy;
            public int OfficerLedHostileStartedWithSpecialForcesDecoy;
            public int Ended;
        }

        private sealed class MissionTargetCounts
        {
            public string MissionTypeId;
            public string PlanetId;
            public int Started;
            public int Ended;
            public int IntelRefreshes;
            public int EarlyInterruptions;
            public int ArrivalInterruptions;
            public int MainRatingTotal;
            public int MainRatingSamples;
            public int MinimumMainRating = int.MaxValue;
            public int MaximumMainRating;
        }

        private sealed class SabotageTargetCounts
        {
            public string TargetId;
            public string PlanetId;
            public string TargetType;
            public int Started;
            public int Ended;
            public int Destroyed;
        }

        private sealed class TrackedMission
        {
            public string InstanceId;
            public string FactionId;
            public string MissionTypeId;
            public string TargetId;
            public string OfficerTargetId;
            public string PlanetId;
            public string TargetType;
            public int InitialIntelTick;
            public int CurrentProgress;
            public int MaxProgress;
            public bool WaitingForParticipants;
            public int MainRating;
            public bool OfficerLedHostile;
            public bool HasDecoy;
            public bool HasSpecialForcesDecoy;

            /// <summary>
            /// Captures the mission state needed to compare it across ticks.
            /// </summary>
            /// <param name="mission">The mission to record.</param>
            /// <param name="game">The current game state.</param>
            /// <returns>The tracked mission state.</returns>
            public static TrackedMission From(Mission mission, GameRoot game)
            {
                string targetId = (mission as SabotageMission)?.SabotageTargetInstanceID;
                IReadOnlyList<IMissionParticipant> mainParticipants = mission.GetMainParticipants();
                Planet targetPlanet = game.GetSceneNodeByInstanceID<Planet>(
                    mission.LocationInstanceID
                );
                string factionId = mission.GetOwnerInstanceID();
                return new TrackedMission
                {
                    InstanceId = mission.InstanceID,
                    FactionId = factionId,
                    MissionTypeId = mission.ConfigKey,
                    TargetId = targetId,
                    OfficerTargetId = (mission as AbductionMission)?.TargetOfficerInstanceID,
                    PlanetId = mission.LocationInstanceID,
                    TargetType = game.GetSceneNodeByInstanceID<ISceneNode>(targetId)
                        ?.GetType()
                        .Name,
                    CurrentProgress = mission.CurrentProgress,
                    MaxProgress = mission.MaxProgress,
                    WaitingForParticipants = mission.IsWaitingForParticipants(),
                    OfficerLedHostile =
                        mainParticipants.OfType<Officer>().Any()
                        && !string.IsNullOrEmpty(targetPlanet?.GetOwnerInstanceID())
                        && targetPlanet.GetOwnerInstanceID() != factionId,
                    HasDecoy = mission.GetDecoyParticipants().Count > 0,
                    HasSpecialForcesDecoy = mission
                        .GetDecoyParticipants()
                        .OfType<SpecialForces>()
                        .Any(),
                    MainRating =
                        mainParticipants.Count > 0
                            ? mainParticipants.Sum(participant =>
                                participant.GetEffectiveRating(mission.ParticipantRating)
                            ) / mainParticipants.Count
                            : 0,
                };
            }
        }

        private sealed class PlanetState
        {
            public string OwnerId;
            public bool IsColonized;

            /// <summary>
            /// Captures the ownership state needed to compare a planet across ticks.
            /// </summary>
            /// <param name="planet">The planet to record.</param>
            /// <returns>The tracked planet state.</returns>
            public static PlanetState From(Planet planet)
            {
                return new PlanetState
                {
                    OwnerId = planet.GetOwnerInstanceID(),
                    IsColonized = planet.IsColonized,
                };
            }
        }

        private sealed class ResearchOrders
        {
            public int Ship;
            public int Facility;
            public int Troop;

            /// <summary>
            /// Captures the faction's current research progression.
            /// </summary>
            /// <param name="faction">The faction to record.</param>
            /// <returns>The tracked research orders.</returns>
            public static ResearchOrders From(Faction faction)
            {
                return new ResearchOrders
                {
                    Ship = faction.GetHighestUnlockedOrder(ResearchDiscipline.ShipDesign),
                    Facility = faction.GetHighestUnlockedOrder(ResearchDiscipline.FacilityDesign),
                    Troop = faction.GetHighestUnlockedOrder(ResearchDiscipline.TroopTraining),
                };
            }
        }
    }

}
