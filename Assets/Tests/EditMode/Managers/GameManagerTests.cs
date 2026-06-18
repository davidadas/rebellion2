using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public class GameManagerTests
    {
        [Test]
        public void Constructor_WithFactions_RebuildsResearchCatalogs()
        {
            GameRoot game = new GameRoot();
            Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            Faction empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            game.Factions.Add(alliance);
            game.Factions.Add(empire);

            Assume.That(
                alliance.ResearchCatalog,
                Is.Empty,
                "Catalog must start empty to prove the rebuild populates it"
            );
            Assume.That(empire.ResearchCatalog, Is.Empty);

            _ = new GameManager(game);

            Assert.IsNotEmpty(
                alliance.ResearchCatalog,
                "Alliance research catalog should be rebuilt after GameManager construction"
            );
            Assert.IsNotEmpty(
                empire.ResearchCatalog,
                "Empire research catalog should be rebuilt after GameManager construction"
            );
        }
    }
}
