using System;
using NUnit.Framework;
using Rebellion.Game.Encyclopedia;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Encyclopedia
{
    [TestFixture]
    public class EncyclopediaWindowTabCatalogTests
    {
        [TestCase(0, EncyclopediaWindowTab.AllDatabases)]
        [TestCase(1, EncyclopediaWindowTab.Systems)]
        [TestCase(2, EncyclopediaWindowTab.Ships)]
        [TestCase(3, EncyclopediaWindowTab.Facilities)]
        [TestCase(4, EncyclopediaWindowTab.Missions)]
        [TestCase(5, EncyclopediaWindowTab.Troops)]
        [TestCase(6, EncyclopediaWindowTab.Personnel)]
        public void GetTab_AuthoredIndex_ReturnsSemanticTab(
            int index,
            EncyclopediaWindowTab expected
        )
        {
            EncyclopediaWindowTab tab = EncyclopediaWindowTabCatalog.GetTab(index);

            Assert.AreEqual(expected, tab);
            Assert.AreEqual(index, EncyclopediaWindowTabCatalog.GetIndex(tab));
            Assert.AreEqual(7, EncyclopediaWindowTabCatalog.Count);
        }

        [TestCase(-1)]
        [TestCase(7)]
        public void GetTab_InvalidIndex_ThrowsArgumentOutOfRangeException(int index)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                EncyclopediaWindowTabCatalog.GetTab(index)
            );
        }

        [TestCase(EncyclopediaWindowTab.AllDatabases, null)]
        [TestCase(EncyclopediaWindowTab.Systems, EncyclopediaEntryCategory.System)]
        [TestCase(EncyclopediaWindowTab.Ships, EncyclopediaEntryCategory.Ship)]
        [TestCase(EncyclopediaWindowTab.Facilities, EncyclopediaEntryCategory.Facility)]
        [TestCase(EncyclopediaWindowTab.Missions, EncyclopediaEntryCategory.Mission)]
        [TestCase(EncyclopediaWindowTab.Troops, EncyclopediaEntryCategory.Troop)]
        [TestCase(EncyclopediaWindowTab.Personnel, EncyclopediaEntryCategory.Personnel)]
        public void GetCategory_SemanticTab_ReturnsCatalogCategory(
            EncyclopediaWindowTab tab,
            EncyclopediaEntryCategory? expected
        )
        {
            EncyclopediaEntryCategory? category = EncyclopediaWindowTabCatalog.GetCategory(tab);

            Assert.AreEqual(expected, category);
        }

        [Test]
        public void GetCategory_UnsupportedTab_ReturnsNull()
        {
            EncyclopediaWindowTab tab = (EncyclopediaWindowTab)20;

            Assert.IsNull(EncyclopediaWindowTabCatalog.GetCategory(tab));
        }

        [TestCase(EncyclopediaWindowTab.AllDatabases, "All Databases")]
        [TestCase(EncyclopediaWindowTab.Systems, "System Database")]
        [TestCase(EncyclopediaWindowTab.Ships, "Ship Database")]
        [TestCase(EncyclopediaWindowTab.Facilities, "Facilities Database")]
        [TestCase(EncyclopediaWindowTab.Missions, "Missions Database")]
        [TestCase(EncyclopediaWindowTab.Troops, "Troop Database")]
        [TestCase(EncyclopediaWindowTab.Personnel, "Personnel Database")]
        public void GetTitle_SemanticTab_ReturnsDisplayedTitle(
            EncyclopediaWindowTab tab,
            string expected
        )
        {
            string title = EncyclopediaWindowTabCatalog.GetTitle(tab);

            Assert.AreEqual(expected, title);
        }

        [Test]
        public void GetTitle_UnsupportedTab_ReturnsEmptyString()
        {
            EncyclopediaWindowTab tab = (EncyclopediaWindowTab)20;

            Assert.AreEqual(string.Empty, EncyclopediaWindowTabCatalog.GetTitle(tab));
        }

        [Test]
        public void GetIndex_UnsupportedTab_ReturnsNegativeOne()
        {
            EncyclopediaWindowTab tab = (EncyclopediaWindowTab)20;

            Assert.AreEqual(-1, EncyclopediaWindowTabCatalog.GetIndex(tab));
        }
    }
}
