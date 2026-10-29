using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Util.Mathematics;

namespace Rebellion.Gameplay
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
            return GetSummary(faction, Array.Empty<Building>());
        }

        /// <summary>
        /// Projects steady resource output after excluding facilities without changing game state.
        /// </summary>
        /// <param name="faction">The faction whose economy is inspected.</param>
        /// <param name="excludedFacilities">Facilities omitted from production and maintenance totals.</param>
        /// <returns>The economy with the selected facilities excluded.</returns>
        public ResourceEconomySummary GetSummary(
            Faction faction,
            IReadOnlyCollection<Building> excludedFacilities
        )
        {
            if (faction == null)
                return ResourceEconomySummary.Empty;

            HashSet<string> excluded = (excludedFacilities ?? Array.Empty<Building>())
                .Where(building =>
                    building != null && building.GetOwnerInstanceID() == faction.InstanceID
                )
                .Select(building => building.InstanceID)
                .ToHashSet(StringComparer.Ordinal);
            List<Building> activeMines = GetActiveFacilities(faction, BuildingType.Mine, excluded);
            List<Building> activeRefineries = GetActiveFacilities(
                faction,
                BuildingType.Refinery,
                excluded
            );
            List<Building> deliveredMines = GetProjectedFacilities(
                faction,
                BuildingType.Mine,
                includeBuilding: false,
                excluded
            );
            List<Building> deliveredRefineries = GetProjectedFacilities(
                faction,
                BuildingType.Refinery,
                includeBuilding: false,
                excluded
            );
            List<Building> projectedMines = GetProjectedFacilities(
                faction,
                BuildingType.Mine,
                includeBuilding: true,
                excluded
            );
            List<Building> projectedRefineries = GetProjectedFacilities(
                faction,
                BuildingType.Refinery,
                includeBuilding: true,
                excluded
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
                activeMineIDs,
                excluded
            );
            ResourceFacilityCounts refineries = CountFacilities(
                faction,
                BuildingType.Refinery,
                activeRefineryIDs,
                excluded
            );
            MaintenanceCostBreakdown maintenance = CalculateMaintenanceCosts(faction, excluded);
            var operating = CalculateOperatingEconomy(
                faction,
                excluded,
                activeMines,
                activeRefineries,
                maintenance.Committed,
                CalculateMaintenanceCapacity(_game, faction, excluded)
            );
            int deliveredMaintenanceCapacity = CalculateProjectedMaintenanceCapacity(
                faction,
                deliveredMines.Count,
                deliveredRefineries.Count
            );
            int projectedMaintenanceCapacity = CalculateProjectedMaintenanceCapacity(
                faction,
                projectedMines.Count,
                projectedRefineries.Count
            );

            return new ResourceEconomySummary(
                mines,
                refineries,
                operating.RawOutputPerTick,
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
                operating.RefinedOutputPerTick,
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
                operating.MaintenanceHeadroom,
                deliveredMaintenanceCapacity - maintenance.Committed,
                projectedMaintenanceCapacity - maintenance.Committed,
                maintenance
            );
        }

        /// <summary>
        /// Projects current operating rates and committed upkeep without calculating future delivery stages.
        /// </summary>
        /// <param name="faction">The faction whose economy is inspected.</param>
        /// <param name="excludedFacilities">Facilities omitted without changing game state.</param>
        /// <returns>Effective maintenance headroom and active resource output.</returns>
        public (
            int MaintenanceHeadroom,
            double RawOutputPerTick,
            double RefinedOutputPerTick
        ) GetOperatingEconomy(Faction faction, IReadOnlyCollection<Building> excludedFacilities)
        {
            if (faction == null)
                return (0, 0, 0);
            HashSet<string> excluded = (excludedFacilities ?? Array.Empty<Building>())
                .Where(building =>
                    building != null && building.GetOwnerInstanceID() == faction.InstanceID
                )
                .Select(building => building.InstanceID)
                .ToHashSet(StringComparer.Ordinal);
            return CalculateOperatingEconomy(
                faction,
                excluded,
                GetActiveFacilities(faction, BuildingType.Mine, excluded),
                GetActiveFacilities(faction, BuildingType.Refinery, excluded),
                CalculateMaintenanceCosts(faction, excluded).Committed,
                CalculateMaintenanceCapacity(_game, faction, excluded)
            );
        }

        /// <summary>
        /// Captures reusable resource inputs for evaluating exclusions while game state is unchanged.
        /// </summary>
        /// <param name="faction">The faction whose current economy is evaluated.</param>
        /// <returns>A projection scope that must be captured again after game-state changes.</returns>
        public OperatingEconomyProjection CaptureOperatingEconomy(Faction faction)
        {
            return new OperatingEconomyProjection(
                this,
                faction ?? throw new ArgumentNullException(nameof(faction))
            );
        }

        /// <summary>
        /// Captures completed stationary facilities on each currently operating owned planet.
        /// </summary>
        /// <param name="faction">The faction whose facilities are captured.</param>
        /// <returns>Planet and facility inputs in stable planet and child order.</returns>
        private static IReadOnlyList<(Planet Planet, Building[] Buildings)> CaptureResourcePlanets(
            Faction faction
        )
        {
            return faction
                .GetOwnedUnitsByType<Planet>()
                .Where(planet => !planet.IsResourceProductionSuspended())
                .OrderBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .Select(planet =>
                    (
                        planet,
                        planet
                            .GetChildren<Building>()
                            .Where(building =>
                                building.ManufacturingStatus == ManufacturingStatus.Complete
                                && building.Movement == null
                            )
                            .ToArray()
                    )
                )
                .ToArray();
        }

        /// <summary>
        /// Shares operating calculations between full summaries and retirement projections.
        /// </summary>
        /// <param name="faction">The operating faction.</param>
        /// <param name="excluded">Omitted facility identifiers.</param>
        /// <param name="mines">Remaining active mines.</param>
        /// <param name="refineries">Remaining active refineries.</param>
        /// <param name="maintenanceDemand">Remaining committed maintenance costs.</param>
        /// <param name="maintenanceCapacity">The usable capacity after the exclusions.</param>
        /// <returns>Effective headroom and current or post-retirement steady resource rates.</returns>
        private (
            int MaintenanceHeadroom,
            double RawOutputPerTick,
            double RefinedOutputPerTick
        ) CalculateOperatingEconomy(
            Faction faction,
            ISet<string> excluded,
            List<Building> mines,
            List<Building> refineries,
            int maintenanceDemand,
            int maintenanceCapacity
        )
        {
            return (
                maintenanceCapacity - maintenanceDemand,
                excluded.Count == 0
                    ? CalculateOutputPerTick(faction, mines, BuildingType.Mine)
                    : CalculateProjectedOutputPerTick(
                        faction,
                        mines,
                        BuildingType.Mine,
                        maintenanceDemand
                    ),
                excluded.Count == 0
                    ? CalculateOutputPerTick(faction, refineries, BuildingType.Refinery)
                    : CalculateProjectedOutputPerTick(
                        faction,
                        refineries,
                        BuildingType.Refinery,
                        maintenanceDemand
                    )
            );
        }

        /// <summary>
        /// Totals committed maintenance by deployed asset type and unfinished order.
        /// </summary>
        /// <param name="faction">The faction whose maintenance is classified.</param>
        /// <param name="excluded">Facility identifiers omitted from the projection.</param>
        /// <returns>The classified maintenance costs.</returns>
        private static MaintenanceCostBreakdown CalculateMaintenanceCosts(
            Faction faction,
            ISet<string> excluded
        )
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
                if (excluded.Contains(item.InstanceID))
                    continue;
                int cost = item.GetMaintenanceCost();
                if (item.GetManufacturingStatus() == ManufacturingStatus.Building)
                {
                    orders += cost;
                    building += cost;
                    continue;
                }

                if (
                    item.GetManufacturingStatus() == ManufacturingStatus.Delivering
                    || item.GetTransitMovement() != null
                )
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
        /// <param name="excluded">Optional facility identifiers omitted from the projection.</param>
        /// <returns>The active facilities in stable planet and building order.</returns>
        internal static List<Building> GetActiveFacilities(
            Faction faction,
            BuildingType buildingType,
            ISet<string> excluded = null
        )
        {
            List<Building> facilities = new List<Building>();
            foreach (
                Planet planet in faction
                    .GetOwnedColonizedPlanets()
                    .OrderBy(planet => planet.InstanceID, StringComparer.Ordinal)
            )
            {
                if (planet.IsResourceProductionSuspended())
                    continue;

                facilities.AddRange(
                    GetActiveFacilities(
                        planet,
                        planet.GetChildren<Building>(),
                        buildingType,
                        excluded
                    )
                );
            }

            return facilities;
        }

        /// <summary>
        /// Selects active facilities from one planet without discarding replacement mines before exclusions.
        /// </summary>
        /// <param name="planet">The planet supplying the mining limit.</param>
        /// <param name="buildings">The planet's live or captured buildings in child order.</param>
        /// <param name="buildingType">The requested processing category.</param>
        /// <param name="excluded">Optional omitted identifiers.</param>
        /// <returns>The eligible facilities in their processing order.</returns>
        private static IEnumerable<Building> GetActiveFacilities(
            Planet planet,
            IEnumerable<Building> buildings,
            BuildingType buildingType,
            ISet<string> excluded
        )
        {
            IEnumerable<Building> facilities = buildings.Where(building =>
                building.BuildingType == buildingType
                && excluded?.Contains(building.InstanceID) != true
                && building.ManufacturingStatus == ManufacturingStatus.Complete
                && building.Movement == null
                && building.ProcessRate > 0
            );
            return buildingType == BuildingType.Mine
                ? facilities.Take(planet.NumRawResourceNodes)
                : facilities;
        }

        /// <summary>
        /// Gets facilities expected to operate after a selected committed lifecycle stage.
        /// </summary>
        /// <param name="faction">The owning faction.</param>
        /// <param name="buildingType">The requested resource-facility type.</param>
        /// <param name="includeBuilding">Whether facilities still being manufactured are included.</param>
        /// <param name="excluded">Facility identifiers omitted from the projection.</param>
        /// <returns>The projected facilities in stable planet and building order.</returns>
        private static List<Building> GetProjectedFacilities(
            Faction faction,
            BuildingType buildingType,
            bool includeBuilding,
            ISet<string> excluded
        )
        {
            List<Building> facilities = new List<Building>();
            foreach (
                Planet planet in faction
                    .GetOwnedColonizedPlanets()
                    .OrderBy(planet => planet.InstanceID, StringComparer.Ordinal)
            )
            {
                if (planet.IsResourceProductionSuspended())
                    continue;

                IEnumerable<Building> planetFacilities = planet
                    .GetChildren<Building>()
                    .Where(building =>
                        building.BuildingType == buildingType
                        && !excluded.Contains(building.InstanceID)
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
            int facilityCapacity = CalculateMaintenanceCapacityPerFacility(game, faction);
            int scaledCapacity = Math.Max(
                1,
                IntegerMath.ScaleByPercent(facilityCapacity, config.ResourceMaintenanceLoadPercent)
            );
            int maintenancePenalty = DivideRoundingUp(maintenanceAllocation, scaledCapacity);
            int baseDuration = Math.Max(
                1,
                IntegerMath.SaturatingAdd(facility.ProcessRate, maintenancePenalty)
            );
            Planet planet = facility.GetParentOfType<Planet>();
            int support = Math.Max(1, planet?.GetPopularSupport(faction.InstanceID) ?? 0);
            int supportModifier = config.ResourceCollectionBasePercent * _percentScale / support;
            return Math.Max(1, IntegerMath.ScaleByPercent(baseDuration, supportModifier));
        }

        /// <summary>
        /// Counts one resource-facility type by its current lifecycle state.
        /// </summary>
        /// <param name="faction">The owning faction.</param>
        /// <param name="buildingType">The resource-facility type.</param>
        /// <param name="activeFacilityIDs">The facilities currently producing resources.</param>
        /// <param name="excluded">Facility identifiers omitted from the projection.</param>
        /// <returns>The classified facility totals.</returns>
        private static ResourceFacilityCounts CountFacilities(
            Faction faction,
            BuildingType buildingType,
            HashSet<string> activeFacilityIDs,
            ISet<string> excluded
        )
        {
            int active = 0;
            int offline = 0;
            int building = 0;
            int enRoute = 0;
            foreach (
                Building facility in faction
                    .GetOwnedUnitsByType<Building>()
                    .Where(candidate =>
                        candidate.BuildingType == buildingType
                        && !excluded.Contains(candidate.InstanceID)
                    )
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
                _game,
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
        /// <param name="game">The game defining the faction's effective capacity.</param>
        /// <param name="facilities">The facilities receiving maintenance demand.</param>
        /// <param name="maintenanceDemand">The faction's committed maintenance demand.</param>
        /// <param name="faction">The faction defining per-facility capacity.</param>
        /// <returns>The rebalanced allocations in matching facility order.</returns>
        internal static List<int> CalculateMaintenanceAllocations(
            GameRoot game,
            IReadOnlyList<Building> facilities,
            int maintenanceDemand,
            Faction faction
        )
        {
            int facilityCapacity = CalculateMaintenanceCapacityPerFacility(game, faction);
            List<int> allocations = facilities
                .Select(facility =>
                    Math.Clamp(facility.ResourceMaintenanceAllocation, 0, facilityCapacity)
                )
                .ToList();
            int totalCapacity = IntegerMath.SaturatingMultiply(allocations.Count, facilityCapacity);
            int targetAllocation = Math.Min(Math.Max(0, maintenanceDemand), totalCapacity);
            int currentAllocation = IntegerMath.SaturateToInt(
                allocations.Sum(allocation => (long)allocation)
            );
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
            int currentAllocation = IntegerMath.SaturateToInt(
                allocations.Sum(allocation => (long)allocation)
            );
            bool changed;
            do
            {
                changed = false;
                for (int index = 0; index < allocations.Count; index++)
                {
                    int idealAllocation = (int)(
                        (long)currentAllocation * facilityCapacity / totalCapacity
                    );
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
                    int idealAllocation = (int)(
                        (long)(remaining - 1) * facilityCapacity / totalCapacity
                    );
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
        private int CalculateProjectedMaintenanceCapacity(
            Faction faction,
            int mineCount,
            int refineryCount
        )
        {
            int materialCapacity = Math.Min(
                faction.GetTotalAvailableResourceNodes(),
                Math.Min(mineCount, refineryCount)
            );
            return IntegerMath.SaturatingMultiply(
                materialCapacity,
                CalculateMaintenanceCapacityPerFacility(_game, faction)
            );
        }

        /// <summary>
        /// Calculates live maintenance headroom using effective capacity and all committed costs.
        /// </summary>
        /// <param name="game">The game defining the active difficulty and faction controller.</param>
        /// <param name="faction">The faction whose remaining capacity is calculated.</param>
        /// <returns>The effective headroom, or zero when the game or faction is absent.</returns>
        internal static int CalculateMaintenanceHeadroom(GameRoot game, Faction faction)
        {
            if (game == null || faction == null)
                return 0;

            return CalculateMaintenanceCapacity(game, faction)
                - faction.GetTotalProjectedMaintenanceCost();
        }

        /// <summary>
        /// Calculates effective maintenance capacity after applying the faction's difficulty bonus.
        /// </summary>
        /// <param name="game">The game defining the active difficulty and faction controller.</param>
        /// <param name="faction">The faction whose capacity is calculated.</param>
        /// <returns>The effective maintenance capacity.</returns>
        internal static int CalculateMaintenanceCapacity(GameRoot game, Faction faction)
        {
            if (game == null || faction == null)
                return 0;

            return IntegerMath.SaturatingMultiply(
                faction.GetTotalAvailableMaterialsRaw(),
                CalculateMaintenanceCapacityPerFacility(game, faction)
            );
        }

        /// <summary>
        /// Recalculates usable resource pairs while omitting selected facilities.
        /// </summary>
        /// <param name="game">The game supplying difficulty modifiers.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="excluded">Facility identifiers omitted from the projection.</param>
        /// <returns>The effective capacity after the omissions.</returns>
        private static int CalculateMaintenanceCapacity(
            GameRoot game,
            Faction faction,
            ISet<string> excluded
        )
        {
            if (excluded.Count == 0)
                return CalculateMaintenanceCapacity(game, faction);
            return CalculateMaintenanceCapacity(
                CaptureResourcePlanets(faction),
                excluded,
                faction.GetTotalAvailableResourceNodes(),
                CalculateMaintenanceCapacityPerFacility(game, faction)
            );
        }

        /// <summary>
        /// Measures capacity from operating planets after omitting selected facilities.
        /// </summary>
        /// <param name="planets">The completed stationary facilities by planet.</param>
        /// <param name="excluded">Omitted facility identifiers.</param>
        /// <param name="availableNodes">The faction's available raw resource nodes.</param>
        /// <param name="facilityCapacity">Effective capacity supplied by each resource pair.</param>
        /// <returns>The same capacity used by live retirement projections.</returns>
        private static int CalculateMaintenanceCapacity(
            IReadOnlyList<(Planet Planet, Building[] Buildings)> planets,
            ISet<string> excluded,
            int availableNodes,
            int facilityCapacity
        )
        {
            int mines = 0;
            int refineries = 0;
            foreach (var entry in planets)
            {
                int planetMines = 0;
                foreach (Building building in entry.Buildings)
                {
                    if (excluded.Contains(building.InstanceID))
                        continue;
                    if (building.BuildingType == BuildingType.Mine)
                        planetMines++;
                    else if (building.BuildingType == BuildingType.Refinery)
                        refineries++;
                }
                mines += Math.Min(entry.Planet.GetRawResourceNodes(), planetMines);
            }
            return Math.Min(availableNodes, Math.Min(mines, refineries)) * facilityCapacity;
        }

        /// <summary>
        /// Calculates effective processing points supplied by one paired resource facility.
        /// </summary>
        /// <param name="game">The game defining the active difficulty and faction controller.</param>
        /// <param name="faction">The faction receiving the processing capacity.</param>
        /// <returns>The effective processing points per facility.</returns>
        internal static int CalculateMaintenanceCapacityPerFacility(GameRoot game, Faction faction)
        {
            if (game == null || faction == null)
                return 0;

            int baseCapacity = Math.Max(0, faction.Settings.ResourceProcessingPointsPerFacility);
            int capacityPercent = Math.Max(
                0,
                game.GetDifficultyModifier(faction).MaintenanceCapacityPercent
            );
            return IntegerMath.ScaleByPercent(baseCapacity, capacityPercent);
        }

        /// <summary>
        /// Divides non-negative integers while rounding any remainder upward.
        /// </summary>
        /// <param name="dividend">The value to divide.</param>
        /// <param name="divisor">The positive divisor.</param>
        /// <returns>The rounded-up quotient.</returns>
        private static int DivideRoundingUp(int dividend, int divisor)
        {
            return IntegerMath.DivideRoundedUp(dividend, divisor);
        }

        /// <summary>
        /// Reuses current resource inputs across exclusion alternatives without mutating the game.
        /// Capture a new scope after any game-state change.
        /// </summary>
        public sealed class OperatingEconomyProjection
        {
            private readonly ResourceProductionQueries _queries;
            private readonly Faction _faction;
            private readonly IReadOnlyList<(Planet Planet, Building[] Buildings)> _planets;
            private readonly Dictionary<string, int> _buildingMaintenance = new(
                StringComparer.Ordinal
            );
            private readonly int _committedMaintenance;
            private readonly int _maintenanceCapacity;
            private readonly int _availableNodes;
            private readonly int _facilityCapacity;

            /// <summary>
            /// Captures inputs for one unchanged resource-evaluation scope.
            /// </summary>
            /// <param name="queries">The resource calculations shared with the live query path.</param>
            /// <param name="faction">The faction whose economy is captured.</param>
            internal OperatingEconomyProjection(ResourceProductionQueries queries, Faction faction)
            {
                _queries = queries;
                _faction = faction;
                _planets = CaptureResourcePlanets(faction);
                _committedMaintenance = CalculateMaintenanceCosts(
                    faction,
                    new HashSet<string>()
                ).Committed;
                _maintenanceCapacity = CalculateMaintenanceCapacity(queries._game, faction);
                _availableNodes = faction.GetTotalAvailableResourceNodes();
                _facilityCapacity = CalculateMaintenanceCapacityPerFacility(queries._game, faction);
                foreach (
                    Building building in faction.GetAllOwnedManufacturables().OfType<Building>()
                )
                {
                    _buildingMaintenance.TryGetValue(building.InstanceID, out int cost);
                    _buildingMaintenance[building.InstanceID] =
                        cost + ((IManufacturable)building).GetMaintenanceCost();
                }
            }

            /// <summary>
            /// Evaluates omitted facilities using the captured inputs and shared resource calculations.
            /// </summary>
            /// <param name="excludedFacilities">Owned facilities omitted from this alternative.</param>
            /// <returns>Maintenance headroom and active processing rates after the exclusions.</returns>
            public (
                int MaintenanceHeadroom,
                double RawOutputPerTick,
                double RefinedOutputPerTick
            ) GetOperatingEconomy(IReadOnlyCollection<Building> excludedFacilities)
            {
                HashSet<string> excluded = (excludedFacilities ?? Array.Empty<Building>())
                    .Where(building =>
                        building != null && building.GetOwnerInstanceID() == _faction.InstanceID
                    )
                    .Select(building => building.InstanceID)
                    .ToHashSet(StringComparer.Ordinal);
                int maintenance = _committedMaintenance;
                foreach (string id in excluded)
                    if (_buildingMaintenance.TryGetValue(id, out int cost))
                        maintenance -= cost;
                List<Building> mines = new();
                List<Building> refineries = new();
                foreach (var entry in _planets)
                {
                    if (!entry.Planet.IsColonized)
                        continue;
                    mines.AddRange(
                        ResourceProductionQueries.GetActiveFacilities(
                            entry.Planet,
                            entry.Buildings,
                            BuildingType.Mine,
                            excluded
                        )
                    );
                    refineries.AddRange(
                        ResourceProductionQueries.GetActiveFacilities(
                            entry.Planet,
                            entry.Buildings,
                            BuildingType.Refinery,
                            excluded
                        )
                    );
                }
                int capacity =
                    excluded.Count == 0
                        ? _maintenanceCapacity
                        : CalculateMaintenanceCapacity(
                            _planets,
                            excluded,
                            _availableNodes,
                            _facilityCapacity
                        );
                return _queries.CalculateOperatingEconomy(
                    _faction,
                    excluded,
                    mines,
                    refineries,
                    maintenance,
                    capacity
                );
            }
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
        public int MaintenanceHeadroom { get; }
        public int DeliveredMaintenanceHeadroom { get; }
        public int ProjectedMaintenanceHeadroom { get; }
        public MaintenanceCostBreakdown Maintenance { get; }
        public int MaintenanceCommitted => Maintenance.Committed;

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
        /// <param name="maintenanceHeadroom">The currently available maintenance headroom.</param>
        /// <param name="deliveredMaintenanceHeadroom">The maintenance headroom after deliveries.</param>
        /// <param name="projectedMaintenanceHeadroom">The maintenance headroom after all committed construction and deliveries.</param>
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
            int maintenanceHeadroom,
            int deliveredMaintenanceHeadroom,
            int projectedMaintenanceHeadroom,
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
            MaintenanceHeadroom = maintenanceHeadroom;
            DeliveredMaintenanceHeadroom = deliveredMaintenanceHeadroom;
            ProjectedMaintenanceHeadroom = projectedMaintenanceHeadroom;
            Maintenance = maintenance ?? throw new ArgumentNullException(nameof(maintenance));
        }
    }
}
