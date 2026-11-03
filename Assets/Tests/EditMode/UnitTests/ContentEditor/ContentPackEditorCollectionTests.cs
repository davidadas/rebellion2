using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentPackEditorCollectionTests
    {
        private ContentPackEditorProject project;
        private string folder;
        private ContentPackEditorRecord Setup =>
            project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record => record.Kind == "GameGenerationConfig");

        /// <summary>
        /// Creates independent starting-setup definitions for collection edits.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(
                Path.GetTempPath(),
                "editor-setup-" + Guid.NewGuid().ToString("N")
            );
            project = ContentEditorTestData.CreateProject(folder);
        }

        /// <summary>
        /// Removes fixture-owned content.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        [Test]
        public void GetCollection_MissingCollection_DoesNotCreateAnOverride()
        {
            var record = Setup;
            XElement ship = record.Element.Descendants("FixedFleetShip").First();
            ship.Element("Cargo")?.Remove();
            string before = record.Catalog.Document.Text;
            var collection = project.GetCollection(record, ship, "Cargo");
            Assert.IsNotNull(collection);
            Assert.AreEqual(before, record.Catalog.Document.Text);
            Assert.IsFalse(project.IsDirty);
        }

        [TestCase("Officers", "StartingOfficers", "StartingOfficerRule")]
        [TestCase("GalaxyClassification", "FactionSetups", "FactionSetup")]
        [TestCase("GalaxyClassification", "FactionBuckets", "FactionBucketConfig")]
        [TestCase("FacilityGeneration", "CoreFacilityTable", "WeightedFacilityEntry")]
        [TestCase("FacilityGeneration", "RimFacilityTable", "WeightedFacilityEntry")]
        [TestCase("FacilityGeneration", "HQLoadouts", "HQFacilityLoadout")]
        [TestCase("UnitDeployment", "FixedGarrisons", "FixedGarrison")]
        [TestCase("UnitDeployment", "FixedFleets", "FixedFleet")]
        [TestCase("UnitDeployment", "FactionBudgets", "FactionBudget")]
        [TestCase("PlanetResources", "Profiles", "PlanetResourceProfile")]
        public void AddCollectionEntry_StartingSetupCollection_AddsIndependentEntryAndUndoesExactly(
            string group,
            string member,
            string elementName
        )
        {
            var record = Setup;
            string before = record.Catalog.Document.Text;
            var collection = project.GetCollection(record, record.Element, group, member);
            int count = collection.Entries.Count;
            project.AddCollectionEntry(collection);
            Assert.AreEqual(count + 1, collection.Entries.Count);
            Assert.AreEqual(elementName, collection.Entries.Last().Name.LocalName);
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(before, Setup.Catalog.Document.Text);
            Assert.IsTrue(project.Redo());
            Assert.AreEqual(
                count + 1,
                project.GetCollection(Setup, Setup.Element, group, member).Entries.Count
            );
        }

        [Test]
        public void SetCollectionQuantities_UnchangedDuplicateUnits_PreservesXmlAndHistory()
        {
            var record = Setup;
            var collection = project.GetCollection(
                record,
                record.Element,
                "UnitDeployment",
                "FixedGarrisons"
            );
            project.AddCollectionEntry(collection);
            XElement placement = collection.Entries.Last();
            var units = project.GetCollection(record, placement, "Units");
            string id = project.GetCollectionUnitOptions(units).Keys.First();
            units.Element.Add(
                new XElement(
                    "UnitEntry",
                    new XComment("retained"),
                    new XElement("TypeID", id),
                    new XElement("Count", 2)
                ),
                new XElement("UnitEntry", new XElement("TypeID", id), new XElement("Count", 3))
            );
            record.Catalog.Commit();
            string before = record.Catalog.Document.Text;
            string history = project.UndoDescription;
            project.SetCollectionQuantities(units, new Dictionary<string, int> { [id] = 5 });
            Assert.AreEqual(before, record.Catalog.Document.Text);
            Assert.AreEqual(history, project.UndoDescription);
        }

        [Test]
        public void SetCollectionQuantities_ChangedSelection_ChangesOnlySelectedCountsAndSupportsUndo()
        {
            var record = Setup;
            var collection = project.GetCollection(
                record,
                record.Element,
                "UnitDeployment",
                "FixedGarrisons"
            );
            project.AddCollectionEntry(collection);
            var units = project.GetCollection(record, collection.Entries.Last(), "Units");
            string[] ids = project.GetCollectionUnitOptions(units).Keys.Take(2).ToArray();
            project.SetCollectionQuantities(units, new Dictionary<string, int> { [ids[0]] = 2 });
            string before = record.Catalog.Document.Text;
            project.SetCollectionQuantities(
                units,
                new Dictionary<string, int> { [ids[0]] = 4, [ids[1]] = 3 }
            );
            CollectionAssert.AreEqual(
                new[] { 4, 3 },
                units.Entries.Select(entry => (int)entry.Element("Count"))
            );
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(before, Setup.Catalog.Document.Text);
        }

        [Test]
        public void SetCollectionQuantities_UnknownUnit_RejectsWithoutChangingTheDocument()
        {
            var record = Setup;
            var placements = project.GetCollection(
                record,
                record.Element,
                "UnitDeployment",
                "FixedGarrisons"
            );
            project.AddCollectionEntry(placements);
            var units = project.GetCollection(record, placements.Entries.Last(), "Units");
            string before = record.Catalog.Document.Text;
            Assert.Throws<ArgumentException>(() =>
                project.SetCollectionQuantities(
                    units,
                    new Dictionary<string, int> { ["missing"] = 2 }
                )
            );
            Assert.AreEqual(before, record.Catalog.Document.Text);
        }

        [Test]
        public void SetCollectionQuantities_RemovingAllCargo_OmitsOptionalBlockAndUndoesExactly()
        {
            var record = Setup;
            XElement ship = record
                .Element.Descendants("FixedFleetShip")
                .First(entry => entry.Element("Cargo") != null);
            var cargo = project.GetCollection(record, ship, "Cargo");
            string before = record.Catalog.Document.Text;
            project.SetCollectionQuantities(cargo, new Dictionary<string, int>());
            Assert.IsNull(ship.Element("Cargo"));
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(before, Setup.Catalog.Document.Text);
        }

        [Test]
        public void RemoveCollectionEntry_SelectedEntry_RemovesOnlyThatEntryAndUndoesExactly()
        {
            var record = Setup;
            var collection = project.GetCollection(
                record,
                record.Element,
                "Officers",
                "StartingOfficers"
            );
            string before = record.Catalog.Document.Text;
            string[] remaining = collection
                .Entries.Skip(1)
                .Select(entry => entry.ToString())
                .ToArray();
            project.RemoveCollectionEntry(collection, 0);
            CollectionAssert.AreEqual(
                remaining,
                collection.Entries.Select(entry => entry.ToString())
            );
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(before, Setup.Catalog.Document.Text);
        }
    }
}
