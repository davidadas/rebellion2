using NUnit.Framework;
using Rebellion.Game.Galaxy;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Bookmarks
{
    [TestFixture]
    public class BookmarkEntryTests
    {
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
