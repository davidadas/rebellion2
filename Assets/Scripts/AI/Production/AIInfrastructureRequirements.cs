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
        /// <param name="isInitialShield">Whether the requirement establishes the first shield.</param>
        /// <param name="portfolio">The current facility portfolio.</param>
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

        /// <summary>
        /// Adds production-facility expansion demands.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="placementScorer">The turn-scoped infrastructure placement scorer.</param>
        /// <param name="facilityPortfolio">The turn-scoped facility portfolio.</param>
        internal void AddProductionFacilityRequirements(
            AITurnContext context,
            List<AIProductionRequirement> demands,
            AIInfrastructurePlacementScorer placementScorer,
            FacilityPortfolio facilityPortfolio
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            AddProductionFacilityDemand(
                context,
                demands,
                ManufacturingType.Ship,
                AIProductionRequirementKind.Shipyard,
                BuildingType.Shipyard,
                config.ShipyardDemandPercent,
                placementScorer,
                facilityPortfolio
            );
            AddProductionFacilityDemand(
                context,
                demands,
                ManufacturingType.Building,
                AIProductionRequirementKind.ConstructionFacility,
                BuildingType.ConstructionFacility,
                config.ConstructionFacilityDemandPercent,
                placementScorer,
                facilityPortfolio
            );
            AddProductionFacilityDemand(
                context,
                demands,
                ManufacturingType.Troop,
                AIProductionRequirementKind.TrainingFacility,
                BuildingType.TrainingFacility,
                config.TrainingFacilityDemandPercent,
                placementScorer,
                facilityPortfolio
            );
        }

        /// <summary>
        /// Adds available production-facility upgrade demands.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        internal void AddProductionFacilityUpgradeRequirements(
            AITurnContext context,
            List<AIProductionRequirement> demands
        )
        {
            foreach (
                Planet planet in context
                    .Assessment.OwnedPlanets.Where(IsUsablePlanet)
                    .OrderByDescending(context.Assessment.GetPlanetValue)
                    .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
            )
            {
                AddProductionFacilityUpgradeDemand(
                    context,
                    demands,
                    planet,
                    BuildingType.ConstructionFacility
                );
                AddProductionFacilityUpgradeDemand(context, demands, planet, BuildingType.Shipyard);
                AddProductionFacilityUpgradeDemand(
                    context,
                    demands,
                    planet,
                    BuildingType.TrainingFacility
                );
            }
        }

        /// <summary>
        /// Adds an upgrade demand for one manufacturing category.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="planet">The planet whose facilities are evaluated.</param>
        /// <param name="buildingType">The production-facility type to upgrade.</param>
        private void AddProductionFacilityUpgradeDemand(
            AITurnContext context,
            List<AIProductionRequirement> demands,
            Planet planet,
            BuildingType buildingType
        )
        {
            if (HasPendingFacility(context, planet, buildingType))
                return;

            List<Building> activeFacilities = context
                .Assessment.GetPlanetBuildings(planet)
                .Where(building =>
                    building.GetOwnerInstanceID() == context.Faction.InstanceID
                    && building.GetBuildingType() == buildingType
                    && building.GetManufacturingStatus() == ManufacturingStatus.Complete
                    && building.Movement == null
                    && building.GetProcessRate() > 0
                )
                .ToList();
            if (
                activeFacilities.Count
                <= context
                    .Game
                    .Config
                    .AI
                    .Infrastructure
                    .ProductionFacilityUpgradeMinimumRemainingCount
            )
                return;

            List<Building> unlockedFacilities = context
                .Faction.GetUnlockedTechnologies(ManufacturingType.Building)
                .Select(technology => technology.GetReference())
                .OfType<Building>()
                .Where(building =>
                    building.GetBuildingType() == buildingType
                    && IManufacturable.CanBeManufacturedBy(building, context.Faction.InstanceID)
                )
                .ToList();
            Building replacement = activeFacilities
                .Where(current =>
                    unlockedFacilities.Any(candidate => current.CanUpgradeTo(candidate))
                )
                .OrderByDescending(building => building.GetProcessRate())
                .ThenBy(building => building.ResearchOrder)
                .ThenBy(building => building.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
            if (replacement == null)
                return;

            AIProductionRequirement demand = new AIProductionRequirement(
                AIProductionRequirement.CreateId(
                    context.Faction.InstanceID,
                    AIProductionRequirementKind.BuildingUpgrade,
                    buildingType,
                    planet.InstanceID,
                    replacement.InstanceID
                ),
                AIProductionRequirementKind.BuildingUpgrade,
                ManufacturingType.Building,
                buildingType,
                planet,
                1,
                GetProductionFacilityUpgradePressure(context, planet),
                buildingToReplace: replacement
            );
            demands.Add(demand);
        }

        /// <summary>
        /// Returns the pressure for upgrading a production facility.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The upgrade destination.</param>
        /// <returns>The demand pressure.</returns>
        private double GetProductionFacilityUpgradePressure(AITurnContext context, Planet planet)
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            GameConfig.AIProductionDemandUtilityConfig utility = config.DemandUtility;
            double pressure = config.ProductionFacilityUpgradeDemandPercent;
            double highestPlanetValue = context.Assessment.GetHighestOwnedPlanetValue();
            if (highestPlanetValue > 0)
            {
                pressure += AIUtility.EvaluatePressure(
                    context.Assessment.GetPlanetValue(planet) / highestPlanetValue,
                    utility.UpgradeValue
                );
            }

            pressure += AIUtility.EvaluatePressure(
                context.Assessment.IsFactionHeadquarters(planet) ? 1 : 0,
                utility.UpgradeHeadquarters
            );

            return Math.Max(0, Math.Min(100, pressure));
        }

        /// <summary>
        /// Adds expansion demand for one production-facility type.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="manufacturingType">The manufacturing category.</param>
        /// <param name="kind">The facility demand kind.</param>
        /// <param name="buildingType">The required facility type.</param>
        /// <param name="baseDemandPercent">The base demand pressure.</param>
        /// <param name="placementScorer">The turn-scoped infrastructure placement scorer.</param>
        /// <param name="facilityPortfolio">The turn-scoped facility portfolio.</param>
        private void AddProductionFacilityDemand(
            AITurnContext context,
            List<AIProductionRequirement> demands,
            ManufacturingType manufacturingType,
            AIProductionRequirementKind kind,
            BuildingType buildingType,
            int baseDemandPercent,
            AIInfrastructurePlacementScorer placementScorer,
            FacilityPortfolio facilityPortfolio
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            List<AIProductionRequirement> productionDemands = demands
                .Where(demand => demand.ManufacturingType == manufacturingType)
                .Where(demand => demand.Kind != kind)
                .OrderByDescending(demand => demand.Pressure)
                .ThenBy(demand => demand.Id, StringComparer.Ordinal)
                .ToList();
            int desiredFacilityCount = GetDesiredFacilityCount(context, buildingType);
            if (buildingType == BuildingType.TrainingFacility)
            {
                int demandCapacityTarget = IntegerMath.DivideRoundedUp(
                    productionDemands.Count,
                    Math.Max(1, config.TrainingDemandsPerFacility)
                );
                desiredFacilityCount = Math.Max(desiredFacilityCount, demandCapacityTarget);
            }
            int hubTarget =
                buildingType == BuildingType.Shipyard
                    ? context.Game.Config.AI.Infrastructure.ShipyardSectorHubTargetCount
                    : context.Game.Config.AI.Infrastructure.FacilitySectorHubTargetCount;
            List<IGrouping<string, Planet>> sectors = context
                .Assessment.OwnedPlanets.Where(planet =>
                    IsUsablePlanet(planet)
                    || (
                        buildingType == BuildingType.ConstructionFacility
                        && planet?.IsDestroyed == false
                        && planet.GetParentOfType<PlanetSector>()?.SectorType
                            == PlanetSectorType.OuterRim
                    )
                )
                .GroupBy(context.Assessment.GetPlanetSystemId)
                .OrderByDescending(sector =>
                    GetSectorFacilityDeficit(sector, buildingType, hubTarget)
                )
                .ThenByDescending(sector => GetColonyFoundationInput(sector, buildingType))
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .ToList();
            int remainingFacilityCount = Math.Max(
                0,
                desiredFacilityCount - GetOwnedFacilityCount(context, buildingType)
            );
            if (buildingType == BuildingType.ConstructionFacility)
                remainingFacilityCount = Math.Max(
                    remainingFacilityCount,
                    CountUnseededOuterRimSectors(sectors)
                );
            if (remainingFacilityCount == 0)
                return;

            double categoryBalancePressure = GetFacilityCategoryBalancePressure(
                sectors,
                buildingType,
                hubTarget,
                config.DemandUtility.FacilityBalance
            );
            foreach (IGrouping<string, Planet> sector in sectors)
            {
                if (remainingFacilityCount == 0)
                    break;

                List<Planet> sectorPlanets = sector
                    .Where(planet => GetAvailableFacilityExpansionEnergy(planet) > 0)
                    .ToList();
                if (sectorPlanets.Count == 0)
                    continue;
                AIProductionRequirement sectorDemand =
                    productionDemands.FirstOrDefault(demand =>
                        context.Assessment.GetPlanetSystemId(GetDemandPlanet(context, demand))
                        == sector.Key
                    ) ?? productionDemands.FirstOrDefault();
                Planet demandPlanet = GetDemandPlanet(context, sectorDemand) ?? sector.First();
                IReadOnlyList<(Planet Planet, double Score)> rankedDestinations =
                    placementScorer.ScoreDestinations(
                        sectorPlanets,
                        demandPlanet,
                        manufacturingType,
                        buildingType,
                        GetAvailableFacilityExpansionEnergy
                    );
                if (rankedDestinations.Count == 0)
                    continue;

                double colonyFoundationInput = GetColonyFoundationInput(sector, buildingType);
                int sectorFacilityCount = sector.Sum(planet =>
                    planet.GetTotalBuildingTypeCount(buildingType)
                );
                int targetCount = sectorFacilityCount > 0 ? hubTarget : 1;
                int sectorDeficit = GetSectorFacilityDeficit(sector, buildingType, hubTarget);
                if (colonyFoundationInput > 0)
                    sectorDeficit = Math.Max(1, sectorDeficit);
                int requestedQuantity = Math.Min(remainingFacilityCount, sectorDeficit);
                if (requestedQuantity <= 0)
                    continue;

                FacilityPortfolio pressurePortfolio =
                    colonyFoundationInput > 0 ? default : facilityPortfolio;
                double strategicBonus =
                    AIUtility.EvaluatePressure(
                        sectorFacilityCount == 0 ? 1 : 0,
                        config.DemandUtility.SectorCoverage
                    )
                    + AIUtility.EvaluatePressure(
                        sectorFacilityCount > 0 ? 1 : 0,
                        config.DemandUtility.PrimaryHub
                    )
                    + AIUtility.EvaluatePressure(
                        colonyFoundationInput,
                        config.DemandUtility.ColonyFoundation
                    )
                    + categoryBalancePressure;
                string demandId = AIProductionRequirement.CreateId(
                    context.Faction.InstanceID,
                    kind,
                    sector.Key,
                    "capacity"
                );
                int alternativeCount = Math.Max(1, config.FacilityPlanetsPerSector);
                bool addedAlternative = false;
                foreach (
                    (Planet destination, double placementUtility) in rankedDestinations.Take(
                        alternativeCount
                    )
                )
                {
                    addedAlternative |= AddSectorFacilityDemand(
                        context,
                        demands,
                        sectorDemand,
                        kind,
                        buildingType,
                        destination,
                        targetCount,
                        baseDemandPercent,
                        strategicBonus,
                        pressurePortfolio,
                        requestedQuantity,
                        demandId,
                        placementUtility
                    );
                }
                if (addedAlternative)
                    remainingFacilityCount -= requestedQuantity;
            }
        }

        /// <summary>
        /// Returns the amount required to seed or complete the strongest facility cluster in a sector.
        /// </summary>
        /// <param name="sector">The planets in one system.</param>
        /// <param name="buildingType">The facility category.</param>
        /// <param name="hubTarget">The desired concentrated facility count.</param>
        /// <returns>The outstanding sector facility quantity.</returns>
        private static int GetSectorFacilityDeficit(
            IEnumerable<Planet> sector,
            BuildingType buildingType,
            int hubTarget
        )
        {
            List<Planet> planets = sector.ToList();
            if (planets.Count == 0)
                return 0;

            int strongestCluster = planets.Max(planet =>
                planet.GetTotalBuildingTypeCount(buildingType)
            );
            return strongestCluster == 0 ? 1 : Math.Max(0, hubTarget - strongestCluster);
        }

        /// <summary>
        /// Counts Outer Rim systems that still lack construction capacity.
        /// </summary>
        /// <param name="sectors">Owned planets grouped by system.</param>
        /// <returns>The number of unseeded Outer Rim systems.</returns>
        private static int CountUnseededOuterRimSectors(
            IEnumerable<IGrouping<string, Planet>> sectors
        )
        {
            return sectors.Count(sector =>
                sector.FirstOrDefault()?.GetParentOfType<PlanetSector>()?.SectorType
                    == PlanetSectorType.OuterRim
                && sector.Sum(planet =>
                    planet.GetTotalBuildingTypeCount(BuildingType.ConstructionFacility)
                ) == 0
            );
        }

        /// <summary>
        /// Returns the normalized need for founding construction capacity in an Outer Rim sector.
        /// </summary>
        /// <param name="sector">Owned planets in one sector.</param>
        /// <param name="buildingType">The facility category being considered.</param>
        /// <returns>One before the first construction yard and zero otherwise.</returns>
        private static double GetColonyFoundationInput(
            IEnumerable<Planet> sector,
            BuildingType buildingType
        )
        {
            if (buildingType != BuildingType.ConstructionFacility)
                return 0;

            List<Planet> planets = sector.ToList();
            if (
                planets.Count == 0
                || planets[0].GetParentOfType<PlanetSector>()?.SectorType
                    != PlanetSectorType.OuterRim
            )
            {
                return 0;
            }

            int constructionFacilityCount = planets.Sum(planet =>
                planet.GetTotalBuildingTypeCount(BuildingType.ConstructionFacility)
            );
            return 1 - AIUtility.Fulfillment(constructionFacilityCount, 1);
        }

        /// <summary>
        /// Returns pressure that keeps production-facility categories advancing at comparable
        /// rates while their sector hubs are established.
        /// </summary>
        /// <param name="sectors">Owned planets grouped by system.</param>
        /// <param name="buildingType">The production facility category.</param>
        /// <param name="hubTarget">The desired facility count at each primary site.</param>
        /// <param name="consideration">The category-balance utility consideration.</param>
        /// <returns>The category balance pressure.</returns>
        private static double GetFacilityCategoryBalancePressure(
            IReadOnlyCollection<IGrouping<string, Planet>> sectors,
            BuildingType buildingType,
            int hubTarget,
            GameConfig.AIConsiderationConfig consideration
        )
        {
            if (sectors.Count == 0 || hubTarget <= 0)
                return 0;

            int completedHubProgress = sectors.Sum(sector =>
                Math.Min(
                    hubTarget,
                    sector.Max(planet => planet.GetTotalBuildingTypeCount(buildingType))
                )
            );
            double targetHubProgress = sectors.Count * (double)hubTarget;
            double completion = completedHubProgress / targetHubProgress;
            return AIUtility.EvaluateCenteredPressure(1 - completion, consideration);
        }

        /// <summary>
        /// Adds one production-facility demand toward a sector hub or established local cluster.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="primaryDemand">The production demand served by the facility.</param>
        /// <param name="kind">The facility demand kind.</param>
        /// <param name="buildingType">The required facility type.</param>
        /// <param name="target">The destination planet.</param>
        /// <param name="targetCount">The desired facility count at the destination.</param>
        /// <param name="baseDemandPercent">The base demand pressure.</param>
        /// <param name="strategicBonus">Additional pressure for the site's strategic role.</param>
        /// <param name="facilityPortfolio">The faction's current strategic-facility mix.</param>
        /// <param name="maximumQuantity">The remaining faction-wide capacity deficit.</param>
        /// <param name="demandId">The shared identifier for alternative destinations.</param>
        /// <param name="placementUtility">The normalized utility of this destination.</param>
        /// <returns>True when an alternative demand was added.</returns>
        private bool AddSectorFacilityDemand(
            AITurnContext context,
            List<AIProductionRequirement> demands,
            AIProductionRequirement primaryDemand,
            AIProductionRequirementKind kind,
            BuildingType buildingType,
            Planet target,
            int targetCount,
            int baseDemandPercent,
            double strategicBonus,
            FacilityPortfolio facilityPortfolio,
            int maximumQuantity,
            string demandId,
            double placementUtility
        )
        {
            if (target == null || target.GetAvailableEnergy() <= 0 || targetCount <= 0)
                return false;

            int currentCount = target.GetTotalBuildingTypeCount(buildingType);
            if (currentCount >= targetCount)
                return false;

            int quantity = Math.Min(
                Math.Min(targetCount - currentCount, target.GetAvailableEnergy()),
                maximumQuantity
            );
            if (quantity <= 0)
                return false;

            double concentrationBonus = baseDemandPercent * currentCount / targetCount;
            double investmentDeficit = (double)(targetCount - currentCount) / targetCount;

            demands.Add(
                new AIProductionRequirement(
                    demandId,
                    kind,
                    ManufacturingType.Building,
                    buildingType,
                    target,
                    quantity,
                    GetProductionFacilityPressure(
                        context,
                        kind,
                        currentCount,
                        targetCount,
                        baseDemandPercent,
                        investmentDeficit,
                        facilityPortfolio
                    )
                        + strategicBonus
                        + baseDemandPercent * placementUtility
                        + concentrationBonus,
                    primaryDemand?.ProductTypeId,
                    primaryDemand?.CapitalShipRole ?? AICapitalShipProductionRole.None
                )
            );
            return true;
        }

        /// <summary>
        /// Returns expansion pressure for a production facility.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">The facility demand kind.</param>
        /// <param name="currentCount">The number of currently owned facilities.</param>
        /// <param name="desiredCount">The minimum strategic facility count.</param>
        /// <param name="baseDemandPercent">The base demand pressure.</param>
        /// <param name="investmentDeficit">The remaining construction-capacity deficit.</param>
        /// <param name="facilityPortfolio">The turn-scoped facility portfolio.</param>
        /// <returns>The adjusted pressure.</returns>
        private double GetProductionFacilityPressure(
            AITurnContext context,
            AIProductionRequirementKind kind,
            int currentCount,
            int desiredCount,
            int baseDemandPercent,
            double investmentDeficit,
            FacilityPortfolio facilityPortfolio
        )
        {
            int targetCount = Math.Max(currentCount + 1, desiredCount);
            int deficit = Math.Max(1, targetCount - currentCount);
            double pressure =
                baseDemandPercent
                + AIUtility.EvaluatePressure(
                    deficit / (double)targetCount,
                    context.Game.Config.AI.Infrastructure.DemandUtility.Deficit
                );
            if (kind == AIProductionRequirementKind.TrainingFacility)
                pressure += AIUtility.EvaluatePressure(
                    1,
                    context.Game.Config.AI.Infrastructure.DemandUtility.TrainingBacklog
                );

            if (kind == AIProductionRequirementKind.ConstructionFacility)
                pressure += AIUtility.EvaluatePressure(
                    investmentDeficit,
                    context.Game.Config.AI.Infrastructure.DemandUtility.FacilityInvestment
                );

            pressure += GetPortfolioPressure(context, kind, facilityPortfolio);

            return pressure;
        }

        /// <summary>
        /// Returns whether a matching facility is already under construction.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="target">The prospective facility destination.</param>
        /// <param name="buildingType">The facility type.</param>
        /// <returns>True when construction is pending.</returns>
        private bool HasPendingFacility(
            AITurnContext context,
            Planet target,
            BuildingType buildingType
        )
        {
            return context
                .Assessment.GetPlanetBuildings(target)
                .Any(building =>
                    building.GetOwnerInstanceID() == context.Faction.InstanceID
                    && building.GetBuildingType() == buildingType
                    && (
                        building.GetManufacturingStatus() != ManufacturingStatus.Complete
                        || building.Movement != null
                    )
                );
        }

        /// <summary>
        /// Returns energy available for another production facility.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The available energy.</returns>
        private static int GetAvailableFacilityExpansionEnergy(Planet planet)
        {
            return planet?.GetAvailableEnergy() ?? 0;
        }

        /// <summary>
        /// Resolves the destination planet for a production requirement.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="requirement">The production requirement.</param>
        /// <returns>The destination planet, or null.</returns>
        private static Planet GetDemandPlanet(
            AITurnContext context,
            AIProductionRequirement requirement
        )
        {
            return requirement?.DestinationPlanet
                ?? context.Assessment.GetFleetPlanet(requirement?.DestinationFleet);
        }

        /// <summary>
        /// Returns the current and queued count for one facility type.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="buildingType">The facility type.</param>
        /// <returns>The owned facility count.</returns>
        private static int GetOwnedFacilityCount(AITurnContext context, BuildingType buildingType)
        {
            return context.Assessment.OwnedPlanets.Sum(planet =>
                planet.GetTotalBuildingTypeCount(buildingType)
            );
        }
    }
}
