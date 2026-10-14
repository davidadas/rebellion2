using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI
{
    [TestFixture]
    public class AIStrategicPlanTests
    {
        [Test]
        public void TryAllocateInfrastructureHub_ExactIndustrialAndShieldSpace_PreservesFinishedLayout()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit = 2;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "hub",
                faction.InstanceID,
                energyCapacity: 7
            );
            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, faction).StrategicPlan;

            Assert.IsTrue(plan.TryAllocateInfrastructureHub(planet, BuildingType.Shipyard, 6));
            Assert.AreEqual(
                6,
                plan.GetAllocatedInfrastructureTarget(planet, BuildingType.Shipyard)
            );
            Assert.AreEqual(6, plan.GetAvailableConstructionSpace(planet, BuildingType.Shipyard));
            Assert.AreEqual(0, plan.GetAvailableConstructionSpace(planet, BuildingType.Mine));
            Assert.AreEqual(1, plan.GetAvailableConstructionSpace(planet, BuildingType.Defense));
        }

        [Test]
        public void TryAllocateInfrastructureHub_NoSpaceForShield_RejectsLayout()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit = 2;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "hub",
                faction.InstanceID,
                energyCapacity: 6
            );
            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, faction).StrategicPlan;

            Assert.IsFalse(plan.TryAllocateInfrastructureHub(planet, BuildingType.Shipyard, 6));
            Assert.AreEqual(
                0,
                plan.GetAllocatedInfrastructureTarget(planet, BuildingType.Shipyard)
            );
        }

        [TestCase(ManufacturingStatus.Complete)]
        [TestCase(ManufacturingStatus.Building)]
        public void GetAvailableConstructionSpace_CommittedShield_DoesNotReserveItsSlotTwice(
            ManufacturingStatus status
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit = 2;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "hub",
                faction.InstanceID,
                energyCapacity: 7
            );
            Building shield = AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shield",
                BuildingType.Defense,
                ManufacturingType.None
            );
            shield.ShieldStrength = 80;
            shield.ManufacturingStatus = status;
            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, faction).StrategicPlan;

            Assert.IsTrue(plan.TryAllocateInfrastructureHub(planet, BuildingType.Shipyard, 6));
            Assert.AreEqual(6, plan.GetAvailableConstructionSpace(planet, BuildingType.Shipyard));
            Assert.AreEqual(0, plan.GetAvailableConstructionSpace(planet, BuildingType.Refinery));
        }

        [Test]
        public void TryAllocateInfrastructureHub_AnotherCategoryAlreadyPlanned_PreventsOverbooking()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit = 2;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "hub",
                faction.InstanceID,
                energyCapacity: 10
            );
            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, faction).StrategicPlan;

            Assert.IsTrue(plan.TryAllocateInfrastructureHub(planet, BuildingType.Shipyard, 6));
            Assert.IsFalse(
                plan.TryAllocateInfrastructureHub(planet, BuildingType.TrainingFacility, 4)
            );
            Assert.AreEqual(
                3,
                plan.GetAvailableConstructionSpace(planet, BuildingType.TrainingFacility)
            );
        }

        [Test]
        public void GetAvailableConstructionSpace_IndustryQueued_ReleasesOnlyItsSatisfiedReservation()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit = 2;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "hub",
                faction.InstanceID,
                energyCapacity: 8
            );
            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, faction).StrategicPlan;
            Assert.IsTrue(plan.TryAllocateInfrastructureHub(planet, BuildingType.Shipyard, 6));
            for (int index = 0; index < 6; index++)
            {
                Building shipyard = AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"yard-{index}",
                    BuildingType.Shipyard,
                    ManufacturingType.Ship
                );
                shipyard.ManufacturingStatus = ManufacturingStatus.Building;
            }

            Assert.AreEqual(1, plan.GetAvailableConstructionSpace(planet, BuildingType.Refinery));
            Assert.AreEqual(2, plan.GetAvailableConstructionSpace(planet, BuildingType.Defense));
            Assert.AreEqual(1, plan.GetPlanetaryShieldTarget(planet, true));
        }

        [Test]
        public void GetAvailableConstructionSpace_SelectedIndustry_DoesNotReserveItTwice()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit = 2;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "hub",
                faction.InstanceID,
                energyCapacity: 7
            );
            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, faction).StrategicPlan;
            Assert.IsTrue(plan.TryAllocateInfrastructureHub(planet, BuildingType.Shipyard, 6));
            var selected = new Dictionary<BuildingType, int> { [BuildingType.Shipyard] = 6 };

            Assert.AreEqual(
                1,
                plan.GetAvailableConstructionSpace(
                    planet,
                    BuildingType.Defense,
                    selectedBuildings: selected
                )
            );
            Assert.AreEqual(
                0,
                plan.GetAvailableConstructionSpace(
                    planet,
                    BuildingType.Mine,
                    selectedBuildings: selected
                )
            );
        }

        [Test]
        public void FleetCountChange_Default_RedistributesIndependentMobileStrengthTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            GameConfig.AIFleetDeploymentConfig config = game.Config.AI.FleetDeployment;
            config.MinimumBattleFleetCount = 4;
            config.PlanetsPerBattleFleet = 8;
            config.MinimumAttackStrength = 1400;
            config.MinimumMobileCombatStrength = 5600;
            config.MobileCombatStrengthPerPlanet = 175;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            for (int index = 0; index < 40; index++)
            {
                AITestSceneBuilder.AddPlanet(game, sector, $"planet-{index}", empire.InstanceID);
            }

            AIStrategicPlan eightPlanetPlan = AITestSceneBuilder
                .CreateContext(game, empire)
                .StrategicPlan;
            config.PlanetsPerBattleFleet = 10;
            AIStrategicPlan tenPlanetPlan = AITestSceneBuilder
                .CreateContext(game, empire)
                .StrategicPlan;

            Assert.AreEqual(7000, eightPlanetPlan.TargetMobileCombatStrength);
            Assert.AreEqual(7000, tenPlanetPlan.TargetMobileCombatStrength);
            Assert.AreEqual(5, eightPlanetPlan.TargetBattleFleetCount);
            Assert.AreEqual(1400, eightPlanetPlan.AssemblyFleetCombatStrength);
            Assert.AreEqual(4, tenPlanetPlan.TargetBattleFleetCount);
            Assert.AreEqual(1750, tenPlanetPlan.AssemblyFleetCombatStrength);
        }

        [Test]
        public void MobileStrengthTarget_WithManyTargetFleets_CoversMinimumStrengthForEveryFleet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            GameConfig.AIFleetDeploymentConfig config = game.Config.AI.FleetDeployment;
            config.MinimumBattleFleetCount = 4;
            config.PlanetsPerBattleFleet = 8;
            config.MinimumAttackStrength = 1400;
            config.MinimumMobileCombatStrength = 5600;
            config.MobileCombatStrengthPerPlanet = 175;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            for (int index = 0; index < 73; index++)
                AITestSceneBuilder.AddPlanet(game, sector, $"planet-{index}", empire.InstanceID);

            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, empire).StrategicPlan;

            Assert.AreEqual(10, plan.TargetBattleFleetCount);
            Assert.AreEqual(14000, plan.TargetMobileCombatStrength);
            Assert.AreEqual(1400, plan.AssemblyFleetCombatStrength);
        }

        [Test]
        public void GetInfrastructureTarget_WithTwoShipyardPlanets_ReturnsBothHubTargets()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.ShipyardPrimaryHubTargetCount = 6;
            game.Config.AI.Infrastructure.ShipyardSecondaryHubTargetCount = 4;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            for (int index = 0; index < 2; index++)
                AITestSceneBuilder.AddPlanet(game, sector, $"planet-{index}", empire.InstanceID);

            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, empire).StrategicPlan;

            Assert.AreEqual(10, plan.GetInfrastructureTarget(BuildingType.Shipyard));
        }

        [Test]
        public void GetInfrastructureTarget_WithOneConstructionPlanet_ReturnsPrimaryHubTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.ConstructionFacilityPrimaryHubTargetCount = 5;
            game.Config.AI.Infrastructure.ConstructionFacilitySecondaryHubTargetCount = 4;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            AITestSceneBuilder.AddPlanet(game, sector, "planet", empire.InstanceID);

            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, empire).StrategicPlan;

            Assert.AreEqual(5, plan.GetInfrastructureTarget(BuildingType.ConstructionFacility));
        }

        [Test]
        public void GetInfrastructureTarget_WithTwoPopulatedSectors_AddsTargetsForEachSector()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.ShipyardPrimaryHubTargetCount = 6;
            game.Config.AI.Infrastructure.ShipyardSecondaryHubTargetCount = 4;
            for (int sectorIndex = 0; sectorIndex < 2; sectorIndex++)
            {
                PlanetSector sector = AITestSceneBuilder.AddSector(game, $"sector-{sectorIndex}");
                for (int planetIndex = 0; planetIndex < 2; planetIndex++)
                {
                    AITestSceneBuilder.AddPlanet(
                        game,
                        sector,
                        $"planet-{sectorIndex}-{planetIndex}",
                        empire.InstanceID
                    );
                }
            }

            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, empire).StrategicPlan;

            Assert.AreEqual(20, plan.GetInfrastructureTarget(BuildingType.Shipyard));
            Assert.AreEqual(20, plan.GetInfrastructureDeficit(BuildingType.Shipyard));
        }

        [Test]
        public void GetInfrastructureTarget_WithMultipleTrainingPlanets_UsesOneSectorHub()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.TrainingFacilityHubTargetCount = 4;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            for (int index = 0; index < 3; index++)
                AITestSceneBuilder.AddPlanet(game, sector, $"planet-{index}", empire.InstanceID);

            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, empire).StrategicPlan;

            Assert.AreEqual(4, plan.GetInfrastructureTarget(BuildingType.TrainingFacility));
            Assert.AreEqual(4, plan.GetInfrastructureDeficit(BuildingType.TrainingFacility));
        }

        [Test]
        public void GetInfrastructureDeficit_WithCommittedFacility_CountsFacilityTowardTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.TrainingFacilityHubTargetCount = 4;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "planet", empire.InstanceID);
            Building trainingFacility = AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "training-facility",
                BuildingType.TrainingFacility,
                ManufacturingType.Troop
            );
            trainingFacility.ManufacturingStatus = ManufacturingStatus.Building;

            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, empire).StrategicPlan;

            Assert.AreEqual(1, plan.GetInfrastructureCount(BuildingType.TrainingFacility));
            Assert.AreEqual(3, plan.GetInfrastructureDeficit(BuildingType.TrainingFacility));
        }

        [Test]
        public void GetInfrastructureDeficit_WithScatteredShipyards_PreservesHubShortfall()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.ShipyardPrimaryHubTargetCount = 6;
            game.Config.AI.Infrastructure.ShipyardSecondaryHubTargetCount = 4;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            for (int planetIndex = 0; planetIndex < 3; planetIndex++)
            {
                Planet planet = AITestSceneBuilder.AddPlanet(
                    game,
                    sector,
                    $"planet-{planetIndex}",
                    empire.InstanceID
                );
                for (int facilityIndex = 0; facilityIndex < 2; facilityIndex++)
                {
                    AITestSceneBuilder.AddProductionFacility(
                        game,
                        planet,
                        $"shipyard-{planetIndex}-{facilityIndex}",
                        BuildingType.Shipyard,
                        ManufacturingType.Ship
                    );
                }
            }

            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, empire).StrategicPlan;

            Assert.AreEqual(6, plan.GetInfrastructureCount(BuildingType.Shipyard));
            Assert.AreEqual(10, plan.GetInfrastructureTarget(BuildingType.Shipyard));
            Assert.AreEqual(6, plan.GetInfrastructureDeficit(BuildingType.Shipyard));
        }

        [Test]
        public void CanReinforce_FriendlyShipUnderOpposingFleet_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "planet", empire.InstanceID);
            Fleet receivingFleet = EntityFactory.CreateFleet("receiving-fleet", empire.InstanceID);
            game.AttachNode(receivingFleet, planet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("receiving-ship", empire.InstanceID),
                receivingFleet
            );
            Fleet opposingFleet = EntityFactory.CreateFleet("opposing-fleet", rebels.InstanceID);
            game.AttachNode(opposingFleet, planet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("opposing-ship", rebels.InstanceID),
                opposingFleet
            );
            AITestSceneBuilder.RevealPlanet(game, empire, planet);
            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, empire).StrategicPlan;

            Assert.IsFalse(plan.CanReinforce(receivingFleet));
        }
    }
}
