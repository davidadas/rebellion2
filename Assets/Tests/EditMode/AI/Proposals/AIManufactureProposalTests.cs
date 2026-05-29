using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Proposals
{
    [TestFixture]
    public class AIManufactureProposalTests
    {
        [Test]
        public void GetClaimKeys_WithBuildingDemand_ClaimsDemandProducerAndDestination()
        {
            Planet producer = new Planet { InstanceID = "producer" };
            Planet destination = new Planet { InstanceID = "destination" };
            AIProductionDemand demand = CreateBuildingDemand(destination);
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                producer,
                new Technology(AITestSceneBuilder.CreateBuildingTemplate("mine", BuildingType.Mine))
            );

            IReadOnlyList<string> claimKeys = proposal.GetClaimKeys();

            CollectionAssert.Contains(claimKeys, "production:demand:mine-demand");
            CollectionAssert.Contains(claimKeys, "production:building:producer");
            CollectionAssert.Contains(claimKeys, "production:building-destination:destination");
        }

        [Test]
        public void Execute_WithValidBuildingProposal_QueuesManufacturing()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "resource-world",
                empire.InstanceID,
                rawResourceNodes: 4
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Building mine = AITestSceneBuilder.CreateBuildingTemplate(
                "mine-template",
                BuildingType.Mine
            );
            mine.MaintenanceCost = 0;
            AIManufactureProposal proposal = new AIManufactureProposal(
                CreateBuildingDemand(planet),
                planet,
                new Technology(mine)
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            proposal.Execute(context);

            Assert.AreEqual(1, planet.GetManufacturingQueue()[ManufacturingType.Building].Count);
        }

        private static AIProductionDemand CreateBuildingDemand(Planet destination)
        {
            return new AIProductionDemand(
                "mine-demand",
                AIProductionDemandKind.Mine,
                ManufacturingType.Building,
                BuildingType.Mine,
                destination,
                1,
                100
            );
        }
    }
}
