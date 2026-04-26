using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
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

        /// <summary>
        /// Creates a new PlanetaryControlSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="movementSystem">Used to evacuate enemy units on ownership change.</param>
        /// <param name="manufacturingSystem">Used to clear queues on ownership change.</param>
        public PlanetaryControlSystem(
            GameRoot game,
            MovementSystem movementSystem,
            ManufacturingSystem manufacturingSystem
        )
        {
            _game = game;
            _movementSystem = movementSystem;
            _manufacturingSystem = manufacturingSystem;
        }

        /// <summary>
        /// Checks for support-driven ownership transfers.
        /// </summary>
        /// <returns>Any ownership change results generated this tick.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();
            ReconcileUncolonizedGarrisons(results);
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
            UpdateGarrisonOwnership(planet, results);
            return results;
        }

        /// <summary>
        /// Reconciles every planet against its current regiment presence.
        /// </summary>
        /// <param name="results">Collection to append any ownership-change results to.</param>
        private void ReconcileUncolonizedGarrisons(List<GameResult> results)
        {
            foreach (Planet planet in _game.GetSceneNodesByType<Planet>())
                UpdateGarrisonOwnership(planet, results);
        }

        /// <summary>
        /// Updates ownership for one planet from its current regiment presence.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <param name="results">Collection to append any ownership-change results to.</param>
        private void UpdateGarrisonOwnership(Planet planet, List<GameResult> results)
        {
            if (planet == null)
                return;

            List<Regiment> regiments = planet.GetAllRegiments();
            string currentOwner = planet.GetOwnerInstanceID();

            if (string.IsNullOrEmpty(currentOwner) && regiments.Count > 0)
            {
                string claimantOwnerId = regiments[0].GetOwnerInstanceID();
                if (string.IsNullOrEmpty(claimantOwnerId))
                    return;

                if (regiments.Any(r => r.GetOwnerInstanceID() != claimantOwnerId))
                    return;

                Faction claimant = _game.GetFactionByOwnerInstanceID(claimantOwnerId);
                if (claimant == null)
                    return;

                ClaimPlanet(planet, claimant);
                results.Add(
                    new PlanetOwnershipChangedResult
                    {
                        Planet = planet,
                        PreviousOwner = null,
                        NewOwner = claimant,
                        Tick = _game.CurrentTick,
                    }
                );
                return;
            }

            if (!planet.IsColonized && !string.IsNullOrEmpty(currentOwner) && regiments.Count == 0)
            {
                Faction previousOwner = _game.GetFactionByOwnerInstanceID(currentOwner);

                ClearPlanetOwnership(planet);
                results.Add(
                    new PlanetOwnershipChangedResult
                    {
                        Planet = planet,
                        PreviousOwner = previousOwner,
                        NewOwner = null,
                        Tick = _game.CurrentTick,
                    }
                );
            }
        }

        /// <summary>
        /// Transfers a planet to a new owner.
        /// </summary>
        /// <param name="planet">The planet to transfer.</param>
        /// <param name="newOwner">The faction receiving ownership.</param>
        public void TransferPlanet(Planet planet, Faction newOwner)
        {
            CancelCompetingMissions(planet, newOwner.InstanceID);
            TransferBuildings(planet, newOwner);
            _manufacturingSystem.ClearQueuesOnOwnershipChange(planet);
            EvictEnemyUnits(planet, newOwner.InstanceID);
            planet.EndUprising();
            _game.ChangeUnitOwnership(planet, newOwner.InstanceID);
        }

        /// <summary>
        /// Claims an uncolonized neutral planet for a faction.
        /// </summary>
        /// <param name="planet">The planet being claimed.</param>
        /// <param name="newOwner">The faction taking control.</param>
        private void ClaimPlanet(Planet planet, Faction newOwner)
        {
            int maxSupport = _game.Config.Planet.MaxPopularSupport;
            _game.ChangeUnitOwnership(planet, newOwner.InstanceID);
            planet.PopularSupport.Clear();

            foreach (Faction faction in _game.GetFactions())
            {
                int support = faction.InstanceID == newOwner.InstanceID ? maxSupport : 0;
                planet.SetPopularSupport(faction.InstanceID, support, maxSupport);
            }
        }

        /// <summary>
        /// Clears the planet's owner, returning it to neutral control. Cancels competing
        /// missions, evicts non-owner units, clears manufacturing queues, and zeroes
        /// popular support for every faction. Buildings are left in place — they remain
        /// where they were built and only transfer when a new faction claims the planet.
        /// </summary>
        /// <param name="planet">The planet whose ownership is being cleared.</param>
        public void ClearPlanetOwnership(Planet planet)
        {
            CancelCompetingMissions(planet, newOwnerID: null);
            EvictEnemyUnits(planet, newOwnerID: null);
            _manufacturingSystem.ClearQueuesOnOwnershipChange(planet);
            _game.DeregsiterOwnedUnit(planet);
            planet.SetOwnerInstanceID(null);

            int maxSupport = _game.Config.Planet.MaxPopularSupport;
            foreach (Faction faction in _game.GetFactions())
                planet.SetPopularSupport(faction.InstanceID, 0, maxSupport);
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
                if (!planet.IsColonized)
                    continue;

                // Only claim unowned planets — owned planets don't flip via support alone.
                if (!string.IsNullOrEmpty(planet.GetOwnerInstanceID()))
                    continue;

                if (planet.GetAllRegiments().Count > 0)
                    continue;

                foreach (Faction faction in _game.GetFactions())
                {
                    int support = planet.GetPopularSupport(faction.InstanceID);
                    if (support <= threshold)
                        continue;

                    TransferPlanet(planet, faction);

                    results.Add(
                        new PlanetOwnershipChangedResult
                        {
                            Planet = planet,
                            PreviousOwner = null,
                            NewOwner = faction,
                            Tick = _game.CurrentTick,
                        }
                    );

                    GameLogger.Log(
                        $"Planet {planet.GetDisplayName()} transferred to {faction.DisplayName} (support {support} > {threshold})"
                    );

                    break;
                }
            }
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
