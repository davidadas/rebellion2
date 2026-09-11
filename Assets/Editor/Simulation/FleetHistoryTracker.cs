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
    private sealed class FleetHistoryTracker
    {
        private readonly Dictionary<string, string> _lastSnapshotKeys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FleetHistorySnapshot> _lastSnapshots = new(
            StringComparer.Ordinal
        );
        private readonly List<FleetHistorySnapshot> _snapshots = new();

        /// <summary>
        /// Records changed fleet state for the current simulation tick.
        /// </summary>
        /// <param name="game">The game state to inspect.</param>
        public void RecordTick(GameRoot game)
        {
            HashSet<string> liveFleetIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (Faction faction in game.GetFactions().OrderBy(faction => faction.InstanceID))
            {
                foreach (
                    Fleet fleet in game.GetSceneNodesByOwnerInstanceID<Fleet>(faction.InstanceID)
                        .OrderBy(fleet => fleet.InstanceID, StringComparer.Ordinal)
                )
                {
                    liveFleetIds.Add(fleet.InstanceID);
                    FleetHistorySnapshot snapshot = BuildSnapshot(game, faction, fleet, false);
                    string snapshotKey = BuildSnapshotKey(snapshot);

                    if (
                        _lastSnapshotKeys.TryGetValue(fleet.InstanceID, out string previousKey)
                        && previousKey == snapshotKey
                    )
                        continue;

                    _snapshots.Add(snapshot);
                    _lastSnapshotKeys[fleet.InstanceID] = snapshotKey;
                    _lastSnapshots[fleet.InstanceID] = snapshot;
                }
            }

            List<string> destroyedFleetIds = _lastSnapshotKeys
                .Keys.Where(fleetId => !liveFleetIds.Contains(fleetId))
                .OrderBy(fleetId => fleetId, StringComparer.Ordinal)
                .ToList();

            foreach (string fleetId in destroyedFleetIds)
            {
                FleetHistorySnapshot previousSnapshot = _lastSnapshots[fleetId];
                FleetHistorySnapshot destroyedSnapshot = BuildDestroyedSnapshot(
                    game.CurrentTick,
                    previousSnapshot
                );
                _snapshots.Add(destroyedSnapshot);
                _lastSnapshotKeys.Remove(fleetId);
                _lastSnapshots.Remove(fleetId);
            }
        }

        /// <summary>
        /// Returns the recorded fleet history snapshots.
        /// </summary>
        /// <returns>The recorded fleet history snapshots.</returns>
        public FleetHistorySnapshot[] ToArray()
        {
            return _snapshots.ToArray();
        }

        /// <summary>
        /// Builds a fleet history snapshot.
        /// </summary>
        /// <param name="game">The game state to inspect.</param>
        /// <param name="faction">The fleet owner faction.</param>
        /// <param name="fleet">The fleet to summarize.</param>
        /// <param name="destroyed">Whether the fleet has been destroyed.</param>
        /// <returns>The fleet history snapshot.</returns>
        private static FleetHistorySnapshot BuildSnapshot(
            GameRoot game,
            Faction faction,
            Fleet fleet,
            bool destroyed
        )
        {
            Planet location = fleet.GetParentOfType<Planet>();
            Planet targetPlanet = string.IsNullOrEmpty(fleet.Order?.TargetPlanetId)
                ? null
                : game.GetSceneNodeByInstanceID<Planet>(fleet.Order.TargetPlanetId);

            return new FleetHistorySnapshot
            {
                Tick = game.CurrentTick,
                FactionId = faction.InstanceID,
                FactionName = faction.GetDisplayName(),
                FleetId = fleet.InstanceID,
                DisplayName = fleet.GetDisplayName(),
                RoleType = fleet.RoleType.ToString(),
                LocationPlanetId = location?.InstanceID,
                LocationPlanetName = location?.GetDisplayName(),
                InTransit = fleet.Movement != null,
                Destroyed = destroyed,
                TransitTicksRemaining = fleet.Movement?.TicksRemaining() ?? 0,
                CombatValue = fleet.GetCombatValue(),
                CapitalShipCount = fleet.GetChildren<CapitalShip>().Count,
                StarfighterCount = fleet.GetStarfighters().Count(),
                RegimentCount = fleet.GetRegiments().Count(),
                OfficerCount = fleet.GetOfficers().Count(),
                OrderType = fleet.Order?.OrderType.ToString(),
                OrderStatus = fleet.Order?.Status.ToString(),
                OrderTargetPlanetId = fleet.Order?.TargetPlanetId,
                OrderTargetPlanetName = targetPlanet?.GetDisplayName(),
                CapitalShips = SummarizeUnits(fleet.GetChildren<CapitalShip>()),
                Starfighters = SummarizeUnits(fleet.GetStarfighters()),
                Regiments = SummarizeUnits(fleet.GetRegiments()),
                Officers = SummarizeUnits(fleet.GetOfficers()),
            };
        }

        /// <summary>
        /// Builds a fleet history snapshot for a destroyed fleet.
        /// </summary>
        /// <param name="tick">The current simulation tick.</param>
        /// <param name="previousSnapshot">The previous fleet snapshot.</param>
        /// <returns>The destroyed fleet history snapshot.</returns>
        private static FleetHistorySnapshot BuildDestroyedSnapshot(
            int tick,
            FleetHistorySnapshot previousSnapshot
        )
        {
            return new FleetHistorySnapshot
            {
                Tick = tick,
                FactionId = previousSnapshot.FactionId,
                FactionName = previousSnapshot.FactionName,
                FleetId = previousSnapshot.FleetId,
                DisplayName = previousSnapshot.DisplayName,
                RoleType = previousSnapshot.RoleType,
                LocationPlanetId = previousSnapshot.LocationPlanetId,
                LocationPlanetName = previousSnapshot.LocationPlanetName,
                Destroyed = true,
                OrderType = previousSnapshot.OrderType,
                OrderStatus = previousSnapshot.OrderStatus,
                OrderTargetPlanetId = previousSnapshot.OrderTargetPlanetId,
                OrderTargetPlanetName = previousSnapshot.OrderTargetPlanetName,
                CapitalShips = Array.Empty<string>(),
                Starfighters = Array.Empty<string>(),
                Regiments = Array.Empty<string>(),
                Officers = Array.Empty<string>(),
            };
        }

        /// <summary>
        /// Builds the stable comparison key for a fleet snapshot.
        /// </summary>
        /// <param name="snapshot">The snapshot to key.</param>
        /// <returns>The stable snapshot key.</returns>
        private static string BuildSnapshotKey(FleetHistorySnapshot snapshot)
        {
            return string.Join(
                "|",
                snapshot.FactionId,
                snapshot.DisplayName,
                snapshot.RoleType,
                snapshot.LocationPlanetId,
                snapshot.InTransit,
                snapshot.Destroyed,
                snapshot.TransitTicksRemaining,
                snapshot.CombatValue,
                snapshot.OrderType,
                snapshot.OrderStatus,
                snapshot.OrderTargetPlanetId,
                string.Join(",", snapshot.CapitalShips),
                string.Join(",", snapshot.Starfighters),
                string.Join(",", snapshot.Regiments),
                string.Join(",", snapshot.Officers)
            );
        }
    }

}
