using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Answers resource-production and maintenance questions from the active game state.
    /// </summary>
    public sealed class ResourceProductionQueries
    {
        private const int _percentScale = 100;

        private readonly GameRoot _game;

        /// <summary>
        /// Creates resource-production queries for one game.
        /// </summary>
        /// <param name="game">The authoritative game state.</param>
        public ResourceProductionQueries(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <summary>
        /// Projects current facility counts, output rates, and maintenance totals for a faction.
        /// </summary>
        /// <param name="faction">The faction whose economy is inspected.</param>
        /// <returns>The current resource economy summary.</returns>
        public ResourceEconomySummary GetSummary(Faction faction)
        {
            if (faction == null)
                return ResourceEconomySummary.Empty;

            List<Building> activeMines = GetActiveFacilities(faction, BuildingType.Mine);
            List<Building> activeRefineries = GetActiveFacilities(faction, BuildingType.Refinery);
            List<Building> deliveredMines = GetProjectedFacilities(
                faction,
                BuildingType.Mine,
                includeBuilding: false
            );
            List<Building> deliveredRefineries = GetProjectedFacilities(
                faction,
                BuildingType.Refinery,
                includeBuilding: false
            );
            List<Building> projectedMines = GetProjectedFacilities(
                faction,
                BuildingType.Mine,
                includeBuilding: true
            );
            List<Building> projectedRefineries = GetProjectedFacilities(
                faction,
                BuildingType.Refinery,
                includeBuilding: true
            );
            HashSet<string> activeMineIDs = activeMines
                .Select(facility => facility.InstanceID)
                .ToHashSet(StringComparer.Ordinal);
            HashSet<string> activeRefineryIDs = activeRefineries
                .Select(facility => facility.InstanceID)
                .ToHashSet(StringComparer.Ordinal);
            ResourceFacilityCounts mines = CountFacilities(
                faction,
                BuildingType.Mine,
                activeMineIDs
            );
            ResourceFacilityCounts refineries = CountFacilities(
                faction,
                BuildingType.Refinery,
                activeRefineryIDs
            );
            MaintenanceCostBreakdown maintenance = CalculateMaintenanceCosts(faction);

            return new ResourceEconomySummary(
                mines,
                refineries,
                CalculateOutputPerTick(faction, activeMines, BuildingType.Mine),
                CalculateProjectedOutputPerTick(
                    faction,
                    deliveredMines,
                    BuildingType.Mine,
                    maintenance.AfterDelivery
                ),
                CalculateProjectedOutputPerTick(
                    faction,
                    projectedMines,
                    BuildingType.Mine,
                    maintenance.Committed
                ),
                CalculateOutputPerTick(faction, activeRefineries, BuildingType.Refinery),
                CalculateProjectedOutputPerTick(
                    faction,
                    deliveredRefineries,
                    BuildingType.Refinery,
                    maintenance.AfterDelivery
                ),
                CalculateProjectedOutputPerTick(
                    faction,
                    projectedRefineries,
                    BuildingType.Refinery,
                    maintenance.Committed
                ),
                faction.MaintenanceCapacity,
                CalculateProjectedMaintenanceCapacity(
                    faction,
                    deliveredMines.Count,
                    deliveredRefineries.Count
                ),
                CalculateProjectedMaintenanceCapacity(
                    faction,
                    projectedMines.Count,
                    projectedRefineries.Count
                ),
                maintenance
            );
        }

        /// <summary>
        /// Totals committed maintenance by deployed asset type and unfinished order.
        /// </summary>
        /// <param name="faction">The faction whose maintenance is classified.</param>
        /// <returns>The classified maintenance costs.</returns>
        private static MaintenanceCostBreakdown CalculateMaintenanceCosts(Faction faction)
        {
            int capitalShips = 0;
            int starfighters = 0;
            int regiments = 0;
            int specialForces = 0;
            int facilities = 0;
            int orders = 0;
            int deployed = 0;
            int enRoute = 0;
            int building = 0;

            foreach (IManufacturable item in faction.GetAllOwnedManufacturables())
            {
                int cost = item.GetMaintenanceCost();
                if (item.GetManufacturingStatus() == ManufacturingStatus.Building)
                {
                    orders += cost;
                    building += cost;
                    continue;
                }

                if (item.GetManufacturingStatus() == ManufacturingStatus.Delivering)
                    enRoute += cost;
                else
                    deployed += cost;

                switch (item)
                {
                    case CapitalShip:
                        capitalShips += cost;
                        break;
                    case Starfighter:
                        starfighters += cost;
                        break;
                    case Regiment:
                        regiments += cost;
                        break;
                    case SpecialForces:
                        specialForces += cost;
                        break;
                    case Building:
                        facilities += cost;
                        break;
                }
            }

            return new MaintenanceCostBreakdown(
                capitalShips,
                starfighters,
                regiments,
                specialForces,
                facilities,
                orders,
                deployed,
                enRoute,
                building
            );
        }

        /// <summary>
        /// Gets facilities that resource processing will advance on the next tick.
        /// </summary>
        /// <param name="faction">The owning faction.</param>
        /// <param name="buildingType">The requested resource-facility type.</param>
        /// <returns>The active facilities in stable planet and building order.</returns>
        internal static List<Building> GetActiveFacilities(
            Faction faction,
            BuildingType buildingType
        )
        {
            List<Building> facilities = new List<Building>();
            foreach (Planet planet in faction.GetOwnedColonizedPlanets())
            {
                if (planet.IsResourceProductionSuspended())
                    continue;

                IEnumerable<Building> planetFacilities = planet
                    .GetChildren<Building>()
                    .Where(building =>
                        building.BuildingType == buildingType
                        && building.ManufacturingStatus == ManufacturingStatus.Complete
                        && building.Movement == null
                        && building.ProcessRate > 0
                    );
                if (buildingType == BuildingType.Mine)
                    planetFacilities = planetFacilities.Take(planet.NumRawResourceNodes);

                facilities.AddRange(planetFacilities);
            }

            return facilities;
        }

        /// <summary>
        /// Gets facilities expected to operate after a selected committed lifecycle stage.
        /// </summary>
        /// <param name="faction">The owning faction.</param>
        /// <param name="buildingType">The requested resource-facility type.</param>
        /// <param name="includeBuilding">Whether facilities still being manufactured are included.</param>
        /// <returns>The projected facilities in stable planet and building order.</returns>
        private static List<Building> GetProjectedFacilities(
            Faction faction,
            BuildingType buildingType,
            bool includeBuilding
        )
        {
            List<Building> facilities = new List<Building>();
            foreach (Planet planet in faction.GetOwnedColonizedPlanets())
            {
                if (planet.IsResourceProductionSuspended())
                    continue;

                IEnumerable<Building> planetFacilities = planet
                    .GetChildren<Building>()
                    .Where(building =>
                        building.BuildingType == buildingType
                        && building.ProcessRate > 0
                        && (
                            includeBuilding
                            || building.ManufacturingStatus != ManufacturingStatus.Building
                        )
                    )
                    .OrderBy(building =>
                        building.ManufacturingStatus == ManufacturingStatus.Complete
                        && building.Movement == null
                            ? 0
                        : building.ManufacturingStatus == ManufacturingStatus.Delivering
                        || building.Movement != null
                            ? 1
                        : 2
                    );
                if (buildingType == BuildingType.Mine)
                    planetFacilities = planetFacilities.Take(planet.NumRawResourceNodes);

                facilities.AddRange(planetFacilities);
            }

            return facilities;
        }

        /// <summary>
        /// Calculates a steady resource cycle from facility rate, maintenance load, and support.
        /// </summary>
        /// <param name="game">The game containing production configuration.</param>
        /// <param name="faction">The faction operating the facility.</param>
        /// <param name="facility">The resource facility.</param>
        /// <returns>The steady cycle duration in ticks.</returns>
        internal static int CalculateSteadyCycleDuration(
            GameRoot game,
            Faction faction,
            Building facility
        )
        {
            return CalculateSteadyCycleDuration(
                game,
                faction,
                facility,
                facility.ResourceMaintenanceAllocation
            );
        }

        /// <summary>
        /// Calculates a steady resource cycle for an explicit projected maintenance allocation.
        /// </summary>
        /// <param name="game">The game containing production configuration.</param>
        /// <param name="faction">The faction operating the facility.</param>
        /// <param name="facility">The resource facility.</param>
        /// <param name="maintenanceAllocation">The projected maintenance load.</param>
        /// <returns>The steady cycle duration in ticks.</returns>
        private static int CalculateSteadyCycleDuration(
            GameRoot game,
            Faction faction,
            Building facility,
            int maintenanceAllocation
        )
        {
            GameConfig.ProductionConfig config = game.Config.Production;
            int facilityCapacity = faction.Settings.ResourceProcessingPointsPerFacility;
            int scaledCapacity = Math.Max(
                1,
                facilityCapacity * config.ResourceMaintenanceLoadPercent / _percentScale
            );
            int maintenancePenalty = DivideRoundingUp(maintenanceAllocation, scaledCapacity);
            int baseDuration = Math.Max(1, facility.ProcessRate + maintenancePenalty);
            Planet planet = facility.GetParentOfType<Planet>();
            int support = Math.Max(1, planet?.GetPopularSupport(faction.InstanceID) ?? 0);
            int supportModifier = config.ResourceCollectionBasePercent * _percentScale / support;
            return Math.Max(1, baseDuration * supportModifier / _percentScale);
        }

        /// <summary>
        /// Counts one resource-facility type by its current lifecycle state.
        /// </summary>
        /// <param name="faction">The owning faction.</param>
        /// <param name="buildingType">The resource-facility type.</param>
        /// <param name="activeFacilityIDs">The facilities currently producing resources.</param>
        /// <returns>The classified facility totals.</returns>
        private static ResourceFacilityCounts CountFacilities(
            Faction faction,
            BuildingType buildingType,
            HashSet<string> activeFacilityIDs
        )
        {
            int active = 0;
            int offline = 0;
            int building = 0;
            int enRoute = 0;
            foreach (
                Building facility in faction
                    .GetOwnedUnitsByType<Building>()
                    .Where(candidate => candidate.BuildingType == buildingType)
            )
            {
                if (facility.ManufacturingStatus == ManufacturingStatus.Building)
                    building++;
                else if (
                    facility.ManufacturingStatus == ManufacturingStatus.Delivering
                    || facility.Movement != null
                )
                    enRoute++;
                else if (activeFacilityIDs.Contains(facility.InstanceID))
                    active++;
                else
                    offline++;
            }

            return new ResourceFacilityCounts(active, offline, building, enRoute);
        }

        /// <summary>
        /// Calculates the current gross output capacity of active facilities per game tick.
        /// </summary>
        /// <param name="faction">The faction operating the facilities.</param>
        /// <param name="facilities">The active facilities.</param>
        /// <param name="buildingType">The resource-facility type.</param>
        /// <returns>The gross output capacity per tick.</returns>
        private double CalculateOutputPerTick(
            Faction faction,
            IEnumerable<Building> facilities,
            BuildingType buildingType
        )
        {
            DifficultyModifiers modifier = _game.GetDifficultyModifier(faction);
            int outputPercent =
                buildingType == BuildingType.Mine
                    ? modifier.MineOutputPercent
                    : modifier.RefineryOutputPercent;
            if (outputPercent <= 0)
                return 0;

            return facilities.Sum(facility =>
            {
                int duration =
                    facility.ProductionCycleDuration > 0
                        ? facility.ProductionCycleDuration
                        : CalculateSteadyCycleDuration(_game, faction, facility);
                return (double)outputPercent / _percentScale / duration;
            });
        }

        /// <summary>
        /// Calculates steady output after every committed facility is operating.
        /// </summary>
        /// <param name="faction">The faction operating the facilities.</param>
        /// <param name="facilities">The projected active facilities.</param>
        /// <param name="buildingType">The resource-facility type.</param>
        /// <param name="maintenanceDemand">The committed maintenance load.</param>
        /// <returns>The projected gross output per tick.</returns>
        private double CalculateProjectedOutputPerTick(
            Faction faction,
            IReadOnlyList<Building> facilities,
            BuildingType buildingType,
            int maintenanceDemand
        )
        {
            if (facilities.Count == 0)
                return 0;

            DifficultyModifiers modifier = _game.GetDifficultyModifier(faction);
            int outputPercent =
                buildingType == BuildingType.Mine
                    ? modifier.MineOutputPercent
                    : modifier.RefineryOutputPercent;
            if (outputPercent <= 0)
                return 0;

            List<int> allocations = CalculateMaintenanceAllocations(
                facilities,
                maintenanceDemand,
                faction
            );
            double output = 0;
            for (int index = 0; index < facilities.Count; index++)
            {
                Building facility = facilities[index];
                int allocation = allocations[index];
                int duration = CalculateSteadyCycleDuration(_game, faction, facility, allocation);
                output += (double)outputPercent / _percentScale / duration;
            }

            return output;
        }

        /// <summary>
        /// Calculates the maintenance allocations produced by the resource-processing rebalance.
        /// </summary>
        /// <param name="facilities">The facilities receiving maintenance demand.</param>
        /// <param name="maintenanceDemand">The faction's committed maintenance demand.</param>
        /// <param name="faction">The faction defining per-facility capacity.</param>
        /// <returns>The rebalanced allocations in matching facility order.</returns>
        internal static List<int> CalculateMaintenanceAllocations(
            IReadOnlyList<Building> facilities,
            int maintenanceDemand,
            Faction faction
        )
        {
            int facilityCapacity = faction.Settings.ResourceProcessingPointsPerFacility;
            List<int> allocations = facilities
                .Select(facility =>
                    Math.Clamp(facility.ResourceMaintenanceAllocation, 0, facilityCapacity)
                )
                .ToList();
            int totalCapacity = allocations.Count * facilityCapacity;
            int targetAllocation = Math.Min(Math.Max(0, maintenanceDemand), totalCapacity);
            int currentAllocation = allocations.Sum();
            if (currentAllocation < targetAllocation)
            {
                IncreaseMaintenanceAllocations(
                    allocations,
                    targetAllocation - currentAllocation,
                    facilityCapacity,
                    totalCapacity
                );
            }
            else if (currentAllocation > targetAllocation)
            {
                DecreaseMaintenanceAllocations(
                    allocations,
                    currentAllocation - targetAllocation,
                    facilityCapacity,
                    totalCapacity
                );
            }

            return allocations;
        }

        /// <summary>
        /// Adds maintenance demand in stable facility order.
        /// </summary>
        /// <param name="allocations">The current allocations by facility.</param>
        /// <param name="remaining">The allocation still to add.</param>
        /// <param name="facilityCapacity">The capacity of each facility.</param>
        /// <param name="totalCapacity">The combined facility capacity.</param>
        private static void IncreaseMaintenanceAllocations(
            IList<int> allocations,
            int remaining,
            int facilityCapacity,
            int totalCapacity
        )
        {
            int currentAllocation = allocations.Sum();
            bool changed;
            do
            {
                changed = false;
                for (int index = 0; index < allocations.Count; index++)
                {
                    int idealAllocation = currentAllocation * facilityCapacity / totalCapacity;
                    int added = Math.Clamp(
                        idealAllocation - allocations[index] + 1,
                        0,
                        Math.Min(remaining, facilityCapacity - allocations[index])
                    );
                    if (added <= 0)
                        continue;

                    allocations[index] += added;
                    currentAllocation += added;
                    remaining -= added;
                    changed = true;
                    if (remaining == 0)
                        return;
                }
            } while (changed);
        }

        /// <summary>
        /// Removes maintenance demand in stable facility order.
        /// </summary>
        /// <param name="allocations">The current allocations by facility.</param>
        /// <param name="remaining">The allocation still to remove.</param>
        /// <param name="facilityCapacity">The capacity of each facility.</param>
        /// <param name="totalCapacity">The combined facility capacity.</param>
        private static void DecreaseMaintenanceAllocations(
            IList<int> allocations,
            int remaining,
            int facilityCapacity,
            int totalCapacity
        )
        {
            bool changed;
            do
            {
                changed = false;
                for (int index = 0; index < allocations.Count; index++)
                {
                    int idealAllocation = (remaining - 1) * facilityCapacity / totalCapacity;
                    int removed = Math.Clamp(
                        allocations[index] - idealAllocation,
                        0,
                        Math.Min(remaining, allocations[index])
                    );
                    if (removed <= 0)
                        continue;

                    allocations[index] -= removed;
                    remaining -= removed;
                    changed = true;
                    if (remaining == 0)
                        return;
                }
            } while (changed);
        }

        /// <summary>
        /// Calculates maintenance capacity after committed resource facilities are operating.
        /// </summary>
        /// <param name="faction">The faction receiving the projected capacity.</param>
        /// <param name="mineCount">The projected usable mine count.</param>
        /// <param name="refineryCount">The projected active refinery count.</param>
        /// <returns>The projected maintenance capacity.</returns>
        private static int CalculateProjectedMaintenanceCapacity(
            Faction faction,
            int mineCount,
            int refineryCount
        )
        {
            int materialCapacity = Math.Min(
                faction.GetTotalAvailableResourceNodes(),
                Math.Min(mineCount, refineryCount)
            );
            return materialCapacity * faction.Settings.ResourceProcessingPointsPerFacility;
        }

        /// <summary>
        /// Divides non-negative integers while rounding any remainder upward.
        /// </summary>
        /// <param name="dividend">The value to divide.</param>
        /// <param name="divisor">The positive divisor.</param>
        /// <returns>The rounded-up quotient.</returns>
        private static int DivideRoundingUp(int dividend, int divisor)
        {
            return (dividend + divisor - 1) / divisor;
        }
    }

    /// <summary>
    /// Contains committed maintenance classified by deployed asset type and unfinished orders.
    /// </summary>
    public sealed class MaintenanceCostBreakdown
    {
        public int CapitalShips { get; }
        public int Starfighters { get; }
        public int Regiments { get; }
        public int SpecialForces { get; }
        public int Facilities { get; }
        public int Orders { get; }
        public int Deployed { get; }
        public int EnRoute { get; }
        public int Building { get; }
        public int AfterDelivery => Deployed + EnRoute;
        public int Committed =>
            CapitalShips + Starfighters + Regiments + SpecialForces + Facilities + Orders;

        /// <summary>
        /// Creates immutable maintenance totals.
        /// </summary>
        /// <param name="capitalShips">Maintenance committed to capital ships.</param>
        /// <param name="starfighters">Maintenance committed to starfighters.</param>
        /// <param name="regiments">Maintenance committed to regiments.</param>
        /// <param name="specialForces">Maintenance committed to special forces.</param>
        /// <param name="facilities">Maintenance committed to completed facilities.</param>
        /// <param name="orders">Maintenance reserved by unfinished orders.</param>
        /// <param name="deployed">Maintenance used by deployed assets.</param>
        /// <param name="enRoute">Maintenance used by assets being delivered.</param>
        /// <param name="building">Maintenance reserved by assets being manufactured.</param>
        public MaintenanceCostBreakdown(
            int capitalShips,
            int starfighters,
            int regiments,
            int specialForces,
            int facilities,
            int orders,
            int deployed = 0,
            int enRoute = 0,
            int building = 0
        )
        {
            CapitalShips = capitalShips;
            Starfighters = starfighters;
            Regiments = regiments;
            SpecialForces = specialForces;
            Facilities = facilities;
            Orders = orders;
            Deployed = deployed;
            EnRoute = enRoute;
            Building = building;
        }
    }

    /// <summary>
    /// Contains facility totals for one resource-production lane.
    /// </summary>
    public sealed class ResourceFacilityCounts
    {
        public int Active { get; }
        public int Offline { get; }
        public int Building { get; }
        public int EnRoute { get; }

        /// <summary>
        /// Creates immutable resource-facility totals.
        /// </summary>
        /// <param name="active">The facilities currently producing.</param>
        /// <param name="offline">The completed facilities currently unable to produce.</param>
        /// <param name="building">The facilities under construction.</param>
        /// <param name="enRoute">The facilities traveling to a destination.</param>
        public ResourceFacilityCounts(int active, int offline, int building, int enRoute)
        {
            Active = active;
            Offline = offline;
            Building = building;
            EnRoute = enRoute;
        }
    }

    /// <summary>
    /// Contains the current resource-production and maintenance calculation for one faction.
    /// </summary>
    public sealed class ResourceEconomySummary
    {
        public static ResourceEconomySummary Empty { get; } =
            new ResourceEconomySummary(
                new ResourceFacilityCounts(0, 0, 0, 0),
                new ResourceFacilityCounts(0, 0, 0, 0),
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                new MaintenanceCostBreakdown(0, 0, 0, 0, 0, 0)
            );

        public ResourceFacilityCounts Mines { get; }
        public ResourceFacilityCounts Refineries { get; }
        public double RawOutputPerTick { get; }
        public double DeliveredRawOutputPerTick { get; }
        public double ProjectedRawOutputPerTick { get; }
        public double RefinedOutputPerTick { get; }
        public double DeliveredRefinedOutputPerTick { get; }
        public double ProjectedRefinedOutputPerTick { get; }
        public int MaintenanceCapacity { get; }
        public int DeliveredMaintenanceCapacity { get; }
        public int ProjectedMaintenanceCapacity { get; }
        public MaintenanceCostBreakdown Maintenance { get; }
        public int MaintenanceCommitted => Maintenance.Committed;
        public int MaintenanceHeadroom => MaintenanceCapacity - Maintenance.Deployed;
        public int DeliveredMaintenanceHeadroom =>
            DeliveredMaintenanceCapacity - Maintenance.AfterDelivery;
        public int ProjectedMaintenanceHeadroom =>
            ProjectedMaintenanceCapacity - Maintenance.Committed;

        /// <summary>
        /// Creates an immutable resource-economy summary.
        /// </summary>
        /// <param name="mines">The mine totals.</param>
        /// <param name="refineries">The refinery totals.</param>
        /// <param name="rawOutputPerTick">The gross raw-material output per tick.</param>
        /// <param name="deliveredRawOutputPerTick">The gross raw-material output after deliveries.</param>
        /// <param name="projectedRawOutputPerTick">The projected gross raw-material output per tick.</param>
        /// <param name="refinedOutputPerTick">The gross refined-material output per tick.</param>
        /// <param name="deliveredRefinedOutputPerTick">The gross refined-material output after deliveries.</param>
        /// <param name="projectedRefinedOutputPerTick">The projected gross refined-material output per tick.</param>
        /// <param name="maintenanceCapacity">The available maintenance capacity.</param>
        /// <param name="deliveredMaintenanceCapacity">The maintenance capacity after deliveries.</param>
        /// <param name="projectedMaintenanceCapacity">The projected maintenance capacity.</param>
        /// <param name="maintenance">The maintenance committed by asset category.</param>
        public ResourceEconomySummary(
            ResourceFacilityCounts mines,
            ResourceFacilityCounts refineries,
            double rawOutputPerTick,
            double deliveredRawOutputPerTick,
            double projectedRawOutputPerTick,
            double refinedOutputPerTick,
            double deliveredRefinedOutputPerTick,
            double projectedRefinedOutputPerTick,
            int maintenanceCapacity,
            int deliveredMaintenanceCapacity,
            int projectedMaintenanceCapacity,
            MaintenanceCostBreakdown maintenance
        )
        {
            Mines = mines ?? throw new ArgumentNullException(nameof(mines));
            Refineries = refineries ?? throw new ArgumentNullException(nameof(refineries));
            RawOutputPerTick = rawOutputPerTick;
            DeliveredRawOutputPerTick = deliveredRawOutputPerTick;
            ProjectedRawOutputPerTick = projectedRawOutputPerTick;
            RefinedOutputPerTick = refinedOutputPerTick;
            DeliveredRefinedOutputPerTick = deliveredRefinedOutputPerTick;
            ProjectedRefinedOutputPerTick = projectedRefinedOutputPerTick;
            MaintenanceCapacity = maintenanceCapacity;
            DeliveredMaintenanceCapacity = deliveredMaintenanceCapacity;
            ProjectedMaintenanceCapacity = projectedMaintenanceCapacity;
            Maintenance = maintenance ?? throw new ArgumentNullException(nameof(maintenance));
        }
    }
}
