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
    /// Builds production demand from faction state and current force needs.
    /// </summary>
    public sealed class AIProductionDemandGenerator
    {
        private readonly struct FacilityPortfolio
        {
            public int ConstructionFacilities { get; }
            public int Shipyards { get; }
            public int TrainingFacilities { get; }
            public int StaticDefenses { get; }
            public int Total =>
                ConstructionFacilities + Shipyards + TrainingFacilities + StaticDefenses;

            /// <summary>
            /// Creates a facility portfolio snapshot.
            /// </summary>
            /// <param name="constructionFacilities">The constructionFacilities value.</param>
            /// <param name="shipyards">The shipyards value.</param>
            /// <param name="trainingFacilities">The trainingFacilities value.</param>
            /// <param name="staticDefenses">The staticDefenses value.</param>
            public FacilityPortfolio(
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

        private static readonly AIDemandSource _colonyDemandSource = new AIColonyDemandSource();
        private static readonly AIDemandSource _specialForcesDemandSource =
            new AISpecialForcesDemandSource();
        private readonly AIInfrastructureDemandPlanner _infrastructureDemandPlanner = new();

        /// <summary>
        /// Returns production demand for the current AI turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Production demand generated for this faction.</returns>
        public List<AIDemand> Generate(AITurnContext context)
        {
            List<AIDemand> demands = new List<AIDemand>();

            if (context?.Game == null || context.Faction == null || context.Assessment == null)
                return demands;

            FacilityPortfolio facilityPortfolio = BuildFacilityPortfolio(context);
            _colonyDemandSource.AddDemands(context, demands);
            AddResourceBalanceDemand(context, demands);
            AddPlanetaryDefenseDemands(context, demands, facilityPortfolio);
            AddPlanetaryStarfighterDemands(context, demands);
            AddFleetSeedDemand(context, demands);
            AddColonizationFleetSeedDemand(context, demands);
            AddFleetReinforcementDemands(context, demands);
            AddPlanetaryGarrisonDemands(context, demands);
            _specialForcesDemandSource.AddDemands(context, demands);
            AddProductionFacilityDemands(
                context,
                demands,
                new AIInfrastructurePlacementScorer(context),
                facilityPortfolio
            );
            AddProductionFacilityUpgradeDemands(context, demands);
            AddIdleShipyardFighterDemands(context, demands);

            return demands;
        }

        /// <summary>
        /// Adds local fighter work for shipyards left without strategic production demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddIdleShipyardFighterDemands(AITurnContext context, List<AIDemand> demands)
        {
            HashSet<string> planetsWithFighterDemand = demands
                .Where(demand => demand.Kind == AIDemandKind.PlanetaryStarfighterReserve)
                .Select(demand => demand.DestinationPlanet?.InstanceID)
                .Where(planetId => !string.IsNullOrEmpty(planetId))
                .ToHashSet(StringComparer.Ordinal);

            foreach (Planet planet in context.Assessment.OwnedPlanets)
            {
                if (
                    !IsOwnedUsablePlanet(planet)
                    || planetsWithFighterDemand.Contains(planet.InstanceID)
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

                int reserveTarget =
                    GetPlanetaryStarfighterRequirement(context, planet)
                    + context.Game.Config.AI.Infrastructure.IdleShipyardFighterReserveCount;
                if (GetOwnedStarfighterCount(context, planet) >= reserveTarget)
                    continue;

                demands.Add(
                    new AIDemand(
                        AIDemand.CreateId(
                            context.Faction.InstanceID,
                            AIDemandKind.PlanetaryStarfighterReserve,
                            "idle-shipyard",
                            planet.InstanceID
                        ),
                        AIDemandKind.PlanetaryStarfighterReserve,
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
        /// Adds static-defense demands for owned planets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="facilityPortfolio">The faction's current strategic-facility mix.</param>
        private void AddPlanetaryDefenseDemands(
            AITurnContext context,
            List<AIDemand> demands,
            FacilityPortfolio facilityPortfolio
        )
        {
            foreach (
                Planet planet in context
                    .Assessment.OwnedPlanets.Where(IsOwnedUsablePlanet)
                    .OrderByDescending(context.Assessment.GetPlanetValue)
                    .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
            )
                AddPlanetaryDefenseDemands(context, demands, planet, facilityPortfolio);
        }

        /// <summary>
        /// Adds static-defense demands for one planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="planet">The planet to evaluate.</param>
        /// <param name="facilityPortfolio">The faction's current strategic-facility mix.</param>
        private void AddPlanetaryDefenseDemands(
            AITurnContext context,
            List<AIDemand> demands,
            Planet planet,
            FacilityPortfolio facilityPortfolio
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int availableEnergy = planet.GetAvailableEnergy();
            int shieldTarget = context.Assessment.GetPlanetaryShieldTargetCount(planet);
            int shieldCount = context
                .Assessment.GetPlanetBuildings(planet)
                .Count(building =>
                    building.GetOwnerInstanceID() == context.Faction.InstanceID
                    && building.IsPlanetaryShieldGenerator()
                );
            int shieldDeficit = Math.Max(0, shieldTarget - shieldCount);
            int shieldQuantity = Math.Min(shieldDeficit, availableEnergy);

            if (shieldQuantity > 0)
            {
                demands.Add(
                    CreatePlanetaryDefenseBuildingDemand(
                        context,
                        planet,
                        BuildingType.Defense,
                        shieldQuantity,
                        shieldTarget,
                        config.PlanetaryShieldDemandPercent,
                        shieldCount == 0,
                        facilityPortfolio
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
            int weaponTarget = context.Assessment.GetPlanetaryWeaponTargetCount(
                planet,
                weaponCount
            );
            int weaponDeficit = weaponTarget - weaponCount;
            if (weaponDeficit <= 0 || availableEnergy <= 0)
                return;

            demands.Add(
                CreatePlanetaryDefenseBuildingDemand(
                    context,
                    planet,
                    BuildingType.Weapon,
                    Math.Min(weaponDeficit, availableEnergy),
                    weaponTarget,
                    config.PlanetaryWeaponDemandPercent,
                    false,
                    facilityPortfolio
                )
            );
        }

        /// <summary>
        /// Returns whether a planet has finished its static defense minimums: the shield
        /// generator limit and the baseline weapon emplacement target.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True when the planet's static defense minimums are complete.</returns>
        private static bool HasCompletedStaticDefense(AITurnContext context, Planet planet)
        {
            int shieldTarget = context.Assessment.GetPlanetaryShieldTargetCount(planet);
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

            int weaponTarget = context.Assessment.GetPlanetaryWeaponTargetCount(
                planet,
                weaponCount
            );
            return shieldCount >= shieldTarget && weaponCount >= weaponTarget;
        }

        /// <summary>
        /// Creates one planetary-defense building demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The destination planet.</param>
        /// <param name="buildingType">The requested defense type.</param>
        /// <param name="deficit">The remaining unit deficit.</param>
        /// <param name="targetCount">The desired unit count.</param>
        /// <param name="baseDemandPercent">The base demand pressure.</param>
        /// <param name="isInitialShield">Whether this establishes the first shield.</param>
        /// <param name="facilityPortfolio">The faction's current strategic-facility mix.</param>
        /// <returns>The defense demand.</returns>
        private AIDemand CreatePlanetaryDefenseBuildingDemand(
            AITurnContext context,
            Planet planet,
            BuildingType buildingType,
            int deficit,
            int targetCount,
            int baseDemandPercent,
            bool isInitialShield,
            FacilityPortfolio facilityPortfolio
        )
        {
            return new AIDemand(
                AIDemand.CreateId(
                    context.Faction.InstanceID,
                    AIDemandKind.PlanetaryDefense,
                    buildingType,
                    planet.InstanceID
                ),
                AIDemandKind.PlanetaryDefense,
                ManufacturingType.Building,
                buildingType,
                planet,
                deficit,
                GetPlanetaryDefensePressure(
                    context,
                    planet,
                    baseDemandPercent,
                    deficit,
                    targetCount,
                    isInitialShield,
                    facilityPortfolio
                )
            );
        }

        /// <summary>
        /// Adds planetary starfighter reserve demands.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddPlanetaryStarfighterDemands(AITurnContext context, List<AIDemand> demands)
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            foreach (
                Planet planet in context
                    .Assessment.OwnedPlanets.Where(IsOwnedUsablePlanet)
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
                int targetCount = GetPlanetaryStarfighterRequirement(context, planet);
                int deficit = targetCount - committedCount;
                if (deficit <= 0)
                    continue;

                demands.Add(
                    new AIDemand(
                        AIDemand.CreateId(
                            context.Faction.InstanceID,
                            AIDemandKind.PlanetaryStarfighterReserve,
                            planet.InstanceID
                        ),
                        AIDemandKind.PlanetaryStarfighterReserve,
                        ManufacturingType.Ship,
                        BuildingType.None,
                        planet,
                        deficit,
                        GetPlanetaryDefensePressure(
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
        /// Returns the strategic and threat-responsive starfighter requirement for a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to assess.</param>
        /// <returns>The number of starfighters required at the planet.</returns>
        private static int GetPlanetaryStarfighterRequirement(AITurnContext context, Planet planet)
        {
            GameConfig.AINonCapitalSummaryConfig config = context.Game.Config.AI.NonCapitalSummary;
            bool hasShipProduction =
                context.Assessment.GetPlanetProductionFacilityCount(planet, ManufacturingType.Ship)
                > 0;
            int baseline =
                planet.IsHeadquarters ? config.StarfighterRequirementHeadquarters
                : hasShipProduction ? config.StarfighterRequirementInfrastructure
                : config.StarfighterRequirementDefault;
            if (!planet.IsHeadquarters && !context.Assessment.IsPlanetThreatened(planet))
            {
                if (!hasShipProduction)
                    baseline = IntegerMath.ScaleByPercent(
                        baseline,
                        config.InteriorStarfighterBaselinePercent
                    );
            }
            int requiredDefenseStrength = context.Assessment.GetRequiredPlanetDefenseStrength(
                planet
            );
            int fighterStrength = GetStrongestAvailableStarfighterStrength(context);
            int threatReinforcement =
                fighterStrength > 0
                    ? IntegerMath.DivideRoundedUp(requiredDefenseStrength, fighterStrength)
                    : 0;
            return baseline + threatReinforcement;
        }

        /// <summary>
        /// Returns whether the planet contains strategic production infrastructure.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True when the planet has at least one production facility.</returns>
        private static bool HasProductionInfrastructure(AITurnContext context, Planet planet) =>
            context.Assessment.HasProductionInfrastructure(planet);

        /// <summary>
        /// Returns the strongest planetary fighter the faction can currently manufacture.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The fighter's combat strength, or zero when none is available.</returns>
        private static int GetStrongestAvailableStarfighterStrength(AITurnContext context)
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
        /// Counts starfighters committed to defending one planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The number of owned starfighters assigned to the planet.</returns>
        private static int GetOwnedStarfighterCount(AITurnContext context, Planet planet)
        {
            return context
                .Assessment.GetPlanetStarfighters(planet)
                .Count(starfighter =>
                    starfighter.GetOwnerInstanceID() == context.Faction.InstanceID
                );
        }

        /// <summary>
        /// Adds demands that establish missing battle fleets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddFleetSeedDemand(AITurnContext context, List<AIDemand> demands)
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
                new AIDemand(
                    AIDemand.CreateId(
                        context.Faction.InstanceID,
                        AIDemandKind.FleetSeedCapitalShip
                    ),
                    AIDemandKind.FleetSeedCapitalShip,
                    ManufacturingType.Ship,
                    BuildingType.None,
                    destination,
                    quantityNeeded,
                    GetDemandPressure(
                        context,
                        AIDemandKind.FleetSeedCapitalShip,
                        quantityNeeded,
                        Math.Max(1, targetCount),
                        context.Game.Config.AI.Infrastructure.FleetSeedCapitalShipDemandPercent
                    ),
                    capitalShipRole: AICapitalShipProductionRole.General
                )
            );
        }

        /// <summary>
        /// Adds demand for a dedicated colonization fleet while settlement opportunities remain.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddColonizationFleetSeedDemand(AITurnContext context, List<AIDemand> demands)
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
                new AIDemand(
                    AIDemand.CreateId(
                        context.Faction.InstanceID,
                        AIDemandKind.ColonizationFleetSeedCapitalShip
                    ),
                    AIDemandKind.ColonizationFleetSeedCapitalShip,
                    ManufacturingType.Ship,
                    BuildingType.None,
                    destination,
                    deficit,
                    context.Game.Config.AI.Infrastructure.ColonizationFleetDemandPercent,
                    capitalShipRole: AICapitalShipProductionRole.TroopTransport
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
        /// Adds production-facility expansion demands.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="placementScorer">The turn-scoped infrastructure placement scorer.</param>
        /// <param name="facilityPortfolio">The turn-scoped facility portfolio.</param>
        private void AddProductionFacilityDemands(
            AITurnContext context,
            List<AIDemand> demands,
            AIInfrastructurePlacementScorer placementScorer,
            FacilityPortfolio facilityPortfolio
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            AddProductionFacilityDemand(
                context,
                demands,
                ManufacturingType.Ship,
                AIDemandKind.Shipyard,
                BuildingType.Shipyard,
                config.ShipyardDemandPercent,
                placementScorer,
                facilityPortfolio
            );
            AddProductionFacilityDemand(
                context,
                demands,
                ManufacturingType.Building,
                AIDemandKind.ConstructionFacility,
                BuildingType.ConstructionFacility,
                config.ConstructionFacilityDemandPercent,
                placementScorer,
                facilityPortfolio
            );
            AddProductionFacilityDemand(
                context,
                demands,
                ManufacturingType.Troop,
                AIDemandKind.TrainingFacility,
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
        private void AddProductionFacilityUpgradeDemands(
            AITurnContext context,
            List<AIDemand> demands
        )
        {
            foreach (
                Planet planet in context
                    .Assessment.OwnedPlanets.Where(IsOwnedUsablePlanet)
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
            List<AIDemand> demands,
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

            AIDemand demand = new AIDemand(
                AIDemand.CreateId(
                    context.Faction.InstanceID,
                    AIDemandKind.BuildingUpgrade,
                    buildingType,
                    planet.InstanceID,
                    replacement.InstanceID
                ),
                AIDemandKind.BuildingUpgrade,
                ManufacturingType.Building,
                buildingType,
                planet,
                1,
                GetProductionFacilityUpgradePressure(context, planet)
            );
            demand.BuildingToReplace = replacement;
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

            return ClampPressure(pressure);
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
            List<AIDemand> demands,
            ManufacturingType manufacturingType,
            AIDemandKind kind,
            BuildingType buildingType,
            int baseDemandPercent,
            AIInfrastructurePlacementScorer placementScorer,
            FacilityPortfolio facilityPortfolio
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            List<AIDemand> productionDemands = demands
                .Where(demand => demand.ManufacturingType == manufacturingType)
                .Where(demand => demand.Kind != kind)
                .OrderByDescending(demand => demand.Pressure)
                .ThenBy(demand => demand.Id, StringComparer.Ordinal)
                .ToList();
            int desiredFacilityCount = _infrastructureDemandPlanner.GetDesiredFacilityCount(
                context,
                buildingType
            );
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
                    IsOwnedUsablePlanet(planet)
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
                AIDemand sectorDemand =
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
                string demandId = AIDemand.CreateId(
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
            List<AIDemand> demands,
            AIDemand primaryDemand,
            AIDemandKind kind,
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
                new AIDemand(
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
            AIDemandKind kind,
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
            if (kind == AIDemandKind.TrainingFacility)
                pressure += AIUtility.EvaluatePressure(
                    1,
                    context.Game.Config.AI.Infrastructure.DemandUtility.TrainingBacklog
                );

            if (kind == AIDemandKind.ConstructionFacility)
                pressure += AIUtility.EvaluatePressure(
                    investmentDeficit,
                    context.Game.Config.AI.Infrastructure.DemandUtility.FacilityInvestment
                );

            pressure += GetFacilityPortfolioPressure(context, kind, facilityPortfolio);

            return pressure;
        }

        /// <summary>
        /// Captures the projected strategic-facility mix once for this production turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Counts for productive and static-defense facilities.</returns>
        private static FacilityPortfolio BuildFacilityPortfolio(AITurnContext context)
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
        /// Returns signed demand pressure based on a facility category's target portfolio share.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">The facility demand category.</param>
        /// <param name="portfolio">The faction's current strategic-facility mix.</param>
        /// <returns>Positive pressure below target and negative pressure above target.</returns>
        private static double GetFacilityPortfolioPressure(
            AITurnContext context,
            AIDemandKind kind,
            FacilityPortfolio portfolio
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            (int count, int targetPercent) = kind switch
            {
                AIDemandKind.ConstructionFacility => (
                    portfolio.ConstructionFacilities,
                    config.ConstructionFacilityPortfolioPercent
                ),
                AIDemandKind.Shipyard => (portfolio.Shipyards, config.ShipyardPortfolioPercent),
                AIDemandKind.TrainingFacility => (
                    portfolio.TrainingFacilities,
                    config.TrainingFacilityPortfolioPercent
                ),
                AIDemandKind.PlanetaryDefense => (
                    portfolio.StaticDefenses,
                    config.StaticDefensePortfolioPercent
                ),
                _ => (0, 0),
            };
            if (portfolio.Total <= 0 || targetPercent <= 0)
                return 0;

            double currentPercent = count * 100.0 / portfolio.Total;
            double targetDeviation = (targetPercent - currentPercent) / targetPercent;
            double normalizedDeviation = AIUtility.Fulfillment(Math.Abs(targetDeviation), 1);
            GameConfig.AIConsiderationConfig consideration = config.DemandUtility.FacilityPortfolio;
            return targetDeviation >= 0
                ? AIUtility.EvaluatePressure(normalizedDeviation, consideration)
                : -AIUtility.EvaluatePressure(normalizedDeviation, consideration);
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
        /// Adds planetary garrison-regiment demands.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddPlanetaryGarrisonDemands(AITurnContext context, List<AIDemand> demands)
        {
            foreach (Planet planet in context.Assessment.OwnedPlanets.Where(IsOwnedUsablePlanet))
                AddGarrisonRegimentReserveDemand(context, demands, planet);
        }

        /// <summary>
        /// Adds local garrison regiment demand for a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="planet">The planet to inspect.</param>
        private void AddGarrisonRegimentReserveDemand(
            AITurnContext context,
            List<AIDemand> demands,
            Planet planet
        )
        {
            int minimumTargetCount = GetTargetGarrisonRegimentReserveCount(context, planet);
            int currentCount = context
                .Assessment.GetPlanetRegiments(planet)
                .Count(regiment => regiment.GetOwnerInstanceID() == context.Faction.InstanceID);
            int deficit = minimumTargetCount - currentCount;
            if (deficit <= 0)
                return;

            demands.Add(
                new AIDemand(
                    AIDemand.CreateId(
                        context.Faction.InstanceID,
                        AIDemandKind.GarrisonRegimentReserve,
                        planet.InstanceID
                    ),
                    AIDemandKind.GarrisonRegimentReserve,
                    ManufacturingType.Troop,
                    BuildingType.None,
                    planet,
                    deficit,
                    GetPlanetaryDefensePressure(
                        context,
                        planet,
                        context.Game.Config.AI.Infrastructure.PlanetaryGarrisonDemandPercent,
                        deficit,
                        minimumTargetCount
                    )
                )
            );
        }

        /// <summary>
        /// Adds reinforcement demand for owned fleets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddFleetReinforcementDemands(AITurnContext context, List<AIDemand> demands)
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
        private void AddFleetDemands(AITurnContext context, List<AIDemand> demands, Fleet fleet)
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
            List<AIDemand> demands,
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
            List<AIDemand> demands,
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
                    AIFleetReinforcementUtility.ScoreDefenseNeed(
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
            int requiredRegiments = assessment.GetProjectedRequiredAttackRegimentCount(
                fleet,
                target
            );
            double readiness = GetFulfillmentRatio(
                assessment.GetProjectedFleetCombatValue(fleet),
                assessment.GetRequiredAttackCombatStrength(target)
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
                    assessment.GetProjectedRequiredAttackRegimentStrength(fleet, target)
                )
            );
            return Math.Min(
                readiness,
                GetFulfillmentRatio(
                    assessment.GetProjectedFleetBombardmentStrength(fleet),
                    assessment.GetRequiredBombardmentStrength(target)
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
            List<AIDemand> demands,
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
                targetPlanet != null
                    ? context.Assessment.GetRequiredAttackCombatStrength(targetPlanet)
                : defenseTarget != null
                    ? context.Assessment.GetRequiredDefenseStrength(defenseTarget)
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
                    : context.Assessment.GetRequiredBombardmentStrength(targetPlanet);
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
                    AIDemandKind.FleetCapitalShip,
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
            List<AIDemand> demands,
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
                    AIDemandKind.FleetStarfighter,
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
            List<AIDemand> demands,
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
                    AIDemandKind.FleetRegiment,
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
        /// Adds mine and refinery demands.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddResourceBalanceDemand(AITurnContext context, List<AIDemand> demands)
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            if (!NeedsEconomyExpansion(context))
                return;

            int economyBatchSize = GetEconomyBatchSize(context, config);
            int rawResourceNodes = context.Faction.GetTotalRawResourceNodes();
            int plannedMines = context.Faction.GetTotalRawMinedResources();
            int plannedRefineries = context.Faction.GetTotalRawRefinementCapacity();
            int mineDeficit = GetMineDeficit(
                rawResourceNodes,
                plannedMines,
                plannedRefineries,
                economyBatchSize
            );
            int refineryDeficit = GetRefineryDeficit(
                plannedMines,
                plannedRefineries,
                mineDeficit,
                economyBatchSize
            );
            int economyDemandPercent = GetEconomyDemandPercent(
                rawResourceNodes,
                plannedMines,
                config
            );
            List<Planet> mineTargets = FindMineTargetPlanets(context, mineDeficit).ToList();
            HashSet<string> mineTargetIds = new HashSet<string>(
                mineTargets.Select(planet => planet.InstanceID),
                StringComparer.Ordinal
            );
            List<Planet> refineryTargets = FindRefineryTargetPlanets(
                    context,
                    refineryDeficit,
                    mineTargetIds
                )
                .ToList();

            foreach (Planet target in mineTargets)
            {
                demands.Add(
                    CreateBuildingDemand(
                        context,
                        AIDemandKind.Mine,
                        BuildingType.Mine,
                        target,
                        1,
                        plannedMines + mineDeficit,
                        economyDemandPercent
                    )
                );
            }

            foreach (Planet target in refineryTargets)
            {
                demands.Add(
                    CreateBuildingDemand(
                        context,
                        AIDemandKind.Refinery,
                        BuildingType.Refinery,
                        target,
                        1,
                        plannedRefineries + refineryDeficit,
                        economyDemandPercent
                    )
                );
            }
        }

        /// <summary>
        /// Returns whether resource production is constraining manufacturing or maintenance.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the faction has unmet material requests or insufficient headroom.</returns>
        private bool NeedsEconomyExpansion(AITurnContext context)
        {
            return context.Assessment.PendingRawMaterialRequestCount > 0
                || context.Assessment.PendingRefinedMaterialRequestCount > 0
                || GetProjectedRefinedMaterialPercent(context)
                    <= context.Game.Config.AI.Selection.RefinedMaterialEconomyWarningPercent
                || context.Assessment.ProjectedEconomyMaintenanceHeadroom
                    < context.Game.Config.AI.Selection.MaintenanceHeadroomTarget;
        }

        /// <summary>
        /// Returns how many mine demands should be generated.
        /// </summary>
        /// <param name="rawResourceNodes">Known raw resource nodes.</param>
        /// <param name="plannedMines">Current and queued mine capacity.</param>
        /// <param name="plannedRefineries">Current and queued refinery capacity.</param>
        /// <param name="economyBatchSize">Maximum economy batch size.</param>
        /// <returns>The mine deficit.</returns>
        private int GetMineDeficit(
            int rawResourceNodes,
            int plannedMines,
            int plannedRefineries,
            int economyBatchSize
        )
        {
            if (rawResourceNodes <= plannedMines)
                return 0;

            if (plannedRefineries > plannedMines)
                return Math.Min(
                    economyBatchSize,
                    Math.Min(plannedRefineries - plannedMines, rawResourceNodes - plannedMines)
                );

            if (plannedRefineries == plannedMines)
                return Math.Min(economyBatchSize, rawResourceNodes - plannedMines);

            return 0;
        }

        /// <summary>
        /// Returns how many refinery demands should be generated.
        /// </summary>
        /// <param name="plannedMines">Current and queued mine capacity.</param>
        /// <param name="plannedRefineries">Current and queued refinery capacity.</param>
        /// <param name="selectedMineDeficit">Mine demand selected for this pass.</param>
        /// <param name="economyBatchSize">Maximum economy batch size.</param>
        /// <returns>The refinery deficit.</returns>
        private int GetRefineryDeficit(
            int plannedMines,
            int plannedRefineries,
            int selectedMineDeficit,
            int economyBatchSize
        )
        {
            int desiredRefineries = plannedMines + selectedMineDeficit;
            if (desiredRefineries <= plannedRefineries)
                return 0;

            return Math.Min(economyBatchSize, desiredRefineries - plannedRefineries);
        }

        /// <summary>
        /// Returns the demand pressure for economy buildings.
        /// </summary>
        /// <param name="rawResourceNodes">Known raw resource nodes.</param>
        /// <param name="plannedMines">Current and queued mine capacity.</param>
        /// <param name="config">AI infrastructure configuration.</param>
        /// <returns>The economy demand pressure.</returns>
        private int GetEconomyDemandPercent(
            int rawResourceNodes,
            int plannedMines,
            GameConfig.AIInfrastructureConfig config
        )
        {
            if (rawResourceNodes <= 0)
                return config.EconomyDemandPercent;

            int minedCoveragePercent = plannedMines * 100 / rawResourceNodes;
            if (minedCoveragePercent <= config.EconomySevereDeficitPercent)
                return config.EconomySevereDemandPercent;

            return config.EconomyDemandPercent;
        }

        /// <summary>
        /// Returns how many economy demands may be generated this turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="config">AI infrastructure configuration.</param>
        /// <returns>The economy batch size.</returns>
        private int GetEconomyBatchSize(
            AITurnContext context,
            GameConfig.AIInfrastructureConfig config
        )
        {
            int availableBuildingLanes = context.Assessment.GetAvailableProductionLaneCount(
                ManufacturingType.Building
            );
            int economyLaneBudget = availableBuildingLanes - config.EconomyCompetingNeedSlotReserve;
            return Math.Max(config.EconomyDefaultBatchSize, Math.Max(0, economyLaneBudget));
        }

        /// <summary>
        /// Creates a building production demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">Demand kind.</param>
        /// <param name="buildingType">Building type requested.</param>
        /// <param name="target">Planet receiving the building.</param>
        /// <param name="deficit">Current deficit.</param>
        /// <param name="targetCount">Target count.</param>
        /// <param name="baseDemandPercent">Base pressure for the demand.</param>
        /// <returns>The production demand.</returns>
        private AIDemand CreateBuildingDemand(
            AITurnContext context,
            AIDemandKind kind,
            BuildingType buildingType,
            Planet target,
            int deficit,
            int targetCount,
            int baseDemandPercent
        )
        {
            return new AIDemand(
                AIDemand.CreateId(context.Faction.InstanceID, kind, target.InstanceID),
                kind,
                ManufacturingType.Building,
                buildingType,
                target,
                deficit,
                GetDemandPressure(context, kind, deficit, targetCount, baseDemandPercent)
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
        private AIDemand CreateFleetDemand(
            AITurnContext context,
            AIDemandKind kind,
            ManufacturingType manufacturingType,
            Fleet fleet,
            int deficit,
            int targetCount,
            int baseDemandPercent,
            AICapitalShipProductionRole capitalShipRole = AICapitalShipProductionRole.None
        )
        {
            return new AIDemand(
                AIDemand.CreateId(context.Faction.InstanceID, kind, fleet.InstanceID),
                kind,
                manufacturingType,
                BuildingType.None,
                fleet,
                deficit,
                GetFleetDemandPressure(
                    context,
                    kind,
                    fleet,
                    deficit,
                    targetCount,
                    baseDemandPercent
                ),
                capitalShipRole: capitalShipRole
            );
        }

        /// <summary>
        /// Returns mine destination planets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="count">Maximum number of planets to return.</param>
        /// <returns>Mine destination planets.</returns>
        private IEnumerable<Planet> FindMineTargetPlanets(AITurnContext context, int count)
        {
            if (count <= 0)
                return Enumerable.Empty<Planet>();

            return GetBuildingDestinationPlanets(context)
                .Where(planet => planet.GetUnminedResourceNodeCount() > 0)
                .OrderByDescending(planet => planet.GetUnminedResourceNodeCount())
                .ThenByDescending(planet => planet.GetAvailableEnergy())
                .ThenBy(planet => planet.InstanceID)
                .Take(count);
        }

        /// <summary>
        /// Returns refinery destination planets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="count">Maximum number of planets to return.</param>
        /// <param name="excludedPlanetIds">Planet ids already selected for mine demand.</param>
        /// <returns>Refinery destination planets.</returns>
        private IEnumerable<Planet> FindRefineryTargetPlanets(
            AITurnContext context,
            int count,
            HashSet<string> excludedPlanetIds
        )
        {
            if (count <= 0)
                return Enumerable.Empty<Planet>();

            List<Planet> preferredTargets = GetBuildingDestinationPlanets(context)
                .Where(planet => !excludedPlanetIds.Contains(planet.InstanceID))
                .OrderBy(planet => planet.GetTotalBuildingTypeCount(BuildingType.Refinery))
                .ThenByDescending(planet => planet.GetAvailableEnergy())
                .ThenBy(planet => planet.InstanceID)
                .Take(count)
                .ToList();

            if (preferredTargets.Count >= count)
                return preferredTargets;

            preferredTargets.AddRange(
                GetBuildingDestinationPlanets(context)
                    .Where(planet => excludedPlanetIds.Contains(planet.InstanceID))
                    .OrderBy(planet => planet.GetTotalBuildingTypeCount(BuildingType.Refinery))
                    .ThenByDescending(planet => planet.GetAvailableEnergy())
                    .ThenBy(planet => planet.InstanceID)
                    .Take(count - preferredTargets.Count)
            );

            return preferredTargets;
        }

        /// <summary>
        /// Returns energy available for additional production facilities.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The available energy.</returns>
        private static int GetAvailableFacilityExpansionEnergy(Planet planet)
        {
            return planet?.GetAvailableEnergy() ?? 0;
        }

        /// <summary>
        /// Resolves the live destination planet for a demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The production demand.</param>
        /// <returns>The destination planet, or null.</returns>
        private Planet GetDemandPlanet(AITurnContext context, AIDemand demand)
        {
            return demand?.DestinationPlanet
                ?? context.Assessment.GetFleetPlanet(demand?.DestinationFleet);
        }

        /// <summary>
        /// Returns planets that can receive buildings.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Building destination planets.</returns>
        private IEnumerable<Planet> GetBuildingDestinationPlanets(AITurnContext context)
        {
            return context.Assessment.OwnedPlanets.Where(planet =>
                IsOwnedBuildingDestination(planet) && planet.GetAvailableEnergy() > 0
            );
        }

        /// <summary>
        /// Returns whether an owned planet can receive a manufactured building.
        /// </summary>
        /// <param name="planet">The prospective destination.</param>
        /// <returns>True when the planet exists and has not been destroyed.</returns>
        private static bool IsOwnedBuildingDestination(Planet planet) =>
            planet?.IsDestroyed == false;

        /// <summary>
        /// Returns whether a planet is an owned usable colony.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True if the planet is usable.</returns>
        private bool IsOwnedUsablePlanet(Planet planet)
        {
            return planet?.IsColonized == true && !planet.IsDestroyed;
        }

        /// <summary>
        /// Returns current and queued facility count for a building type.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="buildingType">Building type to count.</param>
        /// <returns>The owned facility count.</returns>
        private int GetOwnedFacilityCount(AITurnContext context, BuildingType buildingType)
        {
            return context.Assessment.OwnedPlanets.Sum(planet =>
                planet.GetTotalBuildingTypeCount(buildingType)
            );
        }

        /// <summary>
        /// Returns pressure for non-fleet production demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">Demand kind.</param>
        /// <param name="deficit">Current deficit.</param>
        /// <param name="targetCount">Target count.</param>
        /// <param name="baseDemandPercent">Base pressure for the demand.</param>
        /// <returns>The demand pressure.</returns>
        private double GetDemandPressure(
            AITurnContext context,
            AIDemandKind kind,
            int deficit,
            int targetCount,
            int baseDemandPercent
        )
        {
            double pressure = GetBasePressure(context, baseDemandPercent, deficit, targetCount);

            if (kind is AIDemandKind.Mine or AIDemandKind.Refinery)
            {
                pressure += GetEconomyMaintenancePressure(context);
                pressure += GetEconomyRefinedMaterialPressure(context);
                return pressure;
            }

            return ClampPressure(pressure);
        }

        /// <summary>
        /// Returns extra economy pressure as uncommitted refined materials approach the reserve.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The refined-material economy pressure.</returns>
        private double GetEconomyRefinedMaterialPressure(AITurnContext context)
        {
            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            int reservePercent = Math.Max(0, config.RefinedMaterialReservePercent);
            int warningPercent = Math.Max(
                reservePercent,
                config.RefinedMaterialEconomyWarningPercent
            );
            int projectedPercent = GetProjectedRefinedMaterialPercent(context);
            if (projectedPercent >= warningPercent)
                return 0;

            int pressureRange = Math.Max(1, warningPercent - reservePercent);
            double urgency = Math.Min(
                1,
                Math.Max(0, warningPercent - projectedPercent) / (double)pressureRange
            );
            return AIUtility.EvaluatePressure(
                urgency,
                context.Game.Config.AI.Infrastructure.DemandUtility.ResourceShortage
            );
        }

        /// <summary>
        /// Returns projected uncommitted refined materials as a percentage of supply.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The projected refined-material percentage.</returns>
        private int GetProjectedRefinedMaterialPercent(AITurnContext context)
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
        /// Returns production pressure for a planet's defensive requirement.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet requiring defense.</param>
        /// <param name="baseDemandPercent">Base pressure for the demand.</param>
        /// <param name="deficit">Current defense deficit.</param>
        /// <param name="targetCount">Target defense count.</param>
        /// <param name="isInitialShield">Whether the demand establishes the first shield.</param>
        /// <param name="facilityPortfolio">The faction's current strategic-facility mix.</param>
        /// <returns>The defense pressure.</returns>
        private double GetPlanetaryDefensePressure(
            AITurnContext context,
            Planet planet,
            int baseDemandPercent,
            int deficit,
            int targetCount,
            bool isInitialShield = false,
            FacilityPortfolio facilityPortfolio = default
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            GameConfig.AIProductionDemandUtilityConfig utility = config.DemandUtility;
            double highestPlanetValue = context.Assessment.GetHighestOwnedPlanetValue();
            double pressure =
                baseDemandPercent
                + AIUtility.EvaluateDiscretePressure(
                    deficit / (double)Math.Max(1, targetCount),
                    utility.DefenseDeficit
                );

            if (highestPlanetValue > 0)
            {
                pressure += AIUtility.EvaluatePressure(
                    context.Assessment.GetPlanetValue(planet) / highestPlanetValue,
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
            if (facilityPortfolio.Total > 0)
            {
                pressure += GetFacilityPortfolioPressure(
                    context,
                    AIDemandKind.PlanetaryDefense,
                    facilityPortfolio
                );
            }

            double boundedPressure = ClampPressure(pressure);
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
        /// Returns pressure for fleet production demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">Demand kind.</param>
        /// <param name="fleet">Fleet receiving production.</param>
        /// <param name="deficit">Current deficit.</param>
        /// <param name="targetCount">Target count.</param>
        /// <param name="baseDemandPercent">Base pressure for the demand.</param>
        /// <returns>The fleet demand pressure.</returns>
        private double GetFleetDemandPressure(
            AITurnContext context,
            AIDemandKind kind,
            Fleet fleet,
            int deficit,
            int targetCount,
            int baseDemandPercent
        )
        {
            double pressure = GetBasePressure(context, baseDemandPercent, deficit, targetCount);
            Planet targetPlanet = context.Assessment.GetAttackTargetPlanet(fleet);

            if (targetPlanet != null)
            {
                pressure += GetTargetValuePressure(context, targetPlanet);
                pressure += GetFleetReadinessPressure(context, kind, fleet, targetPlanet);
                pressure += GetFinalReadinessGatePressure(context, fleet, targetPlanet, deficit);
                if (kind is AIDemandKind.FleetCapitalShip or AIDemandKind.FleetRegiment)
                {
                    pressure += AIUtility.EvaluatePressure(
                        1,
                        context.Game.Config.AI.Infrastructure.DemandUtility.AttackReinforcement
                    );
                }
            }

            if (kind == AIDemandKind.FleetStarfighter)
                pressure += GetStarfighterFillPressure(context, fleet, targetCount);

            return pressure;
        }

        /// <summary>
        /// Returns base pressure for a demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="baseDemandPercent">Base pressure for the demand.</param>
        /// <param name="deficit">Current deficit.</param>
        /// <param name="targetCount">Target count.</param>
        /// <returns>The base pressure.</returns>
        private static double GetBasePressure(
            AITurnContext context,
            int baseDemandPercent,
            int deficit,
            int targetCount
        )
        {
            double deficitRatio = deficit / (double)Math.Max(1, targetCount);
            return Math.Min(
                100,
                baseDemandPercent
                    + AIUtility.EvaluateDiscretePressure(
                        deficitRatio,
                        context.Game.Config.AI.Infrastructure.DemandUtility.Deficit
                    )
            );
        }

        /// <summary>
        /// Returns extra economy pressure from maintenance headroom.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The economy maintenance pressure.</returns>
        private double GetEconomyMaintenancePressure(AITurnContext context)
        {
            GameConfig.AIProductionDemandUtilityConfig utility = context
                .Game
                .Config
                .AI
                .Infrastructure
                .DemandUtility;
            int headroom = context.Assessment.ProjectedEconomyMaintenanceHeadroom;
            int floor = context.Game.Config.AI.Selection.MaintenanceHeadroomReserve;
            int target = Math.Max(
                floor,
                context.Game.Config.AI.Selection.MaintenanceHeadroomTarget
            );

            if (headroom < floor)
                return AIUtility.EvaluatePressure(1, utility.MaintenanceShortfall);

            if (headroom >= target)
                return 0;

            return AIUtility.EvaluateDiscretePressure(
                (target - headroom) / (double)Math.Max(1, target - floor),
                utility.MaintenanceReserve
            );
        }

        /// <summary>
        /// Returns extra pressure from target planet value.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <returns>The target value pressure.</returns>
        private double GetTargetValuePressure(AITurnContext context, Planet targetPlanet)
        {
            double highestValue = context.Assessment.GetHighestEnemyPlanetValue();
            if (highestValue <= 0)
                return 0;

            return AIUtility.EvaluatePressure(
                context.Assessment.GetPlanetValue(targetPlanet) / highestValue,
                context.Game.Config.AI.Infrastructure.DemandUtility.FleetTargetValue
            );
        }

        /// <summary>
        /// Returns extra pressure from fleet readiness gaps.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">Demand kind.</param>
        /// <param name="fleet">Fleet receiving production.</param>
        /// <param name="targetPlanet">Fleet attack target.</param>
        /// <returns>The fleet readiness pressure.</returns>
        private double GetFleetReadinessPressure(
            AITurnContext context,
            AIDemandKind kind,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            GameConfig.AIConsiderationConfig readiness = context
                .Game
                .Config
                .AI
                .Infrastructure
                .DemandUtility
                .FleetReadiness;
            int requiredCombat = context.Assessment.GetRequiredAttackCombatStrength(targetPlanet);
            int requiredRegiments = context.Assessment.GetProjectedRequiredAttackRegimentCount(
                fleet,
                targetPlanet
            );
            double combatReadiness = GetFulfillmentRatio(
                context.Assessment.GetProjectedFleetCombatValue(fleet),
                requiredCombat
            );
            double regimentReadiness = GetFulfillmentRatio(
                context.Assessment.GetFleetLoadedRegimentCount(fleet),
                requiredRegiments
            );
            double capacityReadiness = GetFulfillmentRatio(
                context.Assessment.GetFleetRegimentCapacity(fleet),
                requiredRegiments
            );

            return kind switch
            {
                AIDemandKind.FleetRegiment => AIUtility.EvaluatePressure(
                    (combatReadiness + capacityReadiness) / 2,
                    readiness
                ),
                AIDemandKind.FleetCapitalShip => AIUtility.EvaluatePressure(
                    (regimentReadiness + capacityReadiness) / 2,
                    readiness
                ),
                AIDemandKind.FleetStarfighter => AIUtility.EvaluatePressure(
                    (combatReadiness + regimentReadiness + capacityReadiness) / 3,
                    readiness
                ),
                _ => 0,
            };
        }

        /// <summary>
        /// Returns extra pressure when a fleet is near final readiness.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet receiving production.</param>
        /// <param name="targetPlanet">Fleet attack target.</param>
        /// <param name="deficit">Current deficit.</param>
        /// <returns>The final readiness pressure.</returns>
        private double GetFinalReadinessGatePressure(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet,
            int deficit
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            if (deficit > config.FleetFinalReadinessGateUnitCount)
                return 0;

            int requiredCombat = context.Assessment.GetRequiredAttackCombatStrength(targetPlanet);
            int requiredRegiments = context.Assessment.GetProjectedRequiredAttackRegimentCount(
                fleet,
                targetPlanet
            );
            bool combatReady =
                context.Assessment.GetProjectedFleetCombatValue(fleet) >= requiredCombat;
            bool capacityReady =
                context.Assessment.GetFleetRegimentCapacity(fleet) >= requiredRegiments;
            bool bombardmentReady =
                context.Assessment.GetProjectedFleetBombardmentStrength(fleet)
                >= context.Assessment.GetRequiredBombardmentStrength(targetPlanet);

            if (!combatReady || !capacityReady || !bombardmentReady)
                return 0;

            return AIUtility.EvaluateDiscretePressure(
                (config.FleetFinalReadinessGateUnitCount - deficit + 1)
                    / (double)config.FleetFinalReadinessGateUnitCount,
                config.DemandUtility.FinalReadiness
            );
        }

        /// <summary>
        /// Returns extra pressure for filling starfighter capacity.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet receiving starfighters.</param>
        /// <param name="targetCount">Target starfighter count.</param>
        /// <returns>The starfighter fill pressure.</returns>
        private double GetStarfighterFillPressure(
            AITurnContext context,
            Fleet fleet,
            int targetCount
        )
        {
            if (fleet == null || targetCount <= 0)
                return 0;

            int loadedCount = context.Assessment.GetFleetLoadedStarfighterCount(fleet);
            return AIUtility.EvaluateDiscretePressure(
                (targetCount - loadedCount) / (double)targetCount,
                context.Game.Config.AI.Infrastructure.DemandUtility.StarfighterFill
            );
        }

        /// <summary>
        /// Returns a bounded fulfillment ratio.
        /// </summary>
        /// <param name="value">Current value.</param>
        /// <param name="target">Target value.</param>
        /// <returns>The bounded fulfillment ratio.</returns>
        private double GetFulfillmentRatio(double value, double target)
        {
            if (target <= 0)
                return 1;

            return Math.Max(0, Math.Min(1, value / target));
        }

        /// <summary>
        /// Clamps pressure to the standard demand-scoring range.
        /// </summary>
        /// <param name="pressure">Pressure to clamp.</param>
        /// <returns>The clamped pressure.</returns>
        private double ClampPressure(double pressure)
        {
            return Math.Max(0, Math.Min(100, pressure));
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
                && context.Assessment.GetRequiredDefenseStrength(target) > 0
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
                    context.Assessment.GetProjectedRequiredAttackRegimentCount(fleet, targetPlanet)
                );

            if (
                targetPlanet != null
                && context.Assessment.GetProjectedFleetRegimentAttackStrength(fleet)
                    < context.Assessment.GetProjectedRequiredAttackRegimentStrength(
                        fleet,
                        targetPlanet
                    )
            )
            {
                int currentCount = fleet.GetCurrentRegimentCount();
                int currentStrength = context.Assessment.GetProjectedFleetRegimentAttackStrength(
                    fleet
                );
                int requiredStrength =
                    context.Assessment.GetProjectedRequiredAttackRegimentStrength(
                        fleet,
                        targetPlanet
                    );
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
        /// Returns garrison regiment reserve target for a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>The target garrison regiment reserve count.</returns>
        private int GetTargetGarrisonRegimentReserveCount(AITurnContext context, Planet planet)
        {
            int stabilityTarget = UprisingSystem.CalculateGarrisonRequirement(
                planet,
                context.Faction,
                context.Game.Config.AI.Garrison
            );
            int sabotageResilientTarget = stabilityTarget > 0 ? stabilityTarget + 1 : 0;
            if (context.Assessment.HasEnemyControlSupport(planet))
                sabotageResilientTarget = Math.Max(sabotageResilientTarget, 2);

            int captureFloor = context.Game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount;
            if (!planet.IsHeadquarters && !context.Assessment.IsPlanetThreatened(planet))
            {
                captureFloor = IntegerMath.ScaleByPercent(
                    captureFloor,
                    context.Game.Config.AI.Garrison.InteriorCaptureFloorPercent
                );
            }

            return Math.Max(sabotageResilientTarget, Math.Max(captureFloor, stabilityTarget));
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
    }
}
