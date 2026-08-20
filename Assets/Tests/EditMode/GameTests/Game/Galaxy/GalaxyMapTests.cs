using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Game.Galaxy
{
    [TestFixture]
    public class GalaxyMapTests
    {
        private GalaxyMap _galaxyMap;
        private PlanetSector _planetSystem1;
        private PlanetSector _planetSystem2;

        [SetUp]
        public void SetUp()
        {
            _galaxyMap = new GalaxyMap { InstanceID = "GALAXY1" };

            _planetSystem1 = new PlanetSector { InstanceID = "SYSTEM1" };

            _planetSystem2 = new PlanetSector { InstanceID = "SYSTEM2" };
        }

        [Test]
        public void AddChild_WithPlanetSector_AddsPlanetSector()
        {
            _galaxyMap.AddChild(_planetSystem1);

            Assert.Contains(_planetSystem1, _galaxyMap.GetChildren<PlanetSector>().ToList());
        }

        [Test]
        public void AddChild_WithMultiplePlanetSectors_AddsAllSystems()
        {
            PlanetSector planetSystem3 = new PlanetSector { InstanceID = "SYSTEM3" };

            _galaxyMap.AddChild(_planetSystem1);
            _galaxyMap.AddChild(_planetSystem2);
            _galaxyMap.AddChild(planetSystem3);

            Assert.AreEqual(3, _galaxyMap.GetChildren<PlanetSector>().Count);
            Assert.Contains(_planetSystem1, _galaxyMap.GetChildren<PlanetSector>().ToList());
            Assert.Contains(_planetSystem2, _galaxyMap.GetChildren<PlanetSector>().ToList());
            Assert.Contains(planetSystem3, _galaxyMap.GetChildren<PlanetSector>().ToList());
        }

        [Test]
        public void AddChild_WithNullPlanetSector_DoesNotThrowException()
        {
            Assert.DoesNotThrow(() => _galaxyMap.AddChild(null));
        }

        [Test]
        public void AddChild_WithNonPlanetSectorNode_DoesNotAddToList()
        {
            ISceneNode nonPlanetSector = new GalaxyMap { InstanceID = "NOT_A_PLANET_SYSTEM" };

            _galaxyMap.AddChild(nonPlanetSector);

            Assert.AreEqual(0, _galaxyMap.GetChildren<PlanetSector>().Count);
        }

        [Test]
        public void AddChild_WithSamePlanetSectorTwice_AddsItTwice()
        {
            _galaxyMap.AddChild(_planetSystem1);
            _galaxyMap.AddChild(_planetSystem1);

            Assert.AreEqual(2, _galaxyMap.GetChildren<PlanetSector>().Count);
        }

        [Test]
        public void RemoveChild_ExistingPlanetSector_RemovesIt()
        {
            _galaxyMap.AddChild(_planetSystem1);

            _galaxyMap.RemoveChild(_planetSystem1);

            Assert.IsFalse(_galaxyMap.GetChildren<PlanetSector>().Contains(_planetSystem1));
        }

        [Test]
        public void RemoveChild_WithMultiplePlanetSectors_RemovesCorrectSystems()
        {
            _galaxyMap.AddChild(_planetSystem1);
            _galaxyMap.AddChild(_planetSystem2);

            _galaxyMap.RemoveChild(_planetSystem1);

            Assert.AreEqual(1, _galaxyMap.GetChildren<PlanetSector>().Count);
            Assert.IsFalse(_galaxyMap.GetChildren<PlanetSector>().Contains(_planetSystem1));
            Assert.Contains(_planetSystem2, _galaxyMap.GetChildren<PlanetSector>().ToList());
        }

        [Test]
        public void RemoveChild_RemovingAllSystems_ResultsInEmptyList()
        {
            _galaxyMap.AddChild(_planetSystem1);
            _galaxyMap.AddChild(_planetSystem2);

            _galaxyMap.RemoveChild(_planetSystem1);
            _galaxyMap.RemoveChild(_planetSystem2);

            Assert.AreEqual(0, _galaxyMap.GetChildren<PlanetSector>().Count);
        }

        [Test]
        public void RemoveChild_WithNullPlanetSector_DoesNotThrowException()
        {
            Assert.DoesNotThrow(() => _galaxyMap.RemoveChild(null));
        }

        [Test]
        public void RemoveChild_WithSystemNotInList_DoesNotChangeCount()
        {
            _galaxyMap.AddChild(_planetSystem1);

            _galaxyMap.RemoveChild(_planetSystem2);

            Assert.AreEqual(1, _galaxyMap.GetChildren<PlanetSector>().Count);
        }

        [Test]
        public void GetChildren_MapWithPlanetSectors_ReturnsAllPlanetSectors()
        {
            _galaxyMap.AddChild(_planetSystem1);
            _galaxyMap.AddChild(_planetSystem2);

            IEnumerable<ISceneNode> children = _galaxyMap.GetChildren();

            CollectionAssert.AreEquivalent(
                new ISceneNode[] { _planetSystem1, _planetSystem2 },
                children,
                "GalaxyMap should return correct children."
            );
        }

        [Test]
        public void SerializeAndDeserialize_MapWithPlanetSectors_MaintainsState()
        {
            _galaxyMap.AddChild(_planetSystem1);
            _galaxyMap.AddChild(_planetSystem2);

            string serialized = SerializationHelper.Serialize(_galaxyMap);
            GalaxyMap deserialized = SerializationHelper.Deserialize<GalaxyMap>(serialized);

            Assert.AreEqual(
                _galaxyMap.InstanceID,
                deserialized.InstanceID,
                "InstanceID should be correctly deserialized."
            );
            Assert.AreEqual(
                _galaxyMap.GetChildren<PlanetSector>().Count,
                deserialized.GetChildren<PlanetSector>().Count,
                "PlanetSectors count should be correctly deserialized."
            );
        }

        [Test]
        public void PlanetSectors_WhenInitialized_IsEmptyList()
        {
            GalaxyMap newMap = new GalaxyMap();

            Assert.IsNotNull(newMap.GetChildren<PlanetSector>());
            Assert.AreEqual(0, newMap.GetChildren<PlanetSector>().Count);
        }

        [Test]
        public void PlanetSectors_AfterAddingAndRemoving_MaintainsCorrectCount()
        {
            Assert.AreEqual(0, _galaxyMap.GetChildren<PlanetSector>().Count);

            _galaxyMap.AddChild(_planetSystem1);
            Assert.AreEqual(1, _galaxyMap.GetChildren<PlanetSector>().Count);

            _galaxyMap.AddChild(_planetSystem2);
            Assert.AreEqual(2, _galaxyMap.GetChildren<PlanetSector>().Count);

            _galaxyMap.RemoveChild(_planetSystem1);
            Assert.AreEqual(1, _galaxyMap.GetChildren<PlanetSector>().Count);

            _galaxyMap.RemoveChild(_planetSystem2);
            Assert.AreEqual(0, _galaxyMap.GetChildren<PlanetSector>().Count);
        }
    }
} // namespace Rebellion.Tests.Game.Galaxy
