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
        public void Score_WithDifferentDemandPressure_PreservesPressureDifference()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.DemandUtility.Weight = 1;
            game.Config.AI.Selection.DemandUtility.InputMaximum = 600;
            game.Config.AI.Selection.ProductionUtility.TravelCost.Weight = 0;
            game.Config.AI.Selection.ProductionUtility.HeadroomRisk.Weight = 0;
            game.Config.AI.Selection.ProductionUtility.Shortfall.Weight = 0;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet producer = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "producer",
                empire.InstanceID
            );
            Building buildingTemplate = AITestSceneBuilder.CreateBuildingTemplate(
                "construction-yard",
                BuildingType.ConstructionFacility
            );
            buildingTemplate.OwnerInstanceID = empire.InstanceID;
            Technology building = new Technology(buildingTemplate);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIProductionProposalScorer scorer = new AIProductionProposalScorer();

            double lowScore = scorer.Score(
                context,
                CreateBuildingProposal(producer, building, 100)
            );
            double highScore = scorer.Score(
                context,
                CreateBuildingProposal(producer, building, 500)
            );

            Assert.That(lowScore, Is.EqualTo(1.0 / 6).Within(0.0001));
            Assert.That(highScore, Is.EqualTo(5.0 / 6).Within(0.0001));
        }

        [Test]
        public void Score_WithFleetReinforcement_DeductsTravelPenalty()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.ProductionUtility.TravelCost.Weight = 1;
            game.Config.AI.Selection.ProductionUtility.TravelCost.InputMaximum = 100;
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet nearProducer = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "near",
                empire.InstanceID,
                positionX: 0,
                positionY: 0
            );
            Planet farProducer = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "far",
                empire.InstanceID,
                positionX: 240,
                positionY: 0
            );
            Planet fleetPlanet = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "fleet-world",
                empire.InstanceID,
                positionX: 1,
                positionY: 0
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, fleetPlanet);
            AIDemand demand = new AIDemand(
                "fleet-regiment-demand",
                AIDemandKind.FleetRegiment,
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
            Assert.Greater(farScore, 0);
        }

        private static AIManufactureProposal CreateBuildingProposal(
            Planet producer,
            Technology building,
            double pressure
        )
        {
            return new AIManufactureProposal(
                new AIDemand(
                    $"building-{pressure}",
                    AIDemandKind.ConstructionFacility,
                    ManufacturingType.Building,
                    BuildingType.ConstructionFacility,
                    producer,
                    1,
                    pressure
                ),
                producer,
                building
            );
        }
    }
}
