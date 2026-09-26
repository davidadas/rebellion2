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
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Scorers
{
    [TestFixture]
    public class AIProductionProposalScorerTests
    {
        /// <summary>
        /// Verifies mandatory facility cleanup receives the neutral domain score.
        /// </summary>
        [Test]
        public void Score_FacilityRemovalProposal_ReturnsZero()
        {
            AIProductionProposalScorer scorer = new AIProductionProposalScorer();

            double score = scorer.Score(
                null,
                new AIFacilityRemovalProposal(null, BuildingType.Shipyard, 1, 0)
            );

            Assert.Zero(score);
        }

        [Test]
        public void Score_WithDifferentBaseDemandPercent_PreservesUrgencyDifference()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.DemandUtility.Weight = 1;
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

            Assert.Greater(highScore, lowScore);
        }

        [Test]
        public void Score_WithFleetReinforcement_DeductsTravelPenalty()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.ProductionUtility.TravelCost.Weight = 1;
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
            AIProductionDemand demand = new AIProductionDemand(
                "fleet-regiment-demand",
                AIProductionDemandKind.FleetRegiment,
                ManufacturingType.Troop,
                BuildingType.None,
                fleet,
                1,
                baseDemandPercent: 100
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
            Assert.Zero(farScore);
        }

        [TestCase(AIProductionDemandKind.Mine, BuildingType.Mine)]
        [TestCase(AIProductionDemandKind.Refinery, BuildingType.Refinery)]
        public void Score_WithEconomyRecoveryBelowMaintenanceReserve_ReturnsPositiveScore(
            AIProductionDemandKind kind,
            BuildingType buildingType
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.DemandUtility.Weight = 1;
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
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            game.Config.AI.Selection.MaintenanceHeadroomReserve =
                context.Assessment.ProjectedMaintenanceHeadroom + 100;
            Building template = AITestSceneBuilder.CreateBuildingTemplate(
                $"{kind}-template",
                buildingType,
                ManufacturingType.None
            );
            template.MaintenanceCost = 10;
            AIManufactureProposal proposal = new AIManufactureProposal(
                new AIProductionDemand(
                    $"{kind}-demand",
                    kind,
                    ManufacturingType.Building,
                    buildingType,
                    producer,
                    1,
                    baseDemandPercent: 100
                ),
                producer,
                new Technology(template)
            );

            double score = new AIProductionProposalScorer().Score(context, proposal);

            Assert.Greater(score, 0);
        }

        [Test]
        public void Score_WithOuterRimEconomyDestination_ReturnsPositiveScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.ProductionUtility.TravelCost.Weight = 0;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "outer-rim");
            sector.SectorType = PlanetSectorType.OuterRim;
            Planet destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "economy-world",
                empire.InstanceID
            );
            Building template = AITestSceneBuilder.CreateBuildingTemplate(
                "refinery",
                BuildingType.Refinery,
                ManufacturingType.None
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                new AIProductionDemand(
                    "refinery-demand",
                    AIProductionDemandKind.Refinery,
                    ManufacturingType.Building,
                    BuildingType.Refinery,
                    destination,
                    1,
                    baseDemandPercent: 100
                ),
                destination,
                new Technology(template)
            );

            double score = new AIProductionProposalScorer().Score(
                AITestSceneBuilder.CreateContext(game, empire),
                proposal
            );

            Assert.Greater(score, 0);
        }

        /// <summary>
        /// Creates a building-production proposal for a test scenario.
        /// </summary>
        /// <param name="producer">The producer value.</param>
        /// <param name="building">The building value.</param>
        /// <param name="baseDemandPercent">The configured base demand percentage.</param>
        /// <returns>The operation result.</returns>
        private static AIManufactureProposal CreateBuildingProposal(
            Planet producer,
            Technology building,
            int baseDemandPercent
        )
        {
            return new AIManufactureProposal(
                new AIProductionDemand(
                    $"building-{baseDemandPercent}",
                    AIProductionDemandKind.ConstructionFacility,
                    ManufacturingType.Building,
                    BuildingType.ConstructionFacility,
                    producer,
                    1,
                    baseDemandPercent: baseDemandPercent,
                    targetCount: 1
                ),
                producer,
                building
            );
        }
    }
}
