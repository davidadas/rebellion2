using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Enforces maintenance capacity limits each tick.
    /// When a faction's unit maintenance cost exceeds its refined materials output,
    /// one random eligible unit is scrapped per tick until balance is restored.
    /// </summary>
    public class MaintenanceSystem : IGameSystem
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly HashSet<string> _shortfallFactions = new HashSet<string>();

        /// <summary>
        /// Creates a new MaintenanceSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">Random number provider for scrap target selection.</param>
        public MaintenanceSystem(GameRoot game, IRandomNumberProvider provider)
        {
            _game = game;
            _provider = provider;
        }

        /// <summary>
        /// Checks each faction for maintenance shortfall and scraps one random
        /// eligible unit per faction per tick if over capacity.
        /// </summary>
        /// <returns>Any maintenance shortfall or auto-scrap results.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();

            foreach (Faction faction in _game.GetFactions())
                EnforceMaintenanceLimit(faction, results);

            return results;
        }

        /// <summary>
        /// Returns the maintenance capacity (refined output) for a faction.
        /// </summary>
        /// <param name="faction">The faction to calculate capacity for.</param>
        /// <returns>The faction's total refined material output.</returns>
        public int GetMaintenanceCapacity(Faction faction)
        {
            int rawCount = faction.GetTotalAvailableMaterialsRaw();
            int multiplier = _game.GetConfig().Production.RefinementMultiplier;
            return rawCount * multiplier;
        }

        /// <summary>
        /// Returns the total maintenance cost of all completed units. Excludes in-progress
        /// construction cost, which is paid upfront when the build is enqueued.
        /// </summary>
        /// <param name="faction">The faction to calculate maintenance for.</param>
        /// <returns>The total maintenance cost of completed owned units.</returns>
        public int GetMaintenanceRequired(Faction faction)
        {
            return faction.GetTotalMaintenanceCost();
        }

        /// <summary>
        /// Checks a single faction for maintenance shortfall, notifies on state change,
        /// and scraps one random unit if over capacity.
        /// </summary>
        private void EnforceMaintenanceLimit(Faction faction, List<GameResult> results)
        {
            int capacity = GetMaintenanceCapacity(faction);
            int required = GetMaintenanceRequired(faction);
            bool inShortfall = required > capacity;

            // Notify on first tick of shortfall; clear when recovered.
            if (inShortfall && _shortfallFactions.Add(faction.InstanceID))
            {
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
                return;
            }

            // Scrap one random unit while over capacity.
            GameObjectAutoscrappedResult result = ScrapRandomUnit(faction);
            if (result != null)
                results.Add(result);
        }

        /// <summary>
        /// Scraps one random eligible unit from the faction.
        /// </summary>
        private GameObjectAutoscrappedResult ScrapRandomUnit(Faction faction)
        {
            List<IManufacturable> candidates = GetScrapCandidates(faction);

            if (candidates.Count == 0)
                return null;

            IManufacturable victim = candidates[_provider.NextInt(0, candidates.Count)];
            Planet location = victim.GetParentOfType<Planet>();

            // Track parent fleet before detaching — may need cleanup if last ship removed.
            Fleet parentFleet = victim is CapitalShip ? victim.GetParent() as Fleet : null;

            _game.DetachNode(victim);

            if (parentFleet?.CapitalShips.Count == 0)
                _game.DetachNode(parentFleet);

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
        /// Returns all completed, stationary units eligible for auto-scrap.
        /// </summary>
        private List<IManufacturable> GetScrapCandidates(Faction faction)
        {
            return faction
                .GetAllOwnedManufacturables()
                .Where(m =>
                    m.GetManufacturingStatus() == ManufacturingStatus.Complete && m.Movement == null
                )
                .ToList();
        }
    }
}
