using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scoring;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Scoring
{
    [TestFixture]
    public class AIMissionProposalScorerTests
    {
        [Test]
        public void Score_DiplomacyProposal_ReturnsHigherScoreForLowerSupportPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet lowSupport = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "low-support",
                empire.InstanceID
            );
            Planet highSupport = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "high-support",
                empire.InstanceID
            );
            lowSupport.SetPopularSupport(empire.InstanceID, 10);
            highSupport.SetPopularSupport(empire.InstanceID, 90);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.Ratings[OfficerRating.Diplomacy] = 100;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double lowSupportScore = scorer.Score(
                context,
                new AIMissionProposal(officer, MissionType.Diplomacy, lowSupport)
            );
            double highSupportScore = scorer.Score(
                context,
                new AIMissionProposal(officer, MissionType.Diplomacy, highSupport)
            );

            Assert.Greater(lowSupportScore, highSupportScore);
        }

        [Test]
        public void Score_TargetedOfficerMission_ReturnsLowerScoreForStrongerTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet enemyPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "enemy",
                rebels.InstanceID
            );
            Officer actor = EntityFactory.CreateOfficer("actor", empire.InstanceID);
            actor.Ratings[OfficerRating.Combat] = 100;
            Officer weakTarget = EntityFactory.CreateOfficer("weak", rebels.InstanceID);
            weakTarget.Ratings[OfficerRating.Combat] = 10;
            Officer strongTarget = EntityFactory.CreateOfficer("strong", rebels.InstanceID);
            strongTarget.Ratings[OfficerRating.Combat] = 90;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double weakTargetScore = scorer.Score(
                context,
                new AIMissionProposal(actor, MissionType.Abduction, enemyPlanet, weakTarget)
            );
            double strongTargetScore = scorer.Score(
                context,
                new AIMissionProposal(actor, MissionType.Abduction, enemyPlanet, strongTarget)
            );

            Assert.Greater(weakTargetScore, strongTargetScore);
        }
    }
}
