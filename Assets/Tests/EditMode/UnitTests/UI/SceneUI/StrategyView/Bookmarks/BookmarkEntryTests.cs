using NUnit.Framework;
using Rebellion.Game.Galaxy;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Bookmarks
{
    [TestFixture]
    public class BookmarkEntryTests
    {
        [Test]
        public void Constructor_CompleteValues_StoresBookmarkIdentityAndPlacement()
        {
            GalaxyMapPlanet planet = CreatePlanet("planet-1", "Coruscant");

            BookmarkEntry entry = new BookmarkEntry(PlanetIcon.Fleet, 120, 240, planet);

            Assert.AreEqual(PlanetIcon.Fleet, entry.Icon);
            Assert.AreEqual(120, entry.X);
            Assert.AreEqual(240, entry.Y);
            Assert.AreSame(planet, entry.Planet);
        }

        [Test]
        public void ReconcilePlanet_FreshProjection_ReplacesPlanetOnly()
        {
            GalaxyMapPlanet original = CreatePlanet("planet-1", "Coruscant");
            GalaxyMapPlanet replacement = CreatePlanet("planet-1", "Coruscant Prime");
            BookmarkEntry entry = new BookmarkEntry(PlanetIcon.Mission, 12, 34, original);

            entry.ReconcilePlanet(replacement);

            Assert.AreEqual(PlanetIcon.Mission, entry.Icon);
            Assert.AreEqual(12, entry.X);
            Assert.AreEqual(34, entry.Y);
            Assert.AreSame(replacement, entry.Planet);
        }

        private static GalaxyMapPlanet CreatePlanet(string instanceId, string displayName)
        {
            Planet planet = new Planet { InstanceID = instanceId, DisplayName = displayName };
            return new GalaxyMapPlanet(null, planet, string.Empty);
        }
    }
}
