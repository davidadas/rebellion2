using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Core;
using Rebellion.AI.Production;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Systems;
using Rebellion.Tests.AI.Core.Helpers;

namespace Rebellion.Tests.AI.Production
{
    [TestFixture]
    public sealed class AIFacilityRemovalProposalTests
    {
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
            proposal.Execute(context);

            int remaining = context
                .Assessment.OwnedPlanets.SelectMany(planet => planet.GetChildren<Building>())
                .Count(building => building.GetBuildingType() == BuildingType.Shipyard);
            Assert.AreEqual(1, remaining);
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
            game.Config.AI.Infrastructure.PlanetsPerShipyard = 2;
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
                maintenance: new MaintenanceSystem(game, random, new FleetSystem(game))
            );
        }
    }
}
