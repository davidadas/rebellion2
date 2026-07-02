using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Encyclopedia;

namespace Rebellion.Tests.Game.Encyclopedia
{
    [TestFixture]
    public class EncyclopediaCatalogTests
    {
        [Test]
        public void GetRows_WithNullCategory_ReturnsVisibleEntriesSortedByName()
        {
            EncyclopediaCatalog catalog = new EncyclopediaCatalog(
                new[]
                {
                    new EncyclopediaEntry
                    {
                        TypeID = "SHIP1",
                        DisplayName = "Nebulon-B Frigate",
                        Category = EncyclopediaEntryCategory.Ship,
                    },
                    new EncyclopediaEntry
                    {
                        TypeID = "SYSTEM1",
                        DisplayName = "Balmorra",
                        Category = EncyclopediaEntryCategory.System,
                    },
                }
            );

            List<EncyclopediaEntry> rows = catalog.GetRows(null, null);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("Balmorra", rows[0].DisplayName);
            Assert.AreEqual("Nebulon-B Frigate", rows[1].DisplayName);
        }

        [Test]
        public void GetRows_WithCategory_ReturnsMatchingCategoryRows()
        {
            EncyclopediaCatalog catalog = new EncyclopediaCatalog(
                new[]
                {
                    new EncyclopediaEntry
                    {
                        TypeID = "SHIP1",
                        DisplayName = "Nebulon-B Frigate",
                        Category = EncyclopediaEntryCategory.Ship,
                    },
                    new EncyclopediaEntry
                    {
                        TypeID = "SYSTEM1",
                        DisplayName = "Balmorra",
                        Category = EncyclopediaEntryCategory.System,
                    },
                }
            );

            List<EncyclopediaEntry> rows = catalog.GetRows(EncyclopediaEntryCategory.Ship, null);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("SHIP1", rows[0].TypeID);
        }

        [Test]
        public void GetRows_WithFactionVisibility_ExcludesOtherFactionEntries()
        {
            EncyclopediaCatalog catalog = new EncyclopediaCatalog(
                new[]
                {
                    new EncyclopediaEntry
                    {
                        TypeID = "VISIBLE",
                        DisplayName = "Visible",
                        Category = EncyclopediaEntryCategory.Ship,
                        VisibleFactionInstanceID = "FNALL1",
                    },
                    new EncyclopediaEntry
                    {
                        TypeID = "HIDDEN",
                        DisplayName = "Hidden",
                        Category = EncyclopediaEntryCategory.Ship,
                        VisibleFactionInstanceID = "FNEMP1",
                    },
                }
            );

            List<EncyclopediaEntry> rows = catalog.GetRows(
                EncyclopediaEntryCategory.Ship,
                "FNALL1"
            );

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("VISIBLE", rows[0].TypeID);
        }
    }
}
