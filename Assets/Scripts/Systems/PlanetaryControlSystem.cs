using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Manages planetary ownership and popular support.
    /// </summary>
    public class PlanetaryControlSystem : IGameResultHandler
    {
        private readonly GameRoot _game;
        private readonly MovementSystem _movementSystem;
        private readonly ManufacturingSystem _manufacturingSystem;
        private readonly FogOfWarSystem _fogOfWarSystem;
        private readonly HashSet<string> _controlShiftedOwners = new HashSet<string>();
        private readonly HashSet<string> _controlChangesInProgress = new HashSet<string>();
        private int _controlShiftTick = -1;

        /// <summary>
        /// Creates a new PlanetaryControlSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="movementSystem">Used to evacuate enemy units on ownership change.</param>
        /// <param name="manufacturingSystem">Used to clear queues on ownership change.</param>
        /// <param name="fogOfWarSystem">Used to refresh faction snapshots on ownership change.</param>
        public PlanetaryControlSystem(
            GameRoot game,
            MovementSystem movementSystem,
            ManufacturingSystem manufacturingSystem,
            FogOfWarSystem fogOfWarSystem
        )
        {
            _game = game;
            _movementSystem = movementSystem;
            _manufacturingSystem = manufacturingSystem;
            _fogOfWarSystem = fogOfWarSystem;
        }

        /// <summary>
        /// Checks for support-driven ownership transfers.
        /// </summary>
        /// <returns>Any ownership change results generated this tick.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();
            UpdateUncolonizedPlanets(results);
            CheckOwnershipTransfers(results);

            return results;
        }

        /// <summary>
        /// Reconciles planets whose active garrisons changed.
        /// </summary>
        /// <param name="results">The result batch to inspect.</param>
        /// <returns>Any ownership changes caused by the garrison changes.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<GameResult> results)
        {
            List<GameResult> controlResults = new List<GameResult>();
            if (results == null)
                return controlResults;

            IEnumerable<Planet> affectedPlanets = results
                .OfType<PlanetGarrisonChangedResult>()
                .Select(result => result.Planet)
                .Where(planet => planet != null)
                .Distinct();
            foreach (Planet planet in affectedPlanets)
                controlResults.AddRange(ReconcilePlanet(planet));

            return controlResults;
        }

        /// <summary>
        /// Re-evaluates one planet's control state.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>Any ownership-change results produced.</returns>
        public List<GameResult> ReconcilePlanet(Planet planet)
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

                List<string> regimentOwners = GetActiveRegimentOwners(planet);
                Faction controller = GetPlanetController(planet, regimentOwners);
                PlanetOwnershipChangedResult result = ChangePlanetControl(planet, controller);
                if (result != null)
                {
                    if (regimentOwners.Count == 0)
                        result.Reason = PlanetOwnershipChangeReason.PopularSupport;

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
        private void UpdateUncolonizedPlanets(List<GameResult> results)
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

            List<Regiment> regiments = planet.GetAllRegiments();
            string currentOwner = planet.GetOwnerInstanceID();

            if (!planet.IsColonized && !string.IsNullOrEmpty(currentOwner) && regiments.Count == 0)
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
        /// Reconciles control after bombardment changes the defending garrison.
        /// </summary>
        /// <param name="planet">The bombarded planet.</param>
        /// <param name="previousOwnerId">The faction instance ID that controlled the planet before bombardment.</param>
        /// <returns>The ownership changes caused by control reconciliation and support propagation.</returns>
        internal List<PlanetOwnershipChangedResult> ResolveBombardmentControl(
            Planet planet,
            string previousOwnerId
        )
        {
            List<PlanetOwnershipChangedResult> results = new List<PlanetOwnershipChangedResult>();
            Faction provisionalOwner = GetPlanetController(planet);
            if (provisionalOwner?.InstanceID == previousOwnerId)
                return results;

            PlanetOwnershipChangedResult controlChange = ChangePlanetControl(
                planet,
                provisionalOwner
            );
            if (controlChange != null)
                results.Add(controlChange);

            Faction supportBeneficiary =
                provisionalOwner
                ?? _game
                    .GetFactions()
                    .FirstOrDefault(faction => faction.InstanceID != previousOwnerId);
            results.AddRange(
                ShiftBombardmentSupport(
                    GetAffectedPlanets(planet.GetParentOfType<PlanetSystem>()),
                    supportBeneficiary,
                    _game.Config.SupportShift.GarrisonRemovalSupportShift
                )
            );

            return results;
        }

        /// <summary>
        /// Applies bombardment-related support shifts and resolves resulting control changes.
        /// </summary>
        /// <param name="planets">The planets receiving the support shift.</param>
        /// <param name="faction">The faction whose support changes.</param>
        /// <param name="shift">The signed support adjustment.</param>
        /// <returns>The ownership changes caused by the support shifts.</returns>
        internal List<PlanetOwnershipChangedResult> ShiftBombardmentSupport(
            IEnumerable<Planet> planets,
            Faction faction,
            int shift
        )
        {
            List<PlanetOwnershipChangedResult> results = new List<PlanetOwnershipChangedResult>();
            Queue<(Planet planet, Faction faction, int shift)> pending =
                new Queue<(Planet planet, Faction faction, int shift)>();
            EnqueueSupportShifts(pending, planets, faction, shift);

            while (pending.Count > 0)
            {
                (Planet planet, Faction shiftFaction, int supportShift) = pending.Dequeue();
                Faction previousController = GetPlanetController(planet);
                ShiftPopularSupport(planet, shiftFaction, supportShift);
                Faction newController = GetPlanetController(planet);
                if (previousController?.InstanceID == newController?.InstanceID)
                    continue;

                PlanetOwnershipChangedResult controlChange = ChangePlanetControl(
                    planet,
                    newController
                );
                if (controlChange != null)
                {
                    controlChange.Reason = PlanetOwnershipChangeReason.PopularSupport;
                    results.Add(controlChange);
                }

                if (!CanApplyControlSupportShift(previousController))
                    continue;

                Faction beneficiary =
                    newController
                    ?? _game
                        .GetFactions()
                        .FirstOrDefault(candidate =>
                            candidate.InstanceID != previousController?.InstanceID
                        );
                EnqueueSupportShifts(
                    pending,
                    GetAffectedPlanets(planet.GetParentOfType<PlanetSystem>()),
                    beneficiary,
                    _game.Config.SupportShift.ControlChangeSupportShift
                );
            }

            return results;
        }

        /// <summary>
        /// Adjusts one faction's popular support on a populated planet.
        /// </summary>
        /// <param name="planet">The planet whose support changes.</param>
        /// <param name="faction">The faction whose support is adjusted.</param>
        /// <param name="shift">The signed support adjustment.</param>
        internal void ShiftPopularSupport(Planet planet, Faction faction, int shift)
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
        private PlanetOwnershipChangedResult ChangePlanetControl(Planet planet, Faction newOwner)
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

                CancelCompetingMissions(planet, newOwnerId);
                if (newOwner != null)
                    TransferBuildings(planet, newOwner);

                _manufacturingSystem.ClearQueuesOnOwnershipChange(planet);
                EvictEnemyUnits(planet, newOwnerId);
                planet.EndUprising();
                if (newOwner == null)
                {
                    _game.DeregsiterOwnedUnit(planet);
                    planet.SetOwnerInstanceID(null);
                }
                else
                {
                    _game.ChangeUnitOwnership(planet, newOwnerId);
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
        /// Finds the faction whose support qualifies it to control a planet.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>The qualifying faction with the greatest support, or null when none qualifies.</returns>
        private Faction GetSupportController(Planet planet)
        {
            int threshold = _game.Config.SupportShift.OwnershipTransferThreshold;
            return _game
                .GetFactions()
                .Where(faction => planet.GetPopularSupport(faction.InstanceID) >= threshold)
                .OrderByDescending(faction => planet.GetPopularSupport(faction.InstanceID))
                .FirstOrDefault();
        }

        /// <summary>
        /// Resolves control from active regiments before falling back to popular support.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>The controlling faction, or null when control is contested or unsupported.</returns>
        private Faction GetPlanetController(Planet planet)
        {
            return GetPlanetController(planet, GetActiveRegimentOwners(planet));
        }

        /// <summary>
        /// Gets the distinct owners of completed, stationary regiments on a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The active regiment owner identifiers.</returns>
        private List<string> GetActiveRegimentOwners(Planet planet)
        {
            return planet
                .GetAllRegiments()
                .Where(regiment =>
                    regiment.ManufacturingStatus == ManufacturingStatus.Complete
                    && regiment.Movement == null
                )
                .Select(regiment => regiment.GetOwnerInstanceID())
                .Where(ownerId => !string.IsNullOrEmpty(ownerId))
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Resolves planetary control from active regiment owners and popular support.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <param name="regimentOwners">The distinct active regiment owner identifiers.</param>
        /// <returns>The controlling faction, or null when control is contested or unsupported.</returns>
        private Faction GetPlanetController(Planet planet, List<string> regimentOwners)
        {
            if (regimentOwners.Count == 1)
                return _game.GetFactionByOwnerInstanceID(regimentOwners[0]);

            if (regimentOwners.Count > 1)
                return null;

            return GetSupportController(planet);
        }

        /// <summary>
        /// Limits propagated control-change support shifts to one per displaced faction each tick.
        /// </summary>
        /// <param name="previousController">The faction displaced by the control change.</param>
        /// <returns>True when the support shift may be propagated.</returns>
        private bool CanApplyControlSupportShift(Faction previousController)
        {
            if (previousController == null)
                return true;

            if (_controlShiftTick != _game.CurrentTick)
            {
                _controlShiftTick = _game.CurrentTick;
                _controlShiftedOwners.Clear();
            }

            return _controlShiftedOwners.Add(previousController.InstanceID);
        }

        /// <summary>
        /// Adds valid support-shift work items to the pending queue.
        /// </summary>
        /// <param name="pending">The queue receiving support shifts.</param>
        /// <param name="planets">The planets to enqueue.</param>
        /// <param name="faction">The faction whose support changes.</param>
        /// <param name="shift">The signed support adjustment.</param>
        private static void EnqueueSupportShifts(
            Queue<(Planet planet, Faction faction, int shift)> pending,
            IEnumerable<Planet> planets,
            Faction faction,
            int shift
        )
        {
            if (planets == null || faction == null || shift == 0)
                return;

            foreach (Planet planet in planets)
                pending.Enqueue((planet, faction, shift));
        }

        /// <summary>
        /// Gets populated, intact planets affected by a system-level support shift.
        /// </summary>
        /// <param name="system">The planet system to inspect.</param>
        /// <returns>The planets eligible for the support shift.</returns>
        private static IEnumerable<Planet> GetAffectedPlanets(PlanetSystem system)
        {
            return system?.Planets.Where(planet => planet.IsPopulated() && !planet.IsDestroyed)
                ?? Enumerable.Empty<Planet>();
        }

        /// <summary>
        /// Checks all planets for support above the ownership threshold and transfers if needed.
        /// </summary>
        /// <param name="results">Collection to append any ownership change results to.</param>
        private void CheckOwnershipTransfers(List<GameResult> results)
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
            PlanetSystem system = planet.GetParentOfType<PlanetSystem>();
            List<Faction> observers = _game
                .GetFactions()
                .Where(faction =>
                    system?.SystemType == PlanetSystemType.CoreSystem
                    || (_fogOfWarSystem?.IsPlanetVisible(planet, faction) ?? false)
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
            PlanetSystem system = planet.GetParentOfType<PlanetSystem>();
            if (_fogOfWarSystem == null || system == null)
                return;

            _fogOfWarSystem.CaptureOwnershipChange(observers, planet, system, _game.CurrentTick);
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

            PlanetSystem system = planet.GetParentOfType<PlanetSystem>();
            if (system == null)
                return;

            _fogOfWarSystem.CaptureSnapshot(faction, planet, system, _game.CurrentTick);
        }

        /// <summary>
        /// Cancels missions targeting this planet that belong to factions other than the new owner.
        /// </summary>
        /// <param name="planet">The planet changing ownership.</param>
        /// <param name="newOwnerID">The instance ID of the new owning faction.</param>
        private void CancelCompetingMissions(Planet planet, string newOwnerID)
        {
            List<Mission> competing = _game
                .GetSceneNodesByType<Mission>()
                .Where(m =>
                    m.CanceledOnOwnershipChange
                    && m.OwnerInstanceID != newOwnerID
                    && m.GetParentOfType<Planet>() == planet
                )
                .ToList();

            foreach (Mission mission in competing)
            {
                foreach (IMissionParticipant participant in mission.GetAllParticipants())
                    _movementSystem.EvacuateToNearestFriendlyPlanet(participant);

                _game.DetachNode(mission);
            }
        }

        /// <summary>
        /// Transfers all buildings on the planet to the new owner.
        /// </summary>
        /// <param name="planet">The planet whose buildings are transferred.</param>
        /// <param name="newOwner">The faction receiving ownership of the buildings.</param>
        private void TransferBuildings(Planet planet, Faction newOwner)
        {
            foreach (Building building in planet.GetChildren<Building>(_ => true, recurse: false))
            {
                building.AllowedOwnerInstanceIDs = new List<string> { newOwner.InstanceID };
                _game.ChangeUnitOwnership(building, newOwner.InstanceID);
            }
        }

        /// <summary>
        /// Evacuates non-owner units from the planet to the nearest friendly planet.
        /// </summary>
        /// <param name="planet">The planet to evict enemy units from.</param>
        /// <param name="newOwnerID">The instance ID of the new owning faction.</param>
        private void EvictEnemyUnits(Planet planet, string newOwnerID)
        {
            List<IMovable> enemies = planet
                .GetChildren<IMovable>(
                    m =>
                        m.GetOwnerInstanceID() != newOwnerID && m is not Fleet && m is not Building,
                    recurse: false
                )
                .ToList();

            foreach (IMovable unit in enemies)
                _movementSystem.EvacuateToNearestFriendlyPlanet(unit);
        }
    }
}
