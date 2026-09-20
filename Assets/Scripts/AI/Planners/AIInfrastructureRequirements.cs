using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Scoring;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Defines strategic production-facility requirements shared by construction and retirement.
    /// </summary>
    internal sealed class AIInfrastructureRequirements
    {
        internal readonly struct FacilityPortfolio
        {
            internal int ConstructionFacilities { get; }
            internal int Shipyards { get; }
            internal int TrainingFacilities { get; }
            internal int StaticDefenses { get; }
            internal int Total =>
                ConstructionFacilities + Shipyards + TrainingFacilities + StaticDefenses;

            /// <summary>
            /// Creates a strategic-facility portfolio snapshot.
            /// </summary>
            /// <param name="constructionFacilities">Construction-facility count.</param>
            /// <param name="shipyards">Shipyard count.</param>
            /// <param name="trainingFacilities">Training-facility count.</param>
            /// <param name="staticDefenses">Static-defense count.</param>
            internal FacilityPortfolio(
                int constructionFacilities,
                int shipyards,
                int trainingFacilities,
                int staticDefenses
            )
            {
                ConstructionFacilities = constructionFacilities;
                Shipyards = shipyards;
                TrainingFacilities = trainingFacilities;
                StaticDefenses = staticDefenses;
            }
        }

        /// <summary>
        /// Captures the projected strategic-facility mix once for a production turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Counts for productive and static-defense facilities.</returns>
        internal FacilityPortfolio BuildPortfolio(AITurnContext context)
        {
            int constructionFacilities = 0;
            int shipyards = 0;
            int trainingFacilities = 0;
            int staticDefenses = 0;
            foreach (Planet planet in context.Assessment.OwnedPlanets)
            {
                foreach (Building building in context.Assessment.GetPlanetBuildings(planet))
                {
                    if (building.GetOwnerInstanceID() != context.Faction.InstanceID)
                        continue;
                    switch (building.GetBuildingType())
                    {
                        case BuildingType.ConstructionFacility:
                            constructionFacilities++;
                            break;
                        case BuildingType.Shipyard:
                            shipyards++;
                            break;
                        case BuildingType.TrainingFacility:
                            trainingFacilities++;
                            break;
                        case BuildingType.Defense:
                        case BuildingType.Weapon:
                            staticDefenses++;
                            break;
                    }
                }
            }
            return new FacilityPortfolio(
                constructionFacilities,
                shipyards,
                trainingFacilities,
                staticDefenses
            );
        }

        /// <summary>
        /// Adds planetary starfighter requirements in strategic priority order.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="requirements">The requirement collection to update.</param>
        internal void AddPlanetaryStarfighterRequirements(
            AITurnContext context,
            ICollection<AIProductionRequirement> requirements
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            foreach (
                Planet planet in context
                    .Assessment.OwnedPlanets.Where(IsUsablePlanet)
                    .OrderByDescending(context.Assessment.GetPlanetValue)
                    .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
            )
            {
                if (
                    context.Game.Config.AI.NonCapitalSummary.RequireStaticDefenseBeforeStarfighters
                    && !HasProductionInfrastructure(context, planet)
                    && !HasCompletedStaticDefense(context, planet)
                )
                    continue;

                int committedCount = GetOwnedStarfighterCount(context, planet);
                int targetCount = GetPlanetaryStarfighterCount(context, planet);
                int deficit = targetCount - committedCount;
                if (deficit <= 0)
                    continue;

                requirements.Add(
                    new AIProductionRequirement(
                        AIProductionRequirement.CreateId(
                            context.Faction.InstanceID,
                            AIProductionRequirementKind.PlanetaryStarfighterReserve,
                            planet.InstanceID
                        ),
                        AIProductionRequirementKind.PlanetaryStarfighterReserve,
                        ManufacturingType.Ship,
                        BuildingType.None,
                        planet,
                        deficit,
                        GetDefensePressure(
                            context,
                            planet,
                            config.PlanetaryStarfighterDemandPercent,
                            deficit,
                            targetCount
                        )
                    )
                );
            }
        }

        /// <summary>
        /// Adds static-defense requirements for owned planets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="requirements">The requirement collection to update.</param>
        /// <param name="portfolio">The turn-scoped facility portfolio.</param>
        internal void AddPlanetaryDefenseRequirements(
            AITurnContext context,
            ICollection<AIProductionRequirement> requirements,
            FacilityPortfolio portfolio
        )
        {
            foreach (
                Planet planet in context
                    .Assessment.OwnedPlanets.Where(IsUsablePlanet)
                    .OrderByDescending(context.Assessment.GetPlanetValue)
                    .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
            )
                AddPlanetaryDefenseRequirements(context, requirements, planet, portfolio);
        }

        /// <summary>
        /// Adds static-defense requirements for one planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="requirements">The requirement collection to update.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="portfolio">The turn-scoped facility portfolio.</param>
        private void AddPlanetaryDefenseRequirements(
            AITurnContext context,
            ICollection<AIProductionRequirement> requirements,
            Planet planet,
            FacilityPortfolio portfolio
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int availableEnergy = planet.GetAvailableEnergy();
            int shieldTarget = GetPlanetaryShieldCount(context, planet);
            int shieldCount = context
                .Assessment.GetPlanetBuildings(planet)
                .Count(building =>
                    building.GetOwnerInstanceID() == context.Faction.InstanceID
                    && building.IsPlanetaryShieldGenerator()
                );
            int shieldQuantity = Math.Min(Math.Max(0, shieldTarget - shieldCount), availableEnergy);
            if (shieldQuantity > 0)
            {
                requirements.Add(
                    CreateDefenseRequirement(
                        context,
                        planet,
                        BuildingType.Defense,
                        shieldQuantity,
                        shieldTarget,
                        config.PlanetaryShieldDemandPercent,
                        shieldCount == 0,
                        portfolio
                    )
                );
                availableEnergy -= shieldQuantity;
            }

            int weaponCount = context
                .Assessment.GetPlanetBuildings(planet)
                .Count(building =>
                    building.GetOwnerInstanceID() == context.Faction.InstanceID
                    && building.GetBuildingType() == BuildingType.Weapon
                );
            int weaponTarget = GetPlanetaryWeaponCount(context, planet, weaponCount);
            int weaponDeficit = weaponTarget - weaponCount;
            if (weaponDeficit <= 0 || availableEnergy <= 0)
                return;
            requirements.Add(
                CreateDefenseRequirement(
                    context,
                    planet,
                    BuildingType.Weapon,
                    Math.Min(weaponDeficit, availableEnergy),
                    weaponTarget,
                    config.PlanetaryWeaponDemandPercent,
                    false,
                    portfolio
                )
            );
        }

        /// <summary>
        /// Creates one static-defense production requirement.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The destination planet.</param>
        /// <param name="buildingType">The defense building type.</param>
        /// <param name="deficit">The current deficit.</param>
        /// <param name="targetCount">The desired count.</param>
        /// <param name="basePercent">The configured base pressure.</param>
        /// <param name="isInitialShield">Whether this establishes the first shield.</param>
        /// <param name="portfolio">The turn-scoped facility portfolio.</param>
        /// <returns>The production requirement.</returns>
        private static AIProductionRequirement CreateDefenseRequirement(
            AITurnContext context,
            Planet planet,
            BuildingType buildingType,
            int deficit,
            int targetCount,
            int basePercent,
            bool isInitialShield,
            FacilityPortfolio portfolio
        )
        {
            return new AIProductionRequirement(
                AIProductionRequirement.CreateId(
                    context.Faction.InstanceID,
                    AIProductionRequirementKind.PlanetaryDefense,
                    buildingType,
                    planet.InstanceID
                ),
                AIProductionRequirementKind.PlanetaryDefense,
                ManufacturingType.Building,
                buildingType,
                planet,
                deficit,
                GetDefensePressure(
                    context,
                    planet,
                    basePercent,
                    deficit,
                    targetCount,
                    isInitialShield,
                    portfolio
                )
            );
        }

        /// <summary>
        /// Adds local fighter work for otherwise idle shipyards.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="requirements">The requirement collection to update.</param>
        internal void AddIdleShipyardRequirements(
            AITurnContext context,
            ICollection<AIProductionRequirement> requirements
        )
        {
            HashSet<string> plannedPlanets = requirements
                .Where(requirement =>
                    requirement.Kind == AIProductionRequirementKind.PlanetaryStarfighterReserve
                )
                .Select(requirement => requirement.DestinationPlanet?.InstanceID)
                .Where(planetId => !string.IsNullOrEmpty(planetId))
                .ToHashSet(StringComparer.Ordinal);

            foreach (Planet planet in context.Assessment.OwnedPlanets)
            {
                if (
                    !IsUsablePlanet(planet)
                    || plannedPlanets.Contains(planet.InstanceID)
                    || context.Assessment.GetPlanetProductionFacilityCount(
                        planet,
                        ManufacturingType.Ship
                    ) <= 0
                    || planet
                        .GetManufacturingQueue()
                        .TryGetValue(ManufacturingType.Ship, out List<IManufacturable> queue)
                        && queue.Any(item => item?.IsManufacturingComplete() == false)
                )
                    continue;

                int targetCount =
                    GetPlanetaryStarfighterCount(context, planet)
                    + context.Game.Config.AI.Infrastructure.IdleShipyardFighterReserveCount;
                if (GetOwnedStarfighterCount(context, planet) >= targetCount)
                    continue;

                requirements.Add(
                    new AIProductionRequirement(
                        AIProductionRequirement.CreateId(
                            context.Faction.InstanceID,
                            AIProductionRequirementKind.PlanetaryStarfighterReserve,
                            "idle-shipyard",
                            planet.InstanceID
                        ),
                        AIProductionRequirementKind.PlanetaryStarfighterReserve,
                        ManufacturingType.Ship,
                        BuildingType.None,
                        planet,
                        1,
                        context.Game.Config.AI.Infrastructure.IdleShipyardFighterDemandPercent
                    )
                );
            }
        }

        /// <summary>
        /// Adds planetary garrison-regiment requirements.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="requirements">The requirement collection to update.</param>
        internal void AddGarrisonRequirements(
            AITurnContext context,
            ICollection<AIProductionRequirement> requirements
        )
        {
            foreach (Planet planet in context.Assessment.OwnedPlanets.Where(IsUsablePlanet))
            {
                int targetCount = GetGarrisonCount(context, planet);
                int currentCount = context
                    .Assessment.GetPlanetRegiments(planet)
                    .Count(regiment => regiment.GetOwnerInstanceID() == context.Faction.InstanceID);
                int deficit = targetCount - currentCount;
                if (deficit <= 0)
                    continue;
                requirements.Add(
                    new AIProductionRequirement(
                        AIProductionRequirement.CreateId(
                            context.Faction.InstanceID,
                            AIProductionRequirementKind.GarrisonRegimentReserve,
                            planet.InstanceID
                        ),
                        AIProductionRequirementKind.GarrisonRegimentReserve,
                        ManufacturingType.Troop,
                        BuildingType.None,
                        planet,
                        deficit,
                        GetDefensePressure(
                            context,
                            planet,
                            context.Game.Config.AI.Infrastructure.PlanetaryGarrisonDemandPercent,
                            deficit,
                            targetCount
                        )
                    )
                );
            }
        }

        /// <summary>
        /// Returns the garrison reserve required for one planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The target regiment count.</returns>
        private static int GetGarrisonCount(AITurnContext context, Planet planet)
        {
            int stabilityTarget = UprisingSystem.CalculateGarrisonRequirement(
                planet,
                context.Faction,
                context.Game.Config.AI.Garrison
            );
            int sabotageTarget = stabilityTarget > 0 ? stabilityTarget + 1 : 0;
            if (context.Assessment.HasEnemyControlSupport(planet))
                sabotageTarget = Math.Max(sabotageTarget, 2);
            int captureFloor = context.Game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount;
            if (!planet.IsHeadquarters && !context.Assessment.IsPlanetThreatened(planet))
            {
                captureFloor = IntegerMath.ScaleByPercent(
                    captureFloor,
                    context.Game.Config.AI.Garrison.InteriorCaptureFloorPercent
                );
            }
            return Math.Max(sabotageTarget, Math.Max(captureFloor, stabilityTarget));
        }

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
        /// Returns whether a planet has completed its required static defenses.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True when shield and weapon requirements are satisfied.</returns>
        private bool HasCompletedStaticDefense(AITurnContext context, Planet planet)
        {
            int shieldTarget = GetPlanetaryShieldCount(context, planet);
            int shieldCount = 0;
            int weaponCount = 0;
            foreach (Building building in context.Assessment.GetPlanetBuildings(planet))
            {
                if (building.GetOwnerInstanceID() != context.Faction.InstanceID)
                    continue;
                if (building.IsPlanetaryShieldGenerator())
                    shieldCount++;
                else if (building.GetBuildingType() == BuildingType.Weapon)
                    weaponCount++;
            }

            return shieldCount >= shieldTarget
                && weaponCount >= GetPlanetaryWeaponCount(context, planet, weaponCount);
        }

        /// <summary>
        /// Returns the starfighter requirement for one planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The required starfighter count.</returns>
        private static int GetPlanetaryStarfighterCount(AITurnContext context, Planet planet)
        {
            GameConfig.AINonCapitalSummaryConfig config = context.Game.Config.AI.NonCapitalSummary;
            bool hasShipProduction =
                context.Assessment.GetPlanetProductionFacilityCount(planet, ManufacturingType.Ship)
                > 0;
            int baseline =
                planet.IsHeadquarters ? config.StarfighterRequirementHeadquarters
                : hasShipProduction ? config.StarfighterRequirementInfrastructure
                : config.StarfighterRequirementDefault;
            if (
                !planet.IsHeadquarters
                && !context.Assessment.IsPlanetThreatened(planet)
                && !hasShipProduction
            )
            {
                baseline = IntegerMath.ScaleByPercent(
                    baseline,
                    config.InteriorStarfighterBaselinePercent
                );
            }

            int requiredStrength = context.StrategicPlan.GetPlanetDefenseStrength(planet);
            int fighterStrength = GetStrongestStarfighterStrength(context);
            return baseline
                + (
                    fighterStrength > 0
                        ? IntegerMath.DivideRoundedUp(requiredStrength, fighterStrength)
                        : 0
                );
        }

        /// <summary>
        /// Returns the strongest available starfighter weapon strength.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The strongest weapon strength, or zero.</returns>
        private static int GetStrongestStarfighterStrength(AITurnContext context)
        {
            return context
                .Faction.GetUnlockedTechnologies(ManufacturingType.Ship)
                .Select(technology => technology.GetReference())
                .OfType<Starfighter>()
                .Where(starfighter =>
                    IManufacturable.CanBeManufacturedBy(starfighter, context.Faction.InstanceID)
                )
                .Select(starfighter => starfighter.GetWeaponStrength())
                .DefaultIfEmpty()
                .Max();
        }

        /// <summary>
        /// Counts owned starfighters assigned to a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The assigned starfighter count.</returns>
        private static int GetOwnedStarfighterCount(AITurnContext context, Planet planet)
        {
            return context
                .Assessment.GetPlanetStarfighters(planet)
                .Count(starfighter =>
                    starfighter.GetOwnerInstanceID() == context.Faction.InstanceID
                );
        }

        /// <summary>
        /// Returns production pressure for planetary defense units.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The defended planet.</param>
        /// <param name="basePercent">The configured base pressure.</param>
        /// <param name="deficit">The current deficit.</param>
        /// <param name="targetCount">The desired count.</param>
        /// <returns>The bounded defense pressure.</returns>
        private static double GetDefensePressure(
            AITurnContext context,
            Planet planet,
            int basePercent,
            int deficit,
            int targetCount,
            bool isInitialShield = false,
            FacilityPortfolio portfolio = default
        )
        {
            GameConfig.AIProductionDemandUtilityConfig utility = context
                .Game
                .Config
                .AI
                .Infrastructure
                .DemandUtility;
            double pressure =
                basePercent
                + AIUtility.EvaluateDiscretePressure(
                    deficit / (double)Math.Max(1, targetCount),
                    utility.DefenseDeficit
                );
            double highestValue = context.Assessment.GetHighestOwnedPlanetValue();
            if (highestValue > 0)
            {
                pressure += AIUtility.EvaluatePressure(
                    context.Assessment.GetPlanetValue(planet) / highestValue,
                    utility.DefenseValue
                );
            }
            pressure += AIUtility.EvaluatePressure(
                context.Assessment.IsFactionHeadquarters(planet) ? 1 : 0,
                utility.DefenseHeadquarters
            );
            pressure += AIUtility.EvaluatePressure(
                context.Assessment.GetPlanetDefenseThreatStrength(planet) > 0 ? 1 : 0,
                utility.DefenseThreat
            );
            if (portfolio.Total > 0)
            {
                pressure += GetPortfolioPressure(
                    context,
                    AIProductionRequirementKind.PlanetaryDefense,
                    portfolio
                );
            }

            double boundedPressure = Math.Max(0, Math.Min(100, pressure));
            return isInitialShield
                ? boundedPressure
                    + AIUtility.EvaluatePressure(
                        planet.GetOpposingPopularSupport(context.Faction.InstanceID) / 100.0,
                        utility.ShieldSupport
                    )
                    + AIUtility.EvaluatePressure(
                        AIUtility.Fulfillment(
                            context.Assessment.GetDefensiveSupportRisk(planet),
                            AIUtilityDomain.SectorSupport
                        ),
                        utility.ShieldSectorRisk
                    )
                : boundedPressure;
        }

        /// <summary>
        /// Returns pressure based on a facility category's target portfolio share.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">The facility requirement category.</param>
        /// <param name="portfolio">The current facility portfolio.</param>
        /// <returns>Signed portfolio pressure.</returns>
        private static double GetPortfolioPressure(
            AITurnContext context,
            AIProductionRequirementKind kind,
            FacilityPortfolio portfolio
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            (int count, int targetPercent) = kind switch
            {
                AIProductionRequirementKind.ConstructionFacility => (
                    portfolio.ConstructionFacilities,
                    config.ConstructionFacilityPortfolioPercent
                ),
                AIProductionRequirementKind.Shipyard => (
                    portfolio.Shipyards,
                    config.ShipyardPortfolioPercent
                ),
                AIProductionRequirementKind.TrainingFacility => (
                    portfolio.TrainingFacilities,
                    config.TrainingFacilityPortfolioPercent
                ),
                AIProductionRequirementKind.PlanetaryDefense => (
                    portfolio.StaticDefenses,
                    config.StaticDefensePortfolioPercent
                ),
                _ => (0, 0),
            };
            if (portfolio.Total <= 0 || targetPercent <= 0)
                return 0;
            double deviation = (targetPercent - count * 100.0 / portfolio.Total) / targetPercent;
            double normalized = AIUtility.Fulfillment(Math.Abs(deviation), 1);
            GameConfig.AIConsiderationConfig consideration = config.DemandUtility.FacilityPortfolio;
            return deviation >= 0
                ? AIUtility.EvaluatePressure(normalized, consideration)
                : -AIUtility.EvaluatePressure(normalized, consideration);
        }

        /// <summary>
        /// Returns whether a planet can receive defensive production.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True when the planet is an intact colony.</returns>
        private static bool IsUsablePlanet(Planet planet)
        {
            return planet?.IsColonized == true && !planet.IsDestroyed;
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
