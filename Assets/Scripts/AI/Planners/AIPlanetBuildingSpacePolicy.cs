using System;
using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Selects the least disruptive building to remove when an AI action requires planetary
    /// building capacity.
    /// </summary>
    internal sealed class AIPlanetBuildingSpacePolicy
    {
        /// <summary>
        /// Chooses one owned building whose removal would create a building slot.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The full planet where capacity is required.</param>
        /// <returns>The preferred removable building, or null when no removal is required or possible.</returns>
        internal Building SelectBuildingToRemove(AITurnContext context, Planet planet)
        {
            return SelectRemoval(context, planet).Building;
        }

        /// <summary>
        /// Chooses one owned building and records its strategic removal cost.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The full planet where capacity is required.</param>
        /// <returns>The preferred removal, or an empty value when no removal is possible.</returns>
        internal Removal SelectRemoval(AITurnContext context, Planet planet)
        {
            if (
                context?.Assessment == null
                || context.Faction == null
                || context.Game?.Config == null
                || context.StrategicPlan == null
                || planet == null
                || planet.GetOwnerInstanceID() != context.Faction.InstanceID
                || planet.GetAvailableEnergy() > 0
            )
                return default;

            IReadOnlyList<Building> buildings = context.Assessment.GetPlanetBuildings(planet);
            int mineCount = 0;
            int activeMineCount = 0;
            foreach (Building building in buildings)
            {
                if (building?.GetBuildingType() == BuildingType.Mine)
                {
                    mineCount++;
                    if (building.IsOperational())
                        activeMineCount++;
                }
            }

            RemovalEconomics economics = new RemovalEconomics(
                context,
                planet,
                mineCount,
                activeMineCount
            );
            Removal selected = default;
            foreach (Building building in buildings)
            {
                if (!IsRemovable(context, planet, building))
                    continue;

                Removal candidate = EvaluateRemoval(context, economics, building);
                if (!selected.IsAvailable || candidate.CompareCostTo(selected) < 0)
                    selected = candidate;
            }

            return selected;
        }

        /// <summary>
        /// Returns whether a building may be cancelled or scrapped to create capacity.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet containing the building.</param>
        /// <param name="building">The candidate building.</param>
        /// <returns>True when the building is an owned, removable direct child.</returns>
        private static bool IsRemovable(AITurnContext context, Planet planet, Building building)
        {
            return building != null
                && building.GetParent() == planet
                && building.GetOwnerInstanceID() == context.Faction.InstanceID
                && building.GetBuildingType() != BuildingType.Headquarters
                && building.Movement == null
                && (
                    building.ManufacturingStatus == ManufacturingStatus.Complete
                    || building.ManufacturingStatus == ManufacturingStatus.Building
                        && context.Assessment.IsQueuedForManufacturing(building)
                );
        }

        /// <summary>
        /// Evaluates the strategic cost of removing one building.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="economics">The destination's cached removal economics.</param>
        /// <param name="building">The candidate building.</param>
        /// <returns>The evaluated removal.</returns>
        private static Removal EvaluateRemoval(
            AITurnContext context,
            RemovalEconomics economics,
            Building building
        )
        {
            AIPlanetBuildingRemovalPriority priority = GetRemovalPriority(
                context,
                economics,
                building
            );
            double strategicImpact = building.IsDestroyed
                ? 0
                : building.GetBuildingType() switch
                {
                    BuildingType.ConstructionFacility
                    or BuildingType.Shipyard
                    or BuildingType.TrainingFacility => GetProductionDeficitAfterRemoval(
                        context,
                        building.GetBuildingType()
                    ),
                    BuildingType.Mine or BuildingType.Refinery =>
                        economics.GetProjectedCapacityLoss(building),
                    _ => 0,
                };
            return new Removal(
                building,
                priority,
                strategicImpact,
                economics.GetMaintenanceHeadroomCost(context, building)
            );
        }

        /// <summary>
        /// Returns the strategic removal tier for a candidate building.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="economics">The destination's cached removal economics.</param>
        /// <param name="building">The candidate building.</param>
        /// <returns>The removal tier, where lower values are preferred.</returns>
        private static AIPlanetBuildingRemovalPriority GetRemovalPriority(
            AITurnContext context,
            RemovalEconomics economics,
            Building building
        )
        {
            if (building.IsDestroyed)
                return AIPlanetBuildingRemovalPriority.Rubble;

            if (building.ManufacturingStatus == ManufacturingStatus.Building)
                return AIPlanetBuildingRemovalPriority.Unfinished;

            BuildingType buildingType = building.GetBuildingType();
            if (
                IsStrategicProductionFacility(buildingType)
                && context.StrategicPlan.GetInfrastructureCount(buildingType)
                    > context.StrategicPlan.GetInfrastructureTarget(buildingType)
            )
            {
                return AIPlanetBuildingRemovalPriority.SurplusProduction;
            }

            return buildingType switch
            {
                BuildingType.Defense or BuildingType.Weapon or BuildingType.None =>
                    AIPlanetBuildingRemovalPriority.StaticFacility,
                BuildingType.Mine
                or BuildingType.Refinery when economics.CanSpare(context, building) =>
                    AIPlanetBuildingRemovalPriority.ExpendableResource,
                BuildingType.ConstructionFacility
                or BuildingType.Shipyard
                or BuildingType.TrainingFacility =>
                    AIPlanetBuildingRemovalPriority.RequiredProduction,
                BuildingType.Mine or BuildingType.Refinery =>
                    AIPlanetBuildingRemovalPriority.RequiredResource,
                _ => AIPlanetBuildingRemovalPriority.RequiredProduction,
            };
        }

        /// <summary>
        /// Calculates the faction-wide production deficit created by removing one facility.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="buildingType">The production facility type.</param>
        /// <returns>The resulting normalized infrastructure deficit.</returns>
        private static double GetProductionDeficitAfterRemoval(
            AITurnContext context,
            BuildingType buildingType
        )
        {
            int target = context.StrategicPlan.GetInfrastructureTarget(buildingType);
            if (target <= 0)
                return 0;

            int remaining = Math.Max(
                0,
                context.StrategicPlan.GetInfrastructureCount(buildingType) - 1
            );
            return Math.Max(0, target - remaining) / (double)target;
        }

        /// <summary>
        /// Returns whether a building type participates in the faction-wide infrastructure plan.
        /// </summary>
        /// <param name="buildingType">The building type to inspect.</param>
        /// <returns>True for construction, ship, and troop production facilities.</returns>
        private static bool IsStrategicProductionFacility(BuildingType buildingType)
        {
            return buildingType
                is BuildingType.ConstructionFacility
                    or BuildingType.Shipyard
                    or BuildingType.TrainingFacility;
        }

        /// <summary>
        /// Describes one building removal and its cached strategic cost.
        /// </summary>
        internal readonly struct Removal
        {
            private readonly AIPlanetBuildingRemovalPriority _priority;
            private readonly double _strategicImpact;
            private readonly int _processRate;
            private readonly int _constructionCost;
            private readonly int _maintenanceCost;

            internal Building Building { get; }

            internal bool IsAvailable => Building != null;

            internal int MaintenanceHeadroomCost { get; }

            /// <summary>
            /// Creates an evaluated building removal.
            /// </summary>
            /// <param name="building">The building selected for removal.</param>
            /// <param name="priority">The broad strategic removal tier.</param>
            /// <param name="strategicImpact">The projected deficit or resource-capacity loss.</param>
            /// <param name="maintenanceHeadroomCost">The immediate net maintenance headroom consumed.</param>
            internal Removal(
                Building building,
                AIPlanetBuildingRemovalPriority priority,
                double strategicImpact,
                int maintenanceHeadroomCost
            )
            {
                Building = building;
                _priority = priority;
                _strategicImpact = strategicImpact;
                MaintenanceHeadroomCost = Math.Max(0, maintenanceHeadroomCost);
                _processRate = building?.GetProcessRate() ?? 0;
                _constructionCost = building?.ConstructionCost ?? 0;
                _maintenanceCost = building?.MaintenanceCost ?? 0;
            }

            /// <summary>
            /// Compares this removal with another, ordering the less disruptive removal first.
            /// </summary>
            /// <param name="other">The other evaluated removal.</param>
            /// <returns>A negative value when this removal is preferred.</returns>
            internal int CompareCostTo(Removal other)
            {
                int comparison = _priority.CompareTo(other._priority);
                if (comparison != 0)
                    return comparison;

                comparison = _strategicImpact.CompareTo(other._strategicImpact);
                if (comparison != 0)
                    return comparison;

                comparison = MaintenanceHeadroomCost.CompareTo(other.MaintenanceHeadroomCost);
                if (comparison != 0)
                    return comparison;

                comparison = other._processRate.CompareTo(_processRate);
                if (comparison != 0)
                    return comparison;

                comparison = _constructionCost.CompareTo(other._constructionCost);
                if (comparison != 0)
                    return comparison;

                comparison = other._maintenanceCost.CompareTo(_maintenanceCost);
                if (comparison != 0)
                    return comparison;

                return string.CompareOrdinal(Building?.InstanceID, other.Building?.InstanceID);
            }
        }

        /// <summary>
        /// Captures resource-capacity changes shared by all removal candidates on one planet.
        /// </summary>
        private readonly struct RemovalEconomics
        {
            private readonly int _availableMineCapacityLoss;
            private readonly int _availableRefineryCapacityLoss;
            private readonly int _projectedMineCapacityLoss;
            private readonly int _projectedRefineryCapacityLoss;

            /// <summary>
            /// Captures destination-local and faction-wide resource effects once per planet.
            /// </summary>
            /// <param name="context">The current AI turn context.</param>
            /// <param name="planet">The destination planet.</param>
            /// <param name="mineCount">The destination's total mine count.</param>
            /// <param name="activeMineCount">The destination's completed stationary mine count.</param>
            internal RemovalEconomics(
                AITurnContext context,
                Planet planet,
                int mineCount,
                int activeMineCount
            )
            {
                AIAssessment assessment = context.Assessment;
                int projectedCapacity = assessment.ProjectedMaterialCapacity;
                int minedAfterRemoval = Math.Max(
                    0,
                    assessment.ProjectedMinedResourceCapacity
                        - (mineCount <= planet.GetRawResourceNodes() ? 1 : 0)
                );
                int refinementAfterRemoval = Math.Max(
                    0,
                    assessment.ProjectedRefinementCapacity - 1
                );
                _projectedMineCapacityLoss = Math.Max(
                    0,
                    projectedCapacity
                        - CalculateMaterialCapacity(
                            assessment.ProjectedResourceNodeCapacity,
                            minedAfterRemoval,
                            assessment.ProjectedRefinementCapacity
                        )
                );
                _projectedRefineryCapacityLoss = Math.Max(
                    0,
                    projectedCapacity
                        - CalculateMaterialCapacity(
                            assessment.ProjectedResourceNodeCapacity,
                            assessment.ProjectedMinedResourceCapacity,
                            refinementAfterRemoval
                        )
                );

                bool productionActive = !planet.IsResourceProductionSuspended();
                int availableMinedAfterRemoval = Math.Max(
                    0,
                    assessment.AvailableMinedResourceCapacity
                        - (
                            productionActive && activeMineCount <= planet.GetRawResourceNodes()
                                ? 1
                                : 0
                        )
                );
                int availableRefinementAfterRemoval = Math.Max(
                    0,
                    assessment.AvailableRefinementCapacity - (productionActive ? 1 : 0)
                );
                _availableMineCapacityLoss = Math.Max(
                    0,
                    assessment.AvailableMaterialCapacity
                        - CalculateMaterialCapacity(
                            assessment.AvailableResourceNodeCapacity,
                            availableMinedAfterRemoval,
                            assessment.AvailableRefinementCapacity
                        )
                );
                _availableRefineryCapacityLoss = Math.Max(
                    0,
                    assessment.AvailableMaterialCapacity
                        - CalculateMaterialCapacity(
                            assessment.AvailableResourceNodeCapacity,
                            assessment.AvailableMinedResourceCapacity,
                            availableRefinementAfterRemoval
                        )
                );
            }

            /// <summary>
            /// Returns whether the faction can remove a resource facility without crossing its
            /// configured economy safety targets.
            /// </summary>
            /// <param name="context">The current AI turn context.</param>
            /// <param name="building">The candidate resource facility.</param>
            /// <returns>True when the resource facility is economically expendable.</returns>
            internal bool CanSpare(AITurnContext context, Building building)
            {
                int availableCapacityLoss = GetAvailableCapacityLoss(building);
                int projectedCapacityLoss = GetProjectedCapacityLoss(building);
                if (availableCapacityLoss <= 0 && projectedCapacityLoss <= 0)
                    return true;

                int remainingProjectedCapacity =
                    context.Assessment.ProjectedMaterialCapacity - projectedCapacityLoss;
                if (
                    remainingProjectedCapacity <= 0
                    || context.Assessment.PendingRawMaterialRequestCount > 0
                    || context.Assessment.PendingRefinedMaterialRequestCount > 0
                    || context.Assessment.ProjectedRefinedMaterialPercent
                        <= context.Game.Config.AI.Selection.RefinedMaterialEconomyWarningPercent
                )
                    return false;

                long remainingAvailableHeadroom =
                    (long)context.Assessment.ProjectedMaintenanceHeadroom
                    - (long)availableCapacityLoss
                        * context.Assessment.MaintenanceCapacityPerResourcePair
                    + building.MaintenanceCost;
                long remainingProjectedHeadroom =
                    (long)context.Assessment.ProjectedEconomyMaintenanceHeadroom
                    - (long)projectedCapacityLoss
                        * context.Assessment.MaintenanceCapacityPerResourcePair
                    + building.MaintenanceCost;
                int target = context.Game.Config.AI.Selection.MaintenanceHeadroomTarget;
                return remainingAvailableHeadroom >= target && remainingProjectedHeadroom >= target;
            }

            /// <summary>
            /// Returns the immediate net maintenance headroom consumed by removing a facility.
            /// </summary>
            /// <param name="context">The current AI turn context.</param>
            /// <param name="building">The candidate facility.</param>
            /// <returns>The non-negative maintenance headroom cost.</returns>
            internal int GetMaintenanceHeadroomCost(AITurnContext context, Building building)
            {
                if (building.IsDestroyed)
                    return 0;

                long capacityCost =
                    (long)GetAvailableCapacityLoss(building)
                    * context.Assessment.MaintenanceCapacityPerResourcePair;
                return (int)
                    Math.Min(int.MaxValue, Math.Max(0, capacityCost - building.MaintenanceCost));
            }

            /// <summary>
            /// Returns the active material capacity lost by removing a facility.
            /// </summary>
            /// <param name="building">The candidate facility.</param>
            /// <returns>The available material-capacity loss.</returns>
            private int GetAvailableCapacityLoss(Building building)
            {
                if (!building.IsOperational())
                    return 0;

                return building.GetBuildingType() switch
                {
                    BuildingType.Mine => _availableMineCapacityLoss,
                    BuildingType.Refinery => _availableRefineryCapacityLoss,
                    _ => 0,
                };
            }

            /// <summary>
            /// Returns the future material capacity lost by removing a facility.
            /// </summary>
            /// <param name="building">The candidate facility.</param>
            /// <returns>The projected material-capacity loss.</returns>
            internal int GetProjectedCapacityLoss(Building building)
            {
                if (building.IsDestroyed)
                    return 0;

                return building.GetBuildingType() switch
                {
                    BuildingType.Mine => _projectedMineCapacityLoss,
                    BuildingType.Refinery => _projectedRefineryCapacityLoss,
                    _ => 0,
                };
            }

            /// <summary>
            /// Returns paired material capacity for the supplied resource totals.
            /// </summary>
            /// <param name="resourceNodes">Accessible resource-node capacity.</param>
            /// <param name="minedResources">Mine capacity.</param>
            /// <param name="refinementCapacity">Refinery capacity.</param>
            /// <returns>The paired material capacity.</returns>
            private static int CalculateMaterialCapacity(
                int resourceNodes,
                int minedResources,
                int refinementCapacity
            )
            {
                return Math.Min(resourceNodes, Math.Min(minedResources, refinementCapacity));
            }
        }

        /// <summary>
        /// Orders building roles from least to most disruptive to remove.
        /// </summary>
        internal enum AIPlanetBuildingRemovalPriority
        {
            Rubble,
            Unfinished,
            SurplusProduction,
            StaticFacility,
            ExpendableResource,
            RequiredProduction,
            RequiredResource,
        }
    }
}
