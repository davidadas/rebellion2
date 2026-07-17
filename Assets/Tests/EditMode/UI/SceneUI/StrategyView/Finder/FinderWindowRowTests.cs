using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Finder
{
    [TestFixture]
    public class FinderWindowRowTests
    {
        [Test]
        public void Constructor_NullOptionalValues_NormalizesNameAndCounts()
        {
            FinderWindowRow row = new FinderWindowRow(null, null);

            Assert.AreEqual(string.Empty, row.Name);
            Assert.AreEqual(string.Empty, row.Identity);
            Assert.AreEqual(PlanetIcon.None, row.TargetIcon);
            Assert.IsNull(row.Planet);
            Assert.IsNull(row.Node);
            Assert.IsNull(row.Fleet);
            Assert.IsNull(row.Mission);
            Assert.IsEmpty(row.Counts);
            Assert.IsNull(row.OwnerFactionId);
        }

        [Test]
        public void Constructor_CountSourceChanges_PreservesCountSnapshot()
        {
            int[] counts = { 1, 2, 3 };

            FinderWindowRow row = new FinderWindowRow("Row", null, counts: counts);
            counts[0] = 99;

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, row.Counts);
            Assert.Throws<System.NotSupportedException>(() =>
                ((System.Collections.Generic.IList<int>)row.Counts)[0] = 4
            );
        }

        [Test]
        public void Identity_NodeAndPlanet_UsesNodeIdentityAndOwner()
        {
            Planet planet = new Planet { InstanceID = "planet", OwnerInstanceID = "planet-owner" };
            Regiment regiment = new Regiment
            {
                InstanceID = "regiment",
                OwnerInstanceID = "unit-owner",
            };
            GalaxyMapPlanet mapPlanet = new GalaxyMapPlanet(
                new GamePlanetSystem(),
                planet,
                string.Empty
            );

            FinderWindowRow row = new FinderWindowRow(
                "Regiment",
                mapPlanet,
                PlanetIcon.Defense,
                regiment
            );

            Assert.AreEqual("regiment", row.Identity);
            Assert.AreEqual("unit-owner", row.OwnerFactionId);
        }

        [Test]
        public void Identity_PlanetOnly_UsesPlanetIdentityAndOwner()
        {
            Planet planet = new Planet { InstanceID = "planet", OwnerInstanceID = "planet-owner" };

            FinderWindowRow row = new FinderWindowRow(
                "Planet",
                new GalaxyMapPlanet(new GamePlanetSystem(), planet, string.Empty)
            );

            Assert.AreEqual("planet", row.Identity);
            Assert.AreEqual("planet-owner", row.OwnerFactionId);
        }
    }
}
