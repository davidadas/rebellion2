using NUnit.Framework;
using Rebellion.Campaign.AI;
using Rebellion.Campaign.AI.Demands;
using Rebellion.Campaign.AI.Planners;
using Rebellion.Campaign.AI.Proposals;
using Rebellion.Campaign.AI.Scorers;
using Rebellion.Campaign.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Tests.Campaign.AI.Helpers;

namespace Rebellion.Tests.Campaign.AI.Proposals
{
    [TestFixture]
    public class AIAbortMissionProposalTests
    {
        [Test]
        public void Execute_WithActiveOwnedMission_AbortsMission()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "planet", empire.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.MissionReturnParentInstanceID = planet.InstanceID;
            officer.MissionReturnLocationInstanceID = planet.InstanceID;
            StubMission mission = EntityFactory.CreateMission(
                "mission",
                empire.InstanceID,
                planet.InstanceID
            );
            game.AttachNode(mission, planet);
            game.AttachNode(officer, mission);
            mission.Initiate(1);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            new AIAbortMissionProposal(mission).Execute(context);

            Assert.IsNull(mission.GetParent());
            Assert.AreSame(planet, officer.GetParent());
        }

        [Test]
        public void CanSelect_WithDetachedMission_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            StubMission mission = EntityFactory.CreateMission(
                "mission",
                empire.InstanceID,
                "planet"
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            bool canSelect = new AIAbortMissionProposal(mission).CanSelect(context);

            Assert.IsFalse(canSelect);
        }
    }
}
