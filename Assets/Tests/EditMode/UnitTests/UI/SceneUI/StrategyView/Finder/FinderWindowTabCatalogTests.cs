using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Factions;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Finder
{
    [TestFixture]
    public class FinderWindowTabCatalogTests
    {
        /// <summary>
        /// Verifies create faction only mode puts player faction first.
        /// </summary>
        /// <param name="mode">The mode.</param>
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

        /// <summary>
        /// Verifies create systems mode preserves system tabs around player first faction tabs.
        /// </summary>
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

        /// <summary>
        /// Verifies create fleets mode preserves all tab before player first faction tabs.
        /// </summary>
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

        /// <summary>
        /// Verifies create null factions returns only mode specific tabs.
        /// </summary>
        [Test]
        public void Create_NullFactions_ReturnsOnlyModeSpecificTabs()
        {
            List<FinderWindowTab> tabs = FinderWindowTabCatalog.Create(
                FinderMode.Systems,
                null,
                "player"
            );

            Assert.AreEqual(3, tabs.Count);
            Assert.IsTrue(tabs[0].IsAll);
            Assert.IsTrue(tabs[1].IsNeutral);
            Assert.IsTrue(tabs[2].IsUnexplored);
        }

        /// <summary>
        /// Verifies create invalid factions excludes null and missing identifiers.
        /// </summary>
        [Test]
        public void Create_InvalidFactions_ExcludesNullAndMissingIdentifiers()
        {
            List<Faction> factions = new List<Faction>
            {
                null,
                new Faction { InstanceID = string.Empty, DisplayName = "Missing" },
                new Faction { InstanceID = "player", DisplayName = "Player" },
            };

            List<FinderWindowTab> tabs = FinderWindowTabCatalog.Create(
                FinderMode.Personnel,
                factions,
                "player"
            );

            Assert.AreEqual(1, tabs.Count);
            Assert.AreEqual("player", tabs[0].FactionInstanceId);
        }

        /// <summary>
        /// Verifies create player faction missing preserves faction source order.
        /// </summary>
        [Test]
        public void Create_PlayerFactionMissing_PreservesFactionSourceOrder()
        {
            List<FinderWindowTab> tabs = FinderWindowTabCatalog.Create(
                FinderMode.Troops,
                CreateFactions(),
                "missing"
            );

            CollectionAssert.AreEqual(
                new[] { "opponent", "player" },
                tabs.Select(tab => tab.FactionInstanceId)
            );
        }

        /// <summary>
        /// Creates factions.
        /// </summary>
        /// <returns>The created factions.</returns>
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
