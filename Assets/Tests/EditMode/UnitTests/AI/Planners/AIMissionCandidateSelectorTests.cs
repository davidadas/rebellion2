using System.Collections.Generic;
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
    public sealed class AIMissionCandidateSelectorTests
    {
        [Test]
        public void TryAdd_ExecutableProposalWithZeroScore_DoesNotRetainProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "uprising",
                empire.InstanceID
            );
            planet.BeginUprising();
            planet.SetPopularSupport(empire.InstanceID, 0);
            SpecialForces participant = AITestSceneBuilder.CreateSpecialForces(
                "participant",
                empire.InstanceID
            );
            participant.AllowedMissionTypeIDs.Add(MissionTypeIDs.SubdueUprising);
            participant.Ratings[OfficerRating.Leadership] = 0;
            game.AttachNode(participant, planet);
            game.Config.ProbabilityTables.Mission.SubdueUprising = new Dictionary<int, int>
            {
                { -1000, 19 },
            };
            game.Config.AI.MissionPlanning.MinimumUprisingMissionSuccessPercent = 20;
            game.Config.AI.MissionPlanning.SubdueUprisingPriorityBonus = 120;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposal proposal = new AIMissionProposal(
                new[] { participant },
                MissionTypeIDs.SubdueUprising,
                planet
            );
            List<AIProposal> proposals = new List<AIProposal>();

            new AIMissionCandidateSelector().TryAdd(context, proposals, proposal);

            Assert.IsTrue(proposal.CanExecute(context));
            Assert.IsEmpty(proposals);
        }
    }
}
