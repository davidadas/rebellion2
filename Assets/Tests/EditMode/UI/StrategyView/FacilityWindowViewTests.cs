using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Tests.UI.StrategyView
{
    [TestFixture]
    public sealed class FacilityWindowViewTests
    {
        [Test]
        public void CanStopFromCard_BuildingQueue_HasQueuedItems()
        {
            const string ownerId = "owner";
            PlanetSystem system = new PlanetSystem { InstanceID = "system" };
            Planet planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = ownerId,
                ManufacturingQueue =
                {
                    [ManufacturingType.Building] = new List<IManufacturable>
                    {
                        new Building { OwnerInstanceID = ownerId },
                    },
                },
            };
            GalaxyMapPlanet mapPlanet = new GalaxyMapPlanet(system, planet, string.Empty);

            Assert.IsTrue(FacilityWindowView.CanStopFromCard(mapPlanet, 3, ownerId));
            Assert.IsFalse(FacilityWindowView.CanStopFromCard(mapPlanet, 2, ownerId));
            Assert.IsFalse(FacilityWindowView.CanStopFromCard(mapPlanet, 3, "other"));
        }
    }
}
