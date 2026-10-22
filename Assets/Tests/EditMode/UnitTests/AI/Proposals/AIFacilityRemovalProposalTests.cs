using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Phases;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Proposals
{
    [TestFixture]
    public sealed class AIFacilityRemovalProposalTests
    {
        [Test]
        public void Execute_ResourcesObstructFullHub_RemovesOnlyRequiredFacilities()
        {
            AITurnContext context = CreateResourceHubContext(out Planet hub);
            Assert.IsTrue(
                context.StrategicPlan.TryAllocateInfrastructureHub(
                    hub,
                    BuildingType.Shipyard,
                    6,
                    reclaimResources: true
                )
            );

            new AIFacilityRemovalProposal(hub, BuildingType.Shipyard, 6).Execute(context);

            Assert.AreEqual(2, hub.GetChildren<Building>().Count);
            Assert.AreEqual(
                6,
                context.StrategicPlan.GetAvailableConstructionSpace(hub, BuildingType.Shipyard)
            );
            Assert.AreEqual(
                0,
                context.StrategicPlan.GetAvailableConstructionSpace(hub, BuildingType.Mine)
            );
        }

        [Test]
        public void Execute_QueuedMaintenanceRisesAfterSelection_PreservesResources()
        {
            AITurnContext context = CreateResourceHubContext(out Planet hub);
            Assert.IsTrue(
                context.StrategicPlan.TryAllocateInfrastructureHub(
                    hub,
                    BuildingType.Shipyard,
                    6,
                    reclaimResources: true
                )
            );
            AIFacilityRemovalProposal proposal = new(hub, BuildingType.Shipyard, 6);
            Assert.IsTrue(proposal.CanExecute(context));
            context.Game.AttachNode(
                new Regiment
                {
                    InstanceID = "queued-cost",
                    OwnerInstanceID = context.Faction.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Building,
                    MaintenanceCost = 1000,
                },
                hub
            );

            proposal.Execute(context);

            Assert.AreEqual(4, hub.GetChildren<Building>().Count);
        }

        [Test]
        public void Execute_ResourceReclamationReducesCapacity_UpdatesTurnMaintenanceBudget()
        {
            AITurnContext context = CreateResourceHubContext(out Planet hub);
            hub.EnergyCapacity = 7;
            Assert.IsTrue(
                context.StrategicPlan.TryAllocateInfrastructureHub(
                    hub,
                    BuildingType.Shipyard,
                    6,
                    reclaimResources: true
                )
            );

            new AIFacilityRemovalProposal(hub, BuildingType.Shipyard, 6).Execute(context);

            Assert.AreEqual(
                new ResourceProductionQueries(context.Game)
                    .GetSummary(context.Faction)
                    .MaintenanceHeadroom,
                context.AvailableProjectedMaintenanceHeadroom
            );
        }

        [Test]
        public void Execute_MaterialReserveExhaustedAfterSelection_PreservesResources()
        {
            AITurnContext context = CreateResourceHubContext(out Planet hub);
            Assert.IsTrue(
                context.StrategicPlan.TryAllocateInfrastructureHub(
                    hub,
                    BuildingType.Shipyard,
                    6,
                    reclaimResources: true
                )
            );
            AIFacilityRemovalProposal proposal = new(hub, BuildingType.Shipyard, 6);
            Assert.IsTrue(proposal.CanExecute(context));
            context.Faction.RefinedMaterialStockpile = 0;
            context.Game.Config.AI.Selection.RefinedMaterialCommitmentHorizonTicks = 0;

            proposal.Execute(context);

            Assert.AreEqual(4, hub.GetChildren<Building>().Count);
        }

        [Test]
        public void Plan_WithHealthyMaintenance_DoesNotRemoveShipyards()
        {
            AITurnContext context = CreateContextWithSurplusShipyards(
                out _,
                out _,
                maintenanceCost: 0
            );

            Assert.IsEmpty(new AIFacilityRemovalPlanner().Plan(context));
        }

        [Test]
        public void Plan_WithMaintenanceDistressAndSurplus_AddsRemovalProposal()
        {
            AITurnContext context = CreateContextWithSurplusShipyards(
                out Planet lowerValue,
                out _,
                maintenanceCost: 10
            );

            AIFacilityRemovalProposal proposal = new AIFacilityRemovalPlanner()
                .Plan(context)
                .Cast<AIFacilityRemovalProposal>()
                .Single();

            Assert.AreSame(lowerValue, proposal.Planet);
            Assert.AreEqual(1, proposal.MaximumRemovalCount);
            Assert.AreEqual(1, proposal.MinimumFactionFacilityCount);
        }

        [Test]
        public void Execute_WithMaintenanceDistressAndSurplus_PreservesStrategicFloor()
        {
            AITurnContext context = CreateContextWithSurplusShipyards(
                out _,
                out _,
                maintenanceCost: 10
            );
            AIProposal proposal = new AIFacilityRemovalPlanner().Plan(context).Single();

            Assert.IsTrue(proposal.CanExecute(context));
            context.SetSelectedProposals(new[] { proposal });
            new AIExecutionPhase().Execute(context);

            int remaining = context
                .Assessment.OwnedPlanets.SelectMany(planet => planet.GetChildren<Building>())
                .Count(building => building.GetBuildingType() == BuildingType.Shipyard);
            Assert.AreEqual(1, remaining);
        }

        [Test]
        public void Execute_WithActiveManufacturingBudget_SynchronizesReleasedMaintenance()
        {
            AITurnContext context = CreateContextWithSurplusShipyards(
                out _,
                out _,
                maintenanceCost: 10
            );
            AIProposal proposal = new AIFacilityRemovalPlanner().Plan(context).Single();
            int initialHeadroom = context.AvailableProjectedMaintenanceHeadroom;

            context.SetSelectedProposals(new[] { proposal });
            new AIExecutionPhase().Execute(context);

            Assert.AreEqual(initialHeadroom + 10, context.AvailableProjectedMaintenanceHeadroom);
        }

        /// <summary>
        /// Creates a crowded hub with sufficient resource production on another planet.
        /// </summary>
        /// <param name="hub">The industrial destination containing four resource facilities.</param>
        /// <returns>The faction turn with scrapping and manufacturing services.</returns>
        private static AITurnContext CreateResourceHubContext(out Planet hub)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out _);
            game.Config.AI.Infrastructure.IndustrialShieldTargetCount = 0;
            game.Config.AI.Infrastructure.PlanetaryWeaponTargetCount = 0;
            game.Config.AI.Selection.MaintenanceHeadroomTarget = 1000;
            faction.RefinedMaterialStockpile = 10000;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            hub = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "hub",
                faction.InstanceID,
                energyCapacity: 8,
                rawResourceNodes: 1
            );
            Planet economy = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "economy",
                faction.InstanceID,
                energyCapacity: 80,
                rawResourceNodes: 30
            );
            hub.SetPopularSupport(faction.InstanceID, 100);
            economy.SetPopularSupport(faction.InstanceID, 100);
            for (int index = 0; index < 4; index++)
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    hub,
                    "local-" + index,
                    index == 0 ? BuildingType.Mine : BuildingType.Refinery,
                    ManufacturingType.None
                );
            for (int index = 0; index < 30; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    economy,
                    "mine-" + index,
                    BuildingType.Mine,
                    ManufacturingType.None
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    economy,
                    "refinery-" + index,
                    BuildingType.Refinery,
                    ManufacturingType.None
                );
            }
            Building shipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            shipyard.MaintenanceCost = 20;
            faction.ResearchQueue[ManufacturingType.Building] = new List<Technology>
            {
                new(shipyard),
            };
            return AITestSceneBuilder.CreateContext(game, faction);
        }

        /// <summary>
        /// Creates a faction with two shipyards and a strategic requirement for one.
        /// </summary>
        /// <param name="lowerValue">The lower-value shipyard planet.</param>
        /// <param name="higherValue">The higher-value shipyard planet.</param>
        /// <param name="maintenanceCost">Maintenance cost assigned to each shipyard.</param>
        /// <returns>The configured AI turn context.</returns>
        private static AITurnContext CreateContextWithSurplusShipyards(
            out Planet lowerValue,
            out Planet higherValue,
            int maintenanceCost
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MaintenanceRecovery.Enabled = false;
            game.Config.AI.Infrastructure.ShipyardPrimaryHubTargetCount = 1;
            game.Config.AI.Infrastructure.ShipyardSecondaryHubTargetCount = 0;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            lowerValue = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "a-lower-value",
                empire.InstanceID,
                energyCapacity: 1
            );
            higherValue = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "z-higher-value",
                empire.InstanceID,
                energyCapacity: 10
            );
            Building lowerShipyard = AITestSceneBuilder.AddProductionFacility(
                game,
                lowerValue,
                "lower-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Building higherShipyard = AITestSceneBuilder.AddProductionFacility(
                game,
                higherValue,
                "higher-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            lowerShipyard.MaintenanceCost = maintenanceCost;
            higherShipyard.MaintenanceCost = maintenanceCost;
            StubRNG random = new StubRNG();
            return AITestSceneBuilder.CreateContext(
                game,
                empire,
                random: random,
                maintenance: new MaintenanceCommands(game, random)
            );
        }
    }
}
