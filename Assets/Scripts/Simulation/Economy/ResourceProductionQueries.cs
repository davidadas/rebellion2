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
                CalculateOutputPerTick(faction, activeRefineries, BuildingType.Refinery),
                faction.MaintenanceCapacity,
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

            foreach (IManufacturable item in faction.GetAllOwnedManufacturables())
            {
                int cost = item.GetMaintenanceCost();
                if (item.GetManufacturingStatus() == ManufacturingStatus.Building)
                {
                    orders += cost;
                    continue;
                }

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
                orders
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
            GameConfig.ProductionConfig config = game.Config.Production;
            int facilityCapacity = faction.Settings.ResourceProcessingPointsPerFacility;
            int scaledCapacity = Math.Max(
                1,
                facilityCapacity * config.ResourceMaintenanceLoadPercent / _percentScale
            );
            int maintenancePenalty = DivideRoundingUp(
                facility.ResourceMaintenanceAllocation,
                scaledCapacity
            );
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
        public MaintenanceCostBreakdown(
            int capitalShips,
            int starfighters,
            int regiments,
            int specialForces,
            int facilities,
            int orders
        )
        {
            CapitalShips = capitalShips;
            Starfighters = starfighters;
            Regiments = regiments;
            SpecialForces = specialForces;
            Facilities = facilities;
            Orders = orders;
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
                new MaintenanceCostBreakdown(0, 0, 0, 0, 0, 0)
            );

        public ResourceFacilityCounts Mines { get; }
        public ResourceFacilityCounts Refineries { get; }
        public double RawOutputPerTick { get; }
        public double RefinedOutputPerTick { get; }
        public int MaintenanceCapacity { get; }
        public MaintenanceCostBreakdown Maintenance { get; }
        public int MaintenanceCommitted => Maintenance.Committed;
        public int MaintenanceHeadroom => MaintenanceCapacity - MaintenanceCommitted;

        /// <summary>
        /// Creates an immutable resource-economy summary.
        /// </summary>
        /// <param name="mines">The mine totals.</param>
        /// <param name="refineries">The refinery totals.</param>
        /// <param name="rawOutputPerTick">The gross raw-material output per tick.</param>
        /// <param name="refinedOutputPerTick">The gross refined-material output per tick.</param>
        /// <param name="maintenanceCapacity">The available maintenance capacity.</param>
        /// <param name="maintenance">The maintenance committed by asset category.</param>
        public ResourceEconomySummary(
            ResourceFacilityCounts mines,
            ResourceFacilityCounts refineries,
            double rawOutputPerTick,
            double refinedOutputPerTick,
            int maintenanceCapacity,
            MaintenanceCostBreakdown maintenance
        )
        {
            Mines = mines ?? throw new ArgumentNullException(nameof(mines));
            Refineries = refineries ?? throw new ArgumentNullException(nameof(refineries));
            RawOutputPerTick = rawOutputPerTick;
            RefinedOutputPerTick = refinedOutputPerTick;
            MaintenanceCapacity = maintenanceCapacity;
            Maintenance = maintenance ?? throw new ArgumentNullException(nameof(maintenance));
        }
    }
}
