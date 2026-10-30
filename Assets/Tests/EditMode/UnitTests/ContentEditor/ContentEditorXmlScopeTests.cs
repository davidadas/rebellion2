using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentEditorXmlScopeTests
    {
        private string folder;
        private ContentPackEditorProject project;

        /// <summary>
        /// Creates a fixture-owned derived pack over installed content.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            string root = ContentPackLoader.ResolveContentRootPath();
            string baseline = ContentPackLoader
                .DiscoverPacks(root)
                .First(pack => string.IsNullOrEmpty(pack.BasePackID))
                .ID;
            folder = Path.Combine(
                Path.GetTempPath(),
                "editor-scope-" + Guid.NewGuid().ToString("N")
            );
            project = ContentPackEditorProject.CreateInFolder(
                root,
                baseline,
                folder,
                "scope-test",
                "Scope test",
                "1.0.0"
            );
        }

        /// <summary>
        /// Removes only this fixture's authored folder.
        /// </summary>
        [TearDown]
        public void TearDown() => Directory.Delete(folder, true);

        [Test]
        public void SetXml_OneOfficer_PreservesSiblingsAndSupportsSeveralHistorySteps()
        {
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .First(item => item.Name == "Officers");
            string sibling = catalog.Records[1].Element.ToString();
            string key = catalog.Records[0].Key;
            foreach (string name in new[] { "First edit", "Second edit", "Third edit" })
            {
                ContentPackEditorRecord record = project
                    .GetCatalogs()
                    .SelectMany(item => item.Records)
                    .Single(item => item.Key == key);
                XElement xml = new XElement(record.Element);
                xml.Element("DisplayName").Value = name;
                project.SetXml(ContentEditorXmlScope.Create(record, null).Single(), xml.ToString());
            }
            foreach (string name in new[] { "Second edit", "First edit" })
            {
                Assert.IsTrue(project.Undo());
                Assert.AreEqual(
                    name,
                    project
                        .GetCatalogs()
                        .SelectMany(item => item.Records)
                        .Single(item => item.Key == key)
                        .Name
                );
            }
            Assert.IsTrue(project.Redo());
            Assert.IsTrue(project.Redo());
            catalog = project.GetCatalogs().Single(item => item.Document == catalog.Document);
            Assert.AreEqual(sibling, catalog.Records[1].Element.ToString());
            Assert.IsFalse(File.Exists(catalog.Document.TargetPath));
        }

        [Test]
        public void SetXml_UnknownOfficerMember_PreservesTheWorkingDocument()
        {
            ContentPackEditorRecord officer = project
                .GetCatalogs()
                .First(item => item.Name == "Officers")
                .Records[0];
            string before = officer.Catalog.Document.Text;
            XElement xml = new XElement(officer.Element);
            xml.Add(new XElement("UnknownOfficerField", 1));
            Assert.Throws<InvalidOperationException>(() =>
                project.SetXml(ContentEditorXmlScope.Create(officer, null).Single(), xml.ToString())
            );
            Assert.AreEqual(before, officer.Catalog.Document.Text);
            Assert.IsFalse(project.CanUndo);
        }

        [Test]
        public void Create_LogicalMission_ExcludesUnrelatedRulesAndAiWeights()
        {
            ContentEditorNavigation navigation = new ContentEditorNavigation();
            navigation.Render(project.GetCatalogs());
            ContentEditorTreeNode page = navigation.Find("missions/Diplomacy");
            var scopes = ContentEditorXmlScope.Create(page.Record, page.Fields);
            Assert.IsTrue(scopes.Any(scope => XElement.Parse(scope.Text).Name == "Diplomacy"));
            Assert.IsFalse(
                scopes.Any(scope =>
                    XElement
                        .Parse(scope.Text)
                        .DescendantsAndSelf()
                        .Any(element => element.Name == "AI" || element.Name == "Sabotage")
                )
            );
        }

        [Test]
        public void SetXml_Planet_PreservesItsSectorAndOtherPlanets()
        {
            ContentPackEditorCatalog catalog = project.GetCatalogs().Single(item => item.IsMap);
            ContentPackEditorRecord sector = catalog.Records.First();
            ContentPackEditorRecord planet = sector.Planets.First();
            string sibling = sector.Planets.Skip(1).First().Element.ToString();
            XElement xml = new XElement(planet.Element);
            xml.Element("DisplayName").Value = "Edited planet";
            project.SetXml(ContentEditorXmlScope.Create(planet, null).Single(), xml.ToString());
            sector = project.GetCatalogs().Single(item => item.IsMap).Records.First();
            Assert.AreEqual("Edited planet", sector.Planets.First().Name);
            Assert.AreEqual(sibling, sector.Planets.Skip(1).First().Element.ToString());
        }

        [Test]
        public void AddTrait_BlankDefinition_UsesNeutralValuesAndCanBeUndone()
        {
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .Single(item => item.CanAddTrait);
            int count = catalog.Records.Count;
            ContentPackEditorRecord trait = project.AddTrait(catalog);
            Assert.AreEqual("Trait", trait.Kind);
            Assert.AreEqual("100", trait.GetField("PersonnelTravelTimePercent").Value);
            Assert.AreEqual("0", trait.GetField("LeadershipBonusPercent").Value);
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(
                count,
                project.GetCatalogs().Single(item => item.CanAddTrait).Records.Count
            );
        }
    }
}
