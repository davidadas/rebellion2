using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scoring;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Scoring
{
    [TestFixture]
    public class AIFleetProposalScorerTests
    {
        [Test]
        public void Score_AttackProposalForHeadquarters_ReturnsHigherScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackStrategicValueWeight = 0;
            game.Config.AI.FleetDeployment.AttackReadinessWeight = 0;
            game.Config.AI.FleetDeployment.AttackCaptureViabilityWeight = 0;
            game.Config.AI.FleetDeployment.AttackTravelEfficiencyWeight = 0;
            game.Config.AI.FleetDeployment.AttackExpectedLossPenaltyWeight = 0;
            game.Config.AI.FleetDeployment.AttackOpportunityCostPenaltyWeight = 0;
            game.Config.AI.FleetDeployment.HeadquartersAttackBonus = 50;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet normalTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "normal",
                rebels.InstanceID
            );
            Planet headquartersTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "hq",
                rebels.InstanceID
            );
            headquartersTarget.IsHeadquarters = true;
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.AddChild(AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID));
            game.AttachNode(fleet, owned);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetProposalScorer scorer = new AIFleetProposalScorer();

            double normalScore = scorer.Score(
                context,
                new AIFleetAttackProposal(
                    fleet,
                    FleetOrderType.Attack,
                    FleetOrderStatus.Staging,
                    normalTarget
                )
            );
            double headquartersScore = scorer.Score(
                context,
                new AIFleetAttackProposal(
                    fleet,
                    FleetOrderType.Attack,
                    FleetOrderStatus.Staging,
                    headquartersTarget
                )
            );

            Assert.Greater(headquartersScore, normalScore);
        }
    }
}
