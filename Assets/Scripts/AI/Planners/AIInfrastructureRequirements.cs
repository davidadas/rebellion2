using System;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Defines strategic production-facility requirements shared by construction and retirement.
    /// </summary>
    internal sealed class AIInfrastructureRequirements
    {
        /// <summary>
        /// Returns the number of shield generators required by a planet's strategic exposure.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The required shield-generator count.</returns>
        internal int GetPlanetaryShieldCount(AITurnContext context, Planet planet)
        {
            if (
                context?.Assessment == null
                || !context.Assessment.IsOwnedPlanet(planet)
                || context.Game?.Config == null
            )
                return 0;

            int limit = context.Game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit;
            if (
                context.Assessment.IsPriorityDefensePlanet(planet)
                || context.Assessment.IsPlanetThreatened(planet)
            )
                return limit;

            bool hasSupportRisk =
                context.Assessment.GetFactionPopularSupport(planet)
                    < context.Game.Config.AI.Garrison.SupportThreshold
                || context.Assessment.GetDefensiveSupportRisk(planet) > 0;
            return hasSupportRisk || HasProductionInfrastructure(context, planet)
                ? Math.Min(1, limit)
                : 0;
        }

        /// <summary>
        /// Returns the number of weapon emplacements required by a planet's strategic exposure.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="currentCount">The current weapon-emplacement count.</param>
        /// <returns>The required weapon-emplacement count.</returns>
        internal int GetPlanetaryWeaponCount(AITurnContext context, Planet planet, int currentCount)
        {
            if (
                context?.Assessment == null
                || !context.Assessment.IsOwnedPlanet(planet)
                || context.Game?.Config == null
                || !context.Assessment.IsPriorityDefensePlanet(planet)
                    && !context.Assessment.IsPlanetThreatened(planet)
            )
                return 0;

            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            return Math.Max(
                config.PlanetaryWeaponTargetCount,
                currentCount + config.PlanetaryDefenseSurplusBatchSize
            );
        }

        /// <summary>
        /// Returns whether a planet contains strategic production infrastructure.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True when the planet has at least one production facility.</returns>
        internal bool HasProductionInfrastructure(AITurnContext context, Planet planet)
        {
            return context?.Assessment != null
                && (
                    context.Assessment.GetPlanetProductionFacilityCount(
                        planet,
                        ManufacturingType.Building
                    ) > 0
                    || context.Assessment.GetPlanetProductionFacilityCount(
                        planet,
                        ManufacturingType.Ship
                    ) > 0
                    || context.Assessment.GetPlanetProductionFacilityCount(
                        planet,
                        ManufacturingType.Troop
                    ) > 0
                );
        }

        /// <summary>
        /// Returns the strategic facility quantity required by the faction's current planet count.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="buildingType">The production-facility category.</param>
        /// <returns>The required faction-wide facility count.</returns>
        internal int GetDesiredFacilityCount(AITurnContext context, BuildingType buildingType)
        {
            if (context?.Assessment == null || context.Game?.Config?.AI?.Infrastructure == null)
                return 0;

            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int planetsPerFacility = GetPlanetsPerFacility(config, buildingType);
            if (planetsPerFacility <= 0)
                return 0;

            int desiredCount = IntegerMath.DivideRoundedUp(
                context.Assessment.OwnedPlanets.Count,
                planetsPerFacility
            );
            return buildingType == BuildingType.ConstructionFacility
                ? Math.Max(
                    desiredCount,
                    Math.Min(
                        context.Assessment.OwnedPlanets.Count,
                        config.MinimumConstructionFacilityLanes
                    )
                )
                : desiredCount;
        }

        /// <summary>
        /// Returns the configured number of planets supported by one facility category.
        /// </summary>
        /// <param name="config">Infrastructure configuration.</param>
        /// <param name="buildingType">The production-facility category.</param>
        /// <returns>The planets-per-facility ratio, or zero for non-production facilities.</returns>
        private static int GetPlanetsPerFacility(
            GameConfig.AIInfrastructureConfig config,
            BuildingType buildingType
        )
        {
            return buildingType switch
            {
                BuildingType.ConstructionFacility => config.PlanetsPerConstructionFacility,
                BuildingType.Shipyard => config.PlanetsPerShipyard,
                BuildingType.TrainingFacility => config.PlanetsPerTrainingFacility,
                _ => 0,
            };
        }
    }
}
