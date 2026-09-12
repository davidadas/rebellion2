using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Systems;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Proposals
{
    [TestFixture]
    public sealed class AIFacilityRemovalProposalTests
    {
        [Test]
        public void Execute_WithEqualFacilityRates_RemovesUnfinishedFacility()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "primary",
                empire.InstanceID,
                energyCapacity: 100
            );
            AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "secondary",
                empire.InstanceID,
                energyCapacity: 50
            );
            Planet surplusPlanet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "surplus",
                empire.InstanceID,
                energyCapacity: 4
            );
            for (int index = 0; index < 3; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    surplusPlanet,
                    $"complete-{index}",
                    BuildingType.Shipyard,
                    ManufacturingType.Ship
                );
            }

            Building unfinished = AITestSceneBuilder.CreateBuildingTemplate(
                "unfinished",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            unfinished.OwnerInstanceID = empire.InstanceID;
            StubRNG random = new StubRNG();
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire, random: random);
            Assert.IsTrue(
                context.Manufacturing.Enqueue(
                    surplusPlanet,
                    unfinished,
                    surplusPlanet,
                    ignoreCost: true
                )
            );
            AIProposal proposal = new AIFacilityRemovalPlanner().Plan(context).Single();

            Assert.IsTrue(proposal.CanExecute(context));
            proposal.Execute(context);

            Assert.IsNull(game.GetSceneNodeByInstanceID<Building>(unfinished.InstanceID));
            Assert.AreEqual(
                3,
                surplusPlanet
                    .GetChildren<Building>()
                    .Count(building => building.ManufacturingStatus == ManufacturingStatus.Complete)
            );
        }

        [Test]
        public void PlanAndExecute_WithFacilityOutsideAllocation_ScrapsFacility()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            for (int index = 0; index < 3; index++)
            {
                AITestSceneBuilder.AddPlanet(
                    game,
                    sector,
                    $"preferred-{index}",
                    empire.InstanceID,
                    energyCapacity: 20
                );
            }

            Planet surplusPlanet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "surplus",
                empire.InstanceID,
                energyCapacity: 0
            );
            Building surplusFacility = AITestSceneBuilder.AddProductionFacility(
                game,
                surplusPlanet,
                "surplus-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            StubRNG random = new StubRNG();
            MaintenanceSystem maintenance = new MaintenanceSystem(
                game,
                random,
                new FleetSystem(game)
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(
                game,
                empire,
                random: random,
                maintenance: maintenance
            );
            AIProposal proposal = new AIFacilityRemovalPlanner().Plan(context).Single();

            Assert.IsTrue(proposal.CanSelect(context));
            Assert.IsTrue(proposal.CanExecute(context));
            proposal.Execute(context);

            Assert.IsNull(game.GetSceneNodeByInstanceID<Building>(surplusFacility.InstanceID));
        }
    }
}
