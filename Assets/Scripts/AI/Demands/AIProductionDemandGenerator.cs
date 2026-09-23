using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Scorers;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Systems;
using Rebellion.Util.Mathematics;

namespace Rebellion.AI.Demands
{
    /// <summary>
    /// Generates production demands from assessed faction needs.
    /// </summary>
    internal sealed class AIProductionDemandGenerator : IAIDemandGenerator
    {
        /// <summary>
        /// Adds production-facility expansion demands.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        internal void AddProductionFacilityDemands(
            AITurnContext context,
            List<AIProductionDemand> demands
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            AddProductionFacilityDemand(
                context,
                demands,
                ManufacturingType.Ship,
                AIProductionDemandKind.Shipyard,
                BuildingType.Shipyard,
                config.ShipyardDemandPercent
            );
            AddProductionFacilityDemand(
                context,
                demands,
                ManufacturingType.Building,
                AIProductionDemandKind.ConstructionFacility,
                BuildingType.ConstructionFacility,
                config.ConstructionFacilityDemandPercent
            );
            AddProductionFacilityDemand(
                context,
                demands,
                ManufacturingType.Troop,
                AIProductionDemandKind.TrainingFacility,
                BuildingType.TrainingFacility,
                config.TrainingFacilityDemandPercent
            );
        }

        /// <summary>
        /// Adds available production-facility upgrade demands.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        internal void AddProductionFacilityUpgradeDemands(
            AITurnContext context,
            List<AIProductionDemand> demands
        )
        {
            foreach (
                Planet planet in context
                    .Assessment.OwnedPlanets.Where(planet =>
                        planet?.IsColonized == true && !planet.IsDestroyed
                    )
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
            List<AIProductionDemand> demands,
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

            AIProductionDemand demand = new AIProductionDemand(
                AIProductionDemand.CreateId(
                    context.Faction.InstanceID,
                    AIProductionDemandKind.BuildingUpgrade,
                    buildingType,
                    planet.InstanceID,
                    replacement.InstanceID
                ),
                AIProductionDemandKind.BuildingUpgrade,
                ManufacturingType.Building,
                buildingType,
                planet,
                1,
                buildingToReplace: replacement,
                targetCount: 1,
                baseDemandPercent: context
                    .Game
                    .Config
                    .AI
                    .Infrastructure
                    .ProductionFacilityUpgradeDemandPercent
            );
            demands.Add(demand);
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
        private void AddProductionFacilityDemand(
            AITurnContext context,
            List<AIProductionDemand> demands,
            ManufacturingType manufacturingType,
            AIProductionDemandKind kind,
            BuildingType buildingType,
            int baseDemandPercent
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int productionDemandCount = demands.Count(demand =>
                demand.ManufacturingType == manufacturingType && demand.Kind != kind
            );
            int desiredFacilityCount = context.StrategicPlan.GetInfrastructureTarget(buildingType);
            if (buildingType == BuildingType.TrainingFacility)
            {
                int demandCapacityTarget = IntegerMath.DivideRoundedUp(
                    productionDemandCount,
                    Math.Max(1, config.TrainingDemandsPerFacility)
                );
                desiredFacilityCount = Math.Max(desiredFacilityCount, demandCapacityTarget);
            }
            List<IGrouping<string, Planet>> sectors = context
                .Assessment.OwnedPlanets.Where(planet =>
                    planet?.IsColonized == true && !planet.IsDestroyed
                    || (
                        buildingType == BuildingType.ConstructionFacility
                        && planet?.IsDestroyed == false
                        && planet.GetParentOfType<PlanetSector>()?.SectorType
                            == PlanetSectorType.OuterRim
                    )
                )
                .GroupBy(context.Assessment.GetPlanetSystemId)
                .ToList();
            int remainingFacilityCount = Math.Max(
                0,
                desiredFacilityCount - context.StrategicPlan.GetInfrastructureCount(buildingType)
            );
            if (buildingType == BuildingType.ConstructionFacility)
                remainingFacilityCount = Math.Max(
                    remainingFacilityCount,
                    CountUnseededOuterRimSectors(sectors)
                );
            if (remainingFacilityCount == 0)
                return;

            demands.Add(
                new AIProductionDemand(
                    AIProductionDemand.CreateId(context.Faction.InstanceID, kind, "capacity"),
                    kind,
                    ManufacturingType.Building,
                    buildingType,
                    null,
                    remainingFacilityCount,
                    targetCount: desiredFacilityCount,
                    baseDemandPercent: baseDemandPercent,
                    deficitCount: remainingFacilityCount
                )
            );
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
        /// Adds planetary starfighter requirements in strategic priority order.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="requirements">The requirement collection to update.</param>
        internal void AddPlanetaryStarfighterDemands(
            AITurnContext context,
            ICollection<AIProductionDemand> requirements
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
                    new AIProductionDemand(
                        AIProductionDemand.CreateId(
                            context.Faction.InstanceID,
                            AIProductionDemandKind.PlanetaryStarfighterReserve,
                            planet.InstanceID
                        ),
                        AIProductionDemandKind.PlanetaryStarfighterReserve,
                        ManufacturingType.Ship,
                        BuildingType.None,
                        planet,
                        deficit,
                        targetCount: targetCount,
                        baseDemandPercent: config.PlanetaryStarfighterDemandPercent
                    )
                );
            }
        }

        /// <summary>
        /// Adds static-defense requirements for owned planets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="requirements">The requirement collection to update.</param>
        internal void AddPlanetaryDefenseDemands(
            AITurnContext context,
            ICollection<AIProductionDemand> requirements
        )
        {
            foreach (
                Planet planet in context
                    .Assessment.OwnedPlanets.Where(IsUsablePlanet)
                    .OrderByDescending(context.Assessment.GetPlanetValue)
                    .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
            )
                AddPlanetaryDefenseDemands(context, requirements, planet);
        }

        /// <summary>
        /// Adds static-defense requirements for one planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="requirements">The requirement collection to update.</param>
        /// <param name="planet">The planet to inspect.</param>
        private void AddPlanetaryDefenseDemands(
            AITurnContext context,
            ICollection<AIProductionDemand> requirements,
            Planet planet
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
                    CreateDefenseDemand(
                        context,
                        planet,
                        BuildingType.Defense,
                        shieldQuantity,
                        shieldTarget,
                        config.PlanetaryShieldDemandPercent,
                        shieldCount == 0
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
                CreateDefenseDemand(
                    context,
                    planet,
                    BuildingType.Weapon,
                    Math.Min(weaponDeficit, availableEnergy),
                    weaponTarget,
                    config.PlanetaryWeaponDemandPercent,
                    false
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
        /// <returns>The production requirement.</returns>
        private static AIProductionDemand CreateDefenseDemand(
            AITurnContext context,
            Planet planet,
            BuildingType buildingType,
            int deficit,
            int targetCount,
            int basePercent,
            bool isInitialShield
        )
        {
            return new AIProductionDemand(
                AIProductionDemand.CreateId(
                    context.Faction.InstanceID,
                    AIProductionDemandKind.PlanetaryDefense,
                    buildingType,
                    planet.InstanceID
                ),
                AIProductionDemandKind.PlanetaryDefense,
                ManufacturingType.Building,
                buildingType,
                planet,
                deficit,
                targetCount: targetCount,
                baseDemandPercent: basePercent,
                establishesInitialShield: isInitialShield
            );
        }

        /// <summary>
        /// Adds planetary garrison-regiment requirements.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="requirements">The requirement collection to update.</param>
        internal void AddGarrisonDemands(
            AITurnContext context,
            ICollection<AIProductionDemand> requirements
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
                    new AIProductionDemand(
                        AIProductionDemand.CreateId(
                            context.Faction.InstanceID,
                            AIProductionDemandKind.GarrisonRegimentReserve,
                            planet.InstanceID
                        ),
                        AIProductionDemandKind.GarrisonRegimentReserve,
                        ManufacturingType.Troop,
                        BuildingType.None,
                        planet,
                        deficit,
                        targetCount: targetCount,
                        baseDemandPercent: context
                            .Game
                            .Config
                            .AI
                            .Infrastructure
                            .PlanetaryGarrisonDemandPercent
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
        /// Returns whether a planet can receive defensive production.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True when the planet is an intact colony.</returns>
        private static bool IsUsablePlanet(Planet planet)
        {
            return planet?.IsColonized == true && !planet.IsDestroyed;
        }

        /// <summary>
        /// Adds local fighter work for otherwise idle shipyards.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="requirements">The requirement collection to update.</param>
        internal void AddIdleShipyardDemands(
            AITurnContext context,
            ICollection<AIProductionDemand> requirements
        )
        {
            HashSet<string> plannedPlanets = requirements
                .Where(requirement =>
                    requirement.Kind == AIProductionDemandKind.PlanetaryStarfighterReserve
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
                    new AIProductionDemand(
                        AIProductionDemand.CreateId(
                            context.Faction.InstanceID,
                            AIProductionDemandKind.PlanetaryStarfighterReserve,
                            "idle-shipyard",
                            planet.InstanceID
                        ),
                        AIProductionDemandKind.PlanetaryStarfighterReserve,
                        ManufacturingType.Ship,
                        BuildingType.None,
                        planet,
                        1,
                        targetCount: targetCount,
                        baseDemandPercent: context
                            .Game
                            .Config
                            .AI
                            .Infrastructure
                            .IdleShipyardFighterDemandPercent
                    )
                );
            }
        }

        /// <summary>
        /// Generates production demands for the current AI turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public void Generate(AITurnContext context)
        {
            if (context == null)
                return;

            context.SetProductionDemands(BuildDemands(context));
        }

        /// <summary>
        /// Builds production requirements for the current AI turn in strategic priority order.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Production requirements generated for this faction.</returns>
        public List<AIProductionDemand> BuildDemands(AITurnContext context)
        {
            List<AIProductionDemand> requirements = new List<AIProductionDemand>();

            if (context?.Game == null || context.Faction == null || context.Assessment == null)
                return requirements;

            AddColonyDemands(context, requirements);
            AddResourceDemands(context, requirements);
            AddPlanetaryDefenseDemands(context, requirements);
            AddPlanetaryStarfighterDemands(context, requirements);
            AddFleetSeedDemands(context, requirements);
            AddColonizationFleetSeedDemands(context, requirements);
            AddFleetReinforcementDemands(context, requirements);
            AddGarrisonDemands(context, requirements);
            AddSpecialForcesDemands(context, requirements);
            AddProductionFacilityDemands(context, requirements);
            AddProductionFacilityUpgradeDemands(context, requirements);
            AddIdleShipyardDemands(context, requirements);

            return requirements;
        }

        /// <summary>
        /// Adds colony requirements to the production plan.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="requirements">The requirement collection to update.</param>
        private void AddColonyDemands(
            AITurnContext context,
            ICollection<AIProductionDemand> requirements
        )
        {
            int plannedMines = context.Faction.GetTotalRawMinedResources();
            int plannedRefineries = context.Faction.GetTotalRawRefinementCapacity();
            foreach (
                Planet planet in context
                    .Assessment.OwnedPlanets.Where(planet =>
                        RequiresFoundingFacility(context, planet)
                    )
                    .OrderByDescending(planet => planet.GetUnminedResourceNodeCount())
                    .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
            )
            {
                BuildingType buildingType = SelectInitialColonyBuildingType(
                    planet,
                    plannedMines,
                    plannedRefineries
                );
                requirements.Add(
                    new AIProductionDemand(
                        AIProductionDemand.CreateId(
                            context.Faction.InstanceID,
                            AIProductionDemandKind.Colony,
                            planet.InstanceID
                        ),
                        AIProductionDemandKind.Colony,
                        ManufacturingType.Building,
                        buildingType,
                        planet,
                        1,
                        targetCount: 1,
                        baseDemandPercent: context
                            .Game
                            .Config
                            .AI
                            .Infrastructure
                            .EconomySevereDemandPercent
                    )
                );

                if (buildingType == BuildingType.Mine)
                    plannedMines++;
                else
                    plannedRefineries++;
            }
        }

        /// <summary>
        /// Selects a colony's founding facility without worsening the faction's resource balance.
        /// </summary>
        /// <param name="planet">Planet receiving its first economic facility.</param>
        /// <param name="plannedMines">Current and queued mine output.</param>
        /// <param name="plannedRefineries">Current and queued refinery capacity.</param>
        /// <returns>The preferred founding facility type.</returns>
        private static BuildingType SelectInitialColonyBuildingType(
            Planet planet,
            int plannedMines,
            int plannedRefineries
        )
        {
            return planet.GetUnminedResourceNodeCount() > 0 && plannedMines <= plannedRefineries
                ? BuildingType.Mine
                : BuildingType.Refinery;
        }

        /// <summary>
        /// Returns whether a claimed planet still needs its first economic facility.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True when the planet has a settled regiment but no colony infrastructure.</returns>
        private static bool RequiresFoundingFacility(AITurnContext context, Planet planet)
        {
            return planet?.IsColonized == false
                && !planet.IsDestroyed
                && planet.GetAvailableEnergy() > 0
                && context
                    .Assessment.GetPlanetRegiments(planet)
                    .Any(regiment =>
                        regiment.ManufacturingStatus == ManufacturingStatus.Complete
                        && regiment.Movement == null
                        && regiment.GetOwnerInstanceID() == planet.GetOwnerInstanceID()
                    );
        }

        /// <summary>
        /// Adds mine and refinery requirements needed to restore resource balance.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="requirements">The requirement collection to update.</param>
        private void AddResourceDemands(
            AITurnContext context,
            ICollection<AIProductionDemand> requirements
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            if (!NeedsExpansion(context))
                return;

            int batchSize = GetBatchSize(context, config);
            int rawResourceNodes = context.Faction.GetTotalRawResourceNodes();
            int plannedMines = context.Faction.GetTotalRawMinedResources();
            int plannedRefineries = context.Faction.GetTotalRawRefinementCapacity();
            int mineDeficit = GetMineDeficit(
                rawResourceNodes,
                plannedMines,
                plannedRefineries,
                batchSize
            );
            int refineryDeficit = GetRefineryDeficit(
                plannedMines,
                plannedRefineries,
                mineDeficit,
                batchSize
            );
            int demandPercent = GetDemandPercent(rawResourceNodes, plannedMines, config);
            List<Planet> mineTargets = FindMineTargets(context, mineDeficit).ToList();
            HashSet<string> mineTargetIds = new HashSet<string>(
                mineTargets.Select(planet => planet.InstanceID),
                StringComparer.Ordinal
            );
            List<Planet> refineryTargets = FindRefineryTargets(
                    context,
                    refineryDeficit,
                    mineTargetIds
                )
                .ToList();

            foreach (Planet target in mineTargets)
            {
                requirements.Add(
                    CreateBuildingDemand(
                        context,
                        AIProductionDemandKind.Mine,
                        BuildingType.Mine,
                        target,
                        plannedMines + mineDeficit,
                        demandPercent
                    )
                );
            }

            foreach (Planet target in refineryTargets)
            {
                requirements.Add(
                    CreateBuildingDemand(
                        context,
                        AIProductionDemandKind.Refinery,
                        BuildingType.Refinery,
                        target,
                        plannedRefineries + refineryDeficit,
                        demandPercent
                    )
                );
            }
        }

        /// <summary>
        /// Returns whether resource production currently constrains the faction.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when economy expansion is required.</returns>
        private static bool NeedsExpansion(AITurnContext context)
        {
            return context.Assessment.PendingRawMaterialRequestCount > 0
                || context.Assessment.PendingRefinedMaterialRequestCount > 0
                || GetProjectedRefinedMaterialPercent(context)
                    <= context.Game.Config.AI.Selection.RefinedMaterialEconomyWarningPercent
                || context.Assessment.ProjectedEconomyMaintenanceHeadroom
                    < context.Game.Config.AI.Selection.MaintenanceHeadroomTarget;
        }

        /// <summary>
        /// Returns projected uncommitted refined materials as a percentage of supply.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The projected refined-material percentage.</returns>
        private static int GetProjectedRefinedMaterialPercent(AITurnContext context)
        {
            long projectedStockpile = Math.Max(
                0,
                (long)context.Assessment.RefinedMaterialStockpile
                    - context.Assessment.NearTermRefinedMaterialCommitment
            );
            int supply = context.Assessment.RefinedMaterialSupply;
            if (supply <= 0)
                return projectedStockpile > 0 ? 100 : 0;
            return (int)Math.Min(100, projectedStockpile * 100 / supply);
        }

        /// <summary>
        /// Returns the number of missing mines to request this turn.
        /// </summary>
        /// <param name="rawResourceNodes">Known raw resource nodes.</param>
        /// <param name="plannedMines">Current and queued mine capacity.</param>
        /// <param name="plannedRefineries">Current and queued refinery capacity.</param>
        /// <param name="batchSize">Maximum economy batch size.</param>
        /// <returns>The mine deficit.</returns>
        private static int GetMineDeficit(
            int rawResourceNodes,
            int plannedMines,
            int plannedRefineries,
            int batchSize
        )
        {
            if (rawResourceNodes <= plannedMines)
                return 0;
            if (plannedRefineries > plannedMines)
            {
                return Math.Min(
                    batchSize,
                    Math.Min(plannedRefineries - plannedMines, rawResourceNodes - plannedMines)
                );
            }
            return plannedRefineries == plannedMines
                ? Math.Min(batchSize, rawResourceNodes - plannedMines)
                : 0;
        }

        /// <summary>
        /// Returns the number of missing refineries to request this turn.
        /// </summary>
        /// <param name="plannedMines">Current and queued mine capacity.</param>
        /// <param name="plannedRefineries">Current and queued refinery capacity.</param>
        /// <param name="mineDeficit">Mine requirements selected for this pass.</param>
        /// <param name="batchSize">Maximum economy batch size.</param>
        /// <returns>The refinery deficit.</returns>
        private static int GetRefineryDeficit(
            int plannedMines,
            int plannedRefineries,
            int mineDeficit,
            int batchSize
        )
        {
            return Math.Min(batchSize, Math.Max(0, plannedMines + mineDeficit - plannedRefineries));
        }

        /// <summary>
        /// Returns the configured economy pressure for current mine coverage.
        /// </summary>
        /// <param name="rawResourceNodes">Known raw resource nodes.</param>
        /// <param name="plannedMines">Current and queued mine capacity.</param>
        /// <param name="config">AI infrastructure configuration.</param>
        /// <returns>The base economy demand percent.</returns>
        private static int GetDemandPercent(
            int rawResourceNodes,
            int plannedMines,
            GameConfig.AIInfrastructureConfig config
        )
        {
            if (rawResourceNodes <= 0)
                return config.EconomyDemandPercent;
            return plannedMines * 100 / rawResourceNodes <= config.EconomySevereDeficitPercent
                ? config.EconomySevereDemandPercent
                : config.EconomyDemandPercent;
        }

        /// <summary>
        /// Returns the economy requirement batch size for available construction lanes.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="config">AI infrastructure configuration.</param>
        /// <returns>The economy batch size.</returns>
        private static int GetBatchSize(
            AITurnContext context,
            GameConfig.AIInfrastructureConfig config
        )
        {
            int availableLanes = context.Assessment.GetAvailableProductionLaneCount(
                ManufacturingType.Building
            );
            return Math.Max(
                config.EconomyDefaultBatchSize,
                Math.Max(0, availableLanes - config.EconomyCompetingNeedSlotReserve)
            );
        }

        /// <summary>
        /// Creates one economy building requirement.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">The economy requirement kind.</param>
        /// <param name="buildingType">The required building type.</param>
        /// <param name="target">The destination planet.</param>
        /// <param name="targetCount">The projected faction-wide target count.</param>
        /// <param name="baseDemandPercent">The base economy pressure.</param>
        /// <returns>The production requirement.</returns>
        private static AIProductionDemand CreateBuildingDemand(
            AITurnContext context,
            AIProductionDemandKind kind,
            BuildingType buildingType,
            Planet target,
            int targetCount,
            int baseDemandPercent
        )
        {
            return new AIProductionDemand(
                AIProductionDemand.CreateId(context.Faction.InstanceID, kind, target.InstanceID),
                kind,
                ManufacturingType.Building,
                buildingType,
                target,
                1,
                targetCount: targetCount,
                baseDemandPercent: baseDemandPercent
            );
        }

        /// <summary>
        /// Returns candidate planets for new mines.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="count">Maximum number of planets.</param>
        /// <returns>Ranked mine destinations.</returns>
        private static IEnumerable<Planet> FindMineTargets(AITurnContext context, int count)
        {
            return count <= 0
                ? Enumerable.Empty<Planet>()
                : GetBuildingDestinations(context)
                    .Where(planet => planet.GetUnminedResourceNodeCount() > 0)
                    .OrderByDescending(planet => planet.GetUnminedResourceNodeCount())
                    .ThenByDescending(planet => planet.GetAvailableEnergy())
                    .ThenBy(planet => planet.InstanceID)
                    .Take(count);
        }

        /// <summary>
        /// Returns candidate planets for new refineries.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="count">Maximum number of planets.</param>
        /// <param name="excludedPlanetIds">Planets already selected for mines.</param>
        /// <returns>Ranked refinery destinations.</returns>
        private static IEnumerable<Planet> FindRefineryTargets(
            AITurnContext context,
            int count,
            HashSet<string> excludedPlanetIds
        )
        {
            if (count <= 0)
                return Enumerable.Empty<Planet>();

            List<Planet> targets = GetBuildingDestinations(context)
                .Where(planet => !excludedPlanetIds.Contains(planet.InstanceID))
                .OrderBy(planet => planet.GetTotalBuildingTypeCount(BuildingType.Refinery))
                .ThenByDescending(planet => planet.GetAvailableEnergy())
                .ThenBy(planet => planet.InstanceID)
                .Take(count)
                .ToList();
            if (targets.Count >= count)
                return targets;

            targets.AddRange(
                GetBuildingDestinations(context)
                    .Where(planet => excludedPlanetIds.Contains(planet.InstanceID))
                    .OrderBy(planet => planet.GetTotalBuildingTypeCount(BuildingType.Refinery))
                    .ThenByDescending(planet => planet.GetAvailableEnergy())
                    .ThenBy(planet => planet.InstanceID)
                    .Take(count - targets.Count)
            );
            return targets;
        }

        /// <summary>
        /// Returns owned planets that can receive an economy building.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Eligible building destinations.</returns>
        private static IEnumerable<Planet> GetBuildingDestinations(AITurnContext context)
        {
            return context.Assessment.OwnedPlanets.Where(planet =>
                planet?.IsDestroyed == false && planet.GetAvailableEnergy() > 0
            );
        }

        /// <summary>
        /// Adds demands that establish missing battle fleets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddFleetSeedDemands(AITurnContext context, List<AIProductionDemand> demands)
        {
            int targetCount = context.StrategicPlan.TargetBattleFleetCount;
            int committedCount = context.Assessment.OwnedFleets.Count(IsCommittedBattleFleet);
            int deficit = targetCount - committedCount;
            Planet unguardedHeadquarters = FindUnguardedHeadquarters(context);
            if (deficit <= 0 && unguardedHeadquarters == null)
                return;

            Planet destination = unguardedHeadquarters ?? FindFleetAssemblyPlanet(context);
            if (destination == null)
                return;

            int quantityNeeded = Math.Max(1, deficit);

            demands.Add(
                new AIProductionDemand(
                    AIProductionDemand.CreateId(
                        context.Faction.InstanceID,
                        AIProductionDemandKind.FleetSeedCapitalShip
                    ),
                    AIProductionDemandKind.FleetSeedCapitalShip,
                    ManufacturingType.Ship,
                    BuildingType.None,
                    destination,
                    quantityNeeded,
                    capitalShipRole: AICapitalShipProductionRole.General,
                    targetCount: Math.Max(1, targetCount),
                    baseDemandPercent: context
                        .Game
                        .Config
                        .AI
                        .Infrastructure
                        .FleetSeedCapitalShipDemandPercent
                )
            );
        }

        /// <summary>
        /// Adds demand for a dedicated colonization fleet while settlement opportunities remain.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddColonizationFleetSeedDemands(
            AITurnContext context,
            List<AIProductionDemand> demands
        )
        {
            int targetCount = Math.Max(
                0,
                context.Game.Config.AI.FleetDeployment.ColonizationFleetTargetCount
            );
            int committedCount = context.Assessment.OwnedFleets.Count(fleet =>
                fleet.RoleType == FleetRoleType.Colonization
            );
            int deficit = targetCount - committedCount;
            if (!HasColonizationOpportunity(context) || deficit <= 0)
                return;

            Planet destination = FindFleetAssemblyPlanet(context);
            if (destination == null)
                return;

            demands.Add(
                new AIProductionDemand(
                    AIProductionDemand.CreateId(
                        context.Faction.InstanceID,
                        AIProductionDemandKind.ColonizationFleetSeedCapitalShip
                    ),
                    AIProductionDemandKind.ColonizationFleetSeedCapitalShip,
                    ManufacturingType.Ship,
                    BuildingType.None,
                    destination,
                    deficit,
                    capitalShipRole: AICapitalShipProductionRole.TroopTransport,
                    targetCount: targetCount,
                    baseDemandPercent: context
                        .Game
                        .Config
                        .AI
                        .Infrastructure
                        .ColonizationFleetDemandPercent
                )
            );
        }

        /// <summary>
        /// Returns whether known unsettled territory or unexplored Outer Rim territory remains.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when a colonization fleet has useful work available.</returns>
        private static bool HasColonizationOpportunity(AITurnContext context)
        {
            return context.Assessment.KnownUncolonizedPlanets.Count > 0
                || context.Assessment.UnexploredPlanets.Any(planet =>
                    planet.GetParentOfType<PlanetSector>()?.SectorType == PlanetSectorType.OuterRim
                );
        }

        /// <summary>
        /// Finds the highest-priority headquarters lacking a defense fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The unguarded headquarters, or null.</returns>
        private Planet FindUnguardedHeadquarters(AITurnContext context)
        {
            return context
                .Assessment.OwnedPlanets.Where(planet =>
                    context.Assessment.IsFactionHeadquarters(planet)
                    && planet.IsColonized
                    && !planet.IsDestroyed
                    && !context.Assessment.HasCommittedHeadquartersFleet(planet)
                )
                .OrderByDescending(context.Assessment.GetPlanetValue)
                .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        /// Finds the preferred planet for assembling a new fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The assembly planet, or null.</returns>
        private Planet FindFleetAssemblyPlanet(AITurnContext context)
        {
            return context
                .Assessment.OwnedPlanets.Where(planet => planet.IsColonized && !planet.IsDestroyed)
                .OrderByDescending(context.Assessment.IsFactionHeadquarters)
                .ThenByDescending(planet =>
                    context.Assessment.GetPlanetProductionRate(planet, ManufacturingType.Ship)
                )
                .ThenByDescending(context.Assessment.GetPlanetValue)
                .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns whether a fleet counts toward the battle-fleet target.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>True when the fleet is committed.</returns>
        private static bool IsCommittedBattleFleet(Fleet fleet)
        {
            return fleet?.RoleType == FleetRoleType.Battle
                && fleet.GetChildren<CapitalShip>().Count > 0;
        }

        /// <summary>
        /// Adds reinforcement demand for owned fleets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddFleetReinforcementDemands(
            AITurnContext context,
            List<AIProductionDemand> demands
        )
        {
            Fleet defenseFleet = GetPriorityDefenseFleet(context);
            AddFleetDemands(context, demands, defenseFleet);

            IReadOnlyList<Fleet> attackFleets = GetPriorityAttackFleets(context);
            AddAttackShipDemands(context, demands, attackFleets);
            AddPriorityAttackRegimentDemand(context, demands, attackFleets);

            foreach (Fleet colonizationFleet in GetPriorityColonizationFleets(context))
                AddFleetDemands(context, demands, colonizationFleet);

            AddFleetDemands(context, demands, GetFleetAssemblyFleets(context).FirstOrDefault());
        }

        /// <summary>
        /// Adds every applicable reinforcement demand for one fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="fleet">The fleet to reinforce.</param>
        private void AddFleetDemands(
            AITurnContext context,
            List<AIProductionDemand> demands,
            Fleet fleet
        )
        {
            if (fleet == null)
                return;

            AddFleetCapitalShipDemand(context, demands, fleet);
            AddFleetStarfighterDemand(context, demands, fleet);
            AddFleetRegimentDemand(context, demands, fleet);
        }

        /// <summary>
        /// Adds ship demands for attack fleets in reinforcement priority order.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="fleets">Attack fleets in reinforcement priority order.</param>
        private void AddAttackShipDemands(
            AITurnContext context,
            List<AIProductionDemand> demands,
            IReadOnlyList<Fleet> fleets
        )
        {
            foreach (Fleet fleet in fleets)
            {
                AddFleetCapitalShipDemand(context, demands, fleet);
                AddFleetStarfighterDemand(context, demands, fleet);
            }
        }

        /// <summary>
        /// Adds regiment demand for the highest-priority attack fleet that still needs troops.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="fleets">Attack fleets in reinforcement priority order.</param>
        private void AddPriorityAttackRegimentDemand(
            AITurnContext context,
            List<AIProductionDemand> demands,
            IReadOnlyList<Fleet> fleets
        )
        {
            foreach (Fleet fleet in fleets)
            {
                int initialCount = demands.Count;
                AddFleetRegimentDemand(context, demands, fleet);
                if (demands.Count > initialCount)
                    return;
            }
        }

        /// <summary>
        /// Returns the defense fleet with the greatest reinforcement need.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The priority defense fleet, or null.</returns>
        private Fleet GetPriorityDefenseFleet(AITurnContext context)
        {
            return context
                .Assessment.OwnedFleets.Where(CanReinforceFleet)
                .Select(fleet => new { Fleet = fleet, Target = GetDefenseTarget(context, fleet) })
                .Where(candidate => candidate.Target != null)
                .OrderByDescending(candidate =>
                    AIFleetProductionAllocationScorer.ScoreDefenseNeed(
                        context,
                        candidate.Target,
                        context.Assessment.GetProjectedFleetCombatValue(candidate.Fleet)
                    )
                )
                .ThenBy(candidate => candidate.Fleet.InstanceID, StringComparer.Ordinal)
                .Select(candidate => candidate.Fleet)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns active attack fleets ordered by proximity to campaign readiness.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The ordered attack fleets that can receive reinforcements.</returns>
        private IReadOnlyList<Fleet> GetPriorityAttackFleets(AITurnContext context)
        {
            return context
                .Assessment.AttackOrderedFleets.Where(CanReinforceFleet)
                .Select(fleet => new
                {
                    Fleet = fleet,
                    Target = GetAttackTargetPlanet(context, fleet),
                })
                .Where(candidate => candidate.Target != null)
                .OrderByDescending(candidate =>
                    AIFleetProductionAllocationScorer.ScoreAttack(
                        context,
                        candidate.Fleet,
                        candidate.Target,
                        GetProjectedAttackReadiness(context, candidate.Fleet, candidate.Target)
                    )
                )
                .ThenBy(candidate => candidate.Fleet.InstanceID, StringComparer.Ordinal)
                .Select(candidate => candidate.Fleet)
                .ToList();
        }

        /// <summary>
        /// Returns the weakest projected readiness ratio for an attack fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to assess.</param>
        /// <param name="target">The fleet's attack target.</param>
        /// <returns>The least-complete attack requirement, from zero through one.</returns>
        private double GetProjectedAttackReadiness(
            AITurnContext context,
            Fleet fleet,
            Planet target
        )
        {
            if (fleet == null || target == null)
                return 0;

            AIAssessment assessment = context.Assessment;
            AIAttackDemand attackDemand = context.GetAttackDemand(target);
            int requiredRegiments = GetProjectedRegimentCount(context, fleet, target);
            double readiness = GetFulfillmentRatio(
                assessment.GetProjectedFleetCombatValue(fleet),
                attackDemand?.CombatStrength ?? 0
            );
            readiness = Math.Min(
                readiness,
                GetFulfillmentRatio(
                    assessment.GetFleetLoadedRegimentCount(fleet),
                    requiredRegiments
                )
            );
            readiness = Math.Min(
                readiness,
                GetFulfillmentRatio(assessment.GetFleetRegimentCapacity(fleet), requiredRegiments)
            );
            readiness = Math.Min(
                readiness,
                GetFulfillmentRatio(
                    assessment.GetProjectedFleetRegimentAttackStrength(fleet),
                    GetProjectedRegimentStrength(context, fleet, target)
                )
            );
            return Math.Min(
                readiness,
                GetFulfillmentRatio(
                    assessment.GetProjectedFleetBombardmentStrength(fleet),
                    attackDemand?.BombardmentStrength ?? 0
                )
            );
        }

        /// <summary>
        /// Returns the primary colonization fleet for reinforcement.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The colonization fleet, or null.</returns>
        private IReadOnlyList<Fleet> GetPriorityColonizationFleets(AITurnContext context)
        {
            return context
                .Assessment.OwnedFleets.Where(fleet =>
                    fleet.RoleType == FleetRoleType.Colonization && CanReinforceFleet(fleet)
                )
                .OrderByDescending(fleet =>
                    AIFleetProductionAllocationScorer.ScoreColonization(context, fleet)
                )
                .ThenBy(fleet => fleet.InstanceID, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Returns stationary battle fleets that can be developed for future campaigns.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The eligible assembly fleets, weakest first.</returns>
        private IReadOnlyList<Fleet> GetFleetAssemblyFleets(AITurnContext context)
        {
            return context
                .Assessment.OwnedFleets.Where(fleet =>
                    fleet.RoleType == FleetRoleType.Battle
                    && CanReinforceFleet(fleet)
                    && fleet.Order == null
                    && context.StrategicPlan.CanFleetDepart(fleet)
                )
                .OrderByDescending(fleet =>
                    AIFleetProductionAllocationScorer.ScoreAssembly(context, fleet)
                )
                .ThenBy(fleet => fleet.InstanceID, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Adds capital ship demand for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        private void AddFleetCapitalShipDemand(
            AITurnContext context,
            List<AIProductionDemand> demands,
            Fleet fleet
        )
        {
            Planet targetPlanet = GetAttackTargetPlanet(context, fleet);
            bool isColonizationFleet = fleet.RoleType == FleetRoleType.Colonization;
            bool isColonizationOrder = fleet.Order?.OrderType == FleetOrderType.Colonize;
            Planet defenseTarget = GetDefenseTarget(context, fleet);
            bool isDefenseOrder = fleet.Order?.OrderType == FleetOrderType.Defend;
            if (
                targetPlanet == null
                && fleet.Order != null
                && !isColonizationOrder
                && defenseTarget == null
            )
                return;

            int projectedCombat = context.Assessment.GetProjectedFleetCombatValue(fleet);
            int targetCombat =
                targetPlanet != null ? context.GetAttackDemand(targetPlanet)?.CombatStrength ?? 0
                : defenseTarget != null ? context.StrategicPlan.GetDefenseStrength(defenseTarget)
                : isColonizationFleet || isColonizationOrder ? projectedCombat
                : context.StrategicPlan.AssemblyFleetCombatStrength;
            int combatDeficit = targetCombat - projectedCombat;
            int targetRegimentCapacity =
                isDefenseOrder ? 0
                : isColonizationFleet || isColonizationOrder
                    ? context.Game.Config.AI.FleetDeployment.ColonizationFleetMaximumRegimentCount
                : targetPlanet == null
                    ? context.Game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount
                : GetDesiredRegimentCount(context, fleet);
            int regimentCapacityDeficit = targetRegimentCapacity - fleet.GetRegimentCapacity();
            int projectedBombardment = context.Assessment.GetProjectedFleetBombardmentStrength(
                fleet
            );
            int targetBombardment =
                targetPlanet == null || isColonizationFleet
                    ? 0
                    : context.GetAttackDemand(targetPlanet)?.BombardmentStrength ?? 0;
            int bombardmentDeficit = targetBombardment - projectedBombardment;
            AICapitalShipProductionRole capitalShipRole;
            int deficit;
            int target;
            if (regimentCapacityDeficit > 0)
            {
                capitalShipRole = AICapitalShipProductionRole.TroopTransport;
                deficit = regimentCapacityDeficit;
                target = targetRegimentCapacity;
            }
            else if (bombardmentDeficit > 0)
            {
                capitalShipRole = AICapitalShipProductionRole.Bombardment;
                deficit = bombardmentDeficit;
                target = targetBombardment;
            }
            else if (combatDeficit > 0)
            {
                capitalShipRole = AICapitalShipProductionRole.General;
                deficit = combatDeficit;
                target = targetCombat;
            }
            else if (!isColonizationFleet && NeedsInterdictionCapitalShip(context, fleet))
            {
                capitalShipRole = AICapitalShipProductionRole.Interdiction;
                deficit = 1;
                target = 1;
            }
            else
            {
                return;
            }

            demands.Add(
                CreateFleetDemand(
                    context,
                    AIProductionDemandKind.FleetCapitalShip,
                    ManufacturingType.Ship,
                    fleet,
                    deficit,
                    target,
                    isColonizationFleet
                        ? context.Game.Config.AI.Infrastructure.ColonizationFleetDemandPercent
                        : context.Game.Config.AI.Infrastructure.FleetCapitalShipDemandPercent,
                    capitalShipRole
                )
            );
        }

        /// <summary>
        /// Returns whether a battle fleet should add an interdiction-capable capital ship.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True when an unlocked gravity-well ship is needed.</returns>
        private static bool NeedsInterdictionCapitalShip(AITurnContext context, Fleet fleet)
        {
            bool supportsInterdiction =
                fleet.Order == null
                || fleet.Order.OrderType is FleetOrderType.Attack or FleetOrderType.Defend;
            if (
                !supportsInterdiction
                || fleet.GetChildren<CapitalShip>().Any(capitalShip => capitalShip.HasGravityWell)
            )
                return false;

            return context
                .Faction.GetUnlockedTechnologies(ManufacturingType.Ship)
                .Any(technology =>
                    technology.GetReference() is CapitalShip capitalShip
                    && IManufacturable.CanBeManufacturedBy(capitalShip, context.Faction.InstanceID)
                    && capitalShip.HasGravityWell
                    && !capitalShip.CanDestroyPlanets
                );
        }

        /// <summary>
        /// Adds starfighter demand for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        private void AddFleetStarfighterDemand(
            AITurnContext context,
            List<AIProductionDemand> demands,
            Fleet fleet
        )
        {
            if (fleet.RoleType == FleetRoleType.Colonization)
                return;

            int targetCount = GetTargetStarfighterCount(context, fleet);
            int deficit = targetCount - fleet.GetCurrentStarfighterCount();
            if (deficit <= 0)
                return;

            demands.Add(
                CreateFleetDemand(
                    context,
                    AIProductionDemandKind.FleetStarfighter,
                    ManufacturingType.Ship,
                    fleet,
                    deficit,
                    targetCount,
                    context.Game.Config.AI.Infrastructure.FleetStarfighterDemandPercent
                )
            );
        }

        /// <summary>
        /// Adds regiment demand for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        private void AddFleetRegimentDemand(
            AITurnContext context,
            List<AIProductionDemand> demands,
            Fleet fleet
        )
        {
            int targetCount = Math.Min(
                fleet.GetRegimentCapacity(),
                GetDesiredRegimentCount(context, fleet)
            );
            int deficit = targetCount - fleet.GetCurrentRegimentCount();
            if (deficit <= 0)
                return;

            demands.Add(
                CreateFleetDemand(
                    context,
                    AIProductionDemandKind.FleetRegiment,
                    ManufacturingType.Troop,
                    fleet,
                    deficit,
                    targetCount,
                    fleet.RoleType == FleetRoleType.Colonization
                        ? context.Game.Config.AI.Infrastructure.ColonizationFleetDemandPercent
                        : context.Game.Config.AI.Infrastructure.FleetRegimentDemandPercent
                )
            );
        }

        /// <summary>
        /// Creates a fleet unit production demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">Demand kind.</param>
        /// <param name="manufacturingType">Manufacturing type required.</param>
        /// <param name="fleet">Fleet receiving the unit.</param>
        /// <param name="deficit">Current deficit.</param>
        /// <param name="targetCount">Target count.</param>
        /// <param name="baseDemandPercent">Base pressure for the demand.</param>
        /// <param name="capitalShipRole">Capital ship role required by the demand.</param>
        /// <returns>The production demand.</returns>
        private AIProductionDemand CreateFleetDemand(
            AITurnContext context,
            AIProductionDemandKind kind,
            ManufacturingType manufacturingType,
            Fleet fleet,
            int deficit,
            int targetCount,
            int baseDemandPercent,
            AICapitalShipProductionRole capitalShipRole = AICapitalShipProductionRole.None
        )
        {
            return new AIProductionDemand(
                AIProductionDemand.CreateId(context.Faction.InstanceID, kind, fleet.InstanceID),
                kind,
                manufacturingType,
                BuildingType.None,
                fleet,
                deficit,
                capitalShipRole: capitalShipRole,
                targetCount: targetCount,
                baseDemandPercent: baseDemandPercent
            );
        }

        /// <summary>
        /// Returns a bounded fulfillment ratio used to measure factual fleet deficits.
        /// </summary>
        /// <param name="value">Current capability.</param>
        /// <param name="target">Required capability.</param>
        /// <returns>The bounded fulfillment ratio.</returns>
        private static double GetFulfillmentRatio(double value, double target)
        {
            return target <= 0 ? 1 : Math.Max(0, Math.Min(1, value / target));
        }

        /// <summary>
        /// Returns whether a fleet can receive reinforcement demand.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True if the fleet can receive reinforcement.</returns>
        private bool CanReinforceFleet(Fleet fleet)
        {
            return fleet?.RoleType is FleetRoleType.Battle or FleetRoleType.Colonization
                && fleet.Movement == null
                && (
                    HasPresentOrUnderConstructionCapitalShips(fleet)
                    || fleet.Order?.OrderType is FleetOrderType.Attack or FleetOrderType.Defend
                );
        }

        /// <summary>
        /// Resolves the defense target assigned to a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The defense target, or null.</returns>
        private Planet GetDefenseTarget(AITurnContext context, Fleet fleet)
        {
            if (fleet?.Order?.OrderType != FleetOrderType.Defend)
                return null;

            Planet target = context.Assessment.GetKnownPlanet(fleet.Order.TargetPlanetId);
            return
                context.Assessment.IsOwnedPlanet(target)
                && context.StrategicPlan.GetDefenseStrength(target) > 0
                ? target
                : null;
        }

        /// <summary>
        /// Returns whether a fleet has capital ships present or being built.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True if the fleet has present or under-construction capital ships.</returns>
        private static bool HasPresentOrUnderConstructionCapitalShips(Fleet fleet)
        {
            return fleet?.GetChildren<CapitalShip>().Count > 0;
        }

        /// <summary>
        /// Returns target starfighter count for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>The target starfighter count.</returns>
        private int GetTargetStarfighterCount(AITurnContext context, Fleet fleet)
        {
            int capacity = fleet.GetStarfighterCapacity();
            return Math.Min(
                capacity,
                IntegerMath.ScaleByPercentRoundedUp(
                    capacity,
                    context.Game.Config.AI.Infrastructure.StarfighterParentFillPercent
                )
            );
        }

        /// <summary>
        /// Returns desired regiment count for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>The desired regiment count.</returns>
        private int GetDesiredRegimentCount(AITurnContext context, Fleet fleet)
        {
            if (fleet.Order?.OrderType == FleetOrderType.Defend)
                return 0;

            if (fleet.RoleType == FleetRoleType.Colonization)
            {
                return Math.Max(
                    context.Game.Config.AI.FleetDeployment.ColonizationFleetMinimumRegimentCount,
                    context.Game.Config.AI.FleetDeployment.ColonizationFleetMaximumRegimentCount
                );
            }

            int capacity = fleet.GetRegimentCapacity();
            int fillTarget = IntegerMath.ScaleByPercentRoundedUp(
                capacity,
                context.Game.Config.AI.Infrastructure.AssaultRegimentLoadPercent
            );
            Planet targetPlanet = context.Assessment.GetAttackTargetPlanet(fleet);
            if (targetPlanet != null)
                fillTarget = Math.Max(
                    fillTarget,
                    GetProjectedRegimentCount(context, fleet, targetPlanet)
                );

            if (
                targetPlanet != null
                && context.Assessment.GetProjectedFleetRegimentAttackStrength(fleet)
                    < GetProjectedRegimentStrength(context, fleet, targetPlanet)
            )
            {
                int currentCount = fleet.GetCurrentRegimentCount();
                int currentStrength = context.Assessment.GetProjectedFleetRegimentAttackStrength(
                    fleet
                );
                int requiredStrength = GetProjectedRegimentStrength(context, fleet, targetPlanet);
                int estimatedStrengthPerRegiment = Math.Max(
                    1,
                    currentCount > 0 ? currentStrength / currentCount : requiredStrength
                );
                int strengthDeficitCount = IntegerMath.DivideRoundedUp(
                    requiredStrength - currentStrength,
                    estimatedStrengthPerRegiment
                );
                fillTarget = Math.Max(fillTarget, currentCount + strengthDeficitCount);
            }

            return fillTarget;
        }

        /// <summary>
        /// Returns the regiment count required after projected fleet bombardment.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The attack target.</param>
        /// <returns>The projected regiment count requirement.</returns>
        private static int GetProjectedRegimentCount(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            AIAttackDemand demand = context.GetAttackDemand(targetPlanet);
            if (demand == null)
                return 0;
            return CanBombardDefenders(context, fleet, targetPlanet)
                ? demand.OccupationRegimentCount
                : demand.RegimentCount;
        }

        /// <summary>
        /// Returns the regiment strength required after projected fleet bombardment.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The attack target.</param>
        /// <returns>The projected regiment strength requirement.</returns>
        private static int GetProjectedRegimentStrength(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            return CanBombardDefenders(context, fleet, targetPlanet)
                ? 0
                : context.GetAttackDemand(targetPlanet)?.RegimentStrength ?? 0;
        }

        /// <summary>
        /// Returns whether projected bombardment can remove defending regiments.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The attack target.</param>
        /// <returns>True when projected bombardment penetrates the target's shields.</returns>
        private static bool CanBombardDefenders(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            return fleet != null
                && targetPlanet != null
                && context.Assessment.GetDefendingRegimentCount(targetPlanet) > 0
                && context.Assessment.GetProjectedFleetBombardmentStrength(fleet)
                    > context.Assessment.GetBombardmentShieldResistance(targetPlanet);
        }

        /// <summary>
        /// Returns the active attack target for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>The attack target planet, or null.</returns>
        private Planet GetAttackTargetPlanet(AITurnContext context, Fleet fleet)
        {
            string targetPlanetId = fleet.Order?.TargetPlanetId;
            if (
                fleet.Order?.OrderType != FleetOrderType.Attack
                || string.IsNullOrEmpty(targetPlanetId)
            )
                return null;

            Planet targetPlanet = context.Assessment.GetKnownPlanet(targetPlanetId);
            string targetOwnerId = targetPlanet?.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(targetOwnerId) || targetOwnerId == context.Faction.InstanceID)
                return null;

            return targetPlanet;
        }

        /// <summary>
        /// Adds special-forces demand to the production plan.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand collection to update.</param>
        private void AddSpecialForcesDemands(
            AITurnContext context,
            ICollection<AIProductionDemand> demands
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            List<SpecialForces> existingUnits =
                context.Faction.GetOwnedUnitsByType<SpecialForces>();
            Dictionary<string, int> existingSupplyByRole = new Dictionary<string, int>(
                StringComparer.Ordinal
            );
            Dictionary<string, int> activeOfficerMissionsByType =
                GetActiveHostileOfficerMissionCounts(context);
            foreach (SpecialForces unit in existingUnits)
                IncrementCount(existingSupplyByRole, GetRoleId(unit));

            foreach (
                IGrouping<string, SpecialForces> role in context
                    .Faction.GetUnlockedTechnologies(ManufacturingType.Troop)
                    .Select(technology => technology.GetReference())
                    .OfType<SpecialForces>()
                    .Where(template => template.AllowedMissionTypeIDs.Count > 0)
                    .GroupBy(GetRoleId, StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
            )
            {
                GameConfig.AIConsiderationConfig buildEfficiency = context
                    .Game
                    .Config
                    .AI
                    .Selection
                    .TechnologyUtility
                    .SpecialForces
                    .BuildEfficiency;
                SpecialForces template = role.OrderByDescending(candidate =>
                        AIUtility.Evaluate(
                            1 - AIUtility.Fulfillment(candidate.ConstructionCost, buildEfficiency),
                            buildEfficiency
                        )
                    )
                    .ThenBy(candidate => candidate.MaintenanceCost)
                    .ThenBy(candidate => candidate.GetTypeID(), StringComparer.Ordinal)
                    .First();
                existingSupplyByRole.TryGetValue(role.Key, out int existingSupply);
                int activeMissionDemand = template.AllowedMissionTypeIDs.Sum(missionTypeId =>
                    activeOfficerMissionsByType.TryGetValue(missionTypeId, out int count)
                        ? count
                        : 0
                );
                int desiredSupply = GetDesiredSupply(
                    activeMissionDemand,
                    config.SpecialForcesMissionCoveragePercent
                );
                int deficit = desiredSupply - existingSupply;
                if (deficit <= 0)
                    continue;

                Planet destination = FindDestination(context);
                if (destination == null)
                    return;

                demands.Add(
                    new AIProductionDemand(
                        AIProductionDemand.CreateId(
                            context.Faction.InstanceID,
                            AIProductionDemandKind.SpecialForces,
                            template.GetTypeID()
                        ),
                        AIProductionDemandKind.SpecialForces,
                        ManufacturingType.Troop,
                        BuildingType.None,
                        destination,
                        deficit,
                        template.GetTypeID(),
                        targetCount: desiredSupply,
                        baseDemandPercent: config.SpecialForcesDemandPercent
                    )
                );
            }
        }

        /// <summary>
        /// Calculates decoy supply from current hostile officer-mission workload.
        /// </summary>
        /// <param name="activeMissionCount">The active officer-led hostile mission count.</param>
        /// <param name="coveragePercent">The portion of that workload to cover with decoys.</param>
        /// <returns>The required decoy supply.</returns>
        private static int GetDesiredSupply(int activeMissionCount, int coveragePercent)
        {
            int boundedCoveragePercent = Math.Max(0, Math.Min(100, coveragePercent));
            return (activeMissionCount * boundedCoveragePercent + 99) / 100;
        }

        /// <summary>
        /// Counts active officer-led missions in enemy territory by mission type.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Active hostile officer-mission counts keyed by mission type.</returns>
        private static Dictionary<string, int> GetActiveHostileOfficerMissionCounts(
            AITurnContext context
        )
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Mission mission in context.Assessment.ActiveMissions)
            {
                Planet target = mission.GetParentOfType<Planet>();
                if (
                    target == null
                    || !context.Assessment.IsEnemyPlanet(target)
                    || !mission.GetMainParticipants().OfType<Officer>().Any()
                )
                    continue;

                string missionTypeId = mission.ConfigKey;
                counts.TryGetValue(missionTypeId, out int count);
                counts[missionTypeId] = count + 1;
            }

            return counts;
        }

        /// <summary>
        /// Increments the count stored for a special-forces role.
        /// </summary>
        /// <param name="counts">The role counts to update.</param>
        /// <param name="roleId">The role identifier to increment.</param>
        private static void IncrementCount(IDictionary<string, int> counts, string roleId)
        {
            counts.TryGetValue(roleId, out int count);
            counts[roleId] = count + 1;
        }

        /// <summary>
        /// Returns the stable role represented by a special-forces unit's mission capabilities.
        /// </summary>
        /// <param name="unit">The special-forces unit or template to inspect.</param>
        /// <returns>The ordered mission-capability identifier.</returns>
        private static string GetRoleId(SpecialForces unit)
        {
            return string.Join(
                "|",
                unit.AllowedMissionTypeIDs.OrderBy(
                    missionTypeId => missionTypeId,
                    StringComparer.Ordinal
                )
            );
        }

        /// <summary>
        /// Finds the owned planet best suited to receive another special-forces unit.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The selected destination, or null when no owned colony is available.</returns>
        private static Planet FindDestination(AITurnContext context)
        {
            return context
                .Assessment.OwnedPlanets.Where(planet => planet.IsColonized && !planet.IsDestroyed)
                .OrderBy(planet => planet.GetChildren<SpecialForces>().Count)
                .ThenByDescending(planet =>
                    context.Assessment.GetPlanetProductionRate(planet, ManufacturingType.Troop)
                )
                .ThenByDescending(context.Assessment.GetPlanetValue)
                .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
        }
    }
}
