using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Advisor
{
    [TestFixture]
    public class AdvisorCommandControllerTests
    {
        /// <summary>
        /// Verifies find producer planet multiple eligible planets returns closest idle producer.
        /// </summary>
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

        /// <summary>
        /// Verifies find producer planet closest producer is busy returns next closest idle producer.
        /// </summary>
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

        /// <summary>
        /// Verifies find producer planet missing faction or destination returns null.
        /// </summary>
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
        /// <returns>The created production planet.</returns>
        private static Planet CreateProductionPlanet(
            string instanceId,
            string ownerInstanceId,
            int x,
            int y
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
                    ProductionType = ManufacturingType.Troop,
                    ProcessRate = 10,
                }
            );
            return planet;
        }
    }
}
