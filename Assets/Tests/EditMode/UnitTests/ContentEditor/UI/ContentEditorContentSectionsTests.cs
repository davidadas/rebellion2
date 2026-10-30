using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentEditorContentSectionsTests
    {
        private string folder;

        /// <summary>
        /// Creates an isolated folder for authored catalog fixtures.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(
                Path.GetTempPath(),
                "editor-sections-" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(folder);
        }

        /// <summary>
        /// Removes the fixture-owned catalog folder.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Directory.Delete(folder, true);
        }

        [Test]
        public void EncyclopediaFields_MissingEmbeddedData_EditsOnlyTheStandaloneFallback()
        {
            ContentPackEditorCatalog units = Catalog(
                "officers",
                "<Officers><Officer><TypeID>OFF1</TypeID></Officer></Officers>"
            );
            ContentPackEditorCatalog articles = Catalog(
                "articles",
                "<EncyclopediaEntries><EncyclopediaEntry><TypeID>OFF1</TypeID><Description>Old article</Description></EncyclopediaEntry></EncyclopediaEntries>"
            );
            ContentEditorContentSections
                .EncyclopediaFields(units.Records[0], new[] { units, articles })
                .Single(field => field.Group == "Description")
                .SetValue("New article");
            Assert.AreEqual("New article", articles.Records[0].GetField("Description").Value);
            Assert.IsFalse(units.Document.IsDirty);
        }

        [Test]
        public void EncyclopediaFields_NewEntity_OffersOptionalFieldsWithoutWritingAnOverride()
        {
            ContentPackEditorCatalog units = Catalog(
                "officers",
                "<Officers><Officer><TypeID>OFF1</TypeID></Officer></Officers>"
            );
            var fields = ContentEditorContentSections.EncyclopediaFields(
                units.Records[0],
                new[] { units }
            );
            Assert.IsTrue(fields.Any(field => field.Group == "EncyclopediaDescription"));
            Assert.IsTrue(fields.Any(field => field.Group == "EncyclopediaImagePath"));
            Assert.IsFalse(units.Document.IsDirty);
            fields
                .Single(field => field.Group == "EncyclopediaDescription")
                .SetValue("New officer article");
            Assert.AreEqual(
                "New officer article",
                units.Records[0].GetField("EncyclopediaDescription").Value
            );
        }

        [TestCase("<EncyclopediaDescription>Embedded article</EncyclopediaDescription>")]
        [TestCase("<EncyclopediaImagePath>Pack/test-image</EncyclopediaImagePath>")]
        [TestCase(
            "<EncyclopediaStats><EncyclopediaEntryStat><Label>Skill</Label><Value>4</Value></EncyclopediaEntryStat></EncyclopediaStats>"
        )]
        public void EncyclopediaFields_AnyEmbeddedData_TakesPriorityOverTheStandaloneArticle(
            string embedded
        )
        {
            ContentPackEditorCatalog units = Catalog(
                "officers",
                "<Officers><Officer><TypeID>OFF1</TypeID>" + embedded + "</Officer></Officers>"
            );
            ContentPackEditorCatalog articles = Catalog(
                "articles",
                "<EncyclopediaEntries><EncyclopediaEntry><TypeID>OFF1</TypeID><Description>Unused fallback</Description></EncyclopediaEntry></EncyclopediaEntries>"
            );
            var fields = ContentEditorContentSections.EncyclopediaFields(
                units.Records[0],
                new[] { units, articles }
            );
            Assert.IsTrue(
                fields.All(field =>
                    field.Group.StartsWith("Encyclopedia", StringComparison.Ordinal)
                )
            );
            fields
                .Single(field => field.Group == "EncyclopediaDescription")
                .SetValue("New embedded article");
            Assert.IsFalse(articles.Document.IsDirty);
            Assert.AreEqual(
                "New embedded article",
                units.Records[0].GetField("EncyclopediaDescription").Value
            );
        }

        /// <summary>
        /// Opens a fixture document as an editable catalog.
        /// </summary>
        /// <param name="name">The fixture filename.</param>
        /// <param name="xml">The catalog contents.</param>
        /// <returns>The editable fixture catalog.</returns>
        private ContentPackEditorCatalog Catalog(string name, string xml)
        {
            string path = Path.Combine(folder, name + ".xml");
            File.WriteAllText(path, xml);
            return new ContentPackEditorCatalog(
                new ContentPackEditorDocument("Pack/" + name + ".xml", path, path)
            );
        }
    }
}
