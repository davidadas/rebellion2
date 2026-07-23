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
    /// When a faction's committed maintenance cost exceeds its resource-facility capacity,
    /// one random eligible unit is scrapped on each configured timer pulse until balance is restored.
    /// </summary>
    public class MaintenanceSystem : IGameSystem
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly FleetSystem _fleetSystem;
        private readonly HashSet<string> _shortfallFactions = new HashSet<string>();
        private readonly Dictionary<string, int> _nextAutoscrapTickByFaction =
            new Dictionary<string, int>();

        /// <summary>
        /// Creates a new MaintenanceSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">Random number provider for scrap target selection.</param>
        /// <param name="fleetSystem">Owns empty-fleet cleanup.</param>
        public MaintenanceSystem(
            GameRoot game,
            IRandomNumberProvider provider,
            FleetSystem fleetSystem
        )
        {
            _game = game ?? throw new System.ArgumentNullException(nameof(game));
            _provider = provider;
            _fleetSystem =
                fleetSystem ?? throw new System.ArgumentNullException(nameof(fleetSystem));
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
        /// Returns the maintenance cost reserved by completed and committed units.
        /// </summary>
        /// <param name="faction">The faction to calculate maintenance for.</param>
        /// <returns>The total maintenance cost of owned units.</returns>
        public int GetMaintenanceRequired(Faction faction)
        {
            return faction.GetTotalProjectedMaintenanceCost();
        }

        /// <summary>
        /// Scraps owned manufacturable units selected by the player.
        /// </summary>
        /// <param name="items">The units selected for scrapping.</param>
        /// <param name="ownerInstanceId">The faction authorized to scrap the units.</param>
        /// <param name="results">Receives results produced by the completed scrap operation.</param>
        /// <returns>True when every selected unit was scrapped.</returns>
        public bool TryScrap(
            IReadOnlyList<IManufacturable> items,
            string ownerInstanceId,
            out List<GameResult> results
        )
        {
            results = new List<GameResult>();
            if (items == null || items.Count == 0 || string.IsNullOrEmpty(ownerInstanceId))
                return false;

            List<IManufacturable> liveItems = new List<IManufacturable>(items.Count);
            foreach (IManufacturable item in items)
            {
                ISceneNode selectedNode = item as ISceneNode;
                ISceneNode liveNode = _game.GetSceneNodeByInstanceID<ISceneNode>(
                    selectedNode?.InstanceID
                );
                if (
                    liveNode is not IManufacturable liveItem
                    || liveNode.GetParent() == null
                    || !string.Equals(
                        liveNode.GetOwnerInstanceID(),
                        ownerInstanceId,
                        System.StringComparison.Ordinal
                    )
                    || liveItem.GetManufacturingStatus() != ManufacturingStatus.Complete
                )
                    return false;

                liveItems.Add(liveItem);
            }

            foreach (IManufacturable item in liveItems)
                Scrap(item, results);

            return true;
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

            ScrapRandomEligibleUnit(faction, results);
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
        /// <param name="results">The result collection receiving the scrap effects.</param>
        private void ScrapRandomEligibleUnit(Faction faction, List<GameResult> results)
        {
            List<IManufacturable> candidates = faction
                .GetAllOwnedManufacturables()
                .Where(m =>
                    IsAutoScrapEligibleType(m)
                    && m.GetManufacturingStatus() == ManufacturingStatus.Complete
                    && m.GetMaintenanceCost() > 0
                )
                .ToList();

            if (candidates.Count == 0)
                return;

            IManufacturable victim = candidates[_provider.NextInt(0, candidates.Count)];
            Planet location = victim.GetParentOfType<Planet>();

            results.Add(
                new GameObjectAutoscrappedResult
                {
                    DestroyedObject = victim,
                    Context = location,
                    Tick = _game.CurrentTick,
                }
            );
            Scrap(victim, results);

            GameLogger.Log(
                $"Maintenance shortfall: scrapped {victim.GetDisplayName()} from {faction.DisplayName}"
            );
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

        /// <summary>
        /// Removes a scrapped manufacturable and any fleet left empty by its removal.
        /// </summary>
        /// <param name="item">The manufacturable to remove.</param>
        /// <param name="results">The result collection receiving a garrison change.</param>
        private void Scrap(IManufacturable item, List<GameResult> results)
        {
            ISceneNode node = item as ISceneNode;
            Planet garrisonPlanet =
                item is Regiment regiment
                && regiment.ManufacturingStatus == ManufacturingStatus.Complete
                && regiment.Movement == null
                && regiment.GetParent() is Planet planet
                    ? planet
                    : null;
            Fleet parentFleet = item is CapitalShip ? node?.GetParent() as Fleet : null;
            RefundScrapMaterials(item);
            _game.DetachNode(node);
            _fleetSystem.RemoveIfEmpty(parentFleet);

            if (
                garrisonPlanet != null
                && !results
                    .OfType<PlanetGarrisonChangedResult>()
                    .Any(result => result.Planet == garrisonPlanet)
            )
            {
                results.Add(
                    new PlanetGarrisonChangedResult
                    {
                        Planet = garrisonPlanet,
                        Tick = _game.CurrentTick,
                    }
                );
            }
        }

        /// <summary>
        /// Returns the configured share of a completed item's construction material to its owner.
        /// </summary>
        /// <param name="item">The completed item being scrapped.</param>
        private void RefundScrapMaterials(IManufacturable item)
        {
            int refund = item.GetConstructionCost() / _game.Config.Production.ScrapRefundDivisor;
            if (refund <= 0)
                return;

            Faction faction = _game.GetFactionByOwnerInstanceID(item.GetOwnerInstanceID());
            faction.RefinedMaterialStockpile += refund;
        }
    }
}
