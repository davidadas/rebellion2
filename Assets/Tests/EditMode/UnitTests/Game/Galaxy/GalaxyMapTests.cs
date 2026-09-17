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
        private PlanetSector _planetSector1;
        private PlanetSector _planetSector2;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _galaxyMap = new GalaxyMap { InstanceID = "GALAXY1" };

            _planetSector1 = new PlanetSector { InstanceID = "SECTOR1" };

            _planetSector2 = new PlanetSector { InstanceID = "SECTOR2" };
        }

        /// <summary>
        /// Verifies add child with planet sector adds planet sector.
        /// </summary>
        [Test]
        public void AddChild_WithPlanetSector_AddsPlanetSector()
        {
            _galaxyMap.AddChild(_planetSector1);

            Assert.Contains(_planetSector1, _galaxyMap.GetChildren<PlanetSector>().ToList());
        }

        /// <summary>
        /// Verifies add child with multiple planet sectors adds all sectors.
        /// </summary>
        [Test]
        public void AddChild_WithMultiplePlanetSectors_AddsAllSectors()
        {
            PlanetSector planetSector3 = new PlanetSector { InstanceID = "SECTOR3" };

            _galaxyMap.AddChild(_planetSector1);
            _galaxyMap.AddChild(_planetSector2);
            _galaxyMap.AddChild(planetSector3);

            Assert.AreEqual(3, _galaxyMap.GetChildren<PlanetSector>().Count);
            Assert.Contains(_planetSector1, _galaxyMap.GetChildren<PlanetSector>().ToList());
            Assert.Contains(_planetSector2, _galaxyMap.GetChildren<PlanetSector>().ToList());
            Assert.Contains(planetSector3, _galaxyMap.GetChildren<PlanetSector>().ToList());
        }

        /// <summary>
        /// Verifies add child with null planet sector leaves children empty.
        /// </summary>
        [Test]
        public void AddChild_WithNullPlanetSector_LeavesChildrenEmpty()
        {
            _galaxyMap.AddChild(null);

            Assert.IsEmpty(_galaxyMap.GetChildren<PlanetSector>());
        }

        /// <summary>
        /// Verifies add child with non planet sector node does not add to list.
        /// </summary>
        [Test]
        public void AddChild_WithNonPlanetSectorNode_DoesNotAddToList()
        {
            ISceneNode nonPlanetSector = new GalaxyMap { InstanceID = "NOT_A_PLANET_SECTOR" };

            _galaxyMap.AddChild(nonPlanetSector);

            Assert.AreEqual(0, _galaxyMap.GetChildren<PlanetSector>().Count);
        }

        /// <summary>
        /// Verifies add child with same planet sector twice adds it twice.
        /// </summary>
        [Test]
        public void AddChild_WithSamePlanetSectorTwice_AddsItTwice()
        {
            _galaxyMap.AddChild(_planetSector1);
            _galaxyMap.AddChild(_planetSector1);

            Assert.AreEqual(2, _galaxyMap.GetChildren<PlanetSector>().Count);
        }

        /// <summary>
        /// Verifies remove child existing planet sector removes it.
        /// </summary>
        [Test]
        public void RemoveChild_ExistingPlanetSector_RemovesIt()
        {
            _galaxyMap.AddChild(_planetSector1);

            _galaxyMap.RemoveChild(_planetSector1);

            Assert.IsFalse(_galaxyMap.GetChildren<PlanetSector>().Contains(_planetSector1));
        }

        /// <summary>
        /// Verifies remove child with multiple planet sectors removes correct sector.
        /// </summary>
        [Test]
        public void RemoveChild_WithMultiplePlanetSectors_RemovesCorrectSector()
        {
            _galaxyMap.AddChild(_planetSector1);
            _galaxyMap.AddChild(_planetSector2);

            _galaxyMap.RemoveChild(_planetSector1);

            Assert.AreEqual(1, _galaxyMap.GetChildren<PlanetSector>().Count);
            Assert.IsFalse(_galaxyMap.GetChildren<PlanetSector>().Contains(_planetSector1));
            Assert.Contains(_planetSector2, _galaxyMap.GetChildren<PlanetSector>().ToList());
        }

        /// <summary>
        /// Verifies remove child removing all sectors results in empty list.
        /// </summary>
        [Test]
        public void RemoveChild_RemovingAllSectors_ResultsInEmptyList()
        {
            _galaxyMap.AddChild(_planetSector1);
            _galaxyMap.AddChild(_planetSector2);

            _galaxyMap.RemoveChild(_planetSector1);
            _galaxyMap.RemoveChild(_planetSector2);

            Assert.AreEqual(0, _galaxyMap.GetChildren<PlanetSector>().Count);
        }

        /// <summary>
        /// Verifies remove child with null planet sector leaves children unchanged.
        /// </summary>
        [Test]
        public void RemoveChild_WithNullPlanetSector_LeavesChildrenUnchanged()
        {
            _galaxyMap.AddChild(_planetSector1);

            _galaxyMap.RemoveChild(null);

            CollectionAssert.AreEqual(
                new[] { _planetSector1 },
                _galaxyMap.GetChildren<PlanetSector>()
            );
        }

        /// <summary>
        /// Verifies remove child with sector not in list does not change count.
        /// </summary>
        [Test]
        public void RemoveChild_WithSectorNotInList_DoesNotChangeCount()
        {
            _galaxyMap.AddChild(_planetSector1);

            _galaxyMap.RemoveChild(_planetSector2);

            Assert.AreEqual(1, _galaxyMap.GetChildren<PlanetSector>().Count);
        }

        /// <summary>
        /// Verifies get children map with planet sectors returns all planet sectors.
        /// </summary>
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

        /// <summary>
        /// Verifies serialize and deserialize map with planet sectors maintains state.
        /// </summary>
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
                _galaxyMap.GetChildren<PlanetSector>().Count,
                deserialized.GetChildren<PlanetSector>().Count,
                "PlanetSectors count should be correctly deserialized."
            );
        }

        /// <summary>
        /// Verifies planet sectors when initialized is empty list.
        /// </summary>
        [Test]
        public void PlanetSectors_WhenInitialized_IsEmptyList()
        {
            GalaxyMap newMap = new GalaxyMap();

            Assert.IsNotNull(newMap.GetChildren<PlanetSector>());
            Assert.AreEqual(0, newMap.GetChildren<PlanetSector>().Count);
        }

        /// <summary>
        /// Verifies planet sectors after adding and removing maintains correct count.
        /// </summary>
        [Test]
        public void PlanetSectors_AfterAddingAndRemoving_MaintainsCorrectCount()
        {
            Assert.AreEqual(0, _galaxyMap.GetChildren<PlanetSector>().Count);

            _galaxyMap.AddChild(_planetSector1);
            Assert.AreEqual(1, _galaxyMap.GetChildren<PlanetSector>().Count);

            _galaxyMap.AddChild(_planetSector2);
            Assert.AreEqual(2, _galaxyMap.GetChildren<PlanetSector>().Count);

            _galaxyMap.RemoveChild(_planetSector1);
            Assert.AreEqual(1, _galaxyMap.GetChildren<PlanetSector>().Count);

            _galaxyMap.RemoveChild(_planetSector2);
            Assert.AreEqual(0, _galaxyMap.GetChildren<PlanetSector>().Count);
        }
    }
} // namespace Rebellion.Tests.Game.Galaxy
