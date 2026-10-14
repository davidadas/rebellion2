using NUnit.Framework;
using Rebellion.Game.ShipComponents;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Game.ShipComponents
{
    [TestFixture]
    public sealed class StarfighterBayTests
    {
        /// <summary>
        /// Checks a copied bay owns its waypoint coordinates independently of the template.
        /// </summary>
        [Test]
        public void CreateCopy_AuthoredRoute_CopiesIndependentWaypoints()
        {
            StarfighterBay bay = new StarfighterBay
            {
                ModelNodePath = "Bay",
                Capacity = 2,
                Health = 100,
            };
            bay.FlightPath.Add(
                new StarfighterBayWaypoint
                {
                    X = 3f,
                    Y = 4f,
                    Z = 5f,
                }
            );
            StarfighterBay copy = (StarfighterBay)bay.CreateCopy();
            copy.FlightPath[0].X = 90f;
            Assert.That(bay.FlightPath[0].X, Is.EqualTo(3f));
            Assert.That(copy.ModelNodePath, Is.EqualTo("Bay"));
            Assert.That(copy.Capacity, Is.EqualTo(2));
            Assert.That(copy.Health, Is.EqualTo(100));
        }

        /// <summary>
        /// Checks the component's authored route survives content and saved-battle serialization.
        /// </summary>
        [Test]
        public void SerializeAndDeserialize_AuthoredRoute_PreservesCoordinates()
        {
            StarfighterBay bay = new StarfighterBay { ModelNodePath = "." };
            bay.FlightPath.Add(
                new StarfighterBayWaypoint
                {
                    X = 3f,
                    Y = 4f,
                    Z = 5f,
                }
            );
            StarfighterBay restored = SerializationHelper.Deserialize<StarfighterBay>(
                SerializationHelper.Serialize(bay)
            );
            Assert.That(restored.ModelNodePath, Is.EqualTo("."));
            Assert.That(restored.FlightPath.Count, Is.EqualTo(1));
            Assert.That(restored.FlightPath[0].X, Is.EqualTo(3f));
            Assert.That(restored.FlightPath[0].Y, Is.EqualTo(4f));
            Assert.That(restored.FlightPath[0].Z, Is.EqualTo(5f));
        }
    }
}
