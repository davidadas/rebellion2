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
        /// <summary>
        /// Verifies score with fleet reinforcement deducts travel penalty.
        /// </summary>
        [Test]
        public void Score_WithDifferentDemandPressure_PreservesPressureDifference()
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

            Assert.That(lowScore, Is.EqualTo(1.0 / 7).Within(0.0001));
            Assert.That(highScore, Is.EqualTo(5.0 / 11).Within(0.0001));
        }

        /// <summary>
        /// Verifies score withfleetreinforcement deductstravelpenalty.
        /// </summary>
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
            Assert.Zero(farScore);
        }

        /// <summary>
        /// Verifies economy recovery remains selectable below the maintenance reserve.
        /// </summary>
        [TestCase(AIDemandKind.Mine, BuildingType.Mine)]
        [TestCase(AIDemandKind.Refinery, BuildingType.Refinery)]
        public void Score_WithEconomyRecoveryBelowMaintenanceReserve_ReturnsPositiveScore(
            AIDemandKind kind,
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
                new AIDemand(
                    $"{kind}-demand",
                    kind,
                    ManufacturingType.Building,
                    buildingType,
                    producer,
                    1,
                    100
                ),
                producer,
                new Technology(template)
            );

            double score = new AIProductionProposalScorer().Score(context, proposal);

            Assert.Greater(score, 0);
        }

        /// <summary>
        /// Verifies an Outer Rim economy building remains useful before local construction is founded.
        /// </summary>
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
                new AIDemand(
                    "refinery-demand",
                    AIDemandKind.Refinery,
                    ManufacturingType.Building,
                    BuildingType.Refinery,
                    destination,
                    1,
                    100
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
        /// <param name="pressure">The pressure value.</param>
        /// <returns>The operation result.</returns>
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
