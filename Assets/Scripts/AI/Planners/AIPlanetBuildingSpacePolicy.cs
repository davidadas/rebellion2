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
            if (
                context?.Assessment == null
                || context.Faction == null
                || planet == null
                || planet.GetOwnerInstanceID() != context.Faction.InstanceID
                || planet.GetAvailableEnergy() > 0
            )
                return null;

            Building selected = null;
            foreach (Building building in context.Assessment.GetPlanetBuildings(planet))
            {
                if (
                    IsRemovable(context, planet, building)
                    && (selected == null || IsPreferredRemoval(context, building, selected))
                )
                {
                    selected = building;
                }
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
        /// Returns whether one removal candidate sorts ahead of the current selection.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="candidate">The candidate building.</param>
        /// <param name="selected">The currently selected building.</param>
        /// <returns>True when the candidate is less disruptive to remove.</returns>
        private static bool IsPreferredRemoval(
            AITurnContext context,
            Building candidate,
            Building selected
        )
        {
            int comparison = GetRemovalPriority(context, candidate)
                .CompareTo(GetRemovalPriority(context, selected));
            if (comparison != 0)
                return comparison < 0;

            comparison = candidate.GetProcessRate().CompareTo(selected.GetProcessRate());
            if (comparison != 0)
                return comparison < 0;

            comparison = candidate.ConstructionCost.CompareTo(selected.ConstructionCost);
            if (comparison != 0)
                return comparison < 0;

            comparison = selected.MaintenanceCost.CompareTo(candidate.MaintenanceCost);
            if (comparison != 0)
                return comparison < 0;

            return string.CompareOrdinal(candidate.InstanceID, selected.InstanceID) < 0;
        }

        /// <summary>
        /// Returns the strategic removal tier for a candidate building.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="building">The candidate building.</param>
        /// <returns>The removal tier, where lower values are preferred.</returns>
        private static AIPlanetBuildingRemovalPriority GetRemovalPriority(
            AITurnContext context,
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
                BuildingType.ConstructionFacility
                or BuildingType.Shipyard
                or BuildingType.TrainingFacility =>
                    AIPlanetBuildingRemovalPriority.RequiredProduction,
                BuildingType.Mine or BuildingType.Refinery =>
                    AIPlanetBuildingRemovalPriority.ResourceProduction,
                _ => AIPlanetBuildingRemovalPriority.RequiredProduction,
            };
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

        private enum AIPlanetBuildingRemovalPriority
        {
            Unfinished,
            SurplusProduction,
            StaticFacility,
            RequiredProduction,
            ResourceProduction,
        }
    }
}
