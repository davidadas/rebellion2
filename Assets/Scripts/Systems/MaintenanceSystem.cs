using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
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
        private readonly GameRoot _game;
        private readonly HashSet<string> _shortfallFactions = new HashSet<string>();

        public MaintenanceSystem(GameRoot game)
        {
            _game = game;
        }

        /// <summary>
        /// Returns the maintenance capacity for a faction.
        /// This is the total refined output: min(mines, resources, refineries) * multiplier.
        /// </summary>
        public int GetMaintenanceCapacity(Faction faction)
        {
            int rawCount = faction.GetTotalAvailableMaterialsRaw();
            int multiplier = _game.GetConfig().Production.RefinementMultiplier;
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
        public List<GameResult> ProcessTick(IRandomNumberProvider provider)
        {
            List<GameResult> results = new List<GameResult>();

            foreach (Faction faction in _game.GetFactions())
            {
                int capacity = GetMaintenanceCapacity(faction);
                int required = GetMaintenanceRequired(faction);
                bool inShortfall = required > capacity;

                if (inShortfall && !_shortfallFactions.Contains(faction.InstanceID))
                {
                    _shortfallFactions.Add(faction.InstanceID);
                    results.Add(
                        new MaintenanceRequiredResult
                        {
                            Faction = faction,
                            Amount = required - capacity,
                            Tick = _game.CurrentTick,
                        }
                    );
                }
                else if (!inShortfall)
                {
                    _shortfallFactions.Remove(faction.InstanceID);
                }

                if (inShortfall)
                {
                    GameObjectAutoscrappedResult result = ScrapRandomUnit(faction, provider);
                    if (result != null)
                        results.Add(result);
                }
            }

            return results;
        }

        /// <summary>
        /// Scraps one random eligible unit from the faction.
        /// Eligible types match the original game's troopsd-to-deffacsd range:
        /// Regiments, Starfighters, CapitalShips (not in transit), and Buildings.
        /// Units currently under construction are excluded.
        /// </summary>
        private GameObjectAutoscrappedResult ScrapRandomUnit(
            Faction faction,
            IRandomNumberProvider provider
        )
        {
            List<ISceneNode> candidates = GetScrapCandidates(faction);

            if (candidates.Count == 0)
                return null;

            int index = provider.NextInt(0, candidates.Count);
            ISceneNode victim = candidates[index];
            Planet location = victim.GetParentOfType<Planet>();

            // Track parent fleet before detaching — may need cleanup if last ship removed.
            Fleet parentFleet = victim is CapitalShip ? victim.GetParent() as Fleet : null;

            _game.DetachNode(victim);

            // Clean up empty fleet after scrapping last capital ship.
            if (parentFleet?.CapitalShips.Count == 0)
            {
                _game.DetachNode(parentFleet);
            }

            GameLogger.Log(
                $"Maintenance shortfall: scrapped {victim.GetDisplayName()} from {faction.DisplayName}"
            );

            return new GameObjectAutoscrappedResult
            {
                DestroyedObject = victim as IGameEntity,
                Context = location,
                Tick = _game.CurrentTick,
            };
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
                if (
                    regiment.ManufacturingStatus == ManufacturingStatus.Complete
                    && regiment.Movement == null
                )
                    candidates.Add(regiment);
            }

            foreach (Starfighter fighter in faction.GetOwnedUnitsByType<Starfighter>())
            {
                if (
                    fighter.ManufacturingStatus == ManufacturingStatus.Complete
                    && fighter.Movement == null
                )
                    candidates.Add(fighter);
            }

            foreach (CapitalShip ship in faction.GetOwnedUnitsByType<CapitalShip>())
            {
                if (
                    ship.ManufacturingStatus == ManufacturingStatus.Complete
                    && ship.Movement == null
                )
                    candidates.Add(ship);
            }

            foreach (Building building in faction.GetOwnedUnitsByType<Building>())
            {
                if (
                    building.GetManufacturingStatus() == ManufacturingStatus.Complete
                    && building.Movement == null
                )
                    candidates.Add(building);
            }

            return candidates;
        }
    }
}
