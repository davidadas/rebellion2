using System.Collections.Generic;
using System.Linq;
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
using Rebellion.Gameplay;
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

        [TestCase(false)]
        [TestCase(true)]
        public void TryAdd_EquivalentAgents_RetainsTheirExactScoresAndIdentities(
            bool differentOrigin
        )
        {
            var (context, first, second, target) = CreateAgentContext(differentOrigin);
            AIMissionProposalScorer scorer = new();
            AIMissionCandidatePool pool = new();
            pool.Reset(context);
            List<AIProposal> proposals = new();
            foreach (SpecialForces agent in new[] { first, second })
            {
                AIMissionProposal expected = new(new[] { agent }, MissionTypeIDs.Espionage, target);
                AIMissionProposal actual = new(new[] { agent }, MissionTypeIDs.Espionage, target);
                double expectedScore = scorer.Score(context, expected);

                pool.TryAdd(context, proposals, actual);

                Assert.That(expectedScore, Is.GreaterThan(0));
                Assert.That(actual.Score, Is.EqualTo(expectedScore));
                Assert.That(actual.FoilProbability, Is.EqualTo(expected.FoilProbability));
                Assert.That(actual.Participant, Is.SameAs(agent));
            }
            Assert.That(proposals.Count, Is.EqualTo(2));
        }

        [Test]
        public void TryAdd_IdenticalScoreAndIdentity_RetainsLatestAlternative()
        {
            var (context, first, _, target) = CreateAgentContext(false);
            context.Game.Config.AI.MissionPlanning.RetainedAlternativesPerMission = 1;
            AIMissionCandidatePool pool = new();
            pool.Reset(context);
            List<AIProposal> proposals = new();
            AIMissionProposal earlier = new(new[] { first }, MissionTypeIDs.Espionage, target);
            AIMissionProposal later = new(new[] { first }, MissionTypeIDs.Espionage, target);

            pool.TryAdd(context, proposals, earlier);
            pool.TryAdd(context, proposals, later);

            Assert.That(later.Score, Is.EqualTo(earlier.Score).And.GreaterThan(0));
            Assert.That(proposals, Has.Count.EqualTo(1));
            Assert.That(proposals[0], Is.SameAs(later));
        }

        [Test]
        public void TryAdd_CallerSuppliedOdds_PreservesIndividualForecast()
        {
            var (context, first, second, target) = CreateAgentContext(false);
            AIMissionCandidatePool pool = new();
            pool.Reset(context);
            List<AIProposal> proposals = new();
            pool.TryAdd(context, proposals, new(new[] { first }, MissionTypeIDs.Espionage, target));
            AIMissionProposal actual = new(new[] { second }, MissionTypeIDs.Espionage, target);
            actual.SetOperationalOdds(new MissionOdds(100, 75, 0));
            AIMissionProposal expected = new(new[] { second }, MissionTypeIDs.Espionage, target);
            expected.SetOperationalOdds(new MissionOdds(100, 75, 0));
            double expectedScore = new AIMissionProposalScorer().Score(context, expected);

            pool.TryAdd(context, proposals, actual);

            Assert.That(actual.Score, Is.EqualTo(expectedScore));
            Assert.That(actual.FoilProbability, Is.EqualTo(75));
            Assert.That(proposals.Count, Is.EqualTo(2));
        }

        [Test]
        public void TryAdd_NewPlanningContext_UsesCurrentInputs()
        {
            var (context, first, second, target) = CreateAgentContext(false);
            AIMissionCandidatePool pool = new();
            pool.Reset(context);
            List<AIProposal> proposals = new();
            pool.TryAdd(context, proposals, new(new[] { first }, MissionTypeIDs.Espionage, target));
            double earlierScore = proposals.Single().Score;
            context.Game.Config.AI.MissionPlanning.Utility.Priority.Espionage.Weight = 0;
            context.Game.Config.AI.MissionPlanning.Utility.Objective.Success.Weight = 1;
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            AIMissionProposal expected = new(new[] { second }, MissionTypeIDs.Espionage, target);
            AIMissionProposal actual = new(new[] { second }, MissionTypeIDs.Espionage, target);
            double expectedScore = new AIMissionProposalScorer().Score(context, expected);

            pool.TryAdd(context, proposals, actual);

            Assert.That(expectedScore, Is.GreaterThan(0).And.Not.EqualTo(earlierScore));
            Assert.That(actual.Score, Is.EqualTo(expectedScore));
        }

        /// <summary>
        /// Creates equivalent agents and an observed espionage objective.
        /// </summary>
        /// <param name="differentOrigin">Whether the agents depart from different planets.</param>
        /// <returns>The planning scope, agents, and observed target.</returns>
        private static (
            AITurnContext Context,
            SpecialForces First,
            SpecialForces Second,
            Planet Target
        ) CreateAgentContext(bool differentOrigin)
        {
            AITurnContext context = AITestSceneBuilder.CreateMissionDefenseContext(100);
            GameRoot game = context.Game;
            Planet origin = game.GetSceneNodeByInstanceID<Planet>("origin");
            Planet secondOrigin = differentOrigin
                ? AITestSceneBuilder.AddPlanet(
                    game,
                    origin.GetParentOfType<PlanetSector>(),
                    "other-origin",
                    context.Faction.InstanceID
                )
                : origin;
            secondOrigin.PositionX = origin.PositionX + (differentOrigin ? 100 : 0);
            SpecialForces first = AITestSceneBuilder.CreateSpecialForces(
                "first",
                context.Faction.InstanceID
            );
            SpecialForces second = AITestSceneBuilder.CreateSpecialForces(
                "second",
                context.Faction.InstanceID
            );
            foreach (SpecialForces agent in new[] { first, second })
            {
                agent.TypeID = "agent";
                agent.Ratings[SkillRating.Espionage] = 50;
                agent.AllowedMissionTypeIDs.Add(MissionTypeIDs.Espionage);
            }
            game.AttachNode(first, origin);
            game.AttachNode(second, secondOrigin);
            game.Config.AI.MissionPlanning.Utility.Priority.Espionage.Weight = 1;
            game.Config.AI.MissionPlanning.MinimumMissionScore = 0;
            game.Config.ProbabilityTables.Mission.Espionage = new Dictionary<int, int>
            {
                { -1000, 100 },
            };
            context = AITestSceneBuilder.CreateContext(game, context.Faction);
            return (context, first, second, context.Assessment.GetKnownPlanet("target"));
        }
    }
}
