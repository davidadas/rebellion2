using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Scoring;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Scoring
{
    [TestFixture]
    public class AIFleetReinforcementUtilityTests
    {
        [Test]
        public void ScoreDefenseNeed_WithStrengthGap_EvaluatesConfiguredCurve()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            game.Config.AI.FleetDeployment.DefenseAllocationUtility.ReinforcementNeed.Weight = 100;
            game.Config.AI.FleetDeployment.DefenseAllocationUtility.ReinforcementNeed.InputMaximum =
                1000;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            double score = AIFleetReinforcementUtility.ScoreDefenseNeed(
                context,
                headquarters,
                availableStrength: 400
            );

            Assert.AreEqual(60, score);
        }
    }
}
