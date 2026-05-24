using System;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Phases;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Game.World;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Phases
{
    [TestFixture]
    public class AIScoringPhaseTests
    {
        [Test]
        public void Execute_WithSupportedProposal_AssignsScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "p1", empire.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.Skills[MissionParticipantSkill.Diplomacy] = 90;
            game.AttachNode(officer, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.AddProposal(new AIMissionProposal(officer, MissionType.Diplomacy, planet));

            new AIScoringPhase().Execute(context);

            Assert.IsTrue(context.Proposals[0].HasScore);
            Assert.Greater(context.Proposals[0].Score, 0);
        }

        [Test]
        public void Execute_WithUnsupportedProposal_ThrowsInvalidOperationException()
        {
            AITurnContext context = new AITurnContext(null, null, null, null, null, null, null);
            context.AddProposal(new TestAIProposal());

            Assert.Throws<InvalidOperationException>(() => new AIScoringPhase().Execute(context));
        }
    }
}
