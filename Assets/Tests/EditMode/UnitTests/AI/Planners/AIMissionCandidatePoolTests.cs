using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Planners
{
    [TestFixture]
    public sealed class AIMissionCandidatePoolTests
    {
        /// <summary>
        /// Verifies only strictly inferior upper bounds are eliminated before evaluation.
        /// </summary>
        /// <param name="distance">Distance to the next mission target.</param>
        /// <param name="expectedEligible">Whether its optimistic score can enter the retained alternatives.</param>
        [TestCase(0, true)]
        [TestCase(100, false)]
        public void CanRetain_FilledAlternatives_RejectsOnlyStrictlyInferiorUpperBounds(
            int distance,
            bool expectedEligible
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", empire.InstanceID);
            target.PositionX = origin.PositionX + distance;
            target.PositionY = origin.PositionY;
            origin.BeginUprising();
            target.BeginUprising();
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.Ratings[SkillRating.Leadership] = 100;
            game.AttachNode(officer, origin);
            game.Config.AI.MissionPlanning.RetainedAlternativesPerMission = 1;
            game.Config.AI.MissionPlanning.Utility.Priority.SubdueUprising.Weight = 1;
            game.Config.AI.MissionPlanning.Utility.Objective.TravelCost.Weight = 1;
            game.Config.ProbabilityTables.Mission.SubdueUprising = new Dictionary<int, int>
            {
                { -1000, 100 },
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            var pool = new AIMissionCandidatePool();
            var proposals = new List<AIProposal>();
            pool.TryAdd(
                context,
                proposals,
                new AIMissionProposal(new[] { officer }, MissionTypeIDs.SubdueUprising, origin)
            );
            Assert.AreEqual(1, proposals.Count);

            bool eligible = pool.CanRetain(
                context,
                new AIMissionProposal(new[] { officer }, MissionTypeIDs.SubdueUprising, target)
            );

            Assert.AreEqual(expectedEligible, eligible);
        }

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
            participant.AllowedMissionTypeIDs.Add(SubdueUprisingMission.MissionTypeID);
            participant.Ratings[SkillRating.Leadership] = 0;
            game.AttachNode(participant, planet);
            game.Config.ProbabilityTables.Mission.SubdueUprising = new Dictionary<int, int>
            {
                { -1000, 19 },
            };
            game.Config.AI.MissionPlanning.MinimumUprisingMissionSuccessPercent = 20;
            game.Config.AI.MissionPlanning.Utility.Priority.SubdueUprising.Weight = 1;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposal proposal = new AIMissionProposal(
                new[] { participant },
                SubdueUprisingMission.MissionTypeID,
                planet
            );
            List<AIProposal> proposals = new List<AIProposal>();

            new AIMissionCandidatePool().TryAdd(context, proposals, proposal);

            Assert.IsTrue(proposal.CanExecute(context));
            Assert.IsEmpty(proposals);
        }
    }
}
