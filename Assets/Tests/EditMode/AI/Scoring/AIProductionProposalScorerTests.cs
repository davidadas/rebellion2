using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scoring;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Scoring
{
    [TestFixture]
    public class AIProductionProposalScorerTests
    {
        [Test]
        public void Score_WithFleetReinforcement_DeductsTravelPenalty()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.FleetReinforcementTravelPenaltyWeight = 1;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet nearProducer = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "near",
                empire.InstanceID,
                positionX: 0,
                positionY: 0
            );
            Planet farProducer = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "far",
                empire.InstanceID,
                positionX: 100,
                positionY: 0
            );
            Planet fleetPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "fleet-world",
                empire.InstanceID,
                positionX: 1,
                positionY: 0
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, fleetPlanet);
            AIProductionDemand demand = new AIProductionDemand(
                "fleet-regiment-demand",
                AIProductionDemandKind.FleetRegiment,
                ManufacturingType.Troop,
                BuildingType.None,
                fleet,
                1,
                100
            );
            Technology regiment = new Technology(
                AITestSceneBuilder.CreateRegiment("regiment", empire.InstanceID)
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIProductionProposalScorer scorer = new AIProductionProposalScorer();

            double nearScore = scorer.Score(
                context,
                new AIManufactureProposal(demand, nearProducer, regiment)
            );
            double farScore = scorer.Score(
                context,
                new AIManufactureProposal(demand, farProducer, regiment)
            );

            Assert.Greater(nearScore, farScore);
        }
    }
}
