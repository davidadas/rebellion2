using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Game.Galaxy
{
    [TestFixture]
    public class GalaxyMapTests
    {
        private GalaxyMap _galaxyMap;
        private PlanetSector _planetSector1;
        private PlanetSector _planetSector2;

        [SetUp]
        public void SetUp()
        {
            _galaxyMap = new GalaxyMap { InstanceID = "GALAXY1" };

            _planetSector1 = new PlanetSector { InstanceID = "SYSTEM1" };

            _planetSector2 = new PlanetSector { InstanceID = "SYSTEM2" };
        }

        [Test]
        public void AddChild_WithPlanetSector_AddsPlanetSector()
        {
            _galaxyMap.AddChild(_planetSector1);

            Assert.Contains(_planetSector1, _galaxyMap.PlanetSectors);
        }

        [Test]
        public void AddChild_WithMultiplePlanetSectors_AddsAllSectors()
        {
            PlanetSector planetSector3 = new PlanetSector { InstanceID = "SYSTEM3" };

            _galaxyMap.AddChild(_planetSector1);
            _galaxyMap.AddChild(_planetSector2);
            _galaxyMap.AddChild(planetSector3);

            Assert.AreEqual(3, _galaxyMap.PlanetSectors.Count);
            Assert.Contains(_planetSector1, _galaxyMap.PlanetSectors);
            Assert.Contains(_planetSector2, _galaxyMap.PlanetSectors);
            Assert.Contains(planetSector3, _galaxyMap.PlanetSectors);
        }

        [Test]
        public void AddChild_WithNullPlanetSector_DoesNotThrowException()
        {
            Assert.DoesNotThrow(() => _galaxyMap.AddChild(null));
        }

        [Test]
        public void AddChild_WithNonPlanetSectorNode_DoesNotAddToList()
        {
            ISceneNode nonPlanetSector = new GalaxyMap { InstanceID = "NOT_A_PLANET_SECTOR" };

            _galaxyMap.AddChild(nonPlanetSector);

            Assert.AreEqual(0, _galaxyMap.PlanetSectors.Count);
        }

        [Test]
        public void AddChild_WithSamePlanetSectorTwice_AddsItTwice()
        {
            _galaxyMap.AddChild(_planetSector1);
            _galaxyMap.AddChild(_planetSector1);

            Assert.AreEqual(2, _galaxyMap.PlanetSectors.Count);
        }

        [Test]
        public void RemoveChild_ExistingPlanetSector_RemovesIt()
        {
            _galaxyMap.AddChild(_planetSector1);

            _galaxyMap.RemoveChild(_planetSector1);

            Assert.IsFalse(_galaxyMap.PlanetSectors.Contains(_planetSector1));
        }

        [Test]
        public void RemoveChild_WithMultiplePlanetSectors_RemovesCorrectSectors()
        {
            _galaxyMap.AddChild(_planetSector1);
            _galaxyMap.AddChild(_planetSector2);

            _galaxyMap.RemoveChild(_planetSector1);

            Assert.AreEqual(1, _galaxyMap.PlanetSectors.Count);
            Assert.IsFalse(_galaxyMap.PlanetSectors.Contains(_planetSector1));
            Assert.Contains(_planetSector2, _galaxyMap.PlanetSectors);
        }

        [Test]
        public void RemoveChild_RemovingAllSectors_ResultsInEmptyList()
        {
            _galaxyMap.AddChild(_planetSector1);
            _galaxyMap.AddChild(_planetSector2);

            _galaxyMap.RemoveChild(_planetSector1);
            _galaxyMap.RemoveChild(_planetSector2);

            Assert.AreEqual(0, _galaxyMap.PlanetSectors.Count);
        }

        [Test]
        public void RemoveChild_WithNullPlanetSector_DoesNotThrowException()
        {
            Assert.DoesNotThrow(() => _galaxyMap.RemoveChild(null));
        }

        [Test]
        public void RemoveChild_WithSectorNotInList_DoesNotChangeCount()
        {
            _galaxyMap.AddChild(_planetSector1);

            _galaxyMap.RemoveChild(_planetSector2);

            Assert.AreEqual(1, _galaxyMap.PlanetSectors.Count);
        }

        [Test]
        public void GetChildren_MapWithPlanetSectors_ReturnsAllPlanetSectors()
        {
            _galaxyMap.AddChild(_planetSector1);
            _galaxyMap.AddChild(_planetSector2);

            IEnumerable<ISceneNode> children = _galaxyMap.GetChildren();

            CollectionAssert.AreEquivalent(
                new ISceneNode[] { _planetSector1, _planetSector2 },
                children,
                "GalaxyMap should return correct children."
            );
        }

        [Test]
        public void SerializeAndDeserialize_MapWithPlanetSectors_MaintainsState()
        {
            _galaxyMap.AddChild(_planetSector1);
            _galaxyMap.AddChild(_planetSector2);

            string serialized = SerializationHelper.Serialize(_galaxyMap);
            GalaxyMap deserialized = SerializationHelper.Deserialize<GalaxyMap>(serialized);

            Assert.AreEqual(
                _galaxyMap.InstanceID,
                deserialized.InstanceID,
                "InstanceID should be correctly deserialized."
            );
            Assert.AreEqual(
                _galaxyMap.PlanetSectors.Count,
                deserialized.PlanetSectors.Count,
                "PlanetSectors count should be correctly deserialized."
            );
        }

        [Test]
        public void PlanetSectors_WhenInitialized_IsEmptyList()
        {
            GalaxyMap newMap = new GalaxyMap();

            Assert.IsNotNull(newMap.PlanetSectors);
            Assert.AreEqual(0, newMap.PlanetSectors.Count);
        }

        [Test]
        public void PlanetSectors_AfterAddingAndRemoving_MaintainsCorrectCount()
        {
            Assert.AreEqual(0, _galaxyMap.PlanetSectors.Count);

            _galaxyMap.AddChild(_planetSector1);
            Assert.AreEqual(1, _galaxyMap.PlanetSectors.Count);

            _galaxyMap.AddChild(_planetSector2);
            Assert.AreEqual(2, _galaxyMap.PlanetSectors.Count);

            _galaxyMap.RemoveChild(_planetSector1);
            Assert.AreEqual(1, _galaxyMap.PlanetSectors.Count);

            _galaxyMap.RemoveChild(_planetSector2);
            Assert.AreEqual(0, _galaxyMap.PlanetSectors.Count);
        }
    }
} // namespace Rebellion.Tests.Game.Galaxy
