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
            foreach (Building building in buildings)
            {
                if (building?.GetBuildingType() == BuildingType.Mine)
                    mineCount++;
            }

            RemovalEconomics economics = new RemovalEconomics(context, planet, mineCount);
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
            double strategicImpact = building.GetBuildingType() switch
            {
                BuildingType.ConstructionFacility
                or BuildingType.Shipyard
                or BuildingType.TrainingFacility => GetProductionDeficitAfterRemoval(
                    context,
                    building.GetBuildingType()
                ),
                BuildingType.Mine or BuildingType.Refinery => economics.GetCapacityLoss(
                    building.GetBuildingType()
                ),
                _ => 0,
            };
            return new Removal(building, priority, strategicImpact);
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

            /// <summary>
            /// Creates an evaluated building removal.
            /// </summary>
            /// <param name="building">The building selected for removal.</param>
            /// <param name="priority">The broad strategic removal tier.</param>
            /// <param name="strategicImpact">The projected deficit or resource-capacity loss.</param>
            internal Removal(
                Building building,
                AIPlanetBuildingRemovalPriority priority,
                double strategicImpact
            )
            {
                Building = building;
                _priority = priority;
                _strategicImpact = strategicImpact;
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
            private readonly int _mineCapacityLoss;
            private readonly int _refineryCapacityLoss;

            /// <summary>
            /// Captures destination-local and faction-wide resource effects once per planet.
            /// </summary>
            /// <param name="context">The current AI turn context.</param>
            /// <param name="planet">The destination planet.</param>
            /// <param name="mineCount">The destination's total mine count.</param>
            internal RemovalEconomics(AITurnContext context, Planet planet, int mineCount)
            {
                int projectedCapacity = context.Assessment.ProjectedMaterialCapacity;
                int minedAfterRemoval = Math.Max(
                    0,
                    context.Assessment.ProjectedMinedResourceCapacity
                        - (mineCount <= planet.GetRawResourceNodes() ? 1 : 0)
                );
                int refinementAfterRemoval = Math.Max(
                    0,
                    context.Assessment.ProjectedRefinementCapacity - 1
                );
                _mineCapacityLoss = Math.Max(
                    0,
                    projectedCapacity - Math.Min(projectedCapacity, minedAfterRemoval)
                );
                _refineryCapacityLoss = Math.Max(
                    0,
                    projectedCapacity - Math.Min(projectedCapacity, refinementAfterRemoval)
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
                int capacityLoss = GetCapacityLoss(building.GetBuildingType());
                if (capacityLoss <= 0)
                    return true;

                int remainingCapacity = context.Assessment.ProjectedMaterialCapacity - capacityLoss;
                if (
                    remainingCapacity <= 0
                    || context.Assessment.PendingRawMaterialRequestCount > 0
                    || context.Assessment.PendingRefinedMaterialRequestCount > 0
                    || context.Assessment.ProjectedRefinedMaterialPercent
                        <= context.Game.Config.AI.Selection.RefinedMaterialEconomyWarningPercent
                )
                    return false;

                long remainingHeadroom =
                    (long)context.Assessment.ProjectedEconomyMaintenanceHeadroom
                    - (long)capacityLoss * context.Assessment.MaintenanceCapacityPerResourcePair
                    + building.MaintenanceCost;
                return remainingHeadroom
                    >= context.Game.Config.AI.Selection.MaintenanceHeadroomTarget;
            }

            /// <summary>
            /// Returns the projected paired-resource capacity lost by removing a facility type.
            /// </summary>
            /// <param name="buildingType">The resource facility type.</param>
            /// <returns>The projected material-capacity loss.</returns>
            internal int GetCapacityLoss(BuildingType buildingType)
            {
                return buildingType switch
                {
                    BuildingType.Mine => _mineCapacityLoss,
                    BuildingType.Refinery => _refineryCapacityLoss,
                    _ => 0,
                };
            }
        }

        /// <summary>
        /// Orders building roles from least to most disruptive to remove.
        /// </summary>
        internal enum AIPlanetBuildingRemovalPriority
        {
            Unfinished,
            SurplusProduction,
            StaticFacility,
            ExpendableResource,
            RequiredProduction,
            RequiredResource,
        }
    }
}
