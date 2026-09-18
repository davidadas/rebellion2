using System.Collections.Generic;
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
    public sealed class AIMissionCandidateSelectorTests
    {
        /// <summary>
        /// Verifies try add executable proposal with zero score does not retain proposal.
        /// </summary>
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
            game.Config.AI.MissionPlanning.Utility.Priority.SubdueUprising.Weight = 1;
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

        /// <summary>
        /// Verifies range selection preserves the highest-scoring candidate when candidates arrive
        /// in the opposite order from their score upper bounds.
        /// </summary>
        [Test]
        public void TryAddRange_ReverseUpperBoundOrder_RetainsHighestScoringCandidate()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet nearTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "near-target",
                empire.InstanceID,
                positionX: 10
            );
            Planet farTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "far-target",
                empire.InstanceID,
                positionX: 100
            );
            nearTarget.BeginUprising();
            farTarget.BeginUprising();
            SpecialForces participant = AITestSceneBuilder.CreateSpecialForces(
                "participant",
                empire.InstanceID
            );
            participant.AllowedMissionTypeIDs.Add(MissionTypeIDs.SubdueUprising);
            participant.Ratings[OfficerRating.Leadership] = 100;
            game.AttachNode(participant, origin);
            game.Config.AI.MissionPlanning.RetainedAlternativesPerMission = 1;
            game.Config.AI.MissionPlanning.MinimumUprisingMissionSuccessPercent = 0;
            game.Config.AI.MissionPlanning.Utility.Objective.TravelCost.Weight = 1;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            List<AIProposal> proposals = new List<AIProposal>();
            AIMissionProposal[] candidates =
            {
                new AIMissionProposal(
                    new[] { participant },
                    MissionTypeIDs.SubdueUprising,
                    farTarget
                ),
                new AIMissionProposal(
                    new[] { participant },
                    MissionTypeIDs.SubdueUprising,
                    nearTarget
                ),
            };

            new AIMissionCandidateSelector().TryAddRange(context, proposals, candidates);

            Assert.AreEqual(
                nearTarget.InstanceID,
                proposals.OfType<AIMissionProposal>().Single().TargetPlanet.InstanceID
            );
        }

        /// <summary>
        /// Verifies upper-bound evaluation does not reorder equal-scoring retained candidates.
        /// </summary>
        [Test]
        public void TryAddRange_EqualScoringCandidates_PreservesInputOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet secondTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "second-target",
                empire.InstanceID,
                positionX: 10
            );
            Planet firstTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "first-target",
                empire.InstanceID,
                positionX: 10
            );
            secondTarget.BeginUprising();
            firstTarget.BeginUprising();
            SpecialForces participant = AITestSceneBuilder.CreateSpecialForces(
                "participant",
                empire.InstanceID
            );
            participant.AllowedMissionTypeIDs.Add(MissionTypeIDs.SubdueUprising);
            participant.Ratings[OfficerRating.Leadership] = 100;
            game.AttachNode(participant, origin);
            game.Config.AI.MissionPlanning.RetainedAlternativesPerMission = 2;
            game.Config.AI.MissionPlanning.MinimumUprisingMissionSuccessPercent = 0;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            List<AIProposal> proposals = new List<AIProposal>();
            AIMissionProposal[] candidates =
            {
                new AIMissionProposal(
                    new[] { participant },
                    MissionTypeIDs.SubdueUprising,
                    secondTarget
                ),
                new AIMissionProposal(
                    new[] { participant },
                    MissionTypeIDs.SubdueUprising,
                    firstTarget
                ),
            };

            new AIMissionCandidateSelector().TryAddRange(context, proposals, candidates);

            CollectionAssert.AreEqual(
                new[] { secondTarget.InstanceID, firstTarget.InstanceID },
                proposals
                    .OfType<AIMissionProposal>()
                    .Select(proposal => proposal.TargetPlanet.InstanceID)
                    .ToArray()
            );
        }

        /// <summary>
        /// Verifies range selection restores input order across distinct mission-type groups.
        /// </summary>
        [Test]
        public void TryAddRange_InterleavedMissionTypes_PreservesInputOrderAcrossGroups()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet firstTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "first-target",
                rebels.InstanceID,
                positionX: 10
            );
            Planet secondTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "second-target",
                rebels.InstanceID,
                positionX: 10
            );
            firstTarget.AddVisitor(empire.InstanceID);
            secondTarget.AddVisitor(empire.InstanceID);
            Officer participant = EntityFactory.CreateOfficer("participant", empire.InstanceID);
            participant.Ratings[OfficerRating.Combat] = 100;
            Officer firstOfficer = EntityFactory.CreateOfficer("first-officer", rebels.InstanceID);
            Officer secondOfficer = EntityFactory.CreateOfficer(
                "second-officer",
                rebels.InstanceID
            );
            game.AttachNode(participant, origin);
            game.AttachNode(firstOfficer, firstTarget);
            game.AttachNode(secondOfficer, secondTarget);
            AITestSceneBuilder.RevealPlanet(game, empire, firstTarget);
            AITestSceneBuilder.RevealPlanet(game, empire, secondTarget);
            game.Config.AI.MissionPlanning.RetainedAlternativesPerMission = 2;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            List<AIProposal> proposals = new List<AIProposal>();
            AIMissionProposal[] candidates =
            {
                new AIMissionProposal(
                    new[] { participant },
                    MissionTypeIDs.Abduction,
                    firstTarget,
                    selectedTarget: firstOfficer,
                    targetOfficer: firstOfficer
                ),
                new AIMissionProposal(
                    new[] { participant },
                    MissionTypeIDs.Assassination,
                    firstTarget,
                    selectedTarget: firstOfficer,
                    targetOfficer: firstOfficer
                ),
                new AIMissionProposal(
                    new[] { participant },
                    MissionTypeIDs.Abduction,
                    secondTarget,
                    selectedTarget: secondOfficer,
                    targetOfficer: secondOfficer
                ),
                new AIMissionProposal(
                    new[] { participant },
                    MissionTypeIDs.Assassination,
                    secondTarget,
                    selectedTarget: secondOfficer,
                    targetOfficer: secondOfficer
                ),
            };

            new AIMissionCandidateSelector().TryAddRange(context, proposals, candidates);

            CollectionAssert.AreEqual(candidates, proposals.OfType<AIMissionProposal>().ToArray());
        }
    }
}
