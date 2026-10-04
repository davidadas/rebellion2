using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Logging;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Manages planetary ownership and popular support.
    /// </summary>
    public class PlanetaryControlCommands
    {
        private readonly GameRoot _game;
        private readonly PlanetaryControlQueries _queries;
        private readonly MovementCommands _movementSystem;
        private readonly FogOfWarCommands _fogOfWarSystem;
        private readonly FogOfWarQueries _fogOfWarQueries;
        private readonly HashSet<string> _controlChangesInProgress = new HashSet<string>();

        /// <summary>
        /// Creates a new PlanetaryControlCommands.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="movementSystem">Used to evacuate enemy units on ownership change.</param>
        /// <param name="fogOfWarSystem">Used to refresh faction snapshots on ownership change.</param>
        /// <param name="fogOfWarQueries">The visibility rules for ownership-change observers.</param>
        /// <param name="queries">The read-only planetary control rules.</param>
        public PlanetaryControlCommands(
            GameRoot game,
            MovementCommands movementSystem,
            FogOfWarCommands fogOfWarSystem,
            PlanetaryControlQueries queries,
            FogOfWarQueries fogOfWarQueries
        )
        {
            _game = game;
            _movementSystem = movementSystem;
            _fogOfWarSystem = fogOfWarSystem;
            _queries = queries;
            _fogOfWarQueries = fogOfWarQueries;
        }

        /// <summary>
        /// Updates timed popular-support changes caused by blockades.
        /// </summary>
        internal void UpdateBlockadeSupport()
        {
            GameConfig.SupportShiftConfig config = _game.Config.SupportShift;
            foreach (Planet planet in _game.GetSceneNodesByType<Planet>())
            {
                UpdateBlockadeSupport(planet, config);
            }
        }

        /// <summary>
        /// Moves support toward the side already favored while a fleet blockades the planet.
        /// </summary>
        /// <param name="planet">The planet.</param>
        /// <param name="config">The config.</param>
        private void UpdateBlockadeSupport(Planet planet, GameConfig.SupportShiftConfig config)
        {
            if (!planet.IsBlockaded())
            {
                ResetBlockadeSupportTimer(planet);
                return;
            }

            Faction blockadingFaction = GetBlockadingFaction(planet);
            if (blockadingFaction == null)
                return;

            bool blockadeSupportsFavoredFaction =
                TryGetFavoredFaction(planet, out Faction supportLeader)
                && blockadingFaction == supportLeader;
            int interval = GetBlockadeSupportInterval(config, blockadeSupportsFavoredFaction);
            if (interval <= 0)
                return;

            if (
                planet.NextBlockadeSupportShiftTick <= 0
                || planet.BlockadeSupportShiftIntervalTicks != interval
            )
            {
                ScheduleBlockadeSupport(planet, interval);
                return;
            }

            if (_game.CurrentTick < planet.NextBlockadeSupportShiftTick)
                return;

            int shift = blockadeSupportsFavoredFaction
                ? config.BlockadeMatchShift
                : config.BlockadeOpposeShift;
            ChangePopularSupport(planet, blockadingFaction, shift);
            ScheduleBlockadeSupport(planet, interval);
        }

        /// <summary>
        /// Returns the faction operating the fleet that currently blockades a planet.
        /// </summary>
        /// <param name="planet">The planet.</param>
        /// <returns>The requested blockading faction.</returns>
        private Faction GetBlockadingFaction(Planet planet)
        {
            Fleet blockadingFleet = planet
                .GetChildren<Fleet>()
                .FirstOrDefault(fleet =>
                    fleet.Movement == null
                    && fleet.HasOperationalCapitalShips()
                    && fleet.OwnerInstanceID != planet.OwnerInstanceID
                );
            return _game.GetFactionByOwnerInstanceID(blockadingFleet?.OwnerInstanceID);
        }

        /// <summary>
        /// Finds the faction with strictly more popular support than every other faction.
        /// </summary>
        /// <param name="planet">The planet.</param>
        /// <param name="supportLeader">Receives the support leader.</param>
        /// <returns>True when the operation succeeds; otherwise false.</returns>
        private bool TryGetFavoredFaction(Planet planet, out Faction supportLeader)
        {
            List<Faction> factions = _game.GetFactions();
            Faction candidate = factions
                .OrderByDescending(faction => planet.GetPopularSupport(faction.InstanceID))
                .FirstOrDefault();
            if (candidate == null)
            {
                supportLeader = null;
                return false;
            }

            int leadingSupport = planet.GetPopularSupport(candidate.InstanceID);
            bool supportIsTied = factions.Any(faction =>
                faction != candidate
                && planet.GetPopularSupport(faction.InstanceID) == leadingSupport
            );
            supportLeader = supportIsTied ? null : candidate;
            return supportLeader != null;
        }

        /// <summary>
        /// Returns the blockade shift interval for the planet's current support alignment.
        /// </summary>
        /// <param name="config">The config.</param>
        /// <param name="blockadeSupportsFavoredFaction">Whether blockade supports favored faction.</param>
        /// <returns>The requested blockade support interval.</returns>
        private static int GetBlockadeSupportInterval(
            GameConfig.SupportShiftConfig config,
            bool blockadeSupportsFavoredFaction
        )
        {
            return blockadeSupportsFavoredFaction
                ? config.BlockadeMatchShiftIntervalTicks
                : config.BlockadeOpposeShiftIntervalTicks;
        }

        /// <summary>
        /// Clears a planet's blockade support-shift schedule.
        /// </summary>
        /// <param name="planet">The planet.</param>
        private static void ResetBlockadeSupportTimer(Planet planet)
        {
            planet.NextBlockadeSupportShiftTick = 0;
            planet.BlockadeSupportShiftIntervalTicks = 0;
        }

        /// <summary>
        /// Schedules the planet's next blockade support shift.
        /// </summary>
        /// <param name="planet">The planet.</param>
        /// <param name="interval">The interval.</param>
        private void ScheduleBlockadeSupport(Planet planet, int interval)
        {
            planet.NextBlockadeSupportShiftTick = _game.CurrentTick + interval;
            planet.BlockadeSupportShiftIntervalTicks = interval;
        }

        /// <summary>
        /// Transfers the selected planets and units through their existing ownership rules.
        /// </summary>
        /// <param name="newOwner">The faction receiving ownership.</param>
        /// <param name="planets">The planets to transfer before the individual units.</param>
        /// <param name="units">The individual units whose ownership indexes must be updated.</param>
        /// <returns>The ownership changes in the order they occurred.</returns>
        public List<GameResult> ChangeOwnership(
            Faction newOwner,
            IReadOnlyList<Planet> planets,
            IReadOnlyList<ISceneNode> units
        )
        {
            List<GameResult> results = new List<GameResult>();
            foreach (Planet planet in planets)
            {
                if (planet.OwnerInstanceID != newOwner.InstanceID)
                    results.Add(TransferPlanet(planet, newOwner));
            }

            foreach (ISceneNode unit in units)
            {
                Faction previousOwner = _game.GetFactionByOwnerInstanceID(unit.OwnerInstanceID);
                if (previousOwner == newOwner)
                    continue;
                _game.ChangeOwnership(unit, newOwner.InstanceID);
                results.Add(
                    new UnitOwnershipChangedResult
                    {
                        Unit = unit,
                        PreviousOwner = previousOwner,
                        NewOwner = newOwner,
                        Tick = _game.CurrentTick,
                    }
                );
            }
            return results;
        }

        /// <summary>
        /// Re-evaluates one planet's control state.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>Any ownership-change results produced.</returns>
        internal List<GameResult> ReconcilePlanet(Planet planet)
        {
            List<GameResult> results = new List<GameResult>();
            if (planet == null || !_controlChangesInProgress.Add(planet.InstanceID))
                return results;

            try
            {
                if (!planet.IsColonized)
                {
                    UpdateUncolonizedPlanet(planet, results);
                    return results;
                }

                List<string> regimentOwners = _queries.GetActiveRegimentOwners(planet);
                Faction controller = _queries.GetPlanetController(planet, regimentOwners);
                PlanetOwnershipChangedResult result = ChangePlanetOwner(planet, controller);
                if (result != null)
                {
                    if (regimentOwners.Count == 0)
                    {
                        result.Reason = PlanetOwnershipChangeReason.PopularSupport;
                    }

                    results.Add(result);
                }
            }
            finally
            {
                _controlChangesInProgress.Remove(planet.InstanceID);
            }

            return results;
        }

        /// <summary>
        /// Reconciles every planet against its current regiment presence.
        /// </summary>
        /// <param name="results">Collection to append any ownership-change results to.</param>
        internal void UpdateUncolonizedPlanets(List<GameResult> results)
        {
            foreach (Planet planet in _game.GetSceneNodesByType<Planet>())
                UpdateUncolonizedPlanet(planet, results);
        }

        /// <summary>
        /// Releases abandoned uncolonized owned planets back to neutral control.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <param name="results">Collection to append any ownership-change results to.</param>
        private void UpdateUncolonizedPlanet(Planet planet, List<GameResult> results)
        {
            if (planet == null)
                return;

            string currentOwner = planet.GetOwnerInstanceID();
            bool hasStationedRegiment = _queries
                .GetActiveRegimentOwners(planet)
                .Contains(currentOwner);

            if (!planet.IsColonized && !string.IsNullOrEmpty(currentOwner) && !hasStationedRegiment)
            {
                results.Add(ClearPlanetOwnership(planet));
            }
        }

        /// <summary>
        /// Transfers a planet to a new owner.
        /// </summary>
        /// <param name="planet">The planet to transfer.</param>
        /// <param name="newOwner">The faction receiving ownership.</param>
        /// <returns>The ownership-change result.</returns>
        public PlanetOwnershipChangedResult TransferPlanet(Planet planet, Faction newOwner)
        {
            return ApplyPlanetOwnershipChange(planet, newOwner);
        }

        /// <summary>
        /// Clears the planet's owner, returning it to neutral control. Cancels competing
        /// missions, evicts non-owner units, clears manufacturing queues, and zeroes
        /// popular support for every faction. Buildings are left in place — they remain
        /// where they were built and only transfer when a new faction claims the planet.
        /// </summary>
        /// <param name="planet">The planet whose ownership is being cleared.</param>
        /// <returns>The ownership-change result.</returns>
        public PlanetOwnershipChangedResult ClearPlanetOwnership(Planet planet)
        {
            PlanetOwnershipChangedResult result = ApplyPlanetOwnershipChange(
                planet,
                newOwner: null
            );

            foreach (Faction faction in _game.GetFactions())
                planet.SetPopularSupport(faction.InstanceID, 0);

            return result;
        }

        /// <summary>
        /// Adjusts one faction's popular support on a populated planet.
        /// </summary>
        /// <param name="planet">The planet whose support changes.</param>
        /// <param name="faction">The faction whose support is adjusted.</param>
        /// <param name="shift">The signed support adjustment.</param>
        internal void ChangePopularSupport(Planet planet, Faction faction, int shift)
        {
            if (planet == null || faction == null || shift == 0 || !planet.IsPopulated())
                return;

            int currentSupport = planet.GetPopularSupport(faction.InstanceID);
            int newSupport = System.Math.Clamp(currentSupport + shift, 0, 100);
            if (newSupport == currentSupport)
                return;

            if (shift > 0)
            {
                planet.SetPopularSupport(faction.InstanceID, newSupport);
                return;
            }

            Faction opposingFaction = _game
                .GetFactions()
                .FirstOrDefault(candidate => candidate.InstanceID != faction.InstanceID);
            if (opposingFaction == null)
            {
                planet.SetPopularSupport(faction.InstanceID, newSupport);
                return;
            }

            planet.SetPopularSupport(opposingFaction.InstanceID, 100 - newSupport);
        }

        /// <summary>
        /// Transfers or clears planet ownership when the resolved controller changes.
        /// </summary>
        /// <param name="planet">The planet whose control is changing.</param>
        /// <param name="newOwner">The resolved owner, or null for neutral control.</param>
        /// <returns>The ownership-change result, or null when ownership is unchanged.</returns>
        internal PlanetOwnershipChangedResult ChangePlanetOwner(Planet planet, Faction newOwner)
        {
            if (planet.GetOwnerInstanceID() == newOwner?.InstanceID)
                return null;

            return ApplyPlanetOwnershipChange(planet, newOwner);
        }

        /// <summary>
        /// Applies the shared state transition for transferring or clearing planet ownership.
        /// </summary>
        /// <param name="planet">The planet whose ownership is changing.</param>
        /// <param name="newOwner">The receiving faction, or null for neutral control.</param>
        /// <returns>The completed ownership-change result.</returns>
        private PlanetOwnershipChangedResult ApplyPlanetOwnershipChange(
            Planet planet,
            Faction newOwner
        )
        {
            bool ownsControlChange = _controlChangesInProgress.Add(planet.InstanceID);
            try
            {
                string previousOwnerId = planet.GetOwnerInstanceID();
                string newOwnerId = newOwner?.InstanceID;
                Faction previousOwner = string.IsNullOrEmpty(previousOwnerId)
                    ? null
                    : _game.GetFactionByOwnerInstanceID(previousOwnerId);
                List<Faction> observers = GetOwnershipChangeObservers(
                    planet,
                    previousOwner,
                    newOwner
                );

                if (newOwner != null)
                    TransferBuildings(planet, newOwner);

                EvictEnemyUnits(planet, newOwnerId);
                planet.EndUprising();
                if (newOwner == null)
                {
                    _game.DeregsiterOwnedUnit(planet);
                    planet.SetOwnerInstanceID(null);
                }
                else
                {
                    _game.ChangeOwnership(planet, newOwnerId);
                }

                if (previousOwner?.InstanceID != newOwnerId)
                    CaptureSnapshotForFaction(planet, previousOwner);
                CaptureOwnershipChange(planet, observers);

                return CreateOwnershipChangedResult(planet, previousOwner, newOwner, observers);
            }
            finally
            {
                if (ownsControlChange)
                    _controlChangesInProgress.Remove(planet.InstanceID);
            }
        }

        /// <summary>
        /// Checks all planets for support above the ownership threshold and transfers if needed.
        /// </summary>
        /// <param name="results">Collection to append any ownership change results to.</param>
        internal void CheckOwnershipTransfers(List<GameResult> results)
        {
            int threshold = _game.Config.SupportShift.OwnershipTransferThreshold;

            foreach (Planet planet in _game.GetSceneNodesByType<Planet>())
            {
                if (!CanTransferByPopularSupport(planet))
                    continue;

                foreach (Faction faction in _game.GetFactions())
                {
                    int support = planet.GetPopularSupport(faction.InstanceID);
                    if (support < threshold)
                        continue;

                    PlanetOwnershipChangedResult result = TransferPlanet(planet, faction);
                    result.Reason = PlanetOwnershipChangeReason.PopularSupport;
                    results.Add(result);

                    GameLogger.Log(
                        $"Planet {planet.GetDisplayName()} transferred to {faction.DisplayName} (support {support} > {threshold})"
                    );

                    break;
                }
            }
        }

        /// <summary>
        /// Returns true when popular support may transfer this planet to a faction.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>True when the planet can transfer by popular support.</returns>
        private static bool CanTransferByPopularSupport(Planet planet)
        {
            return planet.IsColonized
                && string.IsNullOrEmpty(planet.GetOwnerInstanceID())
                && planet.GetAllRegiments().Count == 0;
        }

        /// <summary>
        /// Creates the result describing a completed planet ownership change.
        /// </summary>
        /// <param name="planet">The planet whose ownership changed.</param>
        /// <param name="previousOwner">The faction that previously controlled the planet.</param>
        /// <param name="newOwner">The faction that now controls the planet.</param>
        /// <param name="observers">The factions that observed the ownership change.</param>
        /// <returns>The populated ownership-change result.</returns>
        private PlanetOwnershipChangedResult CreateOwnershipChangedResult(
            Planet planet,
            Faction previousOwner,
            Faction newOwner,
            IEnumerable<Faction> observers
        )
        {
            return new PlanetOwnershipChangedResult
            {
                Planet = planet,
                PreviousOwner = previousOwner,
                NewOwner = newOwner,
                Tick = _game.CurrentTick,
                ObserverFactionInstanceIDs = observers
                    .Select(faction => faction.InstanceID)
                    .Distinct()
                    .ToList(),
            };
        }

        /// <summary>
        /// Finds factions that can observe an ownership change at a planet.
        /// </summary>
        /// <param name="planet">The planet changing ownership.</param>
        /// <param name="previousOwner">The previous owner, when present.</param>
        /// <param name="newOwner">The new owner, when present.</param>
        /// <returns>The observing factions, including both affected owners.</returns>
        private List<Faction> GetOwnershipChangeObservers(
            Planet planet,
            Faction previousOwner,
            Faction newOwner
        )
        {
            PlanetSector sector = planet.GetParentOfType<PlanetSector>();
            List<Faction> observers = _game
                .GetFactions()
                .Where(faction =>
                    sector?.SectorType == PlanetSectorType.Core
                    || (
                        _fogOfWarSystem != null && _fogOfWarQueries.IsPlanetVisible(planet, faction)
                    )
                )
                .ToList();

            if (previousOwner != null && !observers.Contains(previousOwner))
                observers.Add(previousOwner);
            if (newOwner != null && !observers.Contains(newOwner))
                observers.Add(newOwner);

            return observers;
        }

        /// <summary>
        /// Records the new owner for every faction that observed a control change.
        /// </summary>
        /// <param name="planet">The planet whose owner changed.</param>
        /// <param name="observers">The factions that observed the change.</param>
        private void CaptureOwnershipChange(Planet planet, IEnumerable<Faction> observers)
        {
            PlanetSector sector = planet.GetParentOfType<PlanetSector>();
            if (_fogOfWarSystem == null || sector == null)
                return;

            _fogOfWarSystem.CaptureOwnershipChange(observers, planet, sector, _game.CurrentTick);
        }

        /// <summary>
        /// Captures the current planet state for one faction when that faction loses direct ownership.
        /// </summary>
        /// <param name="planet">The planet being snapshotted.</param>
        /// <param name="faction">The faction receiving the snapshot.</param>
        private void CaptureSnapshotForFaction(Planet planet, Faction faction)
        {
            if (_fogOfWarSystem == null || faction == null)
                return;

            PlanetSector sector = planet.GetParentOfType<PlanetSector>();
            if (sector == null)
                return;

            _fogOfWarSystem.CaptureSnapshot(faction, planet, sector, _game.CurrentTick);
        }

        /// <summary>
        /// Transfers all buildings on the planet to the new owner.
        /// </summary>
        /// <param name="planet">The planet whose buildings are transferred.</param>
        /// <param name="newOwner">The faction receiving ownership of the buildings.</param>
        private void TransferBuildings(Planet planet, Faction newOwner)
        {
            foreach (Building building in planet.GetChildren<Building>(includeDisabled: true))
            {
                _game.ChangeOwnership(building, newOwner.InstanceID);
            }
        }

        /// <summary>
        /// Evacuates non-owner units to the nearest friendly planet that accepts them. Regiments
        /// and starfighters with no reachable destination are destroyed; officers with no
        /// reachable destination are captured by the new owner.
        /// </summary>
        /// <param name="planet">The planet to evict enemy units from.</param>
        /// <param name="newOwnerID">The instance ID of the new owning faction.</param>
        private void EvictEnemyUnits(Planet planet, string newOwnerID)
        {
            List<IMovable> enemies = planet
                .GetChildren<IMovable>()
                .Where(m =>
                    m.GetOwnerInstanceID() != newOwnerID && m is not Fleet && m is not Building
                )
                .ToList();

            foreach (IMovable unit in enemies)
            {
                _movementSystem.EvacuateToNearestFriendlyPlanet(
                    unit,
                    evictingOwnerInstanceID: newOwnerID,
                    force: true
                );
            }
        }
    }
}
