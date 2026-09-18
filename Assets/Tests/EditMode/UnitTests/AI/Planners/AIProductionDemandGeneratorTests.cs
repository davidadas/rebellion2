using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;
using Rebellion.Util.Common;

namespace Rebellion.Tests.AI.Planners
{
    [TestFixture]
    public class AIProductionDemandGeneratorTests
    {
        /// <summary>
        /// Verifies a claimed planet does not bypass shared infrastructure demand.
        /// </summary>
        [Test]
        public void Generate_WithClaimedUncolonizedPlanet_DoesNotAddColonyDemand()
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIDemandKind.Colony);

            Assert.AreSame(planet, demand.DestinationPlanet);
            Assert.AreEqual(BuildingType.Mine, demand.BuildingType);
            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        [Test]
        public void Generate_WithMineCapacityAhead_UsesRefineryAsColonyFoundingFacility()
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIDemandKind.Colony);

            Assert.AreEqual(BuildingType.Refinery, demand.BuildingType);
        }

        [Test]
        public void Generate_WithMultipleClaimedPlanets_BalancesFoundingFacilities()
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

            List<AIDemand> demands = new AIProductionDemandGenerator()
                .Generate(context)
                .Where(item => item.Kind == AIDemandKind.Colony)
                .ToList();

            CollectionAssert.AreEquivalent(
                new[] { BuildingType.Mine, BuildingType.Refinery },
                demands.Select(demand => demand.BuildingType)
            );
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(demands.Any(item => item.Kind == AIDemandKind.Colony));
        }

        /// <summary>
        /// Verifies generate with unmined resources and sufficient economy does not add economy demand.
        /// </summary>
        [Test]
        public void Generate_WithUnminedResourcesAndSufficientEconomy_DoesNotAddEconomyDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(demand => demand.Kind is AIDemandKind.Mine or AIDemandKind.Refinery)
            );
        }

        /// <summary>
        /// Verifies generate with projected refined materials near reserve adds economy demand.
        /// </summary>
        [Test]
        public void Generate_WithProjectedRefinedMaterialsNearReserve_AddsEconomyDemand()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsTrue(demands.Any(demand => demand.Kind == AIDemandKind.Mine));
            Assert.IsTrue(demands.Any(demand => demand.Kind == AIDemandKind.Refinery));
        }

        /// <summary>
        /// Verifies generate with pending manufacturing material request adds economy demand.
        /// </summary>
        [Test]
        public void Generate_WithPendingManufacturingMaterialRequest_AddsEconomyDemand()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsTrue(demands.Any(demand => demand.Kind == AIDemandKind.Mine));
            Assert.IsTrue(demands.Any(demand => demand.Kind == AIDemandKind.Refinery));
        }

        /// <summary>
        /// Verifies economy buildings may be delivered to an owned Outer Rim planet before it is operational.
        /// </summary>
        [Test]
        public void Generate_WithOwnedNonOperationalOuterRimPlanet_AddsEconomyDemand()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(
                AITestSceneBuilder.CreateContext(game, empire)
            );

            Assert.IsTrue(
                demands.Any(demand =>
                    demand.Kind is AIDemandKind.Mine or AIDemandKind.Refinery
                    && demand.DestinationPlanet == planet
                )
            );
        }

        /// <summary>
        /// Verifies generate with lowest refinery count at full energy targets eligible planet.
        /// </summary>
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIDemandKind.Refinery);

            Assert.AreSame(eligiblePlanet, demand.DestinationPlanet);
        }

        /// <summary>
        /// Verifies generate with only static defense energy remaining does not add economy demand.
        /// </summary>
        [Test]
        public void Generate_WithOnlyStaticDefenseEnergyRemaining_DoesNotAddEconomyDemand()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(demand => demand.Kind is AIDemandKind.Mine or AIDemandKind.Refinery)
            );
        }

        /// <summary>
        /// Verifies generate with only static defense energy remaining does not add facility expansion.
        /// </summary>
        [Test]
        public void Generate_WithOnlyStaticDefenseEnergyRemaining_DoesNotAddFacilityExpansion()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind
                        is AIDemandKind.ConstructionFacility
                            or AIDemandKind.Shipyard
                            or AIDemandKind.TrainingFacility
                )
            );
        }

        /// <summary>
        /// Verifies generate with reserved hub and eligible world targets eligible world for expansion.
        /// </summary>
        [Test]
        public void Generate_WithReservedDevelopmentCapacity_DoesNotAddTrainingFacility()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.Infrastructure.PlanetsPerTrainingFacility = 1;
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
                energyCapacity: staticDefenseEnergy + 15,
                rawResourceNodes: 4
            );
            hub.IsHeadquarters = true;
            empire.HQInstanceID = hub.InstanceID;
            rebels.HQInstanceID = headquarters.InstanceID;
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);
            Assert.IsFalse(demands.Any(item => item.Kind == AIDemandKind.TrainingFacility));
        }

        /// <summary>
        /// Verifies generate with pending shipyard adds demand toward sector hub target.
        /// </summary>
        [Test]
        public void Generate_WithPendingShipyard_AddsDemandTowardSectorHubTarget()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsTrue(
                demands.Any(demand =>
                    demand.Kind == AIDemandKind.Shipyard && demand.DestinationPlanet == planet
                )
            );
        }

        /// <summary>
        /// Verifies generate with pending shipyard at another planet expands existing shipyard hub.
        /// </summary>
        [Test]
        public void Generate_WithConstrainedPendingShipyard_SelectsFeasibleSectorHub()
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

            Assert.IsTrue(
                context.DevelopmentAllocation.IsPrimaryHub(demandPlanet, BuildingType.Shipyard)
            );
            Assert.IsFalse(
                context.DevelopmentAllocation.IsPrimaryHub(pendingPlanet, BuildingType.Shipyard)
            );

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIDemandKind.Shipyard);

            Assert.AreEqual(
                demandPlanet.InstanceID,
                demand.DestinationPlanet.InstanceID,
                $"Selected {demand.DestinationPlanet.InstanceID}."
            );
        }

        /// <summary>
        /// Verifies generate with unlocked facility upgrade selects slowest facility deterministically.
        /// </summary>
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIDemandKind.BuildingUpgrade);

            Assert.AreSame(planet, demand.DestinationPlanet);
            Assert.AreEqual(BuildingType.Shipyard, demand.BuildingType);
            Assert.AreSame(first, demand.BuildingToReplace);
            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        /// <summary>
        /// Verifies generate with only one facility does not add upgrade demand.
        /// </summary>
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(demands.Any(item => item.Kind == AIDemandKind.BuildingUpgrade));
        }

        /// <summary>
        /// Verifies generate with pending upgrade at one planet still upgrades another planet.
        /// </summary>
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

            List<AIDemand> upgradeDemands = new AIProductionDemandGenerator()
                .Generate(context)
                .Where(item => item.Kind == AIDemandKind.BuildingUpgrade)
                .ToList();

            Assert.AreEqual(1, upgradeDemands.Count);
            Assert.AreSame(eligiblePlanet, upgradeDemands[0].DestinationPlanet);
        }

        /// <summary>
        /// Verifies generate with static defense demand adds construction facility demand.
        /// </summary>
        [Test]
        public void Generate_WithStaticDefenseDemand_AddsConstructionFacilityDemand()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsTrue(demands.Any(demand => demand.Kind == AIDemandKind.ConstructionFacility));
        }

        /// <summary>
        /// Verifies a new Outer Rim sector receives a curve-prioritized construction-yard demand.
        /// </summary>
        [Test]
        public void Generate_WithNewOuterRimColony_PrioritizesConstructionFacility()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.PlanetsPerConstructionFacility = 100;
            PlanetSector core = AITestSceneBuilder.AddSector(game, "core");
            Planet established = AITestSceneBuilder.AddPlanet(
                game,
                core,
                "established",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                established,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            PlanetSector outerRim = AITestSceneBuilder.AddSector(game, "outer-rim");
            outerRim.SectorType = PlanetSectorType.OuterRim;
            Planet colony = AITestSceneBuilder.AddPlanet(
                game,
                outerRim,
                "colony",
                empire.InstanceID
            );
            colony.IsColonized = false;

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(
                AITestSceneBuilder.CreateContext(game, empire)
            );

            AIDemand construction = demands.Single(demand =>
                demand.Kind == AIDemandKind.ConstructionFacility
                && demand.DestinationPlanet == colony
            );
            Assert.Greater(
                construction.Pressure,
                demands
                    .Where(demand => demand.BuildingType != BuildingType.ConstructionFacility)
                    .Max(demand => demand.Pressure)
            );
        }

        /// <summary>
        /// Verifies a seeded Outer Rim construction hub continues to its allocated target.
        /// </summary>
        [Test]
        public void Generate_WithSeededOuterRimConstructionHub_RequestsRemainingHubCapacity()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.PlanetsPerConstructionFacility = 100;
            game.Config.AI.Infrastructure.FacilitySectorHubTargetCount = 5;
            PlanetSector core = AITestSceneBuilder.AddSector(game, "core");
            Planet established = AITestSceneBuilder.AddPlanet(
                game,
                core,
                "established",
                empire.InstanceID,
                energyCapacity: 20
            );
            for (int index = 0; index < 5; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    established,
                    $"core-yard-{index}",
                    BuildingType.ConstructionFacility,
                    ManufacturingType.Building
                );
            }

            PlanetSector outerRim = AITestSceneBuilder.AddSector(game, "outer-rim");
            outerRim.SectorType = PlanetSectorType.OuterRim;
            Planet colony = AITestSceneBuilder.AddPlanet(
                game,
                outerRim,
                "flive",
                empire.InstanceID,
                energyCapacity: 6
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                colony,
                "flive-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(
                AITestSceneBuilder.CreateContext(game, empire)
            );

            AIDemand construction = demands.Single(demand =>
                demand.Kind == AIDemandKind.ConstructionFacility
                && demand.DestinationPlanet == colony
            );
            Assert.AreEqual(4, construction.QuantityNeeded);
        }

        /// <summary>
        /// Verifies a full Outer Rim seed designates a feasible planet in the same system for the
        /// complete construction hub.
        /// </summary>
        [Test]
        public void Generate_WithFullOuterRimSeed_RequestsFullHubAtFeasibleSystemPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.PlanetsPerConstructionFacility = 100;
            game.Config.AI.Infrastructure.FacilitySectorHubTargetCount = 5;
            game.Config.AI.Infrastructure.FacilityPlanetsPerSector = 1;
            PlanetSector outerRim = AITestSceneBuilder.AddSector(game, "dufilvan");
            outerRim.SectorType = PlanetSectorType.OuterRim;
            Planet seed = AITestSceneBuilder.AddPlanet(
                game,
                outerRim,
                "flive",
                empire.InstanceID,
                energyCapacity: 1
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                seed,
                "flive-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Planet hub = AITestSceneBuilder.AddPlanet(
                game,
                outerRim,
                "gamorr",
                empire.InstanceID,
                energyCapacity: 7
            );
            hub.IsColonized = false;

            AIDemand construction = new AIProductionDemandGenerator()
                .Generate(AITestSceneBuilder.CreateContext(game, empire))
                .Single(demand => demand.Kind == AIDemandKind.ConstructionFacility);

            Assert.AreSame(hub, construction.DestinationPlanet);
            Assert.AreEqual(5, construction.QuantityNeeded);
        }

        /// <summary>
        /// Verifies generate with ship demand and no shipyard adds shipyard at demand planet.
        /// </summary>
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIDemandKind.Shipyard);

            Assert.AreSame(planet, demand.DestinationPlanet);
            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        /// <summary>
        /// Verifies generate with shipyard sectors below hub target adds demand in each sector.
        /// </summary>
        [Test]
        public void Generate_WithShipyardSectorsBelowHubTarget_AddsDemandInEachSector()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.ShipyardSectorHubTargetCount = 6;
            PlanetSector firstSector = AITestSceneBuilder.AddSector(game, "sys1");
            Planet firstHub = AITestSceneBuilder.AddPlanet(
                game,
                firstSector,
                "first-hub",
                empire.InstanceID,
                energyCapacity: 10
            );
            PlanetSector secondSector = AITestSceneBuilder.AddSector(game, "sys2");
            Planet secondHub = AITestSceneBuilder.AddPlanet(
                game,
                secondSector,
                "second-hub",
                empire.InstanceID,
                energyCapacity: 10
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                firstHub,
                "first-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                secondHub,
                "second-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIDemand> demands = new AIProductionDemandGenerator()
                .Generate(context)
                .Where(demand => demand.Kind == AIDemandKind.Shipyard)
                .ToList();

            CollectionAssert.AreEquivalent(
                new[] { firstHub, secondHub },
                demands.Select(demand => demand.DestinationPlanet)
            );
        }

        /// <summary>
        /// Verifies generate with established shipyard hub adds demand toward sector hub target.
        /// </summary>
        [Test]
        public void Generate_WithEstablishedShipyardHub_AddsDemandTowardSectorHubTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.ShipyardSectorHubTargetCount = 6;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sys1");
            Planet hub = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "shipyard-hub",
                empire.InstanceID,
                energyCapacity: 10
            );
            Planet colony = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "colony",
                empire.InstanceID,
                energyCapacity: 10
            );
            for (int index = 0; index < 3; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    hub,
                    $"hub-shipyard-{index}",
                    BuildingType.Shipyard,
                    ManufacturingType.Ship
                );
            }
            AITestSceneBuilder.AddProductionFacility(
                game,
                colony,
                "colony-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIDemand> demands = new AIProductionDemandGenerator()
                .Generate(context)
                .Where(demand => demand.Kind == AIDemandKind.Shipyard)
                .ToList();

            CollectionAssert.AreEqual(
                new[] { hub },
                demands.Select(demand => demand.DestinationPlanet)
            );
        }

        /// <summary>
        /// Verifies generate with completed shipyard hub consolidates smaller shipyard cluster.
        /// </summary>
        [Test]
        public void Generate_WithCompletedShipyardHub_ConsolidatesSmallerShipyardCluster()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.ShipyardSectorHubTargetCount = 5;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sys1");
            Planet hub = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "shipyard-hub",
                empire.InstanceID,
                energyCapacity: 10
            );
            Planet colony = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "colony",
                empire.InstanceID,
                energyCapacity: 10
            );
            for (int index = 0; index < 5; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    hub,
                    $"hub-shipyard-{index}",
                    BuildingType.Shipyard,
                    ManufacturingType.Ship
                );
            }
            AITestSceneBuilder.AddProductionFacility(
                game,
                colony,
                "colony-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIDemandKind.Shipyard);

            Assert.AreSame(colony, demand.DestinationPlanet);
        }

        /// <summary>
        /// Verifies generate with incomplete shipyard hub does not expand secondary in another sector.
        /// </summary>
        [Test]
        public void Generate_WithIncompleteShipyardHub_DoesNotExpandSecondaryInAnotherSector()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.ShipyardSectorHubTargetCount = 6;
            PlanetSector incompleteSector = AITestSceneBuilder.AddSector(game, "incomplete");
            Planet incompleteHub = AITestSceneBuilder.AddPlanet(
                game,
                incompleteSector,
                "incomplete-hub",
                empire.InstanceID,
                energyCapacity: 10
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                incompleteHub,
                "incomplete-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );

            PlanetSector completedSector = AITestSceneBuilder.AddSector(game, "completed");
            Planet completedHub = AITestSceneBuilder.AddPlanet(
                game,
                completedSector,
                "completed-hub",
                empire.InstanceID,
                energyCapacity: 10
            );
            Planet secondary = AITestSceneBuilder.AddPlanet(
                game,
                completedSector,
                "secondary",
                empire.InstanceID,
                energyCapacity: 10
            );
            for (int index = 0; index < 6; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    completedHub,
                    $"completed-shipyard-{index}",
                    BuildingType.Shipyard,
                    ManufacturingType.Ship
                );
            }
            AITestSceneBuilder.AddProductionFacility(
                game,
                secondary,
                "secondary-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIDemand> demands = new AIProductionDemandGenerator()
                .Generate(context)
                .Where(demand => demand.Kind == AIDemandKind.Shipyard)
                .ToList();

            Assert.IsTrue(demands.Any(demand => demand.DestinationPlanet == incompleteHub));
            Assert.IsFalse(demands.Any(demand => demand.DestinationPlanet == secondary));
        }

        /// <summary>
        /// Verifies generate with busy shipyard adds shipyard at existing hub.
        /// </summary>
        [Test]
        public void Generate_WithBusyShipyard_AddsShipyardAtExistingHub()
        {
            (GameRoot game, Faction empire, Planet hub, Planet _, Fleet _, CapitalShip ship) =
                CreateBusyShipyardScene();
            Starfighter queuedStarfighter = new Starfighter
            {
                InstanceID = "queued-starfighter",
                OwnerInstanceID = empire.InstanceID,
                ConstructionCost = game.Config.AI.Infrastructure.ShipyardTargetClearTicks + 1,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            game.AttachNode(queuedStarfighter, ship);
            hub.AddToManufacturingQueue(queuedStarfighter);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIDemandKind.Shipyard);

            Assert.AreSame(hub, demand.DestinationPlanet);
        }

        /// <summary>
        /// Verifies generate with available capacity at stacked shipyard adds sector hub demand.
        /// </summary>
        [Test]
        public void Generate_WithAvailableCapacityAtStackedShipyard_AddsSectorHubDemand()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsTrue(
                demands.Any(demand =>
                    demand.Kind == AIDemandKind.Shipyard && demand.DestinationPlanet == hub
                )
            );
        }

        /// <summary>
        /// Verifies generate with defense reserved training hub targets feasible cluster planet.
        /// </summary>
        [Test]
        public void Generate_WithExistingTrainingHub_ExpandsItsCluster()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.PlanetsPerTrainingFacility = 1;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet hub = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "training-hub",
                empire.InstanceID
            );
            Planet colony = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "colony",
                empire.InstanceID,
                energyCapacity: 20
            );
            AITestSceneBuilder.AddPlanet(game, system, "colony-2", empire.InstanceID);
            AITestSceneBuilder.AddPlanet(game, system, "colony-3", empire.InstanceID);
            AITestSceneBuilder.AddPlanet(game, system, "colony-4", empire.InstanceID);
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            AIDemand demand = demands.Single(item => item.Kind == AIDemandKind.TrainingFacility);

            Assert.AreSame(hub, demand.DestinationPlanet);
            Assert.Greater(
                context.DevelopmentAllocation.GetAvailableEnergy(
                    demand.DestinationPlanet,
                    BuildingType.TrainingFacility
                ),
                0
            );
        }

        /// <summary>
        /// Verifies generate with facility count below planet floor adds facility demand.
        /// </summary>
        [Test]
        public void Generate_WithFacilityCountBelowPlanetFloor_AddsFacilityDemand()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsTrue(demands.Any(item => item.Kind == AIDemandKind.Shipyard));
        }

        /// <summary>
        /// Verifies generate with construction capacity deficit adds demands at distinct planets.
        /// </summary>
        [Test]
        public void Generate_WithConstructionCapacityDeficit_AddsDemandsAtDistinctPlanets()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.PlanetsPerConstructionFacility = 1;
            game.Config.AI.Infrastructure.ProductionFacilityMaintenanceAllocationPercent = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet first = AITestSceneBuilder.AddPlanet(game, system, "first", empire.InstanceID);
            AITestSceneBuilder.AddPlanet(game, system, "second", empire.InstanceID);
            AITestSceneBuilder.AddPlanet(game, system, "third", empire.InstanceID);
            AITestSceneBuilder.AddProductionFacility(
                game,
                first,
                "construction-facility",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIDemand> demands = new AIProductionDemandGenerator()
                .Generate(context)
                .Where(item => item.Kind == AIDemandKind.ConstructionFacility)
                .ToList();

            Assert.AreEqual(1, demands.Count);
            Assert.AreEqual(1, demands.Select(item => item.DestinationPlanet).Distinct().Count());
            Assert.IsTrue(demands.All(item => item.QuantityNeeded == 4));
        }

        /// <summary>
        /// Verifies generate with pending facility meeting faction floor adds local capacity demand.
        /// </summary>
        [Test]
        public void Generate_WithPendingFacilityMeetingFactionFloor_AddsLocalCapacityDemand()
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
            Building pendingShipyard = AITestSceneBuilder.AddProductionFacility(
                game,
                hub,
                "pending-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            pendingShipyard.ManufacturingStatus = ManufacturingStatus.Building;
            for (
                int index = 1;
                index < game.Config.AI.Infrastructure.PlanetsPerShipyard + 1;
                index++
            )
            {
                AITestSceneBuilder.AddPlanet(game, system, $"colony-{index}", empire.InstanceID);
            }
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsTrue(demands.Any(item => item.Kind == AIDemandKind.Shipyard));
        }

        /// <summary>
        /// Verifies generate with building demand and no construction capacity adds construction facility.
        /// </summary>
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIDemandKind.ConstructionFacility);

            Assert.AreSame(planet, demand.DestinationPlanet);
            Assert.AreEqual(5, demand.QuantityNeeded);
        }

        /// <summary>
        /// Verifies generate with undefended headquarters and surplus adds shield and weapon demands.
        /// </summary>
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
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent = 0;
            AddMaintenanceCapacity(game, headquarters, 1);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            CollectionAssert.AreEquivalent(
                new[] { BuildingType.Defense, BuildingType.Weapon },
                demands
                    .Where(demand => demand.Kind == AIDemandKind.PlanetaryDefense)
                    .Select(demand => demand.BuildingType)
            );
        }

        /// <summary>
        /// Verifies generate with defensive surplus adds complete planetary defense package.
        /// </summary>
        [Test]
        public void Generate_WithDefensiveSurplus_AddsCompletePlanetaryDefensePackage()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            AIDemand shieldDemand = demands.Single(demand =>
                demand.Kind == AIDemandKind.PlanetaryDefense
                && demand.BuildingType == BuildingType.Defense
                && demand.DestinationPlanet == planet
            );
            AIDemand weaponDemand = demands.Single(demand =>
                demand.Kind == AIDemandKind.PlanetaryDefense
                && demand.BuildingType == BuildingType.Weapon
                && demand.DestinationPlanet == planet
            );
            AIDemand garrisonDemand = demands.Single(demand =>
                demand.Kind == AIDemandKind.GarrisonRegimentReserve
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

        /// <summary>
        /// Verifies generate interior planet with scaled floor reduces garrison target.
        /// </summary>
        [Test]
        public void Generate_InteriorPlanetWithScaledFloor_ReducesGarrisonTarget()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            AIDemand garrisonDemand = demands.Single(demand =>
                demand.Kind == AIDemandKind.GarrisonRegimentReserve
                && demand.DestinationPlanet == planet
            );
            int expected = IntegerMath.ScaleByPercent(
                game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount,
                34
            );
            Assert.AreEqual(expected, garrisonDemand.QuantityNeeded);
        }

        /// <summary>
        /// Verifies generate threatened planet with scaled floor keeps full garrison target.
        /// </summary>
        [Test]
        public void Generate_ThreatenedPlanetWithScaledFloor_KeepsFullGarrisonTarget()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            AIDemand garrisonDemand = demands.Single(demand =>
                demand.Kind == AIDemandKind.GarrisonRegimentReserve
                && demand.DestinationPlanet == planet
            );
            Assert.AreEqual(
                game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount,
                garrisonDemand.QuantityNeeded
            );
        }

        /// <summary>
        /// Verifies generate incomplete static defense with gate skips starfighter reserve.
        /// </summary>
        [Test]
        public void Generate_IncompleteStaticDefenseWithGate_SkipsStarfighterReserve()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIDemandKind.PlanetaryStarfighterReserve
                    && demand.DestinationPlanet == planet
                ),
                "Starfighter reserve should wait for the static defense package"
            );
        }

        /// <summary>
        /// Verifies generate with unthreatened non production planet does not add static defense.
        /// </summary>
        [Test]
        public void Generate_WithUnthreatenedNonProductionPlanet_DoesNotAddStaticDefense()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIDemandKind.PlanetaryDefense
                    && demand.DestinationPlanet == planet
                )
            );
        }

        /// <summary>
        /// Verifies generate with one defense energy slot prioritizes partial shield network.
        /// </summary>
        [Test]
        public void Generate_WithOneDefenseEnergySlot_PrioritizesPartialShieldNetwork()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            AIDemand shieldDemand = demands.Single(demand =>
                demand.Kind == AIDemandKind.PlanetaryDefense
                && demand.BuildingType == BuildingType.Defense
                && demand.DestinationPlanet == planet
            );
            Assert.AreEqual(1, shieldDemand.QuantityNeeded);
            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIDemandKind.PlanetaryDefense
                    && demand.BuildingType == BuildingType.Weapon
                    && demand.DestinationPlanet == planet
                )
            );
        }

        /// <summary>
        /// Verifies generate with static defense coverage and surplus energy adds configured weapon batch.
        /// </summary>
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIDemandKind.PlanetaryDefense
                    && item.BuildingType == BuildingType.Weapon
                    && item.DestinationPlanet == planet
                );

            Assert.AreEqual(2, demand.QuantityNeeded);
        }

        /// <summary>
        /// Verifies generate with inbound threat raises threatened planet defense pressure.
        /// </summary>
        [Test]
        public void Generate_WithInboundThreat_RaisesThreatenedPlanetDefensePressure()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            double valuablePressure = demands
                .Single(demand =>
                    demand.Kind == AIDemandKind.PlanetaryDefense
                    && demand.BuildingType == BuildingType.Defense
                    && demand.DestinationPlanet == valuablePlanet
                )
                .Pressure;
            double threatenedPressure = demands
                .Single(demand =>
                    demand.Kind == AIDemandKind.PlanetaryDefense
                    && demand.BuildingType == BuildingType.Defense
                    && demand.DestinationPlanet == threatenedPlanet
                )
                .Pressure;
            Assert.Greater(threatenedPressure, valuablePressure);
        }

        /// <summary>
        /// Verifies generate with unstable unshielded planet raises initial shield pressure.
        /// </summary>
        [Test]
        public void Generate_WithUnstableUnshieldedPlanet_RaisesInitialShieldPressure()
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

            double pressure = new AIProductionDemandGenerator()
                .Generate(AITestSceneBuilder.CreateContext(game, empire))
                .Single(demand =>
                    demand.Kind == AIDemandKind.PlanetaryDefense
                    && demand.BuildingType == BuildingType.Defense
                    && demand.DestinationPlanet == planet
                )
                .Pressure;

            Assert.AreEqual(
                config.PlanetaryShieldDemandPercent
                    + config.DemandUtility.DefenseDeficit.Weight * 100
                    + 80,
                pressure
            );
            Assert.Greater(pressure, config.EconomySevereDemandPercent);
        }

        /// <summary>
        /// Verifies generate with existing shield does not apply instability pressure.
        /// </summary>
        [Test]
        public void Generate_WithExistingShield_DoesNotApplyInstabilityPressure()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            GameConfig.AIInfrastructureConfig config = game.Config.AI.Infrastructure;
            config.DemandUtility.DefenseDeficit.Weight = 0;
            config.DemandUtility.DefenseValue.Weight = 0;
            config.DemandUtility.ShieldSupport.Weight = 1;
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

            double pressure = new AIProductionDemandGenerator()
                .Generate(AITestSceneBuilder.CreateContext(game, empire))
                .Single(demand =>
                    demand.Kind == AIDemandKind.PlanetaryDefense
                    && demand.BuildingType == BuildingType.Defense
                    && demand.DestinationPlanet == planet
                )
                .Pressure;

            Assert.AreEqual(
                config.PlanetaryShieldDemandPercent
                    + config.DemandUtility.DefenseHeadquarters.Weight * 100,
                pressure
            );
        }

        /// <summary>
        /// Verifies generate with unthreatened infrastructure adds twelve starfighter minimum demand.
        /// </summary>
        [Test]
        public void Generate_WithUnthreatenedInfrastructure_UsesConfiguredStarfighterRequirement()
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIDemandKind.PlanetaryStarfighterReserve
                    && item.DestinationPlanet == planet
                );

            Assert.AreEqual(5, demand.QuantityNeeded);
            Assert.IsTrue(demand.UsesDefensiveReserve);
        }

        /// <summary>
        /// Verifies generate with complete infrastructure starfighter reserve suppresses demand.
        /// </summary>
        [Test]
        public void Generate_WithOnlyConstructionInfrastructure_DoesNotAddStarfighterDemand()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(
                AITestSceneBuilder.CreateContext(game, empire)
            );

            Assert.IsFalse(
                demands.Any(item =>
                    item.Kind == AIDemandKind.PlanetaryStarfighterReserve
                    && item.DestinationPlanet == planet
                )
            );
        }

        /// <summary>
        /// Verifies generate withidleshipyardandcompletereserve addsfallbackfighterdemand.
        /// </summary>
        [Test]
        public void Generate_WithIdleShipyardAndCompleteReserve_AddsFallbackFighterDemand()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            AIDemand demand = demands.Single(item =>
                item.Kind == AIDemandKind.PlanetaryStarfighterReserve
                && item.DestinationPlanet == planet
            );

            Assert.AreEqual(1, demand.QuantityNeeded);
            Assert.AreEqual(
                game.Config.AI.Infrastructure.IdleShipyardFighterDemandPercent,
                demand.Pressure
            );

            game.AttachNode(
                AITestSceneBuilder.CreateStarfighter("fallback-fighter", empire.InstanceID),
                planet
            );
            demands = new AIProductionDemandGenerator().Generate(
                AITestSceneBuilder.CreateContext(game, empire)
            );
            Assert.IsFalse(
                demands.Any(item =>
                    item.Kind == AIDemandKind.PlanetaryStarfighterReserve
                    && item.DestinationPlanet == planet
                )
            );
        }

        /// <summary>
        /// Verifies generate withactiveshipqueueandcompletereserve doesnotaddfallbackdemand.
        /// </summary>
        [Test]
        public void Generate_WithActiveShipQueueAndCompleteReserve_DoesNotAddFallbackDemand()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(
                AITestSceneBuilder.CreateContext(game, empire)
            );

            Assert.IsFalse(
                demands.Any(item =>
                    item.Kind == AIDemandKind.PlanetaryStarfighterReserve
                    && item.DestinationPlanet == planet
                )
            );
        }

        /// <summary>
        /// Verifies generate with ordinary unthreatened planet suppresses starfighter demand.
        /// </summary>
        [Test]
        public void Generate_WithOrdinaryUnthreatenedPlanet_SuppressesStarfighterDemand()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIDemandKind.PlanetaryStarfighterReserve
                    && demand.DestinationPlanet == planet
                )
            );
        }

        /// <summary>
        /// Verifies generate with threatened ordinary planet adds strength based starfighter demand.
        /// </summary>
        [Test]
        public void Generate_WithThreatenedOrdinaryPlanet_AddsStrengthBasedStarfighterDemand()
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIDemandKind.PlanetaryStarfighterReserve
                    && item.DestinationPlanet == planet
                );

            int requiredDefenseStrength = context.Assessment.GetRequiredPlanetDefenseStrength(
                planet
            );
            int expectedThreatReinforcement = (requiredDefenseStrength + 9) / 10;
            Assert.AreEqual(expectedThreatReinforcement, demand.QuantityNeeded);
        }

        /// <summary>
        /// Verifies generate with headquarters and infrastructure raises headquarters starfighter pressure.
        /// </summary>
        [Test]
        public void Generate_WithHeadquartersAndInfrastructure_RaisesHeadquartersStarfighterPressure()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            AIDemand infrastructureDemand = demands.Single(item =>
                item.Kind == AIDemandKind.PlanetaryStarfighterReserve
                && item.DestinationPlanet == infrastructure
            );
            AIDemand headquartersDemand = demands.Single(item =>
                item.Kind == AIDemandKind.PlanetaryStarfighterReserve
                && item.DestinationPlanet == headquarters
            );

            Assert.Greater(headquartersDemand.Pressure, infrastructureDemand.Pressure);
        }

        /// <summary>
        /// Verifies generate with fleet capacity gaps adds fleet reinforcement demands.
        /// </summary>
        [Test]
        public void Generate_WithFleetCapacityGaps_AddsFleetReinforcementDemands()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsTrue(demands.Any(demand => demand.Kind == AIDemandKind.FleetStarfighter));
            Assert.IsTrue(demands.Any(demand => demand.Kind == AIDemandKind.FleetRegiment));
        }

        /// <summary>
        /// Verifies generate with attack fleet readiness gap preserves pressure above standard range.
        /// </summary>
        [Test]
        public void Generate_WithAttackFleetReadinessGap_PreservesPressureAboveStandardRange()
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIDemandKind.FleetStarfighter && item.DestinationFleet == fleet
                );

            Assert.Greater(demand.Pressure, 100);
        }

        /// <summary>
        /// Verifies generate with moving fleet does not add fleet reinforcement demand.
        /// </summary>
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(demands.Any(demand => demand.DestinationFleet == fleet));
        }

        /// <summary>
        /// Verifies generate with active attack and idle understrength fleet adds assembly demand.
        /// </summary>
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsTrue(
                demands.Any(demand =>
                    demand.Kind == AIDemandKind.FleetCapitalShip
                    && demand.DestinationFleet == assemblyFleet
                )
            );
        }

        /// <summary>
        /// Verifies generate with multiple idle understrength fleets adds assembly demand for each fleet.
        /// </summary>
        [Test]
        public void Generate_WithMultipleIdleUnderstrengthFleets_FocusesOneAssemblyFleet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet firstFleet = AddIdleBattleFleet(game, owned, empire.InstanceID, "fleet-1");
            Fleet secondFleet = AddIdleBattleFleet(game, owned, empire.InstanceID, "fleet-2");
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Fleet destination = demands
                .Where(demand =>
                    demand.Kind == AIDemandKind.FleetCapitalShip
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

        /// <summary>
        /// Verifies generate with multiple enemy planets builds for current target resistance.
        /// </summary>
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIDemandKind.FleetCapitalShip && item.DestinationFleet == fleet
                );

            Assert.AreEqual(200, demand.QuantityNeeded);
            Assert.AreEqual(AICapitalShipProductionRole.General, demand.CapitalShipRole);
        }

        /// <summary>
        /// Verifies generate with multiple attack fleets adds demand for each campaign.
        /// </summary>
        [Test]
        public void Generate_WithMultipleAttackFleets_AddsShipDemandForEach()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            List<AIDemand> reinforcementDemands = demands
                .Where(demand =>
                    demand.Kind is AIDemandKind.FleetCapitalShip or AIDemandKind.FleetRegiment
                )
                .ToList();

            Assert.IsTrue(
                reinforcementDemands.Any(demand =>
                    demand.Kind == AIDemandKind.FleetCapitalShip
                    && demand.DestinationFleet == establishedFleet
                )
            );
            Assert.IsTrue(
                reinforcementDemands.Any(demand =>
                    demand.Kind == AIDemandKind.FleetRegiment
                    && demand.DestinationFleet == remoteFleet
                )
            );
            Assert.IsTrue(
                reinforcementDemands.Any(demand =>
                    demand.Kind == AIDemandKind.FleetCapitalShip
                    && demand.DestinationFleet == remoteFleet
                )
            );
        }

        /// <summary>
        /// Verifies generate with attack regiment strength gap adds demand for entire deficit.
        /// </summary>
        [Test]
        public void Generate_WithAttackRegimentStrengthGap_AddsDemandForEntireDeficit()
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIDemandKind.FleetRegiment && item.DestinationFleet == fleet
                );

            Assert.AreEqual(3, demand.QuantityNeeded);
        }

        /// <summary>
        /// Verifies generate with attack regiment strength gap and full capacity adds capital ship demand.
        /// </summary>
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIDemandKind.FleetCapitalShip && item.DestinationFleet == fleet
                );

            Assert.AreEqual(3, demand.QuantityNeeded);
            Assert.AreEqual(AICapitalShipProductionRole.TroopTransport, demand.CapitalShipRole);
        }

        /// <summary>
        /// Verifies generate with weak idle battle fleet adds capital ship demand.
        /// </summary>
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIDemandKind.FleetCapitalShip && item.DestinationFleet == fleet
                );

            Assert.AreEqual(
                game.Config.AI.FleetDeployment.MinimumAttackStrength
                    - ship.GetPrimaryWeaponStrength(),
                demand.QuantityNeeded
            );
            Assert.AreEqual(AICapitalShipProductionRole.General, demand.CapitalShipRole);
        }

        /// <summary>
        /// Verifies generate with shielded attack target and insufficient bombardment adds capital ship demand.
        /// </summary>
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIDemandKind.FleetCapitalShip && item.DestinationFleet == fleet
                );

            Assert.AreEqual(11, demand.QuantityNeeded);
            Assert.AreEqual(AICapitalShipProductionRole.Bombardment, demand.CapitalShipRole);
        }

        /// <summary>
        /// Verifies generate with combat and bombardment gaps prioritizes bombardment ship.
        /// </summary>
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIDemandKind.FleetCapitalShip && item.DestinationFleet == fleet
                );

            Assert.AreEqual(AICapitalShipProductionRole.Bombardment, demand.CapitalShipRole);
        }

        /// <summary>
        /// Verifies generate with ready attack fleet and unlocked gravity well adds interdiction demand.
        /// </summary>
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIDemandKind.FleetCapitalShip && item.DestinationFleet == fleet
                );

            Assert.AreEqual(1, demand.QuantityNeeded);
            Assert.AreEqual(AICapitalShipProductionRole.Interdiction, demand.CapitalShipRole);
        }

        /// <summary>
        /// Verifies generate with ready idle battle fleet and unlocked gravity well adds interdiction demand.
        /// </summary>
        [Test]
        public void Generate_WithReadyIdleBattleFleetAndUnlockedGravityWell_AddsInterdictionDemand()
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIDemandKind.FleetCapitalShip && item.DestinationFleet == fleet
                );

            Assert.AreEqual(1, demand.QuantityNeeded);
            Assert.AreEqual(AICapitalShipProductionRole.Interdiction, demand.CapitalShipRole);
        }

        /// <summary>
        /// Verifies generate with committed gravity well ship does not add interdiction demand.
        /// </summary>
        /// <param name="manufacturingStatus">The manufacturing status.</param>
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIDemandKind.FleetCapitalShip && demand.DestinationFleet == fleet
                )
            );
        }

        /// <summary>
        /// Verifies generate with understrength headquarters defense fleet adds capital ship demand.
        /// </summary>
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIDemandKind.FleetCapitalShip && item.DestinationFleet == fleet
                );

            Assert.AreEqual(50, demand.QuantityNeeded);
        }

        /// <summary>
        /// Verifies generate with inbound capital ship filling combat need does not add capital ship demand.
        /// </summary>
        [Test]
        public void Generate_WithInboundCapitalShipFillingCombatNeed_DoesNotAddCapitalShipDemand()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIDemandKind.FleetCapitalShip && demand.DestinationFleet == fleet
                )
            );
        }

        /// <summary>
        /// Verifies generate with committed capital ship filling combat need does not add capital ship demand.
        /// </summary>
        /// <param name="manufacturingStatus">The manufacturing status.</param>
        [TestCase(ManufacturingStatus.Building)]
        [TestCase(ManufacturingStatus.Complete)]
        public void Generate_WithCommittedCapitalShipFillingCombatNeed_DoesNotAddCapitalShipDemand(
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIDemandKind.FleetCapitalShip && demand.DestinationFleet == fleet
                )
            );
        }

        /// <summary>
        /// Verifies generate with colonization fleet missing regiment capacity adds capital ship demand.
        /// </summary>
        [Test]
        public void Generate_WithColonizationFleetMissingRegimentCapacity_AddsCapitalShipDemand()
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIDemandKind.FleetCapitalShip && item.DestinationFleet == fleet
                );

            Assert.AreEqual(
                game.Config.AI.FleetDeployment.ColonizationFleetMaximumRegimentCount,
                demand.QuantityNeeded
            );
        }

        /// <summary>
        /// Verifies generate with colonization fleet capacity adds target colonization regiments.
        /// </summary>
        [Test]
        public void Generate_WithColonizationFleetCapacity_AddsTargetColonizationRegiments()
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIDemandKind.FleetRegiment && item.DestinationFleet == fleet
                );

            Assert.AreEqual(
                game.Config.AI.FleetDeployment.ColonizationFleetMaximumRegimentCount,
                demand.QuantityNeeded
            );
        }

        /// <summary>
        /// Verifies generate with defense fleet capacity does not add fleet regiment demand.
        /// </summary>
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(item =>
                    item.Kind == AIDemandKind.FleetRegiment && item.DestinationFleet == defenseFleet
                )
            );
        }

        /// <summary>
        /// Verifies generate without active officer mission does not add special forces demand.
        /// </summary>
        [Test]
        public void Generate_WithoutActiveOfficerMission_DoesNotAddSpecialForcesDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "training-world", empire.InstanceID);
            SpecialForces template = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID,
                MissionTypeIDs.Sabotage
            );
            empire.ResearchQueue[ManufacturingType.Troop] = new List<Technology>
            {
                new Technology(template),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.IsFalse(
                new AIProductionDemandGenerator()
                    .Generate(context)
                    .Any(item => item.Kind == AIDemandKind.SpecialForces)
            );
        }

        /// <summary>
        /// Verifies generate with equivalent special forces templates adds one role demand.
        /// </summary>
        [Test]
        public void Generate_WithEquivalentSpecialForcesTemplates_AddsOneRoleDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "training-world", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            SpecialForces expensiveTemplate = AITestSceneBuilder.CreateSpecialForces(
                "expensive-commandos",
                empire.InstanceID,
                MissionTypeIDs.Sabotage,
                MissionTypeIDs.InciteUprising
            );
            expensiveTemplate.ConstructionCost = 2;
            SpecialForces cheapTemplate = AITestSceneBuilder.CreateSpecialForces(
                "cheap-commandos",
                empire.InstanceID,
                MissionTypeIDs.InciteUprising,
                MissionTypeIDs.Sabotage
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
            mission.ConfigKey = MissionTypeIDs.Sabotage;
            game.AttachNode(mission, target);
            game.AttachNode(officer, mission);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIDemandKind.SpecialForces);

            Assert.AreEqual("cheap-commandos", demand.ProductTypeId);
            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        /// <summary>
        /// Verifies generate with active hostile officer missions scales special forces demand.
        /// </summary>
        [Test]
        public void Generate_WithActiveHostileOfficerMissions_ScalesSpecialForcesDemand()
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
                mission.ConfigKey = MissionTypeIDs.Sabotage;
                game.AttachNode(mission, target);
                game.AttachNode(officer, mission);
            }
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIDemandKind.SpecialForces);

            Assert.AreEqual(2, demand.QuantityNeeded);
        }

        /// <summary>
        /// Verifies generate with assigned decoy covering active mission does not add demand.
        /// </summary>
        [Test]
        public void Generate_WithAssignedDecoyCoveringActiveMission_DoesNotAddDemand()
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
                MissionTypeIDs.Sabotage
            );
            empire.ResearchQueue[ManufacturingType.Troop] = new List<Technology>
            {
                new Technology(template),
            };
            SpecialForces busyUnit = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID,
                MissionTypeIDs.Sabotage
            );
            busyUnit.InstanceID = "busy-commandos";
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            StubMission mission = EntityFactory.CreateMission(
                "active-sabotage",
                empire.InstanceID,
                target.InstanceID
            );
            mission.ConfigKey = MissionTypeIDs.Sabotage;
            game.AttachNode(mission, target);
            game.AttachNode(officer, mission);
            mission.AddDecoyParticipant(busyUnit);
            game.AttachNode(busyUnit, mission);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(demands.Any(item => item.Kind == AIDemandKind.SpecialForces));
        }

        /// <summary>
        /// Verifies generate with replacement building covering active mission does not add demand.
        /// </summary>
        [Test]
        public void Generate_WithReplacementBuildingCoveringActiveMission_DoesNotAddDemand()
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
                MissionTypeIDs.Sabotage
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
            mission.ConfigKey = MissionTypeIDs.Sabotage;
            game.AttachNode(mission, target);
            game.AttachNode(officer, mission);
            SpecialForces buildingUnit = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID,
                MissionTypeIDs.Sabotage
            );
            buildingUnit.InstanceID = "building-commandos";
            buildingUnit.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(buildingUnit, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(demands.Any(item => item.Kind == AIDemandKind.SpecialForces));
        }

        /// <summary>
        /// Verifies generate with too few committed battle fleets adds fleet seed demand.
        /// </summary>
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIDemandKind.FleetSeedCapitalShip);

            Assert.AreSame(planet, demand.DestinationPlanet);
            Assert.AreEqual(
                game.Config.AI.FleetDeployment.MinimumBattleFleetCount,
                demand.QuantityNeeded
            );
        }

        /// <summary>
        /// Verifies generate with delivering fleet seed counts fleet as committed.
        /// </summary>
        [Test]
        public void Generate_WithDeliveringFleetSeed_CountsFleetAsCommitted()
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(demands.Any(item => item.Kind == AIDemandKind.FleetSeedCapitalShip));
        }

        /// <summary>
        /// Verifies generate with known uncolonized planet adds colonization fleet seed demand.
        /// </summary>
        [Test]
        public void Generate_WithKnownUncolonizedPlanet_AddsColonizationFleetSeedDemand()
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIDemandKind.ColonizationFleetSeedCapitalShip);

            Assert.AreSame(shipyardPlanet, demand.DestinationPlanet);
            Assert.AreEqual(AICapitalShipProductionRole.TroopTransport, demand.CapitalShipRole);
            Assert.AreEqual(
                game.Config.AI.FleetDeployment.ColonizationFleetTargetCount,
                demand.QuantityNeeded
            );
        }

        /// <summary>
        /// Verifies generate with one of two colonization fleets adds one seed demand.
        /// </summary>
        [Test]
        public void Generate_WithUnexploredOuterRimPlanet_AddsColonizationFleetSeedDemand()
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIDemandKind.ColonizationFleetSeedCapitalShip);

            Assert.AreSame(shipyardPlanet, demand.DestinationPlanet);
            Assert.AreEqual(AICapitalShipProductionRole.TroopTransport, demand.CapitalShipRole);
        }

        /// <summary>
        /// Verifies generate withoneoftwocolonizationfleets addsoneseeddemand.
        /// </summary>
        [Test]
        public void Generate_WithOneOfTwoColonizationFleets_AddsOneSeedDemand()
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIDemandKind.ColonizationFleetSeedCapitalShip);

            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        /// <summary>
        /// Verifies generate with expanding territory scales fleet seed demand.
        /// </summary>
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIDemandKind.FleetSeedCapitalShip);

            Assert.AreEqual(4, demand.QuantityNeeded);
        }

        /// <summary>
        /// Verifies generate with fleet role capacity deficit adds fleet seed demand.
        /// </summary>
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIDemandKind.FleetSeedCapitalShip);

            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        /// <summary>
        /// Verifies generate with unguarded headquarters and fleet role deficit adds headquarters fleet seed demand.
        /// </summary>
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIDemandKind.FleetSeedCapitalShip);

            Assert.AreSame(headquarters, demand.DestinationPlanet);
            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        /// <summary>
        /// Verifies generate with satisfied fleet target and unguarded headquarters adds fleet seed demand.
        /// </summary>
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsTrue(demands.Any(demand => demand.Kind == AIDemandKind.FleetSeedCapitalShip));
        }

        /// <summary>
        /// Verifies generate with under garrisoned planet adds required garrison demand.
        /// </summary>
        [Test]
        public void Generate_WithUnderGarrisonedPlanet_AddsRequiredGarrisonDemand()
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

            AIDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIDemandKind.GarrisonRegimentReserve
                    && item.DestinationPlanet == planet
                );

            Assert.AreEqual(
                game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount,
                demand.QuantityNeeded
            );
        }

        /// <summary>
        /// Verifies generate with satisfied garrison requirement does not add garrison demand.
        /// </summary>
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

            List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(item =>
                    item.Kind == AIDemandKind.GarrisonRegimentReserve
                    && item.DestinationPlanet == planet
                )
            );
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
        /// Creates busy shipyard scene.
        /// </summary>
        /// <returns>The created busy shipyard scene.</returns>
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
