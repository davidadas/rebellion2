using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Game.World;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Planners
{
    [TestFixture]
    public class AIMissionPlannerTests
    {
        [Test]
        public void Plan_WithNonMainRecruiter_DoesNotAddRecruitmentProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "p1", empire.InstanceID);
            Officer officer = CreateRecruiter("officer", empire.InstanceID, isMain: false);
            game.AttachNode(officer, planet);
            AddRecruitableOfficer(game, empire.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIMissionPlanner().Plan(context);

            Assert.IsFalse(
                proposals
                    .OfType<AIMissionProposal>()
                    .Any(proposal => proposal.MissionType == MissionType.Recruitment)
            );
        }

        [Test]
        public void Plan_WithMainRecruiter_AddsRecruitmentProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "p1", empire.InstanceID);
            Officer officer = CreateRecruiter("officer", empire.InstanceID, isMain: true);
            game.AttachNode(officer, planet);
            AddRecruitableOfficer(game, empire.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIMissionPlanner().Plan(context);

            Assert.IsTrue(
                proposals
                    .OfType<AIMissionProposal>()
                    .Any(proposal => proposal.MissionType == MissionType.Recruitment)
            );
        }

        private static Officer CreateRecruiter(
            string instanceId,
            string ownerInstanceId,
            bool isMain
        )
        {
            Officer officer = EntityFactory.CreateOfficer(instanceId, ownerInstanceId);
            officer.IsMain = isMain;
            officer.Skills[MissionParticipantSkill.Leadership] = 100;
            officer.Skills[MissionParticipantSkill.Diplomacy] = 0;
            officer.Skills[MissionParticipantSkill.Combat] = 0;
            officer.Skills[MissionParticipantSkill.Espionage] = 0;
            return officer;
        }

        private static void AddRecruitableOfficer(GameRoot game, string ownerInstanceId)
        {
            Officer target = EntityFactory.CreateOfficer("recruitable", "neutral");
            target.AllowedOwnerInstanceIDs = new List<string> { ownerInstanceId };
            game.UnrecruitedOfficers.Add(target);
        }
    }
}
