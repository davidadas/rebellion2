using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Tests.AI.Helpers;
using Rebellion.Util.Mathematics;

namespace Rebellion.Tests.AI.Demands
{
    public sealed class AIProductionDemandGeneratorTests
    {
        [Test]
        public void BuildDemands_WithClaimedUncolonizedPlanet_DoesNotAddColonyDemand()
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
                .BuildDemands(context)
                .Single(item => item.Kind == AIProductionDemandKind.Colony);

            Assert.AreSame(planet, demand.DestinationPlanet);
            Assert.AreEqual(BuildingType.Mine, demand.BuildingType);
            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        [Test]
        public void BuildDemands_WithMineCapacityAhead_UsesRefineryAsColonyFoundingFacility()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet establishedPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "established-world",
                empire.InstanceID,
                rawResourceNodes: 2
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                establishedPlanet,
                "existing-mine",
                BuildingType.Mine,
                ManufacturingType.None
            );
            Planet claimedPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "claimed-world",
                empire.InstanceID,
                rawResourceNodes: 2
            );
            claimedPlanet.IsColonized = false;
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("garrison", empire.InstanceID),
                claimedPlanet
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .BuildDemands(context)
                .Single(item => item.Kind == AIProductionDemandKind.Colony);

            Assert.AreEqual(BuildingType.Refinery, demand.BuildingType);
        }

        [Test]
        public void BuildDemands_WithMultipleClaimedPlanets_BalancesFoundingFacilities()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            foreach (string planetId in new[] { "claimed-one", "claimed-two" })
            {
                Planet planet = AITestSceneBuilder.AddPlanet(
                    game,
                    system,
                    planetId,
                    empire.InstanceID,
                    rawResourceNodes: 2
                );
                planet.IsColonized = false;
                game.AttachNode(
                    AITestSceneBuilder.CreateRegiment($"{planetId}-garrison", empire.InstanceID),
                    planet
                );
            }
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator()
                .BuildDemands(context)
                .Where(item => item.Kind == AIProductionDemandKind.Colony)
                .ToList();

            CollectionAssert.AreEquivalent(
                new[] { BuildingType.Mine, BuildingType.Refinery },
                demands.Select(demand => demand.BuildingType)
            );
        }

        [Test]
        public void BuildDemands_WithAbandonedUncolonizedPlanet_DoesNotAddColonyDemand()
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

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsFalse(demands.Any(item => item.Kind == AIProductionDemandKind.Colony));
        }

        [Test]
        public void BuildDemands_WithUnminedResourcesAndSufficientEconomy_DoesNotAddEconomyDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Selection.MaintenanceHeadroomTarget = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "resource-world",
                empire.InstanceID,
                energyCapacity: 20,
                rawResourceNodes: 4
            );
            AddResourceFacilities(game, planet, 2);
            empire.RefinedMaterialStockpile = 100;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind is AIProductionDemandKind.Mine or AIProductionDemandKind.Refinery
                )
            );
        }

        [Test]
        public void BuildDemands_WithProjectedRefinedMaterialsNearReserve_AddsEconomyDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            empire.Settings.RefinementMultiplier = 10;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "resource-world",
                empire.InstanceID,
                energyCapacity: 20,
                rawResourceNodes: 4
            );
            AddResourceFacilities(game, planet, 2);
            empire.RefinedMaterialStockpile = 7;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsTrue(demands.Any(demand => demand.Kind == AIProductionDemandKind.Mine));
            Assert.IsTrue(demands.Any(demand => demand.Kind == AIProductionDemandKind.Refinery));
        }

        [Test]
        public void BuildDemands_WithPendingManufacturingMaterialRequest_AddsEconomyDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(
                game,
                system,
                "resource-world",
                empire.InstanceID,
                energyCapacity: 20,
                rawResourceNodes: 4
            );
            empire.PendingRefinedMaterialFacilityIDs.Add("waiting-production-facility");
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsTrue(demands.Any(demand => demand.Kind == AIProductionDemandKind.Mine));
            Assert.IsTrue(demands.Any(demand => demand.Kind == AIProductionDemandKind.Refinery));
        }

        [Test]
        public void BuildDemands_WithMultipleEconomyDestinations_RequestsOneBuildingPerDestination()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            for (int index = 0; index < 3; index++)
            {
                Planet planet = AITestSceneBuilder.AddPlanet(
                    game,
                    system,
                    $"resource-world-{index}",
                    empire.InstanceID,
                    energyCapacity: 20,
                    rawResourceNodes: 4
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"construction-yard-{index}",
                    BuildingType.ConstructionFacility,
                    ManufacturingType.Building
                );
            }
            empire.PendingRefinedMaterialFacilityIDs.Add("waiting-production-facility");

            List<AIProductionDemand> economyDemands = new AIProductionDemandGenerator()
                .BuildDemands(AITestSceneBuilder.CreateContext(game, empire))
                .Where(demand =>
                    demand.Kind is AIProductionDemandKind.Mine or AIProductionDemandKind.Refinery
                )
                .ToList();

            Assert.Greater(economyDemands.Count, 1);
            Assert.IsTrue(economyDemands.All(demand => demand.QuantityNeeded == 1));
        }

        [Test]
        public void BuildDemands_WithOwnedNonOperationalOuterRimPlanet_AddsEconomyDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "outer-rim");
            sector.SectorType = PlanetSectorType.OuterRim;
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "claimed-world",
                empire.InstanceID,
                energyCapacity: 10,
                rawResourceNodes: 5
            );
            planet.IsColonized = false;
            empire.PendingRefinedMaterialFacilityIDs.Add("waiting-production-facility");

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                AITestSceneBuilder.CreateContext(game, empire)
            );

            Assert.IsTrue(
                demands.Any(demand =>
                    demand.Kind is AIProductionDemandKind.Mine or AIProductionDemandKind.Refinery
                    && demand.DestinationPlanet == planet
                )
            );
        }

        [Test]
        public void BuildDemands_WithLowestRefineryCountAtFullEnergy_TargetsEligiblePlanet()
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
                energyCapacity: 20,
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
            empire.PendingRefinedMaterialFacilityIDs.Add("waiting-production-facility");
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .BuildDemands(context)
                .Single(item => item.Kind == AIProductionDemandKind.Refinery);

            Assert.AreSame(eligiblePlanet, demand.DestinationPlanet);
        }

        [Test]
        public void BuildDemands_WithStaticDefenseDeficit_AddsEconomyDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "defense-reserve-world",
                empire.InstanceID,
                energyCapacity: game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit
                    + game.Config.AI.Infrastructure.PlanetaryWeaponTargetCount,
                rawResourceNodes: 4
            );
            planet.IsHeadquarters = true;
            empire.HQInstanceID = planet.InstanceID;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsTrue(
                demands.Any(demand =>
                    demand.Kind is AIProductionDemandKind.Mine or AIProductionDemandKind.Refinery
                )
            );
        }

        [Test]
        public void BuildDemands_WithStaticDefenseDeficit_AddsFacilityExpansion()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
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
            hub.IsHeadquarters = true;
            empire.HQInstanceID = hub.InstanceID;
            hub.SetPopularSupport(empire.InstanceID, 100);
            AITestSceneBuilder.AddProductionFacility(
                game,
                hub,
                "training-facility",
                BuildingType.TrainingFacility,
                ManufacturingType.Troop
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsTrue(
                demands.Any(demand =>
                    demand.Kind
                        is AIProductionDemandKind.ConstructionFacility
                            or AIProductionDemandKind.Shipyard
                            or AIProductionDemandKind.TrainingFacility
                )
            );
        }

        [Test]
        public void BuildDemands_WithMultipleShipyardDestinations_AddsScoreFreeCapacityDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.FacilityPlanetsPerSector = 2;
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
            List<AIProductionDemand> demands = new AIProductionDemandGenerator()
                .BuildDemands(AITestSceneBuilder.CreateContext(game, empire))
                .Where(item => item.Kind == AIProductionDemandKind.Shipyard)
                .ToList();

            Assert.AreEqual(1, demands.Count);
            Assert.IsNull(demands[0].Destination);
            Assert.AreEqual(1, demands[0].QuantityNeeded);
        }

        [Test]
        public void BuildDemands_WithUnlockedFacilityUpgrade_SelectsSlowestFacilityDeterministically()
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
                .BuildDemands(context)
                .Single(item => item.Kind == AIProductionDemandKind.BuildingUpgrade);

            Assert.AreSame(planet, demand.DestinationPlanet);
            Assert.AreEqual(BuildingType.Shipyard, demand.BuildingType);
            Assert.AreSame(first, demand.BuildingToReplace);
            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        [Test]
        public void BuildDemands_WithOnlyOneFacility_DoesNotAddUpgradeDemand()
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

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsFalse(
                demands.Any(item => item.Kind == AIProductionDemandKind.BuildingUpgrade)
            );
        }

        [Test]
        public void BuildDemands_WithPendingUpgradeAtOnePlanet_StillUpgradesAnotherPlanet()
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
                .BuildDemands(context)
                .Where(item => item.Kind == AIProductionDemandKind.BuildingUpgrade)
                .ToList();

            Assert.AreEqual(1, upgradeDemands.Count);
            Assert.AreSame(eligiblePlanet, upgradeDemands[0].DestinationPlanet);
        }

        [Test]
        public void BuildDemands_WithStaticDefenseDemand_AddsConstructionFacilityDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "settled-world",
                empire.InstanceID
            );
            planet.SetPopularSupport(empire.InstanceID, 100);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsTrue(
                demands.Any(demand => demand.Kind == AIProductionDemandKind.ConstructionFacility)
            );
        }

        [Test]
        public void BuildDemands_WithShipDemandAndNoShipyard_AddsScoreFreeShipyardCapacityDemand()
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
                .BuildDemands(context)
                .Single(item => item.Kind == AIProductionDemandKind.Shipyard);

            Assert.IsNull(demand.DestinationPlanet);
            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        [Test]
        public void BuildDemands_WithFacilityCountBelowPlanetFloor_AddsFacilityDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet hub = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-hub",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                hub,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            for (
                int index = 1;
                index < game.Config.AI.Infrastructure.PlanetsPerShipyard + 1;
                index++
            )
            {
                AITestSceneBuilder.AddPlanet(game, system, $"colony-{index}", empire.InstanceID);
            }
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsTrue(demands.Any(item => item.Kind == AIProductionDemandKind.Shipyard));
        }

        [Test]
        public void BuildDemands_WithUndefendedHeadquartersAndSurplus_AddsShieldAndWeaponDemands()
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
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent = 0;
            AddMaintenanceCapacity(game, headquarters, 1);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            CollectionAssert.AreEquivalent(
                new[] { BuildingType.Defense, BuildingType.Weapon },
                demands
                    .Where(demand => demand.Kind == AIProductionDemandKind.PlanetaryDefense)
                    .Select(demand => demand.BuildingType)
            );
        }

        [Test]
        public void BuildDemands_WithDefensiveSurplus_AddsCompletePlanetaryDefensePackage()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
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

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

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
        public void BuildDemands_InteriorPlanetWithScaledFloor_ReducesGarrisonTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Garrison.InteriorCaptureFloorPercent = 34;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "interior",
                empire.InstanceID
            );
            planet.SetPopularSupport(empire.InstanceID, 100);
            AddMaintenanceCapacity(game, planet, 1);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            AIProductionDemand garrisonDemand = demands.Single(demand =>
                demand.Kind == AIProductionDemandKind.GarrisonRegimentReserve
                && demand.DestinationPlanet == planet
            );
            int expected = IntegerMath.ScaleByPercent(
                game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount,
                34
            );
            Assert.AreEqual(expected, garrisonDemand.QuantityNeeded);
        }

        [Test]
        public void BuildDemands_OppositionFavoredPlanet_RequiresSabotageResilientGarrison()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Garrison.InteriorCaptureFloorPercent = 0;
            game.Config.AI.Garrison.GarrisonDivisor = 5;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "favored",
                empire.InstanceID
            );
            planet.SetPopularSupport(empire.InstanceID, 0);
            planet.SetPopularSupport(rebels.InstanceID, 100);
            AddMaintenanceCapacity(game, planet, 1);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .BuildDemands(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.GarrisonRegimentReserve
                    && item.DestinationPlanet == planet
                );

            int stabilityRequirement = UprisingQueries.CalculateGarrisonRequirement(
                planet,
                empire,
                game.Config.AI.Garrison
            );
            Assert.AreEqual(12, stabilityRequirement);
            Assert.AreEqual(stabilityRequirement + 1, demand.QuantityNeeded);
        }

        [Test]
        public void BuildDemands_ThreatenedPlanetWithScaledFloor_KeepsFullGarrisonTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Garrison.InteriorCaptureFloorPercent = 34;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "border", empire.InstanceID);
            planet.SetPopularSupport(empire.InstanceID, 100);
            AddMaintenanceCapacity(game, planet, 1);
            Planet enemyPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "enemy-colony",
                rebels.InstanceID
            );
            AITestSceneBuilder.RevealPlanet(game, empire, enemyPlanet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            AIProductionDemand garrisonDemand = demands.Single(demand =>
                demand.Kind == AIProductionDemandKind.GarrisonRegimentReserve
                && demand.DestinationPlanet == planet
            );
            Assert.AreEqual(
                game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount,
                garrisonDemand.QuantityNeeded
            );
        }

        [Test]
        public void BuildDemands_IncompleteStaticDefenseWithGate_SkipsStarfighterReserve()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.NonCapitalSummary.RequireStaticDefenseBeforeStarfighters = true;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "ungated",
                empire.InstanceID
            );
            planet.SetPopularSupport(empire.InstanceID, 100);
            AddMaintenanceCapacity(game, planet, 1);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIProductionDemandKind.PlanetaryStarfighterReserve
                    && demand.DestinationPlanet == planet
                ),
                "Starfighter reserve should wait for the static defense package"
            );
        }

        [Test]
        public void BuildDemands_WithUnthreatenedNonProductionPlanet_DoesNotAddStaticDefense()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
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

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIProductionDemandKind.PlanetaryDefense
                    && demand.DestinationPlanet == planet
                )
            );
        }

        [Test]
        public void BuildDemands_WithOneDefenseEnergySlot_PrioritizesPartialShieldNetwork()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
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

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

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
        public void BuildDemands_WithStaticDefenseCoverageAndSurplusEnergy_AddsConfiguredWeaponBatch()
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
            planet.IsHeadquarters = true;
            empire.HQInstanceID = planet.InstanceID;
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
                .BuildDemands(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.PlanetaryDefense
                    && item.BuildingType == BuildingType.Weapon
                    && item.DestinationPlanet == planet
                );

            Assert.AreEqual(2, demand.QuantityNeeded);
        }

        [Test]
        public void BuildDemands_WithInboundThreat_RaisesThreatenedPlanetDefensePressure()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.Infrastructure.DemandUtility.DefenseValue.Weight = 0;
            game.Config.AI.Infrastructure.DemandUtility.FacilityPortfolio.Weight = 0;
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
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

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            AIProductionDemand valuableDemand = demands.Single(demand =>
                demand.Kind == AIProductionDemandKind.PlanetaryDefense
                && demand.BuildingType == BuildingType.Defense
                && demand.DestinationPlanet == valuablePlanet
            );
            AIProductionDemand threatenedDemand = demands.Single(demand =>
                demand.Kind == AIProductionDemandKind.PlanetaryDefense
                && demand.BuildingType == BuildingType.Defense
                && demand.DestinationPlanet == threatenedPlanet
            );
            double valuablePressure = AIProductionProposalScorer.GetDemandPressure(
                context,
                valuableDemand
            );
            double threatenedPressure = AIProductionProposalScorer.GetDemandPressure(
                context,
                threatenedDemand
            );
            Assert.Greater(threatenedPressure, valuablePressure);
        }

        [Test]
        public void BuildDemands_WithUnstableUnshieldedPlanet_RaisesInitialShieldPressure()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            GameConfig.AIInfrastructureConfig config = game.Config.AI.Infrastructure;
            config.DemandUtility.DefenseValue.Weight = 0;
            config.DemandUtility.ShieldSupport.Weight = 1;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "unstable-world",
                empire.InstanceID
            );
            planet.SetPopularSupport(empire.InstanceID, 20);
            AddMaintenanceCapacity(game, planet, 1);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIProductionDemand demand = new AIProductionDemandGenerator()
                .BuildDemands(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.PlanetaryDefense
                    && item.BuildingType == BuildingType.Defense
                    && item.DestinationPlanet == planet
                );
            double pressure = AIProductionProposalScorer.GetDemandPressure(context, demand);

            Assert.AreEqual(
                config.PlanetaryShieldDemandPercent
                    + config.DemandUtility.DefenseDeficit.Weight * 100
                    + 80,
                pressure
            );
            Assert.Greater(pressure, config.EconomySevereDemandPercent);
        }

        [Test]
        public void BuildDemands_WithExistingShield_DoesNotApplyInstabilityPressure()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            GameConfig.AIInfrastructureConfig config = game.Config.AI.Infrastructure;
            config.DemandUtility.DefenseDeficit.Weight = 0;
            config.DemandUtility.DefenseValue.Weight = 0;
            config.DemandUtility.ShieldSupport.Weight = 1;
            config.DemandUtility.FacilityPortfolio.Weight = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "partially-shielded-world",
                empire.InstanceID
            );
            planet.IsHeadquarters = true;
            empire.HQInstanceID = planet.InstanceID;
            planet.SetPopularSupport(empire.InstanceID, 20);
            AddMaintenanceCapacity(game, planet, 1);
            AddShield(game, planet, "existing-shield", empire.InstanceID, 40);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIProductionDemand demand = new AIProductionDemandGenerator()
                .BuildDemands(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.PlanetaryDefense
                    && item.BuildingType == BuildingType.Defense
                    && item.DestinationPlanet == planet
                );
            double pressure = AIProductionProposalScorer.GetDemandPressure(context, demand);

            Assert.AreEqual(
                config.PlanetaryShieldDemandPercent
                    + config.DemandUtility.DefenseHeadquarters.Weight * 100,
                pressure
            );
        }

        [Test]
        public void BuildDemands_WithUnthreatenedInfrastructure_UsesConfiguredStarfighterRequirement()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.NonCapitalSummary.RequireStaticDefenseBeforeStarfighters = false;
            game.Config.AI.NonCapitalSummary.StarfighterRequirementInfrastructure = 7;
            game.Config.AI.NonCapitalSummary.UnthreatenedInfrastructureStarfighterBaselinePercent =
                50;
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
                .BuildDemands(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.PlanetaryStarfighterReserve
                    && item.DestinationPlanet == planet
                );

            Assert.AreEqual(5, demand.QuantityNeeded);
            Assert.IsTrue(demand.UsesDefensiveReserve);
        }

        [Test]
        public void BuildDemands_WithOnlyConstructionInfrastructure_DoesNotAddStarfighterDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.NonCapitalSummary.RequireStaticDefenseBeforeStarfighters = false;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "construction-world",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                AITestSceneBuilder.CreateContext(game, empire)
            );

            Assert.IsFalse(
                demands.Any(item =>
                    item.Kind == AIProductionDemandKind.PlanetaryStarfighterReserve
                    && item.DestinationPlanet == planet
                )
            );
        }

        [Test]
        public void BuildDemands_WithIdleShipyardAndCompleteReserve_AddsFallbackFighterDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
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
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 0;
            game.Config.AI.FleetDeployment.MinimumMobileCombatStrength = 0;
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            Fleet fleet = EntityFactory.CreateFleet("battle-fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder { OrderType = FleetOrderType.Engage };
            game.AttachNode(fleet, planet);
            CapitalShip capitalShip = AITestSceneBuilder.CreateCapitalShip(
                "capital-ship",
                empire.InstanceID,
                combatStrength: 1,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            capitalShip.HasGravityWell = true;
            game.AttachNode(capitalShip, fleet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            AIProductionDemand demand = demands.Single(item =>
                item.Kind == AIProductionDemandKind.PlanetaryStarfighterReserve
                && item.DestinationPlanet == planet
            );

            Assert.AreEqual(1, demand.QuantityNeeded);
            Assert.AreEqual(
                game.Config.AI.Infrastructure.IdleShipyardFighterDemandPercent,
                AIProductionProposalScorer.GetDemandPressure(context, demand)
            );

            game.AttachNode(
                AITestSceneBuilder.CreateStarfighter("fallback-fighter", empire.InstanceID),
                planet
            );
            demands = new AIProductionDemandGenerator().BuildDemands(
                AITestSceneBuilder.CreateContext(game, empire)
            );
            Assert.IsFalse(
                demands.Any(item =>
                    item.Kind == AIProductionDemandKind.PlanetaryStarfighterReserve
                    && item.DestinationPlanet == planet
                )
            );
        }

        [Test]
        public void BuildDemands_WithActiveShipQueueAndCompleteReserve_DoesNotAddFallbackDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.NonCapitalSummary.StarfighterRequirementInfrastructure = 0;
            game.Config.AI.Infrastructure.IdleShipyardFighterReserveCount = 2;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "production-world",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            for (int index = 0; index < 12; index++)
            {
                game.AttachNode(
                    AITestSceneBuilder.CreateStarfighter(
                        $"planetary-fighter-{index}",
                        empire.InstanceID
                    ),
                    planet
                );
            }
            Starfighter queuedFighter = AITestSceneBuilder.CreateStarfighter(
                "queued-fighter",
                empire.InstanceID
            );
            queuedFighter.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(queuedFighter, planet);
            planet.AddToManufacturingQueue(queuedFighter);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                AITestSceneBuilder.CreateContext(game, empire)
            );

            Assert.IsFalse(
                demands.Any(item =>
                    item.Kind == AIProductionDemandKind.PlanetaryStarfighterReserve
                    && item.DestinationPlanet == planet
                )
            );
        }

        [Test]
        public void BuildDemands_WithOrdinaryUnthreatenedPlanet_SuppressesStarfighterDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "ordinary-world",
                empire.InstanceID
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIProductionDemandKind.PlanetaryStarfighterReserve
                    && demand.DestinationPlanet == planet
                )
            );
        }

        [Test]
        public void BuildDemands_WithThreatenedOrdinaryPlanet_AddsStrengthBasedStarfighterDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.NonCapitalSummary.RequireStaticDefenseBeforeStarfighters = false;
            game.Config.AI.NonCapitalSummary.InteriorStarfighterBaselinePercent = 100;
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
                .BuildDemands(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.PlanetaryStarfighterReserve
                    && item.DestinationPlanet == planet
                );

            int requiredDefenseStrength = context.StrategicPlan.GetPlanetDefenseStrength(planet);
            int expectedThreatReinforcement = (requiredDefenseStrength + 9) / 10;
            Assert.AreEqual(expectedThreatReinforcement, demand.QuantityNeeded);
        }

        [Test]
        public void BuildDemands_WithHeadquartersAndInfrastructure_RaisesHeadquartersStarfighterPressure()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.NonCapitalSummary.RequireStaticDefenseBeforeStarfighters = false;
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

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            AIProductionDemand infrastructureDemand = demands.Single(item =>
                item.Kind == AIProductionDemandKind.PlanetaryStarfighterReserve
                && item.DestinationPlanet == infrastructure
            );
            AIProductionDemand headquartersDemand = demands.Single(item =>
                item.Kind == AIProductionDemandKind.PlanetaryStarfighterReserve
                && item.DestinationPlanet == headquarters
            );

            Assert.Greater(
                AIProductionProposalScorer.GetDemandPressure(context, headquartersDemand),
                AIProductionProposalScorer.GetDemandPressure(context, infrastructureDemand)
            );
        }

        [Test]
        public void BuildDemands_WithFleetCapacityGaps_AddsFleetReinforcementDemands()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.Infrastructure.StarfighterParentFillPercent = 100;
            game.Config.AI.Infrastructure.AssaultRegimentLoadPercent = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
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
                combatStrength: 10,
                regimentCapacity: 1,
                starfighterCapacity: 2
            );
            fleet.AddChild(ship);
            ship.SetParent(fleet);
            game.AttachNode(fleet, owned);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsTrue(
                demands.Any(demand => demand.Kind == AIProductionDemandKind.FleetStarfighter)
            );
            Assert.IsTrue(
                demands.Any(demand => demand.Kind == AIProductionDemandKind.FleetRegiment)
            );
        }

        [Test]
        public void BuildDemands_WithAttackFleetReadinessGap_PreservesPressureAboveStandardRange()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.Infrastructure.FleetStarfighterDemandPercent = 90;
            game.Config.AI.Infrastructure.StarfighterParentFillPercent = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
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
                .BuildDemands(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetStarfighter
                    && item.DestinationFleet == fleet
                );

            Assert.Greater(AIProductionProposalScorer.GetDemandPressure(context, demand), 100);
        }

        [Test]
        public void BuildDemands_WithMovingFleet_DoesNotAddFleetReinforcementDemand()
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

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsFalse(demands.Any(demand => demand.DestinationFleet == fleet));
        }

        [Test]
        public void BuildDemands_WithActiveAttackAndIdleUnderstrengthFleet_AddsAssemblyDemand()
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

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsTrue(
                demands.Any(demand =>
                    demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && demand.DestinationFleet == assemblyFleet
                )
            );
        }

        [Test]
        public void BuildDemands_WithMultipleIdleUnderstrengthFleets_FocusesOneAssemblyFleet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet firstFleet = AddIdleBattleFleet(game, owned, empire.InstanceID, "fleet-1");
            Fleet secondFleet = AddIdleBattleFleet(game, owned, empire.InstanceID, "fleet-2");
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Fleet destination = demands
                .Where(demand =>
                    demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && (
                        demand.DestinationFleet == firstFleet
                        || demand.DestinationFleet == secondFleet
                    )
                )
                .Select(demand => demand.DestinationFleet)
                .Single();

            Assert.AreSame(
                new[] { firstFleet, secondFleet }
                    .OrderBy(context.Assessment.GetProjectedFleetCombatValue)
                    .ThenBy(fleet => fleet.GetRegimentCapacity())
                    .ThenBy(fleet => fleet.InstanceID)
                    .First(),
                destination
            );
        }

        [Test]
        public void BuildDemands_WithMultipleEnemyPlanets_BuildsForCurrentTargetResistance()
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
                .BuildDemands(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(100, demand.QuantityNeeded);
            Assert.AreEqual(AICapitalShipProductionRole.General, demand.CapitalShipRole);
        }

        [Test]
        public void BuildDemands_WithMultipleAttackFleets_AddsShipDemandForEach()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 2;
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
                    regimentCapacity: 2,
                    starfighterCapacity: 0
                ),
                establishedFleet
            );
            CapitalShip establishedShip = establishedFleet.GetChildren<CapitalShip>().Single();
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("established-regiment-1", empire.InstanceID),
                establishedShip
            );
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("established-regiment-2", empire.InstanceID),
                establishedShip
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
                    combatStrength: 400,
                    regimentCapacity: 2,
                    starfighterCapacity: 0
                ),
                remoteFleet
            );
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("remote-regiment", empire.InstanceID),
                remoteFleet.GetChildren<CapitalShip>().Single()
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            List<AIProductionDemand> reinforcementDemands = demands
                .Where(demand =>
                    demand.Kind
                        is AIProductionDemandKind.FleetCapitalShip
                            or AIProductionDemandKind.FleetRegiment
                )
                .ToList();

            Assert.IsTrue(
                reinforcementDemands.Any(demand =>
                    demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && demand.DestinationFleet == establishedFleet
                )
            );
            Assert.IsTrue(
                reinforcementDemands.Any(demand =>
                    demand.Kind == AIProductionDemandKind.FleetRegiment
                    && demand.DestinationFleet == remoteFleet
                )
            );
            Assert.IsTrue(
                reinforcementDemands.Any(demand =>
                    demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && demand.DestinationFleet == remoteFleet
                )
            );
        }

        [Test]
        public void BuildDemands_WithAttackRegimentStrengthGap_AddsDemandForEntireDeficit()
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
                regimentCapacity: 5
            );
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("attacker", empire.InstanceID, attackRating: 5),
                fleet.GetChildren<CapitalShip>().Single()
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .BuildDemands(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetRegiment
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(3, demand.QuantityNeeded);
        }

        [Test]
        public void BuildDemands_WithAttackRegimentStrengthGapAndFullCapacity_AddsCapitalShipDemand()
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
                .BuildDemands(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(3, demand.QuantityNeeded);
            Assert.AreEqual(AICapitalShipProductionRole.TroopTransport, demand.CapitalShipRole);
        }

        [Test]
        public void BuildDemands_WithWeakIdleBattleFleet_AddsCapitalShipDemand()
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
                .BuildDemands(context)
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
        public void BuildDemands_WithShieldedAttackTargetAndInsufficientBombardment_AddsCapitalShipDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            enemy.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AddShield(game, enemy, "shield-1", rebels.InstanceID, 100);
            AddShield(game, enemy, "shield-2", rebels.InstanceID, 100);
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
                .BuildDemands(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(11, demand.QuantityNeeded);
            Assert.AreEqual(AICapitalShipProductionRole.Bombardment, demand.CapitalShipRole);
        }

        [Test]
        public void BuildDemands_WithCombatAndBombardmentGaps_PrioritizesBombardmentShip()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            enemy.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AddShield(game, enemy, "shield-1", rebels.InstanceID, 100);
            AddShield(game, enemy, "shield-2", rebels.InstanceID, 100);
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
                .BuildDemands(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(AICapitalShipProductionRole.Bombardment, demand.CapitalShipRole);
        }

        [Test]
        public void BuildDemands_WithReadyAttackFleetAndUnlockedGravityWell_AddsInterdictionDemand()
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
                .BuildDemands(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(1, demand.QuantityNeeded);
            Assert.AreEqual(AICapitalShipProductionRole.Interdiction, demand.CapitalShipRole);
        }

        [Test]
        public void BuildDemands_WithReadyIdleBattleFleetAndUnlockedGravityWell_AddsInterdictionDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out _);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.MinimumMobileCombatStrength = 200;
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
                .BuildDemands(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(1, demand.QuantityNeeded);
            Assert.AreEqual(AICapitalShipProductionRole.Interdiction, demand.CapitalShipRole);
        }

        [TestCase(ManufacturingStatus.Building)]
        [TestCase(ManufacturingStatus.Complete)]
        public void BuildDemands_WithCommittedGravityWellShip_DoesNotAddInterdictionDemand(
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

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && demand.DestinationFleet == fleet
                )
            );
        }

        [Test]
        public void BuildDemands_WithUnderstrengthHeadquartersDefenseFleet_AddsCapitalShipDemand()
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
            Fleet mobileFleet = EntityFactory.CreateFleet("mobile-fleet", empire.InstanceID);
            mobileFleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(mobileFleet, fleetPlanet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "mobile-ship",
                    empire.InstanceID,
                    combatStrength: 2000
                ),
                mobileFleet
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .BuildDemands(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(50, demand.QuantityNeeded);
        }

        [Test]
        public void BuildDemands_WithInboundCapitalShipFillingCombatNeed_DoesNotAddCapitalShipDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            game.Config.AI.FleetDeployment.MinimumMobileCombatStrength = 1000;
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

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && demand.DestinationFleet == fleet
                )
            );
        }

        [TestCase(ManufacturingStatus.Building)]
        [TestCase(ManufacturingStatus.Complete)]
        public void BuildDemands_WithCommittedCapitalShipFillingCombatNeed_DoesNotAddCapitalShipDemand(
            ManufacturingStatus manufacturingStatus
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            game.Config.AI.FleetDeployment.MinimumMobileCombatStrength = 1000;
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

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && demand.DestinationFleet == fleet
                )
            );
        }

        [Test]
        public void BuildDemands_WithColonizationFleetMissingRegimentCapacity_AddsCapitalShipDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Colonization;
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
                .BuildDemands(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(
                game.Config.AI.FleetDeployment.ColonizationFleetMaximumRegimentCount,
                demand.QuantityNeeded
            );
        }

        [Test]
        public void BuildDemands_WithColonizationFleetCapacity_AddsTargetColonizationRegiments()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.AI.Infrastructure.AssaultRegimentLoadPercent = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Colonization;
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
                .BuildDemands(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetRegiment
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(
                game.Config.AI.FleetDeployment.ColonizationFleetMaximumRegimentCount,
                demand.QuantityNeeded
            );
        }

        [Test]
        public void BuildDemands_WithDefenseFleetCapacity_DoesNotAddFleetRegimentDemand()
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

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsFalse(
                demands.Any(item =>
                    item.Kind == AIProductionDemandKind.FleetRegiment
                    && item.DestinationFleet == defenseFleet
                )
            );
        }

        [Test]
        public void BuildDemands_WithoutActiveOfficerMission_DoesNotAddSpecialForcesDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "training-world", empire.InstanceID);
            SpecialForces template = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID,
                SabotageMission.MissionTypeID
            );
            empire.ResearchQueue[ManufacturingType.Troop] = new List<Technology>
            {
                new Technology(template),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.IsFalse(
                new AIProductionDemandGenerator()
                    .BuildDemands(context)
                    .Any(item => item.Kind == AIProductionDemandKind.SpecialForces)
            );
        }

        [Test]
        public void BuildDemands_WithEquivalentSpecialForcesTemplates_AddsOneRoleDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "training-world", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            SpecialForces expensiveTemplate = AITestSceneBuilder.CreateSpecialForces(
                "expensive-commandos",
                empire.InstanceID,
                SabotageMission.MissionTypeID,
                InciteUprisingMission.MissionTypeID
            );
            expensiveTemplate.ConstructionCost = 2;
            SpecialForces cheapTemplate = AITestSceneBuilder.CreateSpecialForces(
                "cheap-commandos",
                empire.InstanceID,
                InciteUprisingMission.MissionTypeID,
                SabotageMission.MissionTypeID
            );
            empire.ResearchQueue[ManufacturingType.Troop] = new List<Technology>
            {
                new Technology(expensiveTemplate),
                new Technology(cheapTemplate),
            };
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            StubMission mission = EntityFactory.CreateMission(
                "active-sabotage",
                empire.InstanceID,
                target.InstanceID
            );
            mission.ConfigKey = SabotageMission.MissionTypeID;
            game.AttachNode(mission, target);
            game.AttachNode(officer, mission);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .BuildDemands(context)
                .Single(item => item.Kind == AIProductionDemandKind.SpecialForces);

            Assert.AreEqual("cheap-commandos", demand.ProductTypeId);
            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        [Test]
        public void BuildDemands_WithActiveHostileOfficerMissions_ScalesSpecialForcesDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "training-world", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            SpecialForces template = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID,
                SabotageMission.MissionTypeID
            );
            empire.ResearchQueue[ManufacturingType.Troop] = new List<Technology>
            {
                new Technology(template),
            };
            for (int index = 0; index < 20; index++)
            {
                Officer officer = EntityFactory.CreateOfficer(
                    $"officer-{index}",
                    empire.InstanceID
                );
                StubMission mission = EntityFactory.CreateMission(
                    $"active-sabotage-{index}",
                    empire.InstanceID,
                    target.InstanceID
                );
                mission.ConfigKey = SabotageMission.MissionTypeID;
                game.AttachNode(mission, target);
                game.AttachNode(officer, mission);
            }
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .BuildDemands(context)
                .Single(item => item.Kind == AIProductionDemandKind.SpecialForces);

            Assert.AreEqual(2, demand.QuantityNeeded);
        }

        [Test]
        public void BuildDemands_WithAssignedDecoyCoveringActiveMission_DoesNotAddDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "training-world",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            SpecialForces template = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID,
                SabotageMission.MissionTypeID
            );
            empire.ResearchQueue[ManufacturingType.Troop] = new List<Technology>
            {
                new Technology(template),
            };
            SpecialForces busyUnit = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID,
                SabotageMission.MissionTypeID
            );
            busyUnit.InstanceID = "busy-commandos";
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            StubMission mission = EntityFactory.CreateMission(
                "active-sabotage",
                empire.InstanceID,
                target.InstanceID
            );
            mission.ConfigKey = SabotageMission.MissionTypeID;
            game.AttachNode(mission, target);
            game.AttachNode(officer, mission);
            mission.AddDecoyParticipant(busyUnit);
            game.AttachNode(busyUnit, mission);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsFalse(demands.Any(item => item.Kind == AIProductionDemandKind.SpecialForces));
        }

        [Test]
        public void BuildDemands_WithPrimarySpecialForcesOnActiveMission_DoesNotAddDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "training-world", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            SpecialForces template = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID,
                MissionTypeIDs.Sabotage
            );
            empire.ResearchQueue[ManufacturingType.Troop] = new List<Technology>
            {
                new Technology(template),
            };
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            StubMission officerMission = EntityFactory.CreateMission(
                "active-officer-sabotage",
                empire.InstanceID,
                target.InstanceID
            );
            officerMission.ConfigKey = MissionTypeIDs.Sabotage;
            game.AttachNode(officerMission, target);
            game.AttachNode(officer, officerMission);
            SpecialForces busyUnit = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID,
                MissionTypeIDs.Sabotage
            );
            busyUnit.InstanceID = "busy-commandos";
            StubMission specialForcesMission = EntityFactory.CreateMission(
                "active-special-forces-sabotage",
                empire.InstanceID,
                target.InstanceID
            );
            specialForcesMission.ConfigKey = MissionTypeIDs.Sabotage;
            game.AttachNode(specialForcesMission, target);
            game.AttachNode(busyUnit, specialForcesMission);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsFalse(demands.Any(item => item.Kind == AIProductionDemandKind.SpecialForces));
        }

        [Test]
        public void BuildDemands_WithReplacementBuildingCoveringActiveMission_DoesNotAddDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "training-world",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            SpecialForces template = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID,
                SabotageMission.MissionTypeID
            );
            empire.ResearchQueue[ManufacturingType.Troop] = new List<Technology>
            {
                new Technology(template),
            };
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            StubMission mission = EntityFactory.CreateMission(
                "active-sabotage",
                empire.InstanceID,
                target.InstanceID
            );
            mission.ConfigKey = SabotageMission.MissionTypeID;
            game.AttachNode(mission, target);
            game.AttachNode(officer, mission);
            SpecialForces buildingUnit = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID,
                SabotageMission.MissionTypeID
            );
            buildingUnit.InstanceID = "building-commandos";
            buildingUnit.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(buildingUnit, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsFalse(demands.Any(item => item.Kind == AIProductionDemandKind.SpecialForces));
        }

        [Test]
        public void BuildDemands_WithTooFewCommittedBattleFleets_AddsFleetSeedDemand()
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
                .BuildDemands(context)
                .Single(item => item.Kind == AIProductionDemandKind.FleetSeedCapitalShip);

            Assert.AreSame(planet, demand.DestinationPlanet);
            Assert.AreEqual(
                game.Config.AI.FleetDeployment.MinimumBattleFleetCount,
                demand.QuantityNeeded
            );
        }

        [Test]
        public void BuildDemands_WithDeliveringFleetSeed_CountsFleetAsCommitted()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.PlanetsPerBattleFleet = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-world",
                empire.InstanceID
            );
            planet.IsHeadquarters = true;
            empire.HQInstanceID = planet.InstanceID;
            Fleet fleet = EntityFactory.CreateFleet("seeded-fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip deliveringShip = AITestSceneBuilder.CreateCapitalShip(
                "delivering-ship",
                empire.InstanceID
            );
            deliveringShip.ManufacturingStatus = ManufacturingStatus.Delivering;
            game.AttachNode(fleet, planet);
            game.AttachNode(deliveringShip, fleet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsFalse(
                demands.Any(item => item.Kind == AIProductionDemandKind.FleetSeedCapitalShip)
            );
        }

        [Test]
        public void BuildDemands_WithKnownUncolonizedPlanet_AddsColonizationFleetSeedDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet shipyardPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-world",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .BuildDemands(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.ColonizationFleetSeedCapitalShip
                );

            Assert.AreSame(shipyardPlanet, demand.DestinationPlanet);
            Assert.AreEqual(AICapitalShipProductionRole.TroopTransport, demand.CapitalShipRole);
            Assert.AreEqual(
                game.Config.AI.FleetDeployment.ColonizationFleetTargetCount,
                demand.QuantityNeeded
            );
        }

        [Test]
        public void BuildDemands_WithUnexploredOuterRimPlanet_AddsColonizationFleetSeedDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector ownedSystem = AITestSceneBuilder.AddSector(game, "owned-system");
            Planet shipyardPlanet = AITestSceneBuilder.AddPlanet(
                game,
                ownedSystem,
                "shipyard-world",
                empire.InstanceID
            );
            PlanetSector outerRimSystem = AITestSceneBuilder.AddSector(game, "outer-rim-system");
            outerRimSystem.SectorType = PlanetSectorType.OuterRim;
            AITestSceneBuilder.AddPlanet(game, outerRimSystem, "unexplored", null);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .BuildDemands(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.ColonizationFleetSeedCapitalShip
                );

            Assert.AreSame(shipyardPlanet, demand.DestinationPlanet);
            Assert.AreEqual(AICapitalShipProductionRole.TroopTransport, demand.CapitalShipRole);
        }

        [Test]
        public void BuildDemands_WithOneOfTwoColonizationFleets_AddsOneSeedDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            Fleet fleet = EntityFactory.CreateFleet("colonization-fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Colonization;
            game.AttachNode(fleet, owned);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .BuildDemands(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.ColonizationFleetSeedCapitalShip
                );

            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        [Test]
        public void BuildDemands_WithExpandingTerritory_ScalesFleetSeedDemand()
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
                .BuildDemands(context)
                .Single(item => item.Kind == AIProductionDemandKind.FleetSeedCapitalShip);

            Assert.AreEqual(4, demand.QuantityNeeded);
        }

        [Test]
        public void BuildDemands_WithFleetRoleCapacityDeficit_AddsFleetSeedDemand()
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
                .BuildDemands(context)
                .Single(item => item.Kind == AIProductionDemandKind.FleetSeedCapitalShip);

            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        [Test]
        public void BuildDemands_WithUnguardedHeadquartersAndFleetRoleDeficit_AddsHeadquartersFleetSeedDemand()
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
                .BuildDemands(context)
                .Single(item => item.Kind == AIProductionDemandKind.FleetSeedCapitalShip);

            Assert.AreSame(headquarters, demand.DestinationPlanet);
            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        [Test]
        public void BuildDemands_WithSatisfiedFleetTargetAndUnguardedHeadquarters_AddsFleetSeedDemand()
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

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsTrue(
                demands.Any(demand => demand.Kind == AIProductionDemandKind.FleetSeedCapitalShip)
            );
        }

        [Test]
        public void BuildDemands_WithUnderGarrisonedPlanet_AddsRequiredGarrisonDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Garrison.InteriorCaptureFloorPercent = 100;
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
                .BuildDemands(context)
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
        public void BuildDemands_WithSatisfiedGarrisonRequirement_DoesNotAddGarrisonDemand()
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

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().BuildDemands(
                context
            );

            Assert.IsFalse(
                demands.Any(item =>
                    item.Kind == AIProductionDemandKind.GarrisonRegimentReserve
                    && item.DestinationPlanet == planet
                )
            );
        }

        /// <summary>
        /// Adds unlocked shipyard upgrade.
        /// </summary>
        /// <param name="faction">The faction.</param>
        /// <returns>The result of add unlocked shipyard upgrade.</returns>
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

        /// <summary>
        /// Adds attack fleet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="location">The location.</param>
        /// <param name="target">The target.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="regimentCapacity">The regiment capacity.</param>
        /// <returns>The result of add attack fleet.</returns>
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
        /// Adds matching completed mines and refineries to a planet.
        /// </summary>
        /// <param name="game">The game containing the planet.</param>
        /// <param name="planet">The planet receiving the facilities.</param>
        /// <param name="count">The number of each facility type to add.</param>
        private static void AddResourceFacilities(GameRoot game, Planet planet, int count)
        {
            for (int index = 0; index < count; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"mine-{index}",
                    BuildingType.Mine,
                    ManufacturingType.None
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"refinery-{index}",
                    BuildingType.Refinery,
                    ManufacturingType.None
                );
            }
        }

        /// <summary>
        /// Adds maintenance capacity.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="count">The count.</param>
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

        /// <summary>
        /// Adds shield.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="strength">The strength.</param>
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
            shield.ShieldStrength = strength;
            game.AttachNode(shield, planet);
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
