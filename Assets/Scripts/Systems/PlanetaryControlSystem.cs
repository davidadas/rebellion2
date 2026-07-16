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
    public class PlanetaryControlSystem
    {
        private readonly GameRoot _game;
        private readonly MovementSystem _movementSystem;
        private readonly ManufacturingSystem _manufacturingSystem;
        private readonly FogOfWarSystem _fogOfWarSystem;
        private readonly HashSet<string> _controlShiftedOwners = new HashSet<string>();
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
        /// Re-evaluates one planet's control state.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>Any ownership-change results produced.</returns>
        public List<GameResult> ReconcilePlanet(Planet planet)
        {
            List<GameResult> results = new List<GameResult>();
            UpdateUncolonizedPlanet(planet, results);
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
            string previousOwnerId = planet.GetOwnerInstanceID();
            Faction previousOwner = string.IsNullOrEmpty(previousOwnerId)
                ? null
                : _game.GetFactionByOwnerInstanceID(previousOwnerId);

            CancelCompetingMissions(planet, newOwner.InstanceID);
            TransferBuildings(planet, newOwner);
            _manufacturingSystem.ClearQueuesOnOwnershipChange(planet);
            EvictEnemyUnits(planet, newOwner.InstanceID);
            planet.EndUprising();
            _game.ChangeUnitOwnership(planet, newOwner.InstanceID);
            if (previousOwner?.InstanceID != newOwner.InstanceID)
                CaptureSnapshotForFaction(planet, previousOwner);

            return CreateOwnershipChangedResult(planet, previousOwner, newOwner);
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
            PlanetOwnershipChangedResult result = NeutralizePlanet(planet);

            foreach (Faction faction in _game.GetFactions())
                planet.SetPopularSupport(faction.InstanceID, 0);

            return result;
        }

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
                    results.Add(controlChange);

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

        private PlanetOwnershipChangedResult NeutralizePlanet(Planet planet)
        {
            string previousOwnerId = planet.GetOwnerInstanceID();
            Faction previousOwner = string.IsNullOrEmpty(previousOwnerId)
                ? null
                : _game.GetFactionByOwnerInstanceID(previousOwnerId);

            CancelCompetingMissions(planet, newOwnerID: null);
            EvictEnemyUnits(planet, newOwnerID: null);
            _manufacturingSystem.ClearQueuesOnOwnershipChange(planet);
            _game.DeregsiterOwnedUnit(planet);
            planet.SetOwnerInstanceID(null);
            CaptureSnapshotForFaction(planet, previousOwner);

            return CreateOwnershipChangedResult(planet, previousOwner, null);
        }

        private PlanetOwnershipChangedResult ChangePlanetControl(Planet planet, Faction newOwner)
        {
            if (planet.GetOwnerInstanceID() == newOwner?.InstanceID)
                return null;

            return newOwner == null ? NeutralizePlanet(planet) : TransferPlanet(planet, newOwner);
        }

        private Faction GetSupportController(Planet planet)
        {
            int threshold = _game.Config.SupportShift.OwnershipTransferThreshold;
            return _game
                .GetFactions()
                .Where(faction => planet.GetPopularSupport(faction.InstanceID) >= threshold)
                .OrderByDescending(faction => planet.GetPopularSupport(faction.InstanceID))
                .FirstOrDefault();
        }

        private Faction GetPlanetController(Planet planet)
        {
            List<string> regimentOwners = planet
                .GetAllRegiments()
                .Where(regiment =>
                    regiment.ManufacturingStatus == ManufacturingStatus.Complete
                    && regiment.Movement == null
                )
                .Select(regiment => regiment.GetOwnerInstanceID())
                .Where(ownerId => !string.IsNullOrEmpty(ownerId))
                .Distinct()
                .ToList();

            if (regimentOwners.Count == 1)
                return _game.GetFactionByOwnerInstanceID(regimentOwners[0]);

            if (regimentOwners.Count > 1)
                return null;

            return GetSupportController(planet);
        }

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

        private PlanetOwnershipChangedResult CreateOwnershipChangedResult(
            Planet planet,
            Faction previousOwner,
            Faction newOwner
        )
        {
            return new PlanetOwnershipChangedResult
            {
                Planet = planet,
                PreviousOwner = previousOwner,
                NewOwner = newOwner,
                Tick = _game.CurrentTick,
            };
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
