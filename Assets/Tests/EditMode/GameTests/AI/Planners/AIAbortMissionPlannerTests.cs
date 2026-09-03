using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Planners
{
    [TestFixture]
    public class AIAbortMissionPlannerTests
    {
        [Test]
        public void Plan_WithUnknownMissionTarget_AddsAbortProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            StubMission mission = EntityFactory.CreateMission(
                "mission",
                empire.InstanceID,
                target.InstanceID
            );
            game.AttachNode(mission, target);
            game.AttachNode(officer, mission);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIAbortMissionProposal proposal = new AIAbortMissionPlanner()
                .Plan(context)
                .OfType<AIAbortMissionProposal>()
                .Single();

            Assert.AreSame(mission, proposal.Mission);
        }

        [Test]
        public void Plan_WithSafeSpecialForcesMission_DoesNotAddAbortProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "planet", empire.InstanceID);
            SpecialForces specialForces = AITestSceneBuilder.CreateSpecialForces(
                "special-forces",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            StubMission mission = EntityFactory.CreateMission(
                "mission",
                empire.InstanceID,
                planet.InstanceID
            );
            game.AttachNode(mission, planet);
            game.AttachNode(specialForces, mission);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            bool hasAbortProposal = new AIAbortMissionPlanner()
                .Plan(context)
                .OfType<AIAbortMissionProposal>()
                .Any();

            Assert.IsFalse(hasAbortProposal);
        }
    }
}
