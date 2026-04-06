using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Enforces maintenance capacity limits each tick.
    /// When a faction's total unit maintenance cost exceeds its maintenance capacity
    /// (refined materials output), one random eligible unit is scrapped per tick.
    ///
    /// Matches the original game's FUN_0052de60/FUN_0052eb40 behavior:
    /// - Maintenance capacity = min(mines, resources, refineries) * refinement multiplier
    /// - Eligible scrap targets: Regiments, Starfighters, CapitalShips, Buildings
    /// - One unit scrapped per tick per faction while in shortfall
    /// </summary>
    public class MaintenanceSystem
    {
        private readonly GameRoot game;

        public MaintenanceSystem(GameRoot game)
        {
            this.game = game;
        }

        /// <summary>
        /// Returns the maintenance capacity for a faction.
        /// This is the total refined output: min(mines, resources, refineries) * multiplier.
        /// </summary>
        public int GetMaintenanceCapacity(Faction faction)
        {
            int rawCount = faction.GetTotalAvailableMaterialsRaw();
            int multiplier = game.GetConfig().Production.RefinementMultiplier;
            return rawCount * multiplier;
        }

        /// <summary>
        /// Returns the total maintenance required by all owned units.
        /// </summary>
        public int GetMaintenanceRequired(Faction faction)
        {
            return faction.GetTotalUnitCost();
        }

        /// <summary>
        /// Checks each faction for maintenance shortfall and scraps one random
        /// eligible unit per faction per tick if over capacity.
        /// </summary>
        public void ProcessTick(IRandomNumberProvider provider)
        {
            foreach (Faction faction in game.GetFactions())
            {
                int capacity = GetMaintenanceCapacity(faction);
                int required = GetMaintenanceRequired(faction);

                if (required > capacity)
                {
                    ScrapRandomUnit(faction, provider);
                }
            }
        }

        /// <summary>
        /// Scraps one random eligible unit from the faction.
        /// Eligible types match the original game's troopsd-to-deffacsd range:
        /// Regiments, Starfighters, CapitalShips (not in transit), and Buildings.
        /// Units currently under construction are excluded.
        /// </summary>
        private void ScrapRandomUnit(Faction faction, IRandomNumberProvider provider)
        {
            List<ISceneNode> candidates = GetScrapCandidates(faction);

            if (candidates.Count == 0)
                return;

            int index = provider.NextInt(0, candidates.Count);
            ISceneNode victim = candidates[index];

            // Track parent fleet before detaching — may need cleanup if last ship removed.
            Fleet parentFleet = victim is CapitalShip ? victim.GetParent() as Fleet : null;

            game.DetachNode(victim);

            // Clean up empty fleet after scrapping last capital ship.
            if (parentFleet != null && parentFleet.CapitalShips.Count == 0)
            {
                game.DetachNode(parentFleet);
            }

            GameLogger.Log(
                $"Maintenance shortfall: scrapped {victim.GetDisplayName()} from {faction.DisplayName}"
            );
        }

        /// <summary>
        /// Returns all units eligible for maintenance auto-scrap.
        /// Excludes units under construction or in transit.
        /// </summary>
        private List<ISceneNode> GetScrapCandidates(Faction faction)
        {
            List<ISceneNode> candidates = new List<ISceneNode>();

            foreach (Regiment regiment in faction.GetOwnedUnitsByType<Regiment>())
            {
                if (regiment.ManufacturingStatus == ManufacturingStatus.Complete && regiment.Movement == null)
                    candidates.Add(regiment);
            }

            foreach (Starfighter fighter in faction.GetOwnedUnitsByType<Starfighter>())
            {
                if (fighter.ManufacturingStatus == ManufacturingStatus.Complete && fighter.Movement == null)
                    candidates.Add(fighter);
            }

            foreach (CapitalShip ship in faction.GetOwnedUnitsByType<CapitalShip>())
            {
                if (ship.ManufacturingStatus == ManufacturingStatus.Complete && ship.Movement == null)
                    candidates.Add(ship);
            }

            foreach (Building building in faction.GetOwnedUnitsByType<Building>())
            {
                if (building.GetManufacturingStatus() == ManufacturingStatus.Complete && building.Movement == null)
                    candidates.Add(building);
            }

            return candidates;
        }
    }
}
