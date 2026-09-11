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
    private sealed class ManufacturedUnitTracker
    {
        private readonly HashSet<string> _seenCapitalShips = new HashSet<string>();
        private readonly HashSet<string> _seenStarfighters = new HashSet<string>();
        private readonly HashSet<string> _seenRegiments = new HashSet<string>();
        private readonly HashSet<string> _seenSpecialForces = new HashSet<string>();
        private readonly HashSet<string> _seenBuildings = new HashSet<string>();
        private readonly Dictionary<string, ManufacturedUnitCounts> _manufacturedByFaction =
            new Dictionary<string, ManufacturedUnitCounts>(StringComparer.Ordinal);

        /// <summary>
        /// Records units present before simulation ticks are processed.
        /// </summary>
        /// <param name="game">The game state to inspect.</param>
        /// <param name="specialForces">The shared initial special-forces snapshot.</param>
        public void RecordInitialState(
            GameRoot game,
            IReadOnlyCollection<SpecialForces> specialForces
        )
        {
            RecordSeenOnly(
                game.GetSceneNodesByType<CapitalShip>().Where(IsComplete),
                _seenCapitalShips
            );
            RecordSeenOnly(
                game.GetSceneNodesByType<Starfighter>().Where(IsComplete),
                _seenStarfighters
            );
            RecordSeenOnly(game.GetSceneNodesByType<Regiment>().Where(IsComplete), _seenRegiments);
            RecordSeenOnly(specialForces.Where(IsComplete), _seenSpecialForces);
            RecordSeenOnly(game.GetSceneNodesByType<Building>().Where(IsComplete), _seenBuildings);
        }

        /// <summary>
        /// Records completed manufactured items from resolved lifecycle results.
        /// </summary>
        /// <param name="results">The resolved game results.</param>
        public void Record(IReadOnlyList<GameResult> results)
        {
            if (results == null)
                return;

            foreach (GameObjectDeployedResult result in results.OfType<GameObjectDeployedResult>())
                RecordNewUnit(result.GameObject as IManufacturable);
        }

        /// <summary>
        /// Gets manufactured capital ships for a faction.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <returns>The manufactured capital ship count.</returns>
        public int GetManufacturedCapitalShips(string factionId) =>
            TryGetCounts(factionId, out ManufacturedUnitCounts counts) ? counts.CapitalShips : 0;

        /// <summary>
        /// Gets manufactured starfighters for a faction.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <returns>The manufactured starfighter count.</returns>
        public int GetManufacturedStarfighters(string factionId) =>
            TryGetCounts(factionId, out ManufacturedUnitCounts counts) ? counts.Starfighters : 0;

        /// <summary>
        /// Gets manufactured regiments for a faction.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <returns>The manufactured regiment count.</returns>
        public int GetManufacturedRegiments(string factionId) =>
            TryGetCounts(factionId, out ManufacturedUnitCounts counts) ? counts.Regiments : 0;

        /// <summary>
        /// Gets manufactured special forces for a faction.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <returns>The manufactured special forces count.</returns>
        public int GetManufacturedSpecialForces(string factionId) =>
            TryGetCounts(factionId, out ManufacturedUnitCounts counts) ? counts.SpecialForces : 0;

        /// <summary>
        /// Gets manufactured units grouped by category and content type.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <returns>The manufactured unit-type summaries.</returns>
        public ManufacturedUnitTypeSummary[] GetManufacturedUnitTypes(string factionId) =>
            TryGetCounts(factionId, out ManufacturedUnitCounts counts)
                ? counts
                    .UnitsByType.Values.OrderBy(summary => summary.Category)
                    .ThenBy(summary => summary.DisplayName)
                    .ThenBy(summary => summary.TypeId)
                    .ToArray()
                : Array.Empty<ManufacturedUnitTypeSummary>();

        /// <summary>
        /// Gets manufactured buildings for a faction.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <returns>The manufactured building count.</returns>
        public int GetManufacturedBuildings(string factionId) =>
            TryGetCounts(factionId, out ManufacturedUnitCounts counts) ? counts.Buildings : 0;

        /// <summary>
        /// Gets manufactured buildings of a type for a faction.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <param name="buildingType">The building type to count.</param>
        /// <returns>The manufactured building count.</returns>
        public int GetManufacturedBuildings(string factionId, BuildingType buildingType) =>
            TryGetCounts(factionId, out ManufacturedUnitCounts counts)
            && counts.BuildingsByType.TryGetValue(buildingType, out int count)
                ? count
                : 0;

        /// <summary>
        /// Records existing units without counting them as manufactured.
        /// </summary>
        /// <typeparam name="T">The scene node type to record.</typeparam>
        /// <param name="units">The units to record.</param>
        /// <param name="seen">The set that receives unit IDs.</param>
        private static void RecordSeenOnly<T>(IEnumerable<T> units, HashSet<string> seen)
            where T : ISceneNode, IManufacturable
        {
            foreach (T unit in units)
            {
                string instanceId = unit.GetInstanceID();
                if (!string.IsNullOrEmpty(instanceId))
                    seen.Add(instanceId);
            }
        }

        /// <summary>
        /// Records a newly deployed manufactured item and increments its faction totals.
        /// </summary>
        /// <param name="item">The deployed item.</param>
        private void RecordNewUnit(IManufacturable item)
        {
            if (!IsManufactured(item))
                return;

            string instanceId = item.GetInstanceID();
            string factionId = item.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(factionId))
                return;

            ManufacturedUnitCounts counts = GetCounts(factionId);
            switch (item)
            {
                case CapitalShip when _seenCapitalShips.Add(instanceId):
                    counts.CapitalShips++;
                    counts.RecordType("CapitalShip", item.GetTypeID(), item.GetDisplayName());
                    break;
                case Starfighter when _seenStarfighters.Add(instanceId):
                    counts.Starfighters++;
                    counts.RecordType("Starfighter", item.GetTypeID(), item.GetDisplayName());
                    break;
                case Regiment when _seenRegiments.Add(instanceId):
                    counts.Regiments++;
                    counts.RecordType("Regiment", item.GetTypeID(), item.GetDisplayName());
                    break;
                case SpecialForces when _seenSpecialForces.Add(instanceId):
                    counts.SpecialForces++;
                    counts.RecordType("SpecialForces", item.GetTypeID(), item.GetDisplayName());
                    break;
                case Building building when _seenBuildings.Add(instanceId):
                    counts.Buildings++;
                    counts.BuildingsByType.TryGetValue(building.BuildingType, out int count);
                    counts.BuildingsByType[building.BuildingType] = count + 1;
                    counts.RecordType("Building", item.GetTypeID(), item.GetDisplayName());
                    break;
            }
        }

        /// <summary>
        /// Determines whether a manufacturable item has completed production.
        /// </summary>
        /// <param name="item">The item to inspect.</param>
        /// <returns>True when production is complete.</returns>
        private static bool IsComplete(IManufacturable item) =>
            item?.ManufacturingStatus == ManufacturingStatus.Complete;

        /// <summary>
        /// Determines whether a completed item was produced during the game.
        /// </summary>
        /// <param name="item">The item to inspect.</param>
        /// <returns>True when the item has an originating producer planet.</returns>
        private static bool IsManufactured(IManufacturable item) =>
            IsComplete(item) && !string.IsNullOrEmpty(item.ProducerPlanetID);

        /// <summary>
        /// Gets or creates manufactured unit counts for a faction.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <returns>The manufactured unit counts.</returns>
        private ManufacturedUnitCounts GetCounts(string factionId)
        {
            if (!_manufacturedByFaction.TryGetValue(factionId, out ManufacturedUnitCounts counts))
            {
                counts = new ManufacturedUnitCounts();
                _manufacturedByFaction[factionId] = counts;
            }

            return counts;
        }

        /// <summary>
        /// Gets manufactured unit counts for a faction.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <param name="counts">The manufactured unit counts.</param>
        /// <returns>True if counts exist for the faction.</returns>
        private bool TryGetCounts(string factionId, out ManufacturedUnitCounts counts) =>
            _manufacturedByFaction.TryGetValue(factionId, out counts);
    }

    private sealed class ManufacturedUnitCounts
    {
        public int CapitalShips;
        public int Starfighters;
        public int Regiments;
        public int SpecialForces;
        public int Buildings;
        public Dictionary<BuildingType, int> BuildingsByType = new Dictionary<BuildingType, int>();
        public Dictionary<string, ManufacturedUnitTypeSummary> UnitsByType = new Dictionary<
            string,
            ManufacturedUnitTypeSummary
        >(StringComparer.Ordinal);

        /// <summary>
        /// Records one completed manufactured unit by category and content type.
        /// </summary>
        /// <param name="category">The broad unit category.</param>
        /// <param name="typeId">The content type identifier.</param>
        /// <param name="displayName">The player-facing unit name.</param>
        public void RecordType(string category, string typeId, string displayName)
        {
            string key = $"{category}:{typeId}";
            if (!UnitsByType.TryGetValue(key, out ManufacturedUnitTypeSummary summary))
            {
                summary = new ManufacturedUnitTypeSummary
                {
                    Category = category,
                    TypeId = typeId,
                    DisplayName = displayName,
                };
                UnitsByType[key] = summary;
            }

            summary.Count++;
        }
    }

    [Serializable]
    private sealed class ManufacturedUnitTypeSummary
    {
        public string Category;
        public string TypeId;
        public string DisplayName;
        public int Count;
    }

    [Serializable]
    private sealed class ManufacturingIdleSummary
    {
        public int BuildingIdlePlanetTicks;
        public int ShipIdlePlanetTicks;
        public int TroopIdlePlanetTicks;
        public int BuildingIdleCapacityTicks;
        public int ShipIdleCapacityTicks;
        public int TroopIdleCapacityTicks;
        public ManufacturingIdleResourceSummary BuildingResources;
        public ManufacturingIdleResourceSummary ShipResources;
        public ManufacturingIdleResourceSummary TroopResources;
        public ManufacturingIdlePlanetSummary[] TopIdlePlanets;
    }

    [Serializable]
    private sealed class ManufacturingIdleResourceSummary
    {
        public int SampleCount;
        public int FundedSampleCount;
        public int FundedCapacityTicks;
        public double AverageRawMaterialStockpile;
        public double AverageRefinedMaterialStockpile;
        public double AverageMaintenanceHeadroom;
        public int MinimumRawMaterialStockpile;
        public int MinimumRefinedMaterialStockpile;
        public int MinimumMaintenanceHeadroom;
        public int MaximumRawMaterialStockpile;
        public int MaximumRefinedMaterialStockpile;
        public int MaximumMaintenanceHeadroom;
    }

    [Serializable]
    private sealed class ManufacturingIdlePlanetSummary
    {
        public string PlanetId;
        public string PlanetName;
        public int BuildingIdleTicks;
        public int ShipIdleTicks;
        public int TroopIdleTicks;
        public int BuildingIdleCapacityTicks;
        public int ShipIdleCapacityTicks;
        public int TroopIdleCapacityTicks;
    }

    [Serializable]
    private sealed class CurrentIdlePlanetSummary
    {
        public string PlanetId;
        public string PlanetName;
        public int BuildingSlots;
        public int ShipSlots;
        public int TroopSlots;
        public int RawResourceNodes;
        public int ActiveMines;
        public int ActiveRefineries;
        public int ConstructionFacilities;
        public int Shipyards;
        public int TrainingFacilities;
        public int BuildingQueueCount;
        public int ShipQueueCount;
        public int TroopQueueCount;
    }

    [Serializable]
    private sealed class ProductionFacilityPlanetSummary
    {
        public string PlanetId;
        public string PlanetName;
        public int ConstructionFacilities;
        public int Shipyards;
        public int TrainingFacilities;
    }

    [Serializable]
    private sealed class FleetSimulationSummary
    {
        public string FleetId;
        public string DisplayName;
        public string RoleType;
        public string LocationPlanetId;
        public string LocationPlanetName;
        public bool InTransit;
        public int TransitTicksRemaining;
        public int CombatValue;
        public int CapitalShipCount;
        public int StarfighterCount;
        public int RegimentCount;
        public int OfficerCount;
        public string OrderType;
        public string OrderStatus;
        public string OrderTargetPlanetId;
        public string OrderTargetPlanetName;
        public string OrderTargetOwnerId;
        public int GroundAttackStrength;
        public int BombardmentStrength;
        public int RegimentCapacity;
        public int RequiredAttackCombatStrength;
        public int RequiredAttackRegimentCount;
        public int RequiredAttackRegimentStrength;
        public int RequiredBombardmentStrength;
        public int TargetRegimentDefenseStrength;
        public int TargetShieldStrength;
        public int TargetRegimentCount;
        public int TargetStrongestHostileFleetStrength;
        public string[] CapitalShips;
        public string[] Starfighters;
        public string[] Regiments;
        public string[] Officers;
    }

    [Serializable]
    private sealed class FleetHistorySnapshot
    {
        public int Tick;
        public string FactionId;
        public string FactionName;
        public string FleetId;
        public string DisplayName;
        public string RoleType;
        public string LocationPlanetId;
        public string LocationPlanetName;
        public bool InTransit;
        public bool Destroyed;
        public int TransitTicksRemaining;
        public int CombatValue;
        public int CapitalShipCount;
        public int StarfighterCount;
        public int RegimentCount;
        public int OfficerCount;
        public string OrderType;
        public string OrderStatus;
        public string OrderTargetPlanetId;
        public string OrderTargetPlanetName;
        public string[] CapitalShips;
        public string[] Starfighters;
        public string[] Regiments;
        public string[] Officers;
    }

}
