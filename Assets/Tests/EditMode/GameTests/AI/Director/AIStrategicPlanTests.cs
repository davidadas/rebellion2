using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Director
{
    [TestFixture]
    public class AIStrategicPlanTests
    {
        [Test]
        public void FleetCountChange_RedistributesIndependentMobileStrengthTarget()
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
                AITestSceneBuilder.AddPlanet(
                    game,
                    sector,
                    $"planet-{index}",
                    empire.InstanceID
                );
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
    }
}
