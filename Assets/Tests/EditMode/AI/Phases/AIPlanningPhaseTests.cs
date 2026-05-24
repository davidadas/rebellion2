using System.Linq;
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
    public class AIPlanningPhaseTests
    {
        [Test]
        public void Execute_WithDiplomacyOpportunity_AddsMissionProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "p1", empire.InstanceID);
            planet.AddVisitor(empire.InstanceID);
            planet.SetPopularSupport(empire.InstanceID, 50);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.Skills[MissionParticipantSkill.Diplomacy] = game.Config
                .AI
                .DiplomacyMinimumSkill;
            game.AttachNode(officer, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            new AIPlanningPhase().Execute(context);

            Assert.IsTrue(
                context
                    .Proposals.OfType<AIMissionProposal>()
                    .Any(proposal => proposal.MissionType == MissionType.Diplomacy)
            );
        }
    }
}
