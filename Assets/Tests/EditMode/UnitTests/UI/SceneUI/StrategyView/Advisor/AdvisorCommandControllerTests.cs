using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using GalaxyPlanetSector = Rebellion.Game.Galaxy.PlanetSector;
using GameFleet = Rebellion.Game.Units.Fleet;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Advisor
{
    [TestFixture]
    public class AdvisorCommandControllerTests
    {
        [Test]
        public void BeginConstruction_ManufacturingRequest_UsesDestinationTargetingAction()
        {
            TargetingController targeting = new TargetingController();
            AdvisorCommandController controller = new AdvisorCommandController(
                () => null,
                targeting,
                () => Array.Empty<GalaxyMapSector>(),
                (_, _, _, _) => { }
            );

            controller.BeginConstruction(ManufacturingType.Ship, 12, 34);

            Assert.IsTrue(targeting.IsTargeting);
            Assert.IsInstanceOf<IStrategyTargetingSource>(targeting.ActiveRequest.Source);
            Assert.AreEqual(
                StrategyMenuAction.Destination,
                ((IStrategyTargetingSource)targeting.ActiveRequest.Source).Action
            );
        }

        [Test]
        public void OnTargetSelected_FleetDestination_PreservesExactTarget()
        {
            Faction faction = new Faction { InstanceID = "faction" };
            Planet producer = CreateProductionPlanet(
                "producer",
                faction.InstanceID,
                10,
                10,
                ManufacturingType.Ship
            );
            Planet destination = new Planet
            {
                InstanceID = "destination",
                OwnerInstanceID = faction.InstanceID,
                PositionX = 5,
                PositionY = 5,
            };
            GameFleet fleet = new GameFleet(faction.InstanceID, "Destination Fleet")
            {
                InstanceID = "destination-fleet",
            };
            destination.AddChild(fleet);
            faction.AddOwnedUnit(producer);
            GalaxyPlanetSector planetSector = new GalaxyPlanetSector { InstanceID = "sector" };
            GalaxyMapPlanet producerView = new GalaxyMapPlanet(
                planetSector,
                producer,
                string.Empty
            );
            GalaxyMapPlanet destinationView = new GalaxyMapPlanet(
                planetSector,
                destination,
                string.Empty
            );
            GalaxyMapSector sector = new GalaxyMapSector(
                planetSector,
                new[] { producerView, destinationView }
            );
            GalaxyMapPlanet openedProducer = null;
            GalaxyMapPlanet openedDestination = null;
            FacilityWindowTab? openedTab = null;
            ISceneNode openedItem = null;
            TargetingController targeting = new TargetingController();
            AdvisorCommandController controller = new AdvisorCommandController(
                () => faction,
                targeting,
                () => new[] { sector },
                (selectedProducer, selectedDestination, selectedTab, selectedItem) =>
                {
                    openedProducer = selectedProducer;
                    openedDestination = selectedDestination;
                    openedTab = selectedTab;
                    openedItem = selectedItem;
                }
            );
            controller.BeginConstruction(ManufacturingType.Ship, 0, 0);

            targeting.TrySelectTarget(new StrategyMissionTarget(destinationView, fleet));

            Assert.AreSame(producerView, openedProducer);
            Assert.AreSame(destinationView, openedDestination);
            Assert.AreEqual(FacilityWindowTab.Shipyards, openedTab);
            Assert.AreSame(fleet, openedItem);
        }

        [Test]
        public void FindProducerPlanet_MultipleEligiblePlanets_ReturnsClosestIdleProducer()
        {
            Faction faction = new Faction { InstanceID = "faction" };
            Planet destination = new Planet { PositionX = 5, PositionY = 5 };
            Planet farProducer = CreateProductionPlanet("far", "faction", 50, 50);
            Planet nearProducer = CreateProductionPlanet("near", "faction", 10, 10);
            Planet enemyProducer = CreateProductionPlanet("enemy", "other", 6, 6);
            faction.AddOwnedUnit(farProducer);
            faction.AddOwnedUnit(nearProducer);
            faction.AddOwnedUnit(enemyProducer);

            Planet producer = AdvisorCommandController.FindProducerPlanet(
                faction,
                ManufacturingType.Troop,
                destination
            );

            Assert.AreSame(nearProducer, producer);
        }

        [Test]
        public void FindProducerPlanet_ClosestProducerIsBusy_ReturnsNextClosestIdleProducer()
        {
            Faction faction = new Faction { InstanceID = "faction" };
            Planet destination = new Planet { PositionX = 5, PositionY = 5 };
            Planet farProducer = CreateProductionPlanet("far", "faction", 50, 50);
            Planet nearProducer = CreateProductionPlanet("near", "faction", 10, 10);
            nearProducer.ManufacturingQueue[ManufacturingType.Troop] = new List<IManufacturable>
            {
                new Regiment(),
            };
            faction.AddOwnedUnit(farProducer);
            faction.AddOwnedUnit(nearProducer);

            Planet producer = AdvisorCommandController.FindProducerPlanet(
                faction,
                ManufacturingType.Troop,
                destination
            );

            Assert.AreSame(farProducer, producer);
        }

        [Test]
        public void FindProducerPlanet_MissingFactionOrDestination_ReturnsNull()
        {
            Faction faction = new Faction { InstanceID = "faction" };
            Planet destination = new Planet();

            Planet missingFaction = AdvisorCommandController.FindProducerPlanet(
                null,
                ManufacturingType.Troop,
                destination
            );
            Planet missingDestination = AdvisorCommandController.FindProducerPlanet(
                faction,
                ManufacturingType.Troop,
                null
            );

            Assert.IsNull(missingFaction);
            Assert.IsNull(missingDestination);
        }

        /// <summary>
        /// Creates production planet.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="manufacturingType">The production category supported by the planet.</param>
        /// <returns>The created production planet.</returns>
        private static Planet CreateProductionPlanet(
            string instanceId,
            string ownerInstanceId,
            int x,
            int y,
            ManufacturingType manufacturingType = ManufacturingType.Troop
        )
        {
            Planet planet = new Planet
            {
                InstanceID = instanceId,
                OwnerInstanceID = ownerInstanceId,
                IsColonized = true,
                EnergyCapacity = 1,
                PositionX = x,
                PositionY = y,
            };
            planet.AddChild(
                new Building
                {
                    InstanceID = instanceId + "_training",
                    OwnerInstanceID = ownerInstanceId,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    ProductionType = manufacturingType,
                    ProcessRate = 10,
                }
            );
            return planet;
        }
    }
}
