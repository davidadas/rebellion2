using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;
using Rebellion.Game.World;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Director
{
    [TestFixture]
    public class AIAssessmentTests
    {
        [Test]
        public void Constructor_WithMixedPlanetOwnership_BuildsOwnershipLists()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Planet neutral = AITestSceneBuilder.AddPlanet(game, system, "neutral", null);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            CollectionAssert.Contains(assessment.OwnedPlanets, owned);
            CollectionAssert.Contains(assessment.EnemyPlanets, enemy);
            CollectionAssert.Contains(assessment.NeutralPlanets, neutral);
        }

        [Test]
        public void Constructor_WithEnemyOfficer_BuildsTargetableEnemyOfficerTargets()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Officer target = EntityFactory.CreateOfficer("target", rebels.InstanceID);
            game.AttachNode(target, enemy);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(1, assessment.TargetableEnemyOfficerMissionTargets.Count);
            Assert.AreSame(enemy, assessment.TargetableEnemyOfficerMissionTargets[0].Planet);
            Assert.AreSame(
                target,
                assessment.TargetableEnemyOfficerMissionTargets[0].TargetOfficer
            );
        }
    }
}
