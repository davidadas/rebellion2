using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Factions;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Finder
{
    [TestFixture]
    public class FinderWindowTabCatalogTests
    {
        [TestCase(FinderMode.Troops)]
        [TestCase(FinderMode.Personnel)]
        public void Create_FactionOnlyMode_PutsPlayerFactionFirst(FinderMode mode)
        {
            List<FinderWindowTab> tabs = FinderWindowTabCatalog.Create(
                mode,
                CreateFactions(),
                "player"
            );

            CollectionAssert.AreEqual(
                new[] { "player", "opponent" },
                tabs.Select(tab => tab.FactionInstanceId)
            );
        }

        [Test]
        public void Create_SystemsMode_PreservesSystemTabsAroundPlayerFirstFactionTabs()
        {
            List<FinderWindowTab> tabs = FinderWindowTabCatalog.Create(
                FinderMode.Systems,
                CreateFactions(),
                "player"
            );

            Assert.IsTrue(tabs[0].IsAll);
            CollectionAssert.AreEqual(
                new[] { "player", "opponent" },
                tabs.Skip(1).Take(2).Select(tab => tab.FactionInstanceId)
            );
            Assert.IsTrue(tabs[3].IsNeutral);
            Assert.IsTrue(tabs[4].IsUnexplored);
        }

        [Test]
        public void Create_FleetsMode_PreservesAllTabBeforePlayerFirstFactionTabs()
        {
            List<FinderWindowTab> tabs = FinderWindowTabCatalog.Create(
                FinderMode.Fleets,
                CreateFactions(),
                "player"
            );

            Assert.IsTrue(tabs[0].IsAll);
            CollectionAssert.AreEqual(
                new[] { "player", "opponent" },
                tabs.Skip(1).Select(tab => tab.FactionInstanceId)
            );
        }

        private static IReadOnlyList<Faction> CreateFactions()
        {
            return new[]
            {
                new Faction { InstanceID = "opponent", DisplayName = "Opponent" },
                new Faction { InstanceID = "player", DisplayName = "Player" },
            };
        }
    }
}
