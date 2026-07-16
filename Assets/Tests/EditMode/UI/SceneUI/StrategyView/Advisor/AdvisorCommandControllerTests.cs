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
