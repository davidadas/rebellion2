using NUnit.Framework;
using Rebellion.AI.Core;
using Rebellion.AI.Fleets;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Tests.AI.Core.Helpers;

namespace Rebellion.Tests.AI.Fleets
{
    [TestFixture]
    public class AIFleetReinforcementUtilityTests
    {
        [Test]
        public void ScoreDefenseNeed_WithStrengthGap_EvaluatesConfiguredCurve()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            game.Config.AI.FleetDeployment.DefenseAllocationUtility.ReinforcementNeed.Weight = 1;
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

            Assert.AreEqual(0.06, score);
        }
    }
}
