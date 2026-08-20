using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Game.Galaxy
{
    [TestFixture]
    public class PlanetSectorTests
    {
        private PlanetSector _planetSector;
        private Planet _planet1;
        private Planet _planet2;

        [SetUp]
        public void SetUp()
        {
            _planetSector = new PlanetSector
            {
                InstanceID = "SECTOR1",
                Visibility = GameSize.Medium,
                SectorType = PlanetSectorType.Core,
                Importance = PlanetSectorImportance.High,
            };

            _planet1 = new Planet { InstanceID = "PLANET1", OwnerInstanceID = "FACTION1" };

            _planet2 = new Planet { InstanceID = "PLANET2", OwnerInstanceID = "FACTION1" };
        }

        [Test]
        public void AddChild_WithPlanet_AddsPlanet()
        {
            _planetSector.AddChild(_planet1);

            Assert.IsTrue(_planetSector.GetChildren<Planet>().Contains(_planet1));
        }

        [Test]
        public void AddChild_MultiplePlanets_AddsAllPlanets()
        {
            Planet planet3 = new Planet { InstanceID = "PLANET3", OwnerInstanceID = "FACTION2" };
            Planet planet4 = new Planet { InstanceID = "PLANET4", OwnerInstanceID = "FACTION2" };

            _planetSector.AddChild(_planet1);
            _planetSector.AddChild(_planet2);
            _planetSector.AddChild(planet3);
            _planetSector.AddChild(planet4);

            Assert.AreEqual(4, _planetSector.GetChildren<Planet>().Count);
            Assert.IsTrue(_planetSector.GetChildren<Planet>().Contains(_planet1));
            Assert.IsTrue(_planetSector.GetChildren<Planet>().Contains(_planet2));
            Assert.IsTrue(_planetSector.GetChildren<Planet>().Contains(planet3));
            Assert.IsTrue(_planetSector.GetChildren<Planet>().Contains(planet4));
        }

        [Test]
        public void AddChild_SamePlanetTwice_AddsPlanetTwice()
        {
            _planetSector.AddChild(_planet1);
            _planetSector.AddChild(_planet1);

            Assert.AreEqual(2, _planetSector.GetChildren<Planet>().Count);
        }

        [Test]
        public void RemoveChild_WithAddedPlanet_RemovesPlanet()
        {
            _planetSector.AddChild(_planet1);

            _planetSector.RemoveChild(_planet1);

            Assert.IsFalse(_planetSector.GetChildren<Planet>().Contains(_planet1));
        }

        [Test]
        public void RemoveChild_FromMultiplePlanets_RemovesOnlySpecifiedPlanet()
        {
            _planetSector.AddChild(_planet1);
            _planetSector.AddChild(_planet2);

            _planetSector.RemoveChild(_planet1);

            Assert.AreEqual(1, _planetSector.GetChildren<Planet>().Count);
            Assert.IsFalse(_planetSector.GetChildren<Planet>().Contains(_planet1));
            Assert.IsTrue(_planetSector.GetChildren<Planet>().Contains(_planet2));
        }

        [Test]
        public void GetChildren_WithTwoPlanets_ReturnsAllPlanets()
        {
            _planetSector.AddChild(_planet1);
            _planetSector.AddChild(_planet2);

            IEnumerable<ISceneNode> children = _planetSector.GetChildren();

            CollectionAssert.AreEquivalent(
                new ISceneNode[] { _planet1, _planet2 },
                children,
                "PlanetSector should return correct children."
            );
        }

        [Test]
        public void SerializeAndDeserialize_WithPopulatedSector_MaintainsState()
        {
            _planetSector.AddChild(_planet1);
            _planetSector.AddChild(_planet2);

            string serialized = SerializationHelper.Serialize(_planetSector);
            PlanetSector deserialized = SerializationHelper.Deserialize<PlanetSector>(serialized);

            Assert.AreEqual(
                _planetSector.InstanceID,
                deserialized.InstanceID,
                "InstanceID should be correctly deserialized."
            );
            Assert.AreEqual(
                _planetSector.GetPosition().X,
                deserialized.GetPosition().X,
                "PositionX should be correctly deserialized."
            );
            Assert.AreEqual(
                _planetSector.GetPosition().Y,
                deserialized.GetPosition().Y,
                "PositionY should be correctly deserialized."
            );
            Assert.AreEqual(
                _planetSector.Visibility,
                deserialized.Visibility,
                "Visibility should be correctly deserialized."
            );
            Assert.AreEqual(
                _planetSector.SectorType,
                deserialized.SectorType,
                "SectorType should be correctly deserialized."
            );
            Assert.AreEqual(
                _planetSector.Importance,
                deserialized.Importance,
                "Importance should be correctly deserialized."
            );
            Assert.AreEqual(
                _planetSector.GetChildren<Planet>().Count,
                deserialized.GetChildren<Planet>().Count,
                "Planets count should be correctly deserialized."
            );
        }

        [Test]
        public void SectorType_SetToCoreSector_ReturnsCoreSector()
        {
            _planetSector.SectorType = PlanetSectorType.Core;

            Assert.AreEqual(PlanetSectorType.Core, _planetSector.SectorType);
        }

        [Test]
        public void SectorType_SetToOuterRim_ReturnsOuterRim()
        {
            _planetSector.SectorType = PlanetSectorType.OuterRim;

            Assert.AreEqual(PlanetSectorType.OuterRim, _planetSector.SectorType);
        }

        [Test]
        public void Visibility_SetToSmall_ReturnsSmall()
        {
            _planetSector.Visibility = GameSize.Small;

            Assert.AreEqual(GameSize.Small, _planetSector.Visibility);
        }

        [Test]
        public void Visibility_SetToMedium_ReturnsMedium()
        {
            _planetSector.Visibility = GameSize.Medium;

            Assert.AreEqual(GameSize.Medium, _planetSector.Visibility);
        }

        [Test]
        public void Visibility_SetToLarge_ReturnsLarge()
        {
            _planetSector.Visibility = GameSize.Large;

            Assert.AreEqual(GameSize.Large, _planetSector.Visibility);
        }

        [Test]
        public void Importance_SetToLow_ReturnsLow()
        {
            _planetSector.Importance = PlanetSectorImportance.Low;

            Assert.AreEqual(PlanetSectorImportance.Low, _planetSector.Importance);
        }

        [Test]
        public void Importance_SetToMedium_ReturnsMedium()
        {
            _planetSector.Importance = PlanetSectorImportance.Medium;

            Assert.AreEqual(PlanetSectorImportance.Medium, _planetSector.Importance);
        }

        [Test]
        public void Importance_SetToHigh_ReturnsHigh()
        {
            _planetSector.Importance = PlanetSectorImportance.High;

            Assert.AreEqual(PlanetSectorImportance.High, _planetSector.Importance);
        }

        [Test]
        public void GetPosition_WithZeroCoordinates_ReturnsZeroPoint()
        {
            Point position = _planetSector.GetPosition();

            Assert.AreEqual(0, position.X);
            Assert.AreEqual(0, position.Y);
        }
    }
} // namespace Rebellion.Tests.Game.Galaxy
