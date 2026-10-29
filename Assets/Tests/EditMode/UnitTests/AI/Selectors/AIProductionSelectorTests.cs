using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Proposals;
using Rebellion.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Selectors
{
    [TestFixture]
    public class AIProductionSelectorTests
    {
        [Test]
        public void OrderInfrastructureProduction_ConstructionHubCovered_PrioritizesItsShipyard()
        {
            AITurnContext context = CreateContext(
                5,
                out Planet construction,
                out Planet shipyards,
                out Planet expansion
            );
            AIManufactureProposal shipyard = CreateProposal(shipyards, BuildingType.Shipyard, 0.5);
            AIManufactureProposal anotherHub = CreateProposal(
                expansion,
                BuildingType.ConstructionFacility,
                0.9
            );
            AIProductionSelector selector = new(new AISelectionState());

            List<AIProposal> ordered = selector.OrderInfrastructureProduction(
                context,
                new AIProposal[] { anotherHub, shipyard }
            );

            Assert.AreSame(shipyard, ordered[0]);
        }

        [Test]
        public void OrderInfrastructureProduction_LastConstructionYardSelected_SwitchesToShipyards()
        {
            AITurnContext context = CreateContext(
                4,
                out Planet construction,
                out Planet shipyards,
                out Planet expansion
            );
            AIManufactureProposal shipyard = CreateProposal(shipyards, BuildingType.Shipyard, 0.5);
            AIManufactureProposal anotherHub = CreateProposal(
                expansion,
                BuildingType.ConstructionFacility,
                0.9
            );
            AIProductionSelector selector = new(new AISelectionState());
            selector.Reserve(CreateProposal(construction, BuildingType.ConstructionFacility, 0.9));

            List<AIProposal> ordered = selector.OrderInfrastructureProduction(
                context,
                new AIProposal[] { anotherHub, shipyard }
            );

            Assert.AreSame(shipyard, ordered[0]);
        }

        [Test]
        public void OrderInfrastructureProduction_UnrelatedProduction_PreservesItsPosition()
        {
            AITurnContext context = CreateContext(
                5,
                out _,
                out Planet shipyards,
                out Planet expansion
            );
            AIManufactureProposal shipyard = CreateProposal(shipyards, BuildingType.Shipyard, 0.5);
            AIManufactureProposal anotherHub = CreateProposal(
                expansion,
                BuildingType.ConstructionFacility,
                0.9
            );
            AIManufactureProposal mine = CreateProposal(expansion, BuildingType.Mine, 0.7);
            AIProductionSelector selector = new(new AISelectionState());

            List<AIProposal> ordered = selector.OrderInfrastructureProduction(
                context,
                new AIProposal[] { anotherHub, mine, shipyard }
            );

            Assert.AreSame(mine, ordered[1]);
        }

        [Test]
        public void OrderInfrastructureProduction_LowerPriorityExpansion_RemainsAvailableAsFallback()
        {
            AITurnContext context = CreateContext(
                5,
                out _,
                out Planet shipyards,
                out Planet expansion
            );
            AIManufactureProposal shipyard = CreateProposal(shipyards, BuildingType.Shipyard, 0.5);
            AIManufactureProposal anotherHub = CreateProposal(
                expansion,
                BuildingType.ConstructionFacility,
                0.9
            );
            AIProductionSelector selector = new(new AISelectionState());

            List<AIProposal> ordered = selector.OrderInfrastructureProduction(
                context,
                new AIProposal[] { anotherHub, shipyard }
            );

            CollectionAssert.AreEquivalent(new AIProposal[] { anotherHub, shipyard }, ordered);
        }

        /// <summary>
        /// Creates an established sector and a separate expansion opportunity.
        /// </summary>
        /// <param name="constructionCount">The committed construction-yard count.</param>
        /// <param name="construction">Receives the primary construction site.</param>
        /// <param name="shipyards">Receives the primary shipyard site.</param>
        /// <param name="expansion">Receives the other sector's available site.</param>
        /// <returns>The faction's industrial turn context.</returns>
        private static AITurnContext CreateContext(
            int constructionCount,
            out Planet construction,
            out Planet shipyards,
            out Planet expansion
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out _);
            game.Config.AI.Infrastructure.ConstructionFacilityPrimaryHubTargetCount = 5;
            game.Config.AI.Infrastructure.ShipyardPrimaryHubTargetCount = 6;
            game.Config.AI.Infrastructure.IndustrialShieldTargetCount = 0;
            game.Config.AI.Infrastructure.PlanetaryWeaponTargetCount = 0;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "developed");
            construction = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "construction",
                faction.InstanceID,
                energyCapacity: 5
            );
            shipyards = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "shipyards",
                faction.InstanceID,
                energyCapacity: 6
            );
            construction.SetPopularSupport(faction.InstanceID, 100);
            shipyards.SetPopularSupport(faction.InstanceID, 100);
            for (int index = 0; index < constructionCount; index++)
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    construction,
                    "yard-" + index,
                    BuildingType.ConstructionFacility,
                    ManufacturingType.Building
                );
            expansion = AITestSceneBuilder.AddPlanet(
                game,
                AITestSceneBuilder.AddSector(game, "expansion"),
                "new-hub",
                faction.InstanceID,
                energyCapacity: 6
            );
            expansion.SetPopularSupport(faction.InstanceID, 100);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            context.StrategicPlan.TryAllocateInfrastructureHub(
                construction,
                BuildingType.ConstructionFacility,
                5
            );
            context.StrategicPlan.TryAllocateInfrastructureHub(shipyards, BuildingType.Shipyard, 6);
            return context;
        }

        /// <summary>
        /// Creates a scored building request using the destination as its producer.
        /// </summary>
        /// <param name="planet">The production destination.</param>
        /// <param name="buildingType">The requested facility category.</param>
        /// <param name="score">The proposal's global utility score.</param>
        /// <returns>The ranked production proposal.</returns>
        private static AIManufactureProposal CreateProposal(
            Planet planet,
            BuildingType buildingType,
            double score
        )
        {
            AIProductionDemandKind kind = buildingType switch
            {
                BuildingType.ConstructionFacility => AIProductionDemandKind.ConstructionFacility,
                BuildingType.Shipyard => AIProductionDemandKind.Shipyard,
                _ => AIProductionDemandKind.Mine,
            };
            AIProductionDemand demand = new(
                planet.InstanceID + buildingType,
                kind,
                ManufacturingType.Building,
                buildingType,
                planet,
                1,
                targetCount: 1,
                baseDemandPercent: 100,
                infrastructureHubIndex: 0
            );
            AIManufactureProposal proposal = new(
                demand,
                planet,
                new Technology(
                    AITestSceneBuilder.CreateBuildingTemplate(
                        "template-" + buildingType,
                        buildingType
                    )
                ),
                false
            );
            proposal.SetScore(score);
            return proposal;
        }
    }
}
