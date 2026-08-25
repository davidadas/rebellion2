using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;
using Rebellion.AI.Planners.Demand;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Planners
{
    [TestFixture]
    public class AIProductionDemandGeneratorTests
    {
        [Test]
        public void Generate_WithClaimedUncolonizedPlanet_AddsColonyDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "claimed-world",
                empire.InstanceID,
                rawResourceNodes: 2
            );
            planet.IsColonized = false;
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("garrison", empire.InstanceID),
                planet
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIProductionDemandKind.Colony);

            Assert.AreSame(planet, demand.DestinationPlanet);
            Assert.AreEqual(BuildingType.Mine, demand.BuildingType);
            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        [Test]
        public void Generate_WithAbandonedUncolonizedPlanet_DoesNotAddColonyDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "abandoned-world",
                empire.InstanceID
            );
            planet.IsColonized = false;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(demands.Any(item => item.Kind == AIProductionDemandKind.Colony));
        }

        [Test]
        public void Generate_WithUnminedResourcesAndBalancedEconomy_AddsMineAndRefineryDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(
                game,
                system,
                "resource-world",
                empire.InstanceID,
                rawResourceNodes: 4
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsTrue(demands.Any(demand => demand.Kind == AIProductionDemandKind.Mine));
            Assert.IsTrue(demands.Any(demand => demand.Kind == AIProductionDemandKind.Refinery));
        }

        [Test]
        public void Generate_WithLowestRefineryCountAtFullEnergy_TargetsEligiblePlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet fullPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "full-world",
                empire.InstanceID,
                energyCapacity: 1,
                rawResourceNodes: 1
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                fullPlanet,
                "full-world-mine",
                BuildingType.Mine,
                ManufacturingType.None
            );
            Planet eligiblePlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "eligible-world",
                empire.InstanceID,
                energyCapacity: 6,
                rawResourceNodes: 2
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                eligiblePlanet,
                "eligible-world-mine",
                BuildingType.Mine,
                ManufacturingType.None
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                eligiblePlanet,
                "eligible-world-refinery",
                BuildingType.Refinery,
                ManufacturingType.None
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIProductionDemandKind.Refinery);

            Assert.AreSame(eligiblePlanet, demand.DestinationPlanet);
        }

        [Test]
        public void Generate_WithOnlyStaticDefenseEnergyRemaining_DoesNotAddEconomyDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.SpecialForcesTargetCountPerType = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(
                game,
                system,
                "defense-reserve-world",
                empire.InstanceID,
                energyCapacity: game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit
                    + game.Config.AI.Infrastructure.PlanetaryWeaponTargetCount,
                rawResourceNodes: 4
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind is AIProductionDemandKind.Mine or AIProductionDemandKind.Refinery
                )
            );
        }

        [Test]
        public void Generate_WithOnlyStaticDefenseEnergyRemaining_DoesNotAddFacilityExpansion()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.SpecialForcesTargetCountPerType = 0;
            int staticDefenseEnergy =
                game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit
                + game.Config.AI.Infrastructure.PlanetaryWeaponTargetCount;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet hub = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "training-hub",
                empire.InstanceID,
                energyCapacity: staticDefenseEnergy + 1
            );
            Planet colony = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "colony",
                empire.InstanceID,
                energyCapacity: staticDefenseEnergy
            );
            hub.SetPopularSupport(empire.InstanceID, 100);
            colony.SetPopularSupport(empire.InstanceID, 100);
            AITestSceneBuilder.AddProductionFacility(
                game,
                hub,
                "training-facility",
                BuildingType.TrainingFacility,
                ManufacturingType.Troop
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind
                        is AIProductionDemandKind.ConstructionFacility
                            or AIProductionDemandKind.Shipyard
                            or AIProductionDemandKind.TrainingFacility
                )
            );
        }

        [Test]
        public void Generate_WithReservedHubAndEligibleWorld_TargetsEligibleWorldForExpansion()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.SpecialForcesTargetCountPerType = 0;
            int staticDefenseEnergy =
                game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit
                + game.Config.AI.Infrastructure.PlanetaryWeaponTargetCount;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet hub = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "training-hub",
                empire.InstanceID,
                energyCapacity: staticDefenseEnergy + 1
            );
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID,
                energyCapacity: staticDefenseEnergy
            );
            Planet expansionWorld = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "expansion-world",
                empire.InstanceID,
                energyCapacity: staticDefenseEnergy + 5,
                rawResourceNodes: 4
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            hub.SetPopularSupport(empire.InstanceID, 100);
            headquarters.SetPopularSupport(empire.InstanceID, 100);
            expansionWorld.SetPopularSupport(empire.InstanceID, 100);
            AITestSceneBuilder.AddProductionFacility(
                game,
                hub,
                "training-facility",
                BuildingType.TrainingFacility,
                ManufacturingType.Troop
            );
            Regiment queuedRegiment = new Regiment
            {
                InstanceID = "queued-regiment",
                OwnerInstanceID = empire.InstanceID,
                ConstructionCost = game.Config.AI.TickInterval,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            game.AttachNode(queuedRegiment, hub);
            hub.AddToManufacturingQueue(queuedRegiment);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);
            AIProductionDemand demand = demands.Single(item =>
                item.Kind == AIProductionDemandKind.TrainingFacility
            );
            double economyPressure = demands
                .Where(item =>
                    item.Kind is AIProductionDemandKind.Mine or AIProductionDemandKind.Refinery
                )
                .Max(item => item.Pressure);

            Assert.AreSame(expansionWorld, demand.DestinationPlanet);
            Assert.Greater(demand.Pressure, economyPressure);
        }

        [Test]
        public void Generate_WithPendingShipyard_DoesNotAddDuplicateDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "destination",
                empire.InstanceID
            );
            Building shipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "inbound-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            shipyard.OwnerInstanceID = empire.InstanceID;
            shipyard.Movement = new MovementState { TransitTicks = 10 };
            game.AttachNode(shipyard, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(demands.Any(demand => demand.Kind == AIProductionDemandKind.Shipyard));
        }

        [Test]
        public void Generate_WithPendingShipyardAtAnotherPlanet_AddsShipyardAtDemandPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet demandPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "demand-planet",
                empire.InstanceID
            );
            demandPlanet.IsHeadquarters = true;
            empire.HQInstanceID = demandPlanet.InstanceID;
            Planet pendingPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "pending-planet",
                empire.InstanceID
            );
            Building shipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "pending-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            shipyard.OwnerInstanceID = empire.InstanceID;
            shipyard.Movement = new MovementState { TransitTicks = 10 };
            game.AttachNode(shipyard, pendingPlanet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIProductionDemandKind.Shipyard);

            Assert.AreSame(demandPlanet, demand.DestinationPlanet);
        }

        [Test]
        public void Generate_WithUnlockedFacilityUpgrade_SelectsSlowestFacilityDeterministically()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-world",
                empire.InstanceID
            );
            Building second = AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "legacy-b",
                BuildingType.Shipyard,
                ManufacturingType.Ship,
                processRate: 4
            );
            Building first = AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "legacy-a",
                BuildingType.Shipyard,
                ManufacturingType.Ship,
                processRate: 4
            );
            first.ResearchOrder = 0;
            second.ResearchOrder = 0;
            first.Upgrades.Add("advanced-shipyard");
            second.Upgrades.Add("advanced-shipyard");
            AddUnlockedShipyardUpgrade(empire);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIProductionDemandKind.BuildingUpgrade);

            Assert.AreSame(planet, demand.DestinationPlanet);
            Assert.AreEqual(BuildingType.Shipyard, demand.BuildingType);
            Assert.AreSame(first, demand.BuildingToReplace);
            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        [Test]
        public void Generate_WithOnlyOneFacility_DoesNotAddUpgradeDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-world",
                empire.InstanceID
            );
            Building shipyard = AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "only-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship,
                processRate: 4
            );
            shipyard.Upgrades.Add("advanced-shipyard");
            AddUnlockedShipyardUpgrade(empire);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(item => item.Kind == AIProductionDemandKind.BuildingUpgrade)
            );
        }

        [Test]
        public void Generate_WithPendingUpgradeAtOnePlanet_StillUpgradesAnotherPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet pendingPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "pending-world",
                empire.InstanceID
            );
            Planet eligiblePlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "eligible-world",
                empire.InstanceID
            );
            for (int index = 0; index < 2; index++)
            {
                Building pendingFacility = AITestSceneBuilder.AddProductionFacility(
                    game,
                    pendingPlanet,
                    $"pending-legacy-{index}",
                    BuildingType.Shipyard,
                    ManufacturingType.Ship,
                    processRate: 4
                );
                pendingFacility.Upgrades.Add("advanced-shipyard");
                Building eligibleFacility = AITestSceneBuilder.AddProductionFacility(
                    game,
                    eligiblePlanet,
                    $"eligible-legacy-{index}",
                    BuildingType.Shipyard,
                    ManufacturingType.Ship,
                    processRate: 4
                );
                eligibleFacility.Upgrades.Add("advanced-shipyard");
            }
            Building pendingUpgrade = AITestSceneBuilder.CreateBuildingTemplate(
                "pending-upgrade",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            pendingUpgrade.OwnerInstanceID = empire.InstanceID;
            pendingUpgrade.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(pendingUpgrade, pendingPlanet);
            AddUnlockedShipyardUpgrade(empire);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> upgradeDemands = new AIProductionDemandGenerator()
                .Generate(context)
                .Where(item => item.Kind == AIProductionDemandKind.BuildingUpgrade)
                .ToList();

            Assert.AreEqual(1, upgradeDemands.Count);
            Assert.AreSame(eligiblePlanet, upgradeDemands[0].DestinationPlanet);
        }

        [Test]
        public void Generate_WithStaticDefenseDemand_AddsConstructionFacilityDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.SpecialForcesTargetCountPerType = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "settled-world",
                empire.InstanceID
            );
            planet.SetPopularSupport(empire.InstanceID, 100);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsTrue(
                demands.Any(demand => demand.Kind == AIProductionDemandKind.ConstructionFacility)
            );
        }

        [Test]
        public void Generate_WithShipDemandAndNoShipyard_AddsShipyardAtDemandPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "assembly-world",
                empire.InstanceID
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIProductionDemandKind.Shipyard);

            Assert.AreSame(planet, demand.DestinationPlanet);
            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        [Test]
        public void Generate_WithBusyShipyard_AddsShipyardAtExistingHub()
        {
            (GameRoot game, Faction empire, Planet hub, Planet _, Fleet _, CapitalShip ship) =
                CreateBusyShipyardScene();
            Starfighter queuedStarfighter = new Starfighter
            {
                InstanceID = "queued-starfighter",
                OwnerInstanceID = empire.InstanceID,
                ConstructionCost = game.Config.AI.TickInterval,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            game.AttachNode(queuedStarfighter, ship);
            hub.AddToManufacturingQueue(queuedStarfighter);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIProductionDemandKind.Shipyard);

            Assert.AreSame(hub, demand.DestinationPlanet);
        }

        [Test]
        public void Generate_WithAvailableCapacityAtStackedShipyard_DoesNotAddShipyardDemand()
        {
            (GameRoot game, Faction empire, Planet hub, Planet _, Fleet _, CapitalShip ship) =
                CreateBusyShipyardScene();
            AITestSceneBuilder.AddProductionFacility(
                game,
                hub,
                "second-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Starfighter queuedStarfighter = new Starfighter
            {
                InstanceID = "queued-starfighter",
                OwnerInstanceID = empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            game.AttachNode(queuedStarfighter, ship);
            hub.AddToManufacturingQueue(queuedStarfighter);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(demands.Any(demand => demand.Kind == AIProductionDemandKind.Shipyard));
        }

        [Test]
        public void Generate_WithIdleTrainingFacility_DoesNotAddTrainingFacilityDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.SpecialForcesTargetCountPerType = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet hub = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "training-hub",
                empire.InstanceID
            );
            Planet colony = AITestSceneBuilder.AddPlanet(game, system, "colony", empire.InstanceID);
            hub.SetPopularSupport(empire.InstanceID, 100);
            colony.SetPopularSupport(empire.InstanceID, 100);
            AITestSceneBuilder.AddProductionFacility(
                game,
                hub,
                "training-facility",
                BuildingType.TrainingFacility,
                ManufacturingType.Troop
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(item => item.Kind == AIProductionDemandKind.TrainingFacility)
            );
        }

        [Test]
        public void Generate_WithBuildingDemandAndNoConstructionCapacity_AddsConstructionFacility()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "resource-world",
                empire.InstanceID,
                rawResourceNodes: 4
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIProductionDemandKind.ConstructionFacility);

            Assert.AreSame(planet, demand.DestinationPlanet);
            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        [Test]
        public void Generate_WithUndefendedHeadquartersAndSurplus_AddsShieldAndWeaponDemands()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            game.Config.AI.Selection.MinimumMaintenanceHeadroomAfterProduction = 0;
            game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent = 0;
            AddMaintenanceCapacity(game, headquarters, 1);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            CollectionAssert.AreEquivalent(
                new[] { BuildingType.Defense, BuildingType.Weapon },
                demands
                    .Where(demand => demand.Kind == AIProductionDemandKind.PlanetaryDefense)
                    .Select(demand => demand.BuildingType)
            );
        }

        [Test]
        public void Generate_WithDefensiveSurplus_AddsCompletePlanetaryDefensePackage()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MinimumMaintenanceHeadroomAfterProduction = 0;
            game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "valuable-world",
                empire.InstanceID
            );
            planet.IsHeadquarters = true;
            empire.HQInstanceID = planet.InstanceID;
            planet.SetPopularSupport(empire.InstanceID, 100);
            AddMaintenanceCapacity(game, planet, 1);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            AIProductionDemand shieldDemand = demands.Single(demand =>
                demand.Kind == AIProductionDemandKind.PlanetaryDefense
                && demand.BuildingType == BuildingType.Defense
                && demand.DestinationPlanet == planet
            );
            AIProductionDemand weaponDemand = demands.Single(demand =>
                demand.Kind == AIProductionDemandKind.PlanetaryDefense
                && demand.BuildingType == BuildingType.Weapon
                && demand.DestinationPlanet == planet
            );
            AIProductionDemand garrisonDemand = demands.Single(demand =>
                demand.Kind == AIProductionDemandKind.GarrisonRegimentReserve
                && demand.DestinationPlanet == planet
            );
            Assert.AreEqual(
                game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit,
                shieldDemand.QuantityNeeded
            );
            Assert.AreEqual(1, weaponDemand.QuantityNeeded);
            Assert.AreEqual(
                game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount,
                garrisonDemand.QuantityNeeded
            );
        }

        [Test]
        public void Generate_WithUnthreatenedNonProductionPlanet_AddsStaticDefense()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MinimumMaintenanceHeadroomAfterProduction = 0;
            game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "non-production-world",
                empire.InstanceID
            );
            planet.SetPopularSupport(empire.InstanceID, 100);
            AddMaintenanceCapacity(game, planet, 1);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            CollectionAssert.AreEquivalent(
                new[] { BuildingType.Defense, BuildingType.Weapon },
                demands
                    .Where(demand =>
                        demand.Kind == AIProductionDemandKind.PlanetaryDefense
                        && demand.DestinationPlanet == planet
                    )
                    .Select(demand => demand.BuildingType)
            );
        }

        [Test]
        public void Generate_WithOneDefenseEnergySlot_PrioritizesPartialShieldNetwork()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MinimumMaintenanceHeadroomAfterProduction = 0;
            game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "constrained-world",
                empire.InstanceID,
                energyCapacity: 3
            );
            planet.IsHeadquarters = true;
            empire.HQInstanceID = planet.InstanceID;
            planet.SetPopularSupport(empire.InstanceID, 100);
            AddMaintenanceCapacity(game, planet, 1);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            AIProductionDemand shieldDemand = demands.Single(demand =>
                demand.Kind == AIProductionDemandKind.PlanetaryDefense
                && demand.BuildingType == BuildingType.Defense
                && demand.DestinationPlanet == planet
            );
            Assert.AreEqual(1, shieldDemand.QuantityNeeded);
            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIProductionDemandKind.PlanetaryDefense
                    && demand.BuildingType == BuildingType.Weapon
                    && demand.DestinationPlanet == planet
                )
            );
        }

        [Test]
        public void Generate_WithStaticDefenseCoverageAndSurplusEnergy_AddsConfiguredWeaponBatch()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.PlanetaryDefenseSurplusBatchSize = 2;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "defended-world",
                empire.InstanceID,
                energyCapacity: 5
            );
            for (
                int index = 0;
                index < game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit;
                index++
            )
                AddShield(game, planet, $"shield-{index}", empire.InstanceID, 40);

            Building weapon = AITestSceneBuilder.CreateBuildingTemplate(
                "weapon",
                BuildingType.Weapon
            );
            weapon.OwnerInstanceID = empire.InstanceID;
            game.AttachNode(weapon, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.PlanetaryDefense
                    && item.BuildingType == BuildingType.Weapon
                    && item.DestinationPlanet == planet
                );

            Assert.AreEqual(2, demand.QuantityNeeded);
        }

        [Test]
        public void Generate_WithInboundThreat_RaisesThreatenedPlanetDefensePressure()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.Selection.MinimumMaintenanceHeadroomAfterProduction = 0;
            game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet valuablePlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "valuable-world",
                empire.InstanceID,
                rawResourceNodes: 4
            );
            valuablePlanet.SetPopularSupport(empire.InstanceID, 100);
            AITestSceneBuilder.AddProductionFacility(
                game,
                valuablePlanet,
                "valuable-construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Planet threatenedPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "threatened-world",
                empire.InstanceID
            );
            threatenedPlanet.SetPopularSupport(empire.InstanceID, 100);
            AddMaintenanceCapacity(game, valuablePlanet, 1);
            Fleet hostileFleet = EntityFactory.CreateFleet("hostile-fleet", rebels.InstanceID);
            hostileFleet.RoleType = FleetRoleType.Battle;
            hostileFleet.Movement = new MovementState { TransitTicks = 10 };
            game.AttachNode(hostileFleet, threatenedPlanet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("hostile-ship", rebels.InstanceID),
                hostileFleet
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            double valuablePressure = demands
                .Single(demand =>
                    demand.Kind == AIProductionDemandKind.PlanetaryDefense
                    && demand.BuildingType == BuildingType.Defense
                    && demand.DestinationPlanet == valuablePlanet
                )
                .Pressure;
            double threatenedPressure = demands
                .Single(demand =>
                    demand.Kind == AIProductionDemandKind.PlanetaryDefense
                    && demand.BuildingType == BuildingType.Defense
                    && demand.DestinationPlanet == threatenedPlanet
                )
                .Pressure;
            Assert.Greater(threatenedPressure, valuablePressure);
        }

        [Test]
        public void Generate_WithInfrastructureStarfighterDeficit_AddsReserveDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.SpecialForcesTargetCountPerType = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "defense-world",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Starfighter complete = AITestSceneBuilder.CreateStarfighter(
                "complete-fighter",
                empire.InstanceID
            );
            Starfighter building = AITestSceneBuilder.CreateStarfighter(
                "building-fighter",
                empire.InstanceID
            );
            building.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(complete, planet);
            game.AttachNode(building, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.PlanetaryStarfighterReserve
                    && item.DestinationPlanet == planet
                );

            Assert.AreEqual(
                game.Config.AI.NonCapitalSummary.StarfighterRequirementInfrastructure - 2,
                demand.QuantityNeeded
            );
            Assert.IsTrue(demand.UsesDefensiveReserve);
        }

        [Test]
        public void Generate_WithCompleteInfrastructureStarfighterReserve_SuppressesDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.SpecialForcesTargetCountPerType = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "defense-world",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            for (
                int index = 0;
                index < game.Config.AI.NonCapitalSummary.StarfighterRequirementInfrastructure;
                index++
            )
            {
                game.AttachNode(
                    AITestSceneBuilder.CreateStarfighter(
                        $"planetary-fighter-{index}",
                        empire.InstanceID
                    ),
                    planet
                );
            }
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(item =>
                    item.Kind == AIProductionDemandKind.PlanetaryStarfighterReserve
                    && item.DestinationPlanet == planet
                )
            );
        }

        [Test]
        public void Generate_WithOrdinaryUnthreatenedPlanet_SuppressesStarfighterDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.SpecialForcesTargetCountPerType = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "ordinary-world",
                empire.InstanceID
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIProductionDemandKind.PlanetaryStarfighterReserve
                    && demand.DestinationPlanet == planet
                )
            );
        }

        [Test]
        public void Generate_WithThreatenedOrdinaryPlanet_AddsStrengthBasedStarfighterDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.Infrastructure.SpecialForcesTargetCountPerType = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "threatened-world",
                empire.InstanceID
            );
            Fleet hostileFleet = EntityFactory.CreateFleet("hostile-fleet", rebels.InstanceID);
            CapitalShip hostileShip = AITestSceneBuilder.CreateCapitalShip(
                "hostile-ship",
                rebels.InstanceID,
                combatStrength: 100
            );
            game.AttachNode(hostileFleet, planet);
            game.AttachNode(hostileShip, hostileFleet);
            Starfighter defender = AITestSceneBuilder.CreateStarfighter(
                "defender-template",
                empire.InstanceID,
                laserCannon: 10
            );
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(defender),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.PlanetaryStarfighterReserve
                    && item.DestinationPlanet == planet
                );

            int requiredDefenseStrength = context.Assessment.GetRequiredPlanetDefenseStrength(
                planet
            );
            int expectedThreatReinforcement = (requiredDefenseStrength + 9) / 10;
            Assert.AreEqual(expectedThreatReinforcement, demand.QuantityNeeded);
        }

        [Test]
        public void Generate_WithHeadquartersAndInfrastructure_RaisesHeadquartersStarfighterPressure()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.SpecialForcesTargetCountPerType = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet infrastructure = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "infrastructure-world",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                infrastructure,
                "infrastructure-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID,
                rawResourceNodes: 4
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            AITestSceneBuilder.AddProductionFacility(
                game,
                headquarters,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            AIProductionDemand infrastructureDemand = demands.Single(item =>
                item.Kind == AIProductionDemandKind.PlanetaryStarfighterReserve
                && item.DestinationPlanet == infrastructure
            );
            AIProductionDemand headquartersDemand = demands.Single(item =>
                item.Kind == AIProductionDemandKind.PlanetaryStarfighterReserve
                && item.DestinationPlanet == headquarters
            );

            Assert.Greater(headquartersDemand.Pressure, infrastructureDemand.Pressure);
        }

        [Test]
        public void Generate_WithFleetCapacityGaps_AddsFleetReinforcementDemands()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.Infrastructure.StarfighterParentFillPercent = 100;
            game.Config.AI.Infrastructure.AssaultRegimentLoadPercent = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = enemy.InstanceID,
            };
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                empire.InstanceID,
                combatStrength: 10,
                regimentCapacity: 1,
                starfighterCapacity: 2
            );
            fleet.AddChild(ship);
            ship.SetParent(fleet);
            game.AttachNode(fleet, owned);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsTrue(
                demands.Any(demand => demand.Kind == AIProductionDemandKind.FleetStarfighter)
            );
            Assert.IsTrue(
                demands.Any(demand => demand.Kind == AIProductionDemandKind.FleetRegiment)
            );
        }

        [Test]
        public void Generate_WithAttackFleetReadinessGap_PreservesPressureAboveStandardRange()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.Infrastructure.FleetStarfighterDemandPercent = 90;
            game.Config.AI.Infrastructure.StarfighterParentFillPercent = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = enemy.InstanceID,
            };
            game.AttachNode(fleet, owned);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "ship",
                    empire.InstanceID,
                    combatStrength: 10,
                    regimentCapacity: 0,
                    starfighterCapacity: 2
                ),
                fleet
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetStarfighter
                    && item.DestinationFleet == fleet
                );

            Assert.Greater(demand.Pressure, 100);
        }

        [Test]
        public void Generate_WithMovingFleet_DoesNotAddFleetReinforcementDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = enemy.InstanceID,
            };
            fleet.Movement = new MovementState { TransitTicks = 10 };
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                empire.InstanceID,
                combatStrength: 10,
                regimentCapacity: 1,
                starfighterCapacity: 2
            );
            game.AttachNode(fleet, owned);
            game.AttachNode(ship, fleet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(demands.Any(demand => demand.DestinationFleet == fleet));
        }

        [Test]
        public void Generate_WithActiveAttackAndIdleUnderstrengthFleet_AddsAssemblyDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            AddAttackFleet(game, owned, enemy, empire.InstanceID, regimentCapacity: 1);
            Fleet assemblyFleet = AddIdleBattleFleet(
                game,
                owned,
                empire.InstanceID,
                "assembly-fleet"
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsTrue(
                demands.Any(demand =>
                    demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && demand.DestinationFleet == assemblyFleet
                )
            );
        }

        [Test]
        public void Generate_WithMultipleIdleUnderstrengthFleets_AddsAssemblyDemandForEachFleet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet firstFleet = AddIdleBattleFleet(game, owned, empire.InstanceID, "fleet-1");
            Fleet secondFleet = AddIdleBattleFleet(game, owned, empire.InstanceID, "fleet-2");
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            CollectionAssert.AreEquivalent(
                new[] { firstFleet, secondFleet },
                demands
                    .Where(demand =>
                        demand.Kind == AIProductionDemandKind.FleetCapitalShip
                        && (
                            demand.DestinationFleet == firstFleet
                            || demand.DestinationFleet == secondFleet
                        )
                    )
                    .Select(demand => demand.DestinationFleet)
            );
        }

        [Test]
        public void Generate_WithMultipleEnemyPlanets_BuildsForCurrentTargetResistance()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 100;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet firstEnemy = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "enemy-1",
                rebels.InstanceID
            );
            Planet secondEnemy = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "enemy-2",
                rebels.InstanceID
            );
            firstEnemy.SetPopularSupport(
                empire.InstanceID,
                game.Config.AI.Garrison.SupportThreshold
            );
            secondEnemy.SetPopularSupport(
                empire.InstanceID,
                game.Config.AI.Garrison.SupportThreshold
            );
            Fleet firstDefense = EntityFactory.CreateFleet("defense-1", rebels.InstanceID);
            Fleet secondDefense = EntityFactory.CreateFleet("defense-2", rebels.InstanceID);
            game.AttachNode(firstDefense, firstEnemy);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "defense-ship-1",
                    rebels.InstanceID,
                    combatStrength: 200
                ),
                firstDefense
            );
            game.AttachNode(secondDefense, secondEnemy);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "defense-ship-2",
                    rebels.InstanceID,
                    combatStrength: 300
                ),
                secondDefense
            );
            AITestSceneBuilder.RevealPlanet(game, empire, firstEnemy);
            AITestSceneBuilder.RevealPlanet(game, empire, secondEnemy);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Building,
                TargetPlanetId = firstEnemy.InstanceID,
            };
            game.AttachNode(fleet, owned);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "attacker",
                    empire.InstanceID,
                    combatStrength: 100,
                    regimentCapacity: 1,
                    starfighterCapacity: 0
                ),
                fleet
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(100, demand.QuantityNeeded);
            Assert.AreEqual(AICapitalShipProductionRole.General, demand.CapitalShipRole);
        }

        [Test]
        public void Generate_WithMultipleAttackFleets_AddsDemandForEachCampaign()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.AI.Infrastructure.AssaultRegimentLoadPercent = 0;
            game.Config.AI.Infrastructure.StarfighterParentFillPercent = 0;
            PlanetSector establishedSystem = AITestSceneBuilder.AddSector(
                game,
                "established-system"
            );
            Planet establishedOwned = AITestSceneBuilder.AddPlanet(
                game,
                establishedSystem,
                "established-owned",
                empire.InstanceID
            );
            AITestSceneBuilder.AddPlanet(
                game,
                establishedSystem,
                "established-owned-2",
                empire.InstanceID
            );
            Planet establishedEnemy = AITestSceneBuilder.AddPlanet(
                game,
                establishedSystem,
                "established-enemy",
                rebels.InstanceID
            );
            PlanetSector remoteSystem = AITestSceneBuilder.AddSector(game, "remote-system");
            Planet remoteEnemy = AITestSceneBuilder.AddPlanet(
                game,
                remoteSystem,
                "remote-enemy",
                rebels.InstanceID
            );
            AITestSceneBuilder.RevealPlanet(game, empire, establishedEnemy);
            AITestSceneBuilder.RevealPlanet(game, empire, remoteEnemy);

            Fleet establishedFleet = EntityFactory.CreateFleet(
                "established-fleet",
                empire.InstanceID
            );
            establishedFleet.RoleType = FleetRoleType.Battle;
            establishedFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Building,
                TargetPlanetId = establishedEnemy.InstanceID,
            };
            game.AttachNode(establishedFleet, establishedOwned);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "established-ship",
                    empire.InstanceID,
                    combatStrength: 100,
                    regimentCapacity: 0,
                    starfighterCapacity: 0
                ),
                establishedFleet
            );

            Fleet remoteFleet = EntityFactory.CreateFleet("remote-fleet", empire.InstanceID);
            remoteFleet.RoleType = FleetRoleType.Battle;
            remoteFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Building,
                TargetPlanetId = remoteEnemy.InstanceID,
            };
            game.AttachNode(remoteFleet, establishedOwned);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "remote-ship",
                    empire.InstanceID,
                    combatStrength: 500,
                    regimentCapacity: 1,
                    starfighterCapacity: 0
                ),
                remoteFleet
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            List<AIProductionDemand> reinforcementDemands = demands
                .Where(demand =>
                    demand.Kind
                        is AIProductionDemandKind.FleetCapitalShip
                            or AIProductionDemandKind.FleetRegiment
                )
                .ToList();

            Assert.IsTrue(
                reinforcementDemands.Any(demand => demand.DestinationFleet == remoteFleet)
            );
            Assert.IsTrue(
                reinforcementDemands.Any(demand => demand.DestinationFleet == establishedFleet)
            );
        }

        [Test]
        public void Generate_WithAttackRegimentStrengthGap_AddsFleetRegimentDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 0;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense = 100;
            game.Config.AI.Infrastructure.AssaultRegimentLoadPercent = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            enemy.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("defender", rebels.InstanceID, defenseRating: 20),
                enemy
            );
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Fleet fleet = AddAttackFleet(
                game,
                owned,
                enemy,
                empire.InstanceID,
                regimentCapacity: 2
            );
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("attacker", empire.InstanceID, attackRating: 5),
                fleet.GetChildren<CapitalShip>().Single()
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetRegiment
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        [Test]
        public void Generate_WithAttackRegimentStrengthGapAndFullCapacity_AddsCapitalShipDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 0;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense = 100;
            game.Config.AI.Infrastructure.AssaultRegimentLoadPercent = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            enemy.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("defender", rebels.InstanceID, defenseRating: 20),
                enemy
            );
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Fleet fleet = AddAttackFleet(
                game,
                owned,
                enemy,
                empire.InstanceID,
                regimentCapacity: 1
            );
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("attacker", empire.InstanceID, attackRating: 5),
                fleet.GetChildren<CapitalShip>().Single()
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(1, demand.QuantityNeeded);
            Assert.AreEqual(AICapitalShipProductionRole.TroopTransport, demand.CapitalShipRole);
        }

        [Test]
        public void Generate_WithWeakIdleBattleFleet_AddsCapitalShipDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                empire.InstanceID,
                combatStrength: 100,
                regimentCapacity: 1,
                starfighterCapacity: 0
            );
            fleet.AddChild(ship);
            ship.SetParent(fleet);
            game.AttachNode(fleet, owned);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(
                game.Config.AI.FleetDeployment.MinimumAttackStrength
                    - ship.GetPrimaryWeaponStrength(),
                demand.QuantityNeeded
            );
            Assert.AreEqual(AICapitalShipProductionRole.General, demand.CapitalShipRole);
        }

        [Test]
        public void Generate_WithShieldedAttackTargetAndInsufficientBombardment_AddsCapitalShipDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            enemy.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AddShield(game, enemy, "shield-1", rebels.InstanceID, 10);
            AddShield(game, enemy, "shield-2", rebels.InstanceID, 10);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = enemy.InstanceID,
            };
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                empire.InstanceID,
                combatStrength: 100,
                regimentCapacity: 1,
                starfighterCapacity: 0
            );
            ship.Bombardment = 10;
            game.AttachNode(fleet, owned);
            game.AttachNode(ship, fleet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(11, demand.QuantityNeeded);
            Assert.AreEqual(AICapitalShipProductionRole.Bombardment, demand.CapitalShipRole);
        }

        [Test]
        public void Generate_WithCombatAndBombardmentGaps_PrioritizesBombardmentShip()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            enemy.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AddShield(game, enemy, "shield-1", rebels.InstanceID, 10);
            AddShield(game, enemy, "shield-2", rebels.InstanceID, 10);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Building,
                TargetPlanetId = enemy.InstanceID,
            };
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                empire.InstanceID,
                combatStrength: 100,
                regimentCapacity: 1,
                starfighterCapacity: 0
            );
            ship.Bombardment = 10;
            game.AttachNode(fleet, owned);
            game.AttachNode(ship, fleet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(AICapitalShipProductionRole.Bombardment, demand.CapitalShipRole);
        }

        [Test]
        public void Generate_WithReadyAttackFleetAndUnlockedGravityWell_AddsInterdictionDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            enemy.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = enemy.InstanceID,
            };
            game.AttachNode(fleet, owned);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "existing",
                    empire.InstanceID,
                    combatStrength: 100,
                    regimentCapacity: 1,
                    starfighterCapacity: 0
                ),
                fleet
            );
            CapitalShip interdictor = AITestSceneBuilder.CreateCapitalShip(
                "interdictor-template",
                empire.InstanceID
            );
            interdictor.TypeID = "interdictor";
            interdictor.HasGravityWell = true;
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(interdictor),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(1, demand.QuantityNeeded);
            Assert.AreEqual(AICapitalShipProductionRole.Interdiction, demand.CapitalShipRole);
        }

        [Test]
        public void Generate_WithReadyIdleBattleFleetAndUnlockedGravityWell_AddsInterdictionDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out _);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(fleet, owned);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "existing",
                    empire.InstanceID,
                    combatStrength: 100,
                    regimentCapacity: 0,
                    starfighterCapacity: 0
                ),
                fleet
            );
            CapitalShip interdictor = AITestSceneBuilder.CreateCapitalShip(
                "interdictor-template",
                empire.InstanceID
            );
            interdictor.TypeID = "interdictor";
            interdictor.HasGravityWell = true;
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(interdictor),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(1, demand.QuantityNeeded);
            Assert.AreEqual(AICapitalShipProductionRole.Interdiction, demand.CapitalShipRole);
        }

        [TestCase(ManufacturingStatus.Building)]
        [TestCase(ManufacturingStatus.Complete)]
        public void Generate_WithCommittedGravityWellShip_DoesNotAddInterdictionDemand(
            ManufacturingStatus manufacturingStatus
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            enemy.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = enemy.InstanceID,
            };
            game.AttachNode(fleet, owned);
            CapitalShip interdictor = AITestSceneBuilder.CreateCapitalShip(
                "interdictor",
                empire.InstanceID,
                combatStrength: 100,
                regimentCapacity: 1,
                starfighterCapacity: 0
            );
            interdictor.HasGravityWell = true;
            interdictor.ManufacturingStatus = manufacturingStatus;
            game.AttachNode(interdictor, fleet);
            CapitalShip template = AITestSceneBuilder.CreateCapitalShip(
                "interdictor-template",
                empire.InstanceID
            );
            template.TypeID = "interdictor";
            template.HasGravityWell = true;
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(template),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && demand.DestinationFleet == fleet
                )
            );
        }

        [Test]
        public void Generate_WithUnderstrengthHeadquartersDefenseFleet_AddsCapitalShipDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 125;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Fleet hostileFleet = EntityFactory.CreateFleet("hostile-fleet", rebels.InstanceID);
            hostileFleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(hostileFleet, headquarters);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "hostile-ship",
                    rebels.InstanceID,
                    combatStrength: 2000
                ),
                hostileFleet
            );
            Planet fleetPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "fleet-world",
                empire.InstanceID
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Defend,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = headquarters.InstanceID,
            };
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                empire.InstanceID,
                combatStrength: 1000,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            game.AttachNode(fleet, fleetPlanet);
            game.AttachNode(ship, fleet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(1500, demand.QuantityNeeded);
        }

        [Test]
        public void Generate_WithInboundCapitalShipFillingCombatNeed_DoesNotAddCapitalShipDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip presentShip = AITestSceneBuilder.CreateCapitalShip(
                "present-ship",
                empire.InstanceID,
                combatStrength: 100,
                regimentCapacity: 1
            );
            CapitalShip inboundShip = AITestSceneBuilder.CreateCapitalShip(
                "inbound-ship",
                empire.InstanceID,
                combatStrength: 400
            );
            inboundShip.Movement = new MovementState { TransitTicks = 10 };
            fleet.AddChild(presentShip);
            presentShip.SetParent(fleet);
            fleet.AddChild(inboundShip);
            inboundShip.SetParent(fleet);
            game.AttachNode(fleet, owned);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && demand.DestinationFleet == fleet
                )
            );
        }

        [TestCase(ManufacturingStatus.Building)]
        [TestCase(ManufacturingStatus.Complete)]
        public void Generate_WithCommittedCapitalShipFillingCombatNeed_DoesNotAddCapitalShipDemand(
            ManufacturingStatus manufacturingStatus
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "committed-ship",
                empire.InstanceID,
                combatStrength: 500,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            ship.ManufacturingStatus = manufacturingStatus;
            game.AttachNode(fleet, owned);
            game.AttachNode(ship, fleet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && demand.DestinationFleet == fleet
                )
            );
        }

        [Test]
        public void Generate_WithColonizationFleetMissingRegimentCapacity_AddsCapitalShipDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Colonize,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = target.InstanceID,
            };
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                empire.InstanceID,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            fleet.AddChild(ship);
            ship.SetParent(fleet);
            game.AttachNode(fleet, owned);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(
                game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount,
                demand.QuantityNeeded
            );
        }

        [Test]
        public void Generate_WithColonizationFleetCapacity_AddsOnlyRequiredColonizationRegiment()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.AI.Infrastructure.AssaultRegimentLoadPercent = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Colonize,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = target.InstanceID,
            };
            game.AttachNode(fleet, owned);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "ship",
                    empire.InstanceID,
                    regimentCapacity: 5,
                    starfighterCapacity: 0
                ),
                fleet
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetRegiment
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(
                game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount,
                demand.QuantityNeeded
            );
        }

        [Test]
        public void Generate_WithDefenseFleetCapacity_DoesNotAddFleetRegimentDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", empire.InstanceID);
            Fleet hostileFleet = EntityFactory.CreateFleet("hostile-fleet", rebels.InstanceID);
            hostileFleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(hostileFleet, target);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "hostile-ship",
                    rebels.InstanceID,
                    combatStrength: 500
                ),
                hostileFleet
            );
            Fleet defenseFleet = EntityFactory.CreateFleet("defense-fleet", empire.InstanceID);
            defenseFleet.RoleType = FleetRoleType.Battle;
            defenseFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Defend,
                Status = FleetOrderStatus.Building,
                TargetPlanetId = target.InstanceID,
            };
            game.AttachNode(defenseFleet, target);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "defense-ship",
                    empire.InstanceID,
                    combatStrength: 100,
                    regimentCapacity: 5,
                    starfighterCapacity: 0
                ),
                defenseFleet
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(item =>
                    item.Kind == AIProductionDemandKind.FleetRegiment
                    && item.DestinationFleet == defenseFleet
                )
            );
        }

        [Test]
        public void Generate_WithSpecialForcesDeficit_AddsDemandForUnlockedType()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "training-world",
                empire.InstanceID
            );
            SpecialForces template = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID
            );
            empire.ResearchQueue[ManufacturingType.Troop] = new List<Technology>
            {
                new Technology(template),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIProductionDemandKind.SpecialForces);

            Assert.AreEqual("commandos", demand.ProductTypeId);
            Assert.AreSame(planet, demand.DestinationPlanet);
            Assert.AreEqual(
                game.Config.AI.Infrastructure.SpecialForcesTargetCountPerType,
                demand.QuantityNeeded
            );
        }

        [Test]
        public void Generate_WithTooFewCommittedBattleFleets_AddsFleetSeedDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-world",
                empire.InstanceID
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIProductionDemandKind.FleetSeedCapitalShip);

            Assert.AreSame(planet, demand.DestinationPlanet);
            Assert.AreEqual(
                game.Config.AI.FleetDeployment.MinimumBattleFleetCount,
                demand.QuantityNeeded
            );
        }

        [Test]
        public void Generate_WithExpandingTerritory_ScalesFleetSeedDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.PlanetsPerBattleFleet = 2;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            for (int index = 0; index < 7; index++)
            {
                AITestSceneBuilder.AddPlanet(game, system, $"owned-{index}", empire.InstanceID);
            }
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIProductionDemandKind.FleetSeedCapitalShip);

            Assert.AreEqual(4, demand.QuantityNeeded);
        }

        [Test]
        public void Generate_WithFleetRoleCapacityDeficit_AddsFleetSeedDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 4;
            game.Config.AI.FleetDeployment.PlanetsPerBattleFleet = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            for (int index = 0; index < 3; index++)
            {
                Fleet fleet = EntityFactory.CreateFleet($"fleet-{index}", empire.InstanceID);
                fleet.RoleType = FleetRoleType.Battle;
                game.AttachNode(fleet, headquarters);
                game.AttachNode(
                    AITestSceneBuilder.CreateCapitalShip($"ship-{index}", empire.InstanceID),
                    fleet
                );
            }
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIProductionDemandKind.FleetSeedCapitalShip);

            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        [Test]
        public void Generate_WithUnguardedHeadquartersAndFleetRoleDeficit_AddsHeadquartersFleetSeedDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Planet fleetPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "fleet-world",
                empire.InstanceID
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(fleet, fleetPlanet);
            game.AttachNode(AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID), fleet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIProductionDemandKind.FleetSeedCapitalShip);

            Assert.AreSame(headquarters, demand.DestinationPlanet);
            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        [Test]
        public void Generate_WithSatisfiedFleetTargetAndUnguardedHeadquarters_AddsFleetSeedDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Planet fleetPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "fleet-world",
                empire.InstanceID
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(fleet, fleetPlanet);
            game.AttachNode(AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID), fleet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsTrue(
                demands.Any(demand => demand.Kind == AIProductionDemandKind.FleetSeedCapitalShip)
            );
        }

        [Test]
        public void Generate_WithUnderGarrisonedPlanet_AddsRequiredGarrisonDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "under-garrisoned",
                empire.InstanceID
            );
            planet.SetPopularSupport(empire.InstanceID, 20);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.GarrisonRegimentReserve
                    && item.DestinationPlanet == planet
                );

            Assert.AreEqual(
                game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount,
                demand.QuantityNeeded
            );
        }

        [Test]
        public void Generate_WithSatisfiedGarrisonRequirement_DoesNotAddGarrisonDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "garrisoned",
                empire.InstanceID
            );
            planet.SetPopularSupport(empire.InstanceID, 20);
            for (
                int index = 0;
                index < game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount;
                index++
            )
            {
                game.AttachNode(
                    AITestSceneBuilder.CreateRegiment($"regiment-{index}", empire.InstanceID),
                    planet
                );
            }
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(item =>
                    item.Kind == AIProductionDemandKind.GarrisonRegimentReserve
                    && item.DestinationPlanet == planet
                )
            );
        }

        private static void AddShield(
            GameRoot game,
            Planet planet,
            string instanceId,
            string ownerInstanceId,
            int strength
        )
        {
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                instanceId,
                BuildingType.Defense
            );
            shield.OwnerInstanceID = ownerInstanceId;
            shield.DefenseFacilityClass = DefenseFacilityClass.Shield;
            shield.ShieldStrength = strength;
            game.AttachNode(shield, planet);
        }

        private static void AddMaintenanceCapacity(GameRoot game, Planet planet, int count)
        {
            planet.NumRawResourceNodes += count;
            for (int index = 0; index < count; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"maintenance-mine-{index}",
                    BuildingType.Mine,
                    ManufacturingType.None
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"maintenance-refinery-{index}",
                    BuildingType.Refinery,
                    ManufacturingType.None
                );
            }
        }

        private static Building AddUnlockedShipyardUpgrade(Faction faction)
        {
            Building advancedShipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "advanced-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            advancedShipyard.TypeID = "advanced-shipyard";
            advancedShipyard.ProcessRate = 2;
            advancedShipyard.ResearchOrder = 5;
            faction.ResearchQueue[ManufacturingType.Building] = new List<Technology>
            {
                new Technology(advancedShipyard),
            };
            return advancedShipyard;
        }

        private static (
            GameRoot game,
            Faction empire,
            Planet hub,
            Planet destination,
            Fleet fleet,
            CapitalShip ship
        ) CreateBusyShipyardScene()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet hub = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-hub",
                empire.InstanceID,
                positionX: 0
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                hub,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Planet destination = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "fleet-world",
                empire.InstanceID,
                positionX: 100
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                empire.InstanceID,
                combatStrength: 100,
                regimentCapacity: 0,
                starfighterCapacity: 1
            );
            game.AttachNode(fleet, destination);
            game.AttachNode(ship, fleet);
            return (game, empire, hub, destination, fleet, ship);
        }

        private static Fleet AddAttackFleet(
            GameRoot game,
            Planet location,
            Planet target,
            string ownerInstanceId,
            int regimentCapacity
        )
        {
            Fleet fleet = EntityFactory.CreateFleet("fleet", ownerInstanceId);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = target.InstanceID,
            };
            game.AttachNode(fleet, location);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "ship",
                    ownerInstanceId,
                    regimentCapacity: regimentCapacity
                ),
                fleet
            );
            return fleet;
        }

        /// <summary>
        /// Adds an understrength idle battle fleet to the requested planet.
        /// </summary>
        /// <param name="game">The game that owns the fleet.</param>
        /// <param name="location">The planet where the fleet is stationed.</param>
        /// <param name="ownerInstanceId">The owning faction identifier.</param>
        /// <param name="fleetId">The fleet identifier.</param>
        /// <returns>The newly created battle fleet.</returns>
        private static Fleet AddIdleBattleFleet(
            GameRoot game,
            Planet location,
            string ownerInstanceId,
            string fleetId
        )
        {
            Fleet fleet = EntityFactory.CreateFleet(fleetId, ownerInstanceId);
            fleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(fleet, location);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    $"{fleetId}-ship",
                    ownerInstanceId,
                    combatStrength: 100,
                    regimentCapacity: 1
                ),
                fleet
            );
            return fleet;
        }
    }
}
