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
        public void MobileStrengthTarget_CoversMinimumStrengthForEveryTargetFleet()
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
        public void GetInfrastructureTarget_WithShipyardRatio_ReturnsFactionWideRequirement()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.PlanetsPerShipyard = 2;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            for (int index = 0; index < 5; index++)
                AITestSceneBuilder.AddPlanet(game, sector, $"planet-{index}", empire.InstanceID);

            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, empire).StrategicPlan;

            Assert.AreEqual(3, plan.GetInfrastructureTarget(BuildingType.Shipyard));
        }

        [Test]
        public void GetInfrastructureTarget_WithConstructionLaneFloor_PreservesMinimum()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.PlanetsPerConstructionFacility = 100;
            game.Config.AI.Infrastructure.MinimumConstructionFacilityLanes = 4;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            for (int index = 0; index < 5; index++)
                AITestSceneBuilder.AddPlanet(game, sector, $"planet-{index}", empire.InstanceID);

            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, empire).StrategicPlan;

            Assert.AreEqual(4, plan.GetInfrastructureTarget(BuildingType.ConstructionFacility));
        }

        [Test]
        public void GetInfrastructureDeficit_WithCommittedFacility_CountsFacilityTowardTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.PlanetsPerTrainingFacility = 1;
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
            Assert.AreEqual(0, plan.GetInfrastructureDeficit(BuildingType.TrainingFacility));
        }
    }
}
