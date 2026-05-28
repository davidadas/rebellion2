using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Enforces maintenance capacity limits each tick.
    /// When a faction's unit maintenance cost exceeds its refined materials output,
    /// one random eligible unit is scrapped on each configured timer pulse until balance is restored.
    /// </summary>
    public class MaintenanceSystem : IGameSystem
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly HashSet<string> _shortfallFactions = new HashSet<string>();
        private readonly Dictionary<string, int> _nextAutoscrapTickByFaction =
            new Dictionary<string, int>();

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
        /// eligible unit per faction on each auto-scrap timer pulse if over capacity.
        /// </summary>
        /// <returns>Any maintenance shortfall or auto-scrap results.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();

            foreach (Faction faction in _game.GetFactions())
                ProcessFactionMaintenance(faction, results);

            return results;
        }

        /// <summary>
        /// Returns the maintenance cost of completed units.
        /// </summary>
        /// <param name="faction">The faction to calculate maintenance for.</param>
        /// <returns>The total maintenance cost of owned units.</returns>
        public int GetMaintenanceRequired(Faction faction)
        {
            return faction.GetTotalMaintenanceCost();
        }

        /// <summary>
        /// Processes maintenance shortfall state and auto-scrapping for one faction.
        /// </summary>
        /// <param name="faction">The faction to process.</param>
        /// <param name="results">Result list to append to.</param>
        private void ProcessFactionMaintenance(Faction faction, List<GameResult> results)
        {
            int capacity = faction.MaintenanceCapacity;
            int required = GetMaintenanceRequired(faction);

            if (!IsInMaintenanceShortfall(required, capacity))
            {
                ClearMaintenanceShortfall(faction);
                return;
            }

            RecordMaintenanceShortfall(faction, required, capacity, results);

            if (!TryConsumeAutoScrapPulse(faction))
                return;

            GameObjectAutoscrappedResult result = ScrapRandomEligibleUnit(faction);
            if (result != null)
                results.Add(result);
        }

        /// <summary>
        /// Returns true when maintenance demand is above capacity.
        /// </summary>
        /// <param name="required">The required maintenance.</param>
        /// <param name="capacity">The available maintenance capacity.</param>
        /// <returns>True if the faction is in shortfall.</returns>
        private static bool IsInMaintenanceShortfall(int required, int capacity)
        {
            return required > capacity;
        }

        /// <summary>
        /// Clears shortfall timer state for a faction.
        /// </summary>
        /// <param name="faction">The faction no longer in shortfall.</param>
        private void ClearMaintenanceShortfall(Faction faction)
        {
            _shortfallFactions.Remove(faction.InstanceID);
            _nextAutoscrapTickByFaction.Remove(faction.InstanceID);
        }

        /// <summary>
        /// Adds the first shortfall result for a faction entering shortfall.
        /// </summary>
        /// <param name="faction">The faction in shortfall.</param>
        /// <param name="required">The required maintenance.</param>
        /// <param name="capacity">The available maintenance capacity.</param>
        /// <param name="results">Result list to append to.</param>
        private void RecordMaintenanceShortfall(
            Faction faction,
            int required,
            int capacity,
            List<GameResult> results
        )
        {
            if (!_shortfallFactions.Add(faction.InstanceID))
                return;

            results.Add(
                new MaintenanceRequiredResult
                {
                    Faction = faction,
                    Amount = required - capacity,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Returns true when the faction's auto-scrap timer has reached its next pulse.
        /// </summary>
        /// <param name="faction">The faction in shortfall.</param>
        /// <returns>True if a unit may be scrapped this tick.</returns>
        private bool TryConsumeAutoScrapPulse(Faction faction)
        {
            int interval = _game.Config.Production.MaintenanceShortfallAutoscrapInterval;
            if (!_nextAutoscrapTickByFaction.TryGetValue(faction.InstanceID, out int nextTick))
            {
                _nextAutoscrapTickByFaction[faction.InstanceID] = _game.CurrentTick + interval;
                return false;
            }

            if (_game.CurrentTick < nextTick)
                return false;

            _nextAutoscrapTickByFaction[faction.InstanceID] = _game.CurrentTick + interval;
            return true;
        }

        /// <summary>
        /// Scraps one random eligible unit from the faction.
        /// </summary>
        /// <param name="faction">The faction whose unit is scrapped.</param>
        /// <returns>The auto-scrap result, or null if no unit can be scrapped.</returns>
        private GameObjectAutoscrappedResult ScrapRandomEligibleUnit(Faction faction)
        {
            List<IManufacturable> candidates = faction
                .GetAllOwnedManufacturables()
                .Where(m =>
                    IsAutoScrapEligibleType(m)
                    && m.GetManufacturingStatus() == ManufacturingStatus.Complete
                    && m.Movement == null
                    && m.GetMaintenanceCost() > 0
                )
                .ToList();

            if (candidates.Count == 0)
                return null;

            IManufacturable victim = candidates[_provider.NextInt(0, candidates.Count)];
            Planet location = victim.GetParentOfType<Planet>();

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
        /// Returns true when a manufacturable type can be auto-scrapped.
        /// </summary>
        /// <param name="manufacturable">The manufacturable to inspect.</param>
        /// <returns>True if the type can be auto-scrapped.</returns>
        private static bool IsAutoScrapEligibleType(IManufacturable manufacturable)
        {
            return manufacturable
                is Regiment
                    or CapitalShip
                    or Starfighter
                    or SpecialForces
                    or Building;
        }
    }
}
