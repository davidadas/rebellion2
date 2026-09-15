using NUnit.Framework;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Finder
{
    [TestFixture]
    public class FinderWindowTabTests
    {
        /// <summary>
        /// Verifies all default factory creates tab without faction identity.
        /// </summary>
        [Test]
        public void All_DefaultFactory_CreatesTabWithoutFactionIdentity()
        {
            FinderWindowTab tab = FinderWindowTab.All();

            Assert.AreEqual(FinderWindowTabKind.All, tab.Kind);
            Assert.IsTrue(tab.IsAll);
            Assert.IsFalse(tab.IsNeutral);
            Assert.IsFalse(tab.IsUnexplored);
            Assert.IsNull(tab.FactionInstanceId);
            Assert.IsNull(tab.FactionDisplayName);
        }

        /// <summary>
        /// Verifies neutral default factory creates tab without faction identity.
        /// </summary>
        [Test]
        public void Neutral_DefaultFactory_CreatesTabWithoutFactionIdentity()
        {
            FinderWindowTab tab = FinderWindowTab.Neutral();

            Assert.AreEqual(FinderWindowTabKind.Neutral, tab.Kind);
            Assert.IsFalse(tab.IsAll);
            Assert.IsTrue(tab.IsNeutral);
            Assert.IsFalse(tab.IsUnexplored);
            Assert.IsNull(tab.FactionInstanceId);
        }

        /// <summary>
        /// Verifies unexplored default factory creates tab without faction identity.
        /// </summary>
        [Test]
        public void Unexplored_DefaultFactory_CreatesTabWithoutFactionIdentity()
        {
            FinderWindowTab tab = FinderWindowTab.Unexplored();

            Assert.AreEqual(FinderWindowTabKind.Unexplored, tab.Kind);
            Assert.IsFalse(tab.IsAll);
            Assert.IsFalse(tab.IsNeutral);
            Assert.IsTrue(tab.IsUnexplored);
            Assert.IsNull(tab.FactionInstanceId);
        }

        /// <summary>
        /// Verifies faction identity creates faction tab.
        /// </summary>
        [Test]
        public void Faction_Identity_CreatesFactionTab()
        {
            FinderWindowTab tab = FinderWindowTab.Faction("faction", "Faction");

            Assert.AreEqual(FinderWindowTabKind.Faction, tab.Kind);
            Assert.IsFalse(tab.IsAll);
            Assert.IsFalse(tab.IsNeutral);
            Assert.IsFalse(tab.IsUnexplored);
            Assert.AreEqual("faction", tab.FactionInstanceId);
            Assert.AreEqual("Faction", tab.FactionDisplayName);
        }
    }
}
