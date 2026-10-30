using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NUnit.Framework;
using Rebellion.Game.Messages;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentPackEditorProjectTests
    {
        private string contentRoot;

        /// <summary>
        ///
        /// Creates an isolated content root for each test.
        ///
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            contentRoot = Path.Combine(
                Path.GetTempPath(),
                "content-pack-editor-" + Guid.NewGuid().ToString("N"),
                "Content"
            );
            Directory.CreateDirectory(Path.Combine(contentRoot, "Packs", "BasePack", "Data"));
            WriteBasePack();
        }

        /// <summary>
        ///
        /// Removes the isolated content root after each test.
        ///
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(contentRoot))
                Directory.Delete(Directory.GetParent(contentRoot).FullName, true);
        }

        [Test]
        public void AddFaction_BlankDefinition_RegistersEmptyCatalogsWithoutWritingFiles()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            int before = project.Documents.Count;
            ContentPackEditorRecord faction = project.AddFaction();
            Assert.AreEqual(before + 9, project.Documents.Count);
            Assert.AreEqual("New faction", faction.Name);
            Assert.IsTrue(faction.GetFields().Any(field => field.Group == "Settings"));
            Assert.IsTrue(project.GetFactionOptions().ContainsKey(faction.ID));
            Assert.AreEqual(1, project.Definition.FactionPaths.Count);
            ContentPackEditorCatalog[] units = project
                .GetCatalogs()
                .Where(catalog => catalog.Faction == faction.Catalog.Faction && catalog.CanAddUnit)
                .ToArray();
            Assert.AreEqual(5, units.Length);
            Assert.IsTrue(units.All(catalog => catalog.Records.Count == 0));
            Assert.IsFalse(
                Directory.Exists(Path.GetDirectoryName(faction.Catalog.Document.TargetPath))
            );
            ContentPackEditorRecord officer = project.AddUnit(
                units.Single(catalog => catalog.Name == "Officers")
            );
            Assert.IsEmpty(officer.GetField("RecruitingFactionInstanceIDs").Values);
        }

        [Test]
        public void AddUnit_UnknownFaction_RejectsCreationWithoutChangingTheCatalog()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorRecord faction = project.AddFaction();
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .Single(item => item.Name == "Officers" && item.Faction == faction.Catalog.Faction);
            string before = catalog.Document.Text;

            Assert.Throws<ArgumentException>(() => project.AddUnit(catalog, "missing-faction"));

            Assert.IsEmpty(catalog.Records);
            Assert.AreEqual(before, catalog.Document.Text);
        }

        [Test]
        public void AddUnit_NewFactionCatalog_AssignsEligibilityInOneUndoableEdit()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorRecord faction = project.AddFaction();
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .Single(item => item.Name == "Officers" && item.Faction == faction.Catalog.Faction);

            ContentPackEditorRecord officer = project.AddUnit(catalog, faction.ID);

            CollectionAssert.AreEqual(
                new[] { faction.ID },
                officer.GetField("RecruitingFactionInstanceIDs").Values
            );
            Assert.IsTrue(project.Undo());
            Assert.IsEmpty(
                project.GetCatalogs().Single(item => item.Document == catalog.Document).Records
            );
            Assert.IsTrue(project.Redo());
            CollectionAssert.AreEqual(
                new[] { faction.ID },
                project
                    .GetCatalogs()
                    .Single(item => item.Document == catalog.Document)
                    .Records.Single()
                    .GetField("RecruitingFactionInstanceIDs")
                    .Values
            );
        }

        [Test]
        public void DuplicateRecord_Faction_RejectsAnUnregisteredClone()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorRecord faction = project.AddFaction();
            string before = faction.Catalog.Document.Text;
            Assert.IsFalse(faction.Catalog.CanDuplicate);
            Assert.Throws<InvalidOperationException>(() => project.DuplicateRecord(faction));
            Assert.AreEqual(before, faction.Catalog.Document.Text);
            Assert.AreEqual(1, project.GetFactionOptions().Count);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RemoveRecord_Faction_RemovesLinkedDocumentsAndSupportsSaveUndoRedo(
            bool savedCreation
        )
        {
            ContentPackEditorProject project = CreateWorkingProject();
            string[] before = project.Documents.Select(document => document.Address).ToArray();
            ContentPackEditorRecord faction = project.AddFaction();
            string id = faction.ID;
            string[] createdPaths = project
                .Documents.Where(document => !before.Contains(document.Address))
                .Select(document => document.TargetPath)
                .ToArray();
            if (savedCreation)
                ContentPackEditorStorage.Save(project);
            project.RemoveRecord(faction);
            Assert.IsEmpty(project.Definition.FactionPaths);
            Assert.IsEmpty(project.GetFactionOptions());
            CollectionAssert.AreEquivalent(
                before,
                project.Documents.Select(document => document.Address)
            );
            Assert.IsTrue(createdPaths.All(path => File.Exists(path) == savedCreation));
            ContentPackEditorStorage.Save(project);
            Assert.IsTrue(createdPaths.All(path => !File.Exists(path)));
            ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                project.PackRootPath
            );
            Assert.IsEmpty(reopened.GetFactionOptions());
            Assert.IsFalse(
                reopened
                    .GetCatalogs()
                    .Any(catalog =>
                        catalog.DocumentRecord.Element.Element("FactionDataPath") != null
                    )
            );
            Assert.IsTrue(project.Undo());
            Assert.IsTrue(project.GetFactionOptions().ContainsKey(id));
            ContentPackEditorStorage.Save(project);
            Assert.IsTrue(createdPaths.All(File.Exists));
            Assert.IsTrue(project.Redo());
            ContentPackEditorStorage.Save(project);
            Assert.IsTrue(createdPaths.All(path => !File.Exists(path)));
        }

        [Test]
        public void RemoveRecord_InheritedFaction_ReopeningKeepsItRemovedWithoutChangingBaseFiles()
        {
            var inherited = ContentEditorFactionFactory.Create("FACTION", "Factions/Example", null);
            foreach (var file in inherited)
                WriteFixtureFile(file.Key, file.Value.ToString());
            string manifestPath = Path.Combine(contentRoot, "Packs", "BasePack", "pack.xml");
            XDocument manifest = XDocument.Load(manifestPath);
            manifest.Root.Element("FactionPaths")?.Remove();
            manifest.Root.Add(
                new XElement("FactionPaths", new XElement("Path", "Factions/Example/faction.xml"))
            );
            manifest.Save(manifestPath);
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorRecord faction = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record =>
                    record.Kind == "Faction"
                    && record.Element.Element("InstanceID")?.Value == "FACTION"
                );
            Assert.IsTrue(faction.Catalog.Document.IsInherited);
            project.RemoveRecord(faction);
            ContentPackEditorStorage.Save(project);
            ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                project.PackRootPath
            );
            Assert.IsEmpty(reopened.GetFactionOptions());
            Assert.IsFalse(reopened.GetCatalogs().Any(catalog => catalog.Faction == "Example"));
            foreach (var file in inherited)
                Assert.AreEqual(
                    file.Value.ToString(),
                    File.ReadAllText(Path.Combine(contentRoot, "Packs", "BasePack", file.Key))
                );
        }

        [Test]
        public void RemoveRecord_FactionSharingACatalog_PreservesTheOtherFactionsDefinitions()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorRecord first = project.AddFaction();
            ContentPackEditorRecord second = project.AddFaction();
            first = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record =>
                    record.Kind == "Faction"
                    && record.Element.Element("InstanceID")?.Value == first.ID
                );
            ContentPackEditorCatalog ships = project
                .GetCatalogs()
                .Single(catalog =>
                    catalog.Name == "Capital ships" && catalog.Faction == first.Catalog.Faction
                );
            ContentPackEditorRecord ship = project.AddUnit(ships, second.ID);
            ContentPackEditorRecord declaration = project
                .GetCatalogs()
                .Single(catalog =>
                    catalog.DocumentRecord.Element.Element("FactionDataPath") != null
                    && catalog.DocumentRecord.Element.Element("ID")?.Value == second.ID
                )
                .DocumentRecord;
            project.SetField(declaration.GetField("CapitalShipsPath"), ships.Document.Address[5..]);
            project.RemoveRecord(first);
            Assert.IsFalse(project.GetFactionOptions().ContainsKey(first.ID));
            Assert.IsTrue(project.GetFactionOptions().ContainsKey(second.ID));
            Assert.AreEqual(
                ship.ID,
                project
                    .GetCatalogs()
                    .Single(catalog => catalog.Document == ships.Document)
                    .Records.Single()
                    .ID
            );
            ContentPackEditorStorage.Save(project);
            ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                project.PackRootPath
            );
            Assert.AreEqual(
                ship.ID,
                reopened
                    .GetCatalogs()
                    .Single(catalog => catalog.Document.Address == ships.Document.Address)
                    .Records.Single()
                    .ID
            );
        }

        [Test]
        public void RemoveRecord_RenamedFactionIdentity_RemovesTheDeclarationForItsDataFile()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorRecord faction = project.AddFaction();
            project.SetField(faction.GetField("InstanceID"), "RENAMED_FACTION");
            project.RemoveRecord(faction);
            Assert.IsEmpty(project.Definition.FactionPaths);
            Assert.IsEmpty(project.GetFactionOptions());
            Assert.IsFalse(
                project
                    .GetCatalogs()
                    .Any(catalog =>
                        catalog.DocumentRecord.Element.Element("FactionDataPath") != null
                    )
            );
        }

        [Test]
        public void RemoveRecord_FactionWithAnAlreadyRemovedGameplayEntry_CleansUpItsDeclaration()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorRecord faction = project.AddFaction();
            faction.Catalog.Remove(faction);
            ContentPackEditorRecord declaration = project
                .GetCatalogs()
                .Single(catalog =>
                    catalog.DocumentRecord.Element.Element("ID")?.Value == faction.ID
                    && catalog.DocumentRecord.Element.Element("FactionDataPath") != null
                )
                .DocumentRecord;
            project.RemoveRecord(declaration);
            Assert.IsEmpty(project.Definition.FactionPaths);
            Assert.IsFalse(
                project
                    .GetCatalogs()
                    .Any(catalog =>
                        catalog.DocumentRecord.Element.Element("FactionDataPath") != null
                    )
            );
        }

        [Test]
        public void RemoveRecord_MultipleFactionsInOneDataFile_PreservesTheRemainingEntry()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorRecord faction = project.AddFaction();
            ContentPackEditorRecord retained = faction.Catalog.Duplicate(faction);
            string before = faction.Catalog.Document.Text;
            project.RemoveRecord(faction);
            Assert.IsFalse(project.GetFactionOptions().ContainsKey(faction.ID));
            Assert.IsTrue(project.GetFactionOptions().ContainsKey(retained.ID));
            Assert.AreEqual(1, project.Definition.FactionPaths.Count);
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(
                before,
                project
                    .GetCatalogs()
                    .Single(catalog => catalog.Document == faction.Catalog.Document)
                    .Document.Text
            );
        }

        [Test]
        public void AddFaction_RepeatedCreation_UsesIndependentIdentitiesAndCatalogs()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorRecord first = project.AddFaction();
            ContentPackEditorRecord second = project.AddFaction();
            Assert.AreNotEqual(first.ID, second.ID);
            Assert.AreNotEqual(first.Catalog.Document.Address, second.Catalog.Document.Address);
            Assert.AreEqual(2, project.GetFactionOptions().Count);
            Assert.AreEqual(2, project.Definition.FactionPaths.Distinct().Count());
        }

        [Test]
        public void AddFaction_IdentityUsedByPlanet_AllocatesAnUnusedIdentity()
        {
            WriteFixtureFile(
                "Data/units.xml",
                "<PlanetSectors><PlanetSector><TypeID>SECTOR</TypeID><Planets>"
                    + "<Planet><InstanceID>NEW_FACTION</InstanceID></Planet></Planets></PlanetSector></PlanetSectors>"
            );
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorRecord faction = project.AddFaction();
            Assert.AreNotEqual("NEW_FACTION", faction.ID);
            Assert.IsTrue(project.GetFactionOptions().ContainsKey(faction.ID));
        }

        [Test]
        public void AddFaction_UndoRedo_RestoresAllDocumentsAndManifestAsOneEdit()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            string[] before = project.Documents.Select(document => document.Text).ToArray();
            string id = project.AddFaction().ID;
            Assert.IsTrue(project.Undo());
            CollectionAssert.AreEqual(before, project.Documents.Select(document => document.Text));
            Assert.IsFalse(project.IsDirty);
            Assert.IsEmpty(project.Definition.FactionPaths);
            Assert.IsEmpty(project.GetFactionOptions());
            Assert.IsTrue(project.Redo());
            Assert.IsTrue(project.GetFactionOptions().ContainsKey(id));
            Assert.AreEqual(1, project.Definition.FactionPaths.Count);
        }

        [Test]
        public void AddFaction_UndoSavedCreation_RemovesOnlyItsFilesOnSaveAndCanBeRedone()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            string[] before = project.Documents.Select(document => document.Address).ToArray();
            string id = project.AddFaction().ID;
            string[] addedPaths = project
                .Documents.Where(document => !before.Contains(document.Address))
                .Select(document => document.TargetPath)
                .ToArray();
            ContentPackEditorStorage.Save(project);
            Assert.IsTrue(addedPaths.All(File.Exists));
            Assert.IsTrue(project.Undo());
            ContentPackEditorStorage.Save(project);
            Assert.IsTrue(addedPaths.All(path => !File.Exists(path)));
            ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                project.PackRootPath
            );
            CollectionAssert.AreEquivalent(
                before,
                reopened.Documents.Select(document => document.Address)
            );
            Assert.IsEmpty(reopened.Definition.FactionPaths);
            Assert.IsTrue(project.Redo());
            ContentPackEditorStorage.Save(project);
            reopened = ContentPackEditorStorage.OpenFolder(contentRoot, project.PackRootPath);
            Assert.IsTrue(reopened.GetFactionOptions().ContainsKey(id));
            Assert.AreEqual(
                "<Units />",
                File.ReadAllText(
                    Path.Combine(contentRoot, "Packs", "BasePack", "Data", "units.xml")
                )
            );
        }

        [Test]
        public async Task AddFaction_UndoSavedCreation_SnapshotExcludesRemovedFilesAsync()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            string[] before = project.Documents.Select(document => document.Address).ToArray();
            project.AddFaction();
            ContentPackEditorStorage.Save(project);
            project.Undo();
            string snapshot = Path.Combine(Directory.GetParent(contentRoot).FullName, "Snapshot");
            Directory.CreateDirectory(snapshot);
            await ContentPackEditorExporter.WriteSnapshotAsync(
                project,
                snapshot,
                CancellationToken.None
            );
            Assert.IsFalse(
                Directory
                    .EnumerateFiles(snapshot, "*.xml", SearchOption.AllDirectories)
                    .Any(path => Path.GetRelativePath(snapshot, path).StartsWith("Factions"))
            );
            ContentPackEditorProject copy = ContentPackEditorStorage.SaveAs(
                project,
                Path.Combine(Directory.GetParent(contentRoot).FullName, "Copy"),
                "copy",
                "Copy",
                "1.0.0"
            );
            CollectionAssert.AreEquivalent(
                before,
                copy.Documents.Select(document => document.Address)
            );
        }

        [Test]
        public void AddFaction_ExternalChangeAfterSave_RejectsRemovalWithoutDeletingFiles()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            string path = project.AddFaction().Catalog.Document.TargetPath;
            ContentPackEditorStorage.Save(project);
            File.AppendAllText(path, "\n<!-- External edit -->");
            project.Undo();
            Assert.Throws<IOException>(() => ContentPackEditorStorage.Save(project));
            StringAssert.Contains("External edit", File.ReadAllText(path));
        }

        [Test]
        public void AddFaction_LoadablePack_ProductionLoaderReadsFactionWithoutChangingScenario()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            string[] playable = project
                .Documents.Where(document => document.Text.Contains("<Scenario>"))
                .Select(document => document.Text)
                .ToArray();
            int factions = project.GetFactionOptions().Count;
            ContentPackEditorRecord added = project.AddFaction();
            string id = added.ID;
            int authoredProcessing = int.Parse(
                added
                    .GetFields()
                    .Single(field => field.MemberName == "ResourceProcessingPointsPerFacility")
                    .Value
            );
            ContentPackEditorStorage.Save(project);
            ContentPack pack = ContentPackLoader.LoadPack(
                ContentPackLoader.CreateFolderResolver(
                    project.ContentRootPath,
                    project.PackRootPath,
                    project.Definition
                ),
                project.Definition,
                null
            );
            Assert.AreEqual(factions + 1, pack.GameData.Factions.Length);
            Assert.IsTrue(pack.GameData.Factions.Any(faction => faction.InstanceID == id));
            Assert.AreEqual(
                authoredProcessing,
                pack.GameData.Factions.Single(faction =>
                    faction.InstanceID == id
                ).Settings.ResourceProcessingPointsPerFacility
            );
            Assert.IsFalse(pack.Scenario.PlayableFactionIDs.Contains(id));
            CollectionAssert.AreEqual(
                playable,
                project
                    .Documents.Where(document => document.Text.Contains("<Scenario>"))
                    .Select(document => document.Text)
            );
            Assert.IsNotNull(
                pack.GameData.FactionThemes.Single(theme =>
                    theme.FactionInstanceID == id
                ).PlanetOverlayTheme
            );
        }

        [Test]
        public void AddFaction_InstalledPack_RejectsCreation()
        {
            ContentPackEditorProject project = ContentPackEditorStorage.Open(
                contentRoot,
                "base-pack"
            );
            Assert.Throws<IOException>(() => project.AddFaction());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AddFactionMessage_ExistingOrMissingCategory_PreservesMessagesAndSupportsUndoRedo(
            bool existing
        )
        {
            WriteFixtureFile(
                "Data/units.xml",
                "<Factions><Faction><InstanceID>SIDE</InstanceID>"
                    + (
                        existing
                            ? "<Messages><Entry><Key>Advice</Key><Value><ArrayOfMessage><Message><Type>Advice</Type><Title>Existing advice</Title><Body>Keep me</Body></Message></ArrayOfMessage></Value></Entry></Messages>"
                            : ""
                    )
                    + "</Faction></Factions>"
            );
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .Single(item => item.Records.Any(record => record.Kind == "Faction"));
            string before = catalog.Document.Text;

            ContentPackEditorField title = project.AddFactionMessage(
                catalog.Records.Single(),
                MessageType.Advice
            );
            project.SetField(title, "My new advice");
            Assert.AreEqual(
                existing ? 2 : 1,
                catalog.DocumentRecord.Element.Descendants("Message").Count()
            );
            Assert.AreEqual("My new advice", title.Value);
            using (StringReader reader = new(catalog.Document.Text))
            {
                var factions = (Rebellion.Game.Factions.Faction[])
                    new Rebellion.Util.Serialization.GameSerializer(
                        typeof(Rebellion.Game.Factions.Faction[]),
                        new Rebellion.Util.Serialization.GameSerializerSettings
                        {
                            RootName = "Factions",
                        }
                    ).Deserialize(reader);
                Assert.AreEqual(
                    existing ? 2 : 1,
                    factions.Single().Messages[MessageType.Advice].Count
                );
            }
            Assert.IsTrue(project.Undo());
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(
                before,
                project
                    .GetCatalogs()
                    .Single(item => item.Document.Address == catalog.Document.Address)
                    .Document.Text
            );
            Assert.IsTrue(project.Redo());
            Assert.IsTrue(project.Redo());
            ContentPackEditorStorage.Save(project);
            ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                project.ContentRootPath,
                project.PackRootPath
            );
            Assert.IsTrue(
                reopened
                    .GetCatalogs()
                    .SelectMany(item => item.Records)
                    .SelectMany(record => record.Element.Descendants("Title"))
                    .Any(element => element.Value == "My new advice")
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AddSector_EmptyOrPopulatedGalaxy_CreatesIndependentDefinitionsAndPreservesHistory(
            bool populated
        )
        {
            WriteFixtureFile(
                "Data/units.xml",
                populated
                    ? "<PlanetSectors><PlanetSector><TypeID>SECTOR</TypeID><Planets><Planet><TypeID>SECTOR2</TypeID></Planet></Planets></PlanetSector></PlanetSectors>"
                    : "<PlanetSectors/>"
            );
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorCatalog map = project.GetCatalogs().Single(catalog => catalog.IsMap);
            string before = map.Document.Text;
            ContentPackEditorRecord first = project.AddSector(map, 150, 250);
            ContentPackEditorRecord second = project.AddSector(map, 200, 300);
            Assert.AreEqual("New sector", first.Name);
            Assert.AreEqual(150, first.X);
            Assert.AreEqual(250, first.Y);
            Assert.IsEmpty(first.Planets);
            Assert.AreNotEqual(first.ID, second.ID);
            string[] identities = map
                .Records.SelectMany(record => new[] { record }.Concat(record.Planets))
                .SelectMany(record => ContentPackEditorCatalog.GetIdentityValues(record.Element))
                .ToArray();
            Assert.AreEqual(identities.Length, identities.Distinct().Count());
            Assert.IsTrue(project.Undo());
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(
                before,
                project.GetCatalogs().Single(catalog => catalog.IsMap).Document.Text
            );
            Assert.IsTrue(project.Redo());
            Assert.IsTrue(project.Redo());
            ContentPackEditorStorage.Save(project);
            ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                project.ContentRootPath,
                project.PackRootPath
            );
            ContentPackEditorRecord saved = reopened
                .GetCatalogs()
                .Single(catalog => catalog.IsMap)
                .Records.Single(record => record.ID == first.ID);
            Assert.AreEqual(150, saved.X);
            Assert.AreEqual(250, saved.Y);
            Assert.IsEmpty(saved.Planets);
            using StringReader text = new(saved.Element.ToString());
            var sector = (Rebellion.Game.Galaxy.PlanetSector)
                new Rebellion.Util.Serialization.GameSerializer(
                    typeof(Rebellion.Game.Galaxy.PlanetSector)
                ).Deserialize(text);
            Assert.AreEqual(first.ID, sector.TypeID);
        }

        [Test]
        public void AddSector_NonMapCatalog_RejectsTheDestinationWithoutChangingTheProject()
        {
            WriteFixtureFile(
                "Data/units.xml",
                "<Officers><Officer><TypeID>OFFICER</TypeID></Officer></Officers>"
            );
            ContentPackEditorProject project = CreateWorkingProject();
            Assert.Throws<ArgumentException>(() =>
                project.AddSector(
                    project.GetCatalogs().Single(catalog => catalog.Name == "Officers"),
                    10,
                    20
                )
            );
            Assert.IsFalse(project.IsDirty);
            Assert.IsFalse(project.CanUndo);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void AddPlanet_EmptyOrPopulatedSector_CreatesUniqueBlankDefinitionsAndPreservesHistory(
            bool populated,
            bool hasCollection
        )
        {
            WriteFixtureFile(
                "Data/units.xml",
                "<PlanetSectors><PlanetSector><TypeID>SECTOR</TypeID>"
                    + "<PositionX>123</PositionX><PositionY>456</PositionY>"
                    + (hasCollection ? "<Planets>" : string.Empty)
                    + (
                        populated
                            ? "<Planet><TypeID>PLANET</TypeID><InstanceID>NEW_PLANET</InstanceID><DisplayName>Existing</DisplayName></Planet>"
                            : string.Empty
                    )
                    + (hasCollection ? "</Planets>" : string.Empty)
                    + "</PlanetSector></PlanetSectors>"
            );
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorCatalog map = project.GetCatalogs().Single(catalog => catalog.IsMap);
            string before = map.Document.Text;
            ContentPackEditorRecord first = project.AddPlanet(map.Records.Single(), 123, 456);
            ContentPackEditorRecord second = project.AddPlanet(map.Records.Single(), 123, 456);
            Assert.AreEqual("New planet", first.Name);
            Assert.AreEqual(123, first.X);
            Assert.AreEqual(456, first.Y);
            Assert.AreEqual(string.Empty, first.GetField("PlanetIconPath").Value);
            Assert.IsFalse(first.Element.Element("IsColonized").Value == "true");
            string[] ids = map
                .Records.SelectMany(record => record.Planets)
                .SelectMany(record => ContentPackEditorCatalog.GetIdentityValues(record.Element))
                .ToArray();
            Assert.AreEqual(ids.Length, ids.Distinct().Count());
            Assert.AreNotEqual(first.ID, second.ID);
            Assert.IsTrue(project.Undo());
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(
                before,
                project.GetCatalogs().Single(catalog => catalog.IsMap).Document.Text
            );
            Assert.IsTrue(project.Redo());
            Assert.IsTrue(project.Redo());
            ContentPackEditorStorage.Save(project);
            ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                project.ContentRootPath,
                project.PackRootPath
            );
            Assert.AreEqual(
                (populated ? 1 : 0) + 2,
                reopened
                    .GetCatalogs()
                    .Single(catalog => catalog.IsMap)
                    .Records.Single()
                    .Planets.Count
            );
        }

        [Test]
        public void AddPlanet_NonSectorRecord_RejectsTheDestinationWithoutChangingTheProject()
        {
            WriteFixtureFile(
                "Data/units.xml",
                "<PlanetSectors><PlanetSector><TypeID>SECTOR</TypeID><Planets><Planet><TypeID>PLANET</TypeID></Planet></Planets></PlanetSector></PlanetSectors>"
            );
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorRecord planet = project
                .GetCatalogs()
                .Single(catalog => catalog.IsMap)
                .Records.Single()
                .Planets.Single();
            Assert.Throws<ArgumentException>(() => project.AddPlanet(planet, 123, 456));
            Assert.IsFalse(project.IsDirty);
            Assert.IsFalse(project.CanUndo);
        }

        [Test]
        public void SetField_MissingImage_RejectsTheReferenceWithoutChangingTheDocument()
        {
            WriteFixtureFile(
                "Data/officers.xml",
                "<Officers><Officer><TypeID>one</TypeID><DisplayImagePath /></Officer></Officers>"
            );
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorField field = project
                .GetCatalogs()
                .Single(catalog => catalog.Name == "Officers")
                .Records[0]
                .GetField("DisplayImagePath");
            ArgumentException error = Assert.Throws<ArgumentException>(() =>
                project.SetField(field, "Pack/Images/missing")
            );
            Assert.AreEqual("File not found. Use Import.", error.Message);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void SetField_UnsupportedImageFile_RejectsTheReference()
        {
            WriteFixtureFile(
                "Data/officers.xml",
                "<Officers><Officer><TypeID>one</TypeID><DisplayImagePath /></Officer></Officers>"
            );
            WriteFixtureFile("Images/wrong.txt", "not an image");
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorField field = project
                .GetCatalogs()
                .Single(catalog => catalog.Name == "Officers")
                .Records[0]
                .GetField("DisplayImagePath");
            ArgumentException error = Assert.Throws<ArgumentException>(() =>
                project.SetField(field, "Pack/Images/wrong.txt")
            );
            Assert.AreEqual("Unsupported file type.", error.Message);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void SetField_UnknownRecruitingFaction_RejectsTheValue()
        {
            WriteFixtureFile(
                "Data/officers.xml",
                "<Officers><Officer><TypeID>one</TypeID><RecruitingFactionInstanceIDs /></Officer></Officers>"
            );
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorField field = project
                .GetCatalogs()
                .Single(catalog => catalog.Name == "Officers")
                .Records[0]
                .GetField("RecruitingFactionInstanceIDs");
            Assert.Throws<ArgumentException>(() =>
                project.SetFieldValues(field, new[] { "not-a-faction" })
            );
            Assert.IsFalse(project.IsDirty);
            Assert.IsEmpty(field.Values);
        }

        [Test]
        public void SetFieldValues_OfficerTraits_UsesInheritedDefinitionsAndPreservesHistory()
        {
            WriteFixtureFile(
                "Data/units.xml",
                "<OfficerTraits><Trait ID=\"fast-travel\"><PersonnelTravelTimePercent>70</PersonnelTravelTimePercent></Trait><Trait ID=\"leadership\" /></OfficerTraits>"
            );
            WriteFixtureFile(
                "Data/officers.xml",
                "<Officers><Officer><TypeID>one</TypeID><Traits /></Officer></Officers>"
            );
            ContentPackEditorProject project = CreateWorkingProject();
            CollectionAssert.AreEquivalent(
                new[] { "fast-travel", "leadership" },
                project.GetTraitOptions().Keys
            );
            ContentPackEditorCatalog officers = project
                .GetCatalogs()
                .Single(catalog => catalog.Name == "Officers");
            project.SetFieldValues(
                officers.Records.Single().GetField("Traits"),
                new[] { "fast-travel", "leadership" }
            );
            CollectionAssert.AreEqual(
                new[] { "fast-travel", "leadership" },
                officers.Records.Single().GetField("Traits").Values
            );
            Assert.IsTrue(project.Undo());
            Assert.IsEmpty(
                project
                    .GetCatalogs()
                    .Single(catalog => catalog.Name == "Officers")
                    .Records.Single()
                    .GetField("Traits")
                    .Values
            );
            Assert.IsTrue(project.Redo());
            StringAssert.Contains(
                "<TraitID>fast-travel</TraitID>",
                project.GetCatalogs().Single(catalog => catalog.Name == "Officers").Document.Text
            );
        }

        [Test]
        public void SetFieldValues_UnknownOfficerTrait_RejectsTheValueWithoutChangingTheDocument()
        {
            WriteFixtureFile(
                "Data/officers.xml",
                "<Officers><Officer><TypeID>one</TypeID><Traits /></Officer></Officers>"
            );
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorField field = project
                .GetCatalogs()
                .Single(catalog => catalog.Name == "Officers")
                .Records.Single()
                .GetField("Traits");
            Assert.Throws<ArgumentException>(() =>
                project.SetFieldValues(field, new[] { "not-a-trait" })
            );
            Assert.IsEmpty(field.Values);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void GetMediaFiles_ImageDirectory_UsesTheInheritedFiles()
        {
            WriteFixtureFile(
                "Data/theme.xml",
                "<FactionTheme><StrategyAdvisor><AnimationImageRoot>Pack/Frames</AnimationImageRoot></StrategyAdvisor></FactionTheme>"
            );
            WriteFixtureFile("Frames/idle/frame-001.png", "fixture");
            WriteFixtureFile("Frames/idle/frame-002.png", "fixture");
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorField field = project
                .GetCatalogs()
                .Single(catalog => catalog.Document.Address == "Pack/Data/theme.xml")
                .Records[0]
                .GetFields()
                .Single();
            Assert.IsTrue(field.IsMediaDirectory);
            CollectionAssert.AreEqual(
                new[] { "Pack/Frames/idle/frame-001.png", "Pack/Frames/idle/frame-002.png" },
                project.GetMediaFiles(field)
            );
            Assert.DoesNotThrow(() => project.ValidateField(field));
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void ImportAsset_RelativeAdvisorAudio_KeepsTheDeclaredRootAndClipName()
        {
            WriteFixtureFile(
                "Data/theme.xml",
                "<FactionTheme><StrategyAdvisor><AudioRoot>Pack/Advisor/Audio</AudioRoot><InvalidOrderRejected><AudioOptions><Path>existing</Path></AudioOptions></InvalidOrderRejected></StrategyAdvisor></FactionTheme>"
            );
            WriteFixtureFile("Advisor/Audio/existing.wav", "fixture");
            string source = Path.Combine(
                Directory.GetParent(contentRoot).FullName,
                "replacement.wav"
            );
            File.WriteAllText(source, "fixture");
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorField field = project
                .GetCatalogs()
                .Single(catalog => catalog.Document.Address == "Pack/Data/theme.xml")
                .Records[0]
                .GetFields()
                .Single(candidate => candidate.MemberName == "AudioOptions");
            string stored = ContentPackEditorStorage.ImportAsset(project, source, field);
            Assert.AreEqual("replacement.wav", stored);
            Assert.AreEqual("Pack/Advisor/Audio/replacement.wav", field.GetMediaAddress(stored));
            Assert.IsTrue(
                File.Exists(Path.Combine(project.PackRootPath, "Advisor", "Audio", stored))
            );
            Assert.DoesNotThrow(() => project.SetFieldValues(field, new[] { stored }));
        }

        [Test]
        public async Task ValidateAsync_UnknownFactionInSavedXml_RejectsThePackAsync()
        {
            WriteFixtureFile(
                "Data/officers.xml",
                "<Officers><Officer><TypeID>one</TypeID><RecruitingFactionInstanceIDs><string>missing</string></RecruitingFactionInstanceIDs></Officer></Officers>"
            );
            ContentPackEditorProject project = CreateWorkingProject();
            ArgumentException error = await ExpectFailureAsync<ArgumentException>(() =>
                ContentEditorPackValidator.ValidateAsync(project)
            );
            Assert.AreEqual("Choose a listed faction.", error.Message);
        }

        [Test]
        public void Create_DefaultLocation_UsesModsAndCanBeResolvedByTheGame()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            Assert.AreEqual(
                Path.Combine(Directory.GetParent(contentRoot).FullName, "Mods", "sample-pack"),
                project.PackRootPath
            );
            Assert.IsTrue(
                ContentPackLoader.DiscoverPacks(contentRoot).Any(pack => pack.ID == "sample-pack")
            );
            ContentFileResolver resolver = ContentPackLoader.CreatePackResolver(
                contentRoot,
                "sample-pack"
            );
            Assert.AreEqual(
                Path.Combine(contentRoot, "Packs", "BasePack", "Data", "units.xml"),
                resolver.ResolveFile("Pack/Data/units.xml")
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Save_ChangingTheBasePack_RefreshesInheritedDocumentsAndKeepsLocalOverrides(
            bool localOverride
        )
        {
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorDocument units = project.Documents.Single(document =>
                document.Address == "Pack/Data/units.xml"
            );
            if (localOverride)
                units.SetText("<Units><Unit ID=\"local\" /></Units>");
            ContentPackEditorStorage.Save(project);
            string nextRoot = Path.Combine(contentRoot, "Packs", "AnotherBase");
            Directory.CreateDirectory(Path.Combine(nextRoot, "Data"));
            ContentDefinitionSerializer.Serialize(
                Path.Combine(nextRoot, "pack.xml"),
                new ContentPackDefinition
                {
                    ID = "another-base",
                    Version = "1.0.0",
                    DisplayName = "Another base",
                }
            );
            File.WriteAllText(
                Path.Combine(nextRoot, "Data", "units.xml"),
                "<Units><Unit ID=\"base\" /></Units>"
            );
            File.WriteAllText(Path.Combine(nextRoot, "Data", "new.xml"), "<NewEntries />");
            ContentPackEditorDocument manifest = project.Documents.Single(document =>
                document.Address == "Pack/pack.xml"
            );
            project.SetDocumentText(
                manifest,
                manifest.Text.Replace(
                    "<BasePackID>base-pack</BasePackID>",
                    "<BasePackID>another-base</BasePackID>"
                )
            );

            ContentPackEditorStorage.Save(project);

            Assert.AreEqual("another-base", project.Definition.BasePackID);
            Assert.AreEqual(
                "<NewEntries />",
                project.Documents.Single(document => document.Address == "Pack/Data/new.xml").Text
            );
            ContentPackEditorDocument effective = project.Documents.Single(document =>
                document.Address == "Pack/Data/units.xml"
            );
            Assert.AreEqual(
                localOverride
                    ? "<Units><Unit ID=\"local\" /></Units>"
                    : "<Units><Unit ID=\"base\" /></Units>",
                effective.Text
            );
            Assert.AreEqual(!localOverride, effective.IsInherited);
            Assert.AreEqual(
                "<Units />",
                File.ReadAllText(
                    Path.Combine(contentRoot, "Packs", "BasePack", "Data", "units.xml")
                )
            );
        }

        [Test]
        public void Save_ChangingToAMissingBase_DoesNotCommitTheManifest()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorDocument manifest = project.Documents.Single(document =>
                document.Address == "Pack/pack.xml"
            );
            string saved = File.ReadAllText(manifest.TargetPath);
            project.SetDocumentText(
                manifest,
                manifest.Text.Replace(
                    "<BasePackID>base-pack</BasePackID>",
                    "<BasePackID>missing</BasePackID>"
                )
            );
            Assert.Throws<InvalidDataException>(() => ContentPackEditorStorage.Save(project));
            Assert.AreEqual(saved, File.ReadAllText(manifest.TargetPath));
            Assert.AreEqual("base-pack", project.Definition.BasePackID);
        }

        [Test]
        public void CreateInFolder_ExistingEmptyFolder_CreatesThePack()
        {
            string folder = Path.Combine(contentRoot, "Working");
            Directory.CreateDirectory(folder);
            ContentPackEditorProject project = ContentPackEditorStorage.CreateInFolder(
                contentRoot,
                "base-pack",
                folder,
                "new-pack",
                "New pack",
                "1.0.0"
            );
            Assert.AreEqual("new-pack", project.Definition.ID);
            Assert.IsTrue(File.Exists(Path.Combine(folder, "pack.xml")));
        }

        [Test]
        public void CreateInFolder_NonemptyFolder_PreservesExistingFiles()
        {
            string folder = Path.Combine(contentRoot, "Working");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "existing.txt"), "Keep this");
            Assert.Throws<IOException>(() =>
                ContentPackEditorStorage.CreateInFolder(
                    contentRoot,
                    "base-pack",
                    folder,
                    "new-pack",
                    "New pack",
                    "1.0.0"
                )
            );
            Assert.AreEqual("Keep this", File.ReadAllText(Path.Combine(folder, "existing.txt")));
        }

        [Test]
        public void Create_ValidDerivedPack_InheritsManifestAndDocuments()
        {
            ContentPackEditorProject project = ContentPackEditorStorage.Create(
                contentRoot,
                "base-pack",
                "sample-pack",
                "Sample pack",
                "1.0.0"
            );

            Assert.AreEqual("sample-pack", project.Definition.ID);
            Assert.AreEqual("base-pack", project.Definition.BasePackID);
            Assert.AreEqual("Sample pack", project.Definition.DisplayName);
            ContentPackEditorDocument document = project.Documents.Single(candidate =>
                candidate.Address == "Pack/Data/units.xml"
            );
            Assert.IsTrue(document.IsInherited);
            Assert.AreEqual("<Units />", document.Text);
        }

        [Test]
        public void Save_InheritedDocument_CreatesLocalOverride()
        {
            ContentPackEditorProject project = ContentPackEditorStorage.Create(
                contentRoot,
                "base-pack",
                "sample-pack",
                "Sample pack",
                "1.0.0"
            );
            ContentPackEditorDocument document = project.Documents.Single(candidate =>
                candidate.Address == "Pack/Data/units.xml"
            );

            document.SetText("<Units><Unit /></Units>");
            ContentPackEditorStorage.Save(project);

            Assert.IsFalse(document.IsDirty);
            Assert.IsFalse(document.IsInherited);
            Assert.AreEqual("<Units><Unit /></Units>", File.ReadAllText(document.TargetPath));
            Assert.AreEqual(
                "<Units />",
                File.ReadAllText(
                    Path.Combine(contentRoot, "Packs", "BasePack", "Data", "units.xml")
                )
            );
        }

        [Test]
        public void Save_MalformedXml_DoesNotReplaceTarget()
        {
            ContentPackEditorProject project = ContentPackEditorStorage.Create(
                contentRoot,
                "base-pack",
                "sample-pack",
                "Sample pack",
                "1.0.0"
            );
            ContentPackEditorDocument document = project.Documents.Single(candidate =>
                candidate.Address == "Pack/pack.xml"
            );
            string original = File.ReadAllText(document.TargetPath);
            document.SetText("<ContentPack>");

            Assert.Throws<System.Xml.XmlException>(() => ContentPackEditorStorage.Save(project));
            Assert.AreEqual(original, File.ReadAllText(document.TargetPath));
        }

        [Test]
        public void Save_AnyMalformedDocument_PreventsAllWrites()
        {
            ContentPackEditorProject project = ContentPackEditorStorage.Create(
                contentRoot,
                "base-pack",
                "sample-pack",
                "Sample pack",
                "1.0.0"
            );
            ContentPackEditorDocument manifest = project.Documents.Single(candidate =>
                candidate.Address == "Pack/pack.xml"
            );
            ContentPackEditorDocument units = project.Documents.Single(candidate =>
                candidate.Address == "Pack/Data/units.xml"
            );
            string originalManifest = File.ReadAllText(manifest.TargetPath);
            manifest.SetText(manifest.Text.Replace("Sample pack", "Changed pack"));
            units.SetText("<Units>");

            Assert.Throws<System.Xml.XmlException>(() => ContentPackEditorStorage.Save(project));
            Assert.AreEqual(originalManifest, File.ReadAllText(manifest.TargetPath));
            Assert.IsFalse(File.Exists(units.TargetPath));
        }

        [Test]
        public void Create_InvalidIdentifier_DoesNotCreatePackDirectory()
        {
            Assert.Throws<ArgumentException>(() =>
                ContentPackEditorStorage.Create(
                    contentRoot,
                    "base-pack",
                    "Bad Pack",
                    "Bad pack",
                    "1.0.0"
                )
            );
            Assert.AreEqual(
                1,
                Directory.EnumerateDirectories(Path.Combine(contentRoot, "Packs")).Count()
            );
        }

        [TestCase("-")]
        [TestCase("--")]
        [TestCase("starts-")]
        [TestCase("-ends")]
        [TestCase("two--hyphens")]
        public void Create_StructurallyInvalidIdentifier_DoesNotCreatePackDirectory(string packID)
        {
            Assert.Throws<ArgumentException>(() =>
                ContentPackEditorStorage.Create(
                    contentRoot,
                    "base-pack",
                    packID,
                    "Bad pack",
                    "1.0.0"
                )
            );
            Assert.AreEqual(
                1,
                Directory.EnumerateDirectories(Path.Combine(contentRoot, "Packs")).Count()
            );
        }

        [Test]
        public void CreateInFolder_OutsideInstalledPacks_ResolvesInheritedDocumentsAndReopens()
        {
            string folder = Path.Combine(contentRoot, "Working", "MyPack");
            ContentPackEditorProject project = ContentPackEditorStorage.CreateInFolder(
                contentRoot,
                "base-pack",
                folder,
                "my-pack",
                "My pack",
                "1.0.0"
            );
            ContentPackEditorDocument units = project.Documents.Single(document =>
                document.Address == "Pack/Data/units.xml"
            );
            units.SetText("<Units><Unit ID='changed'/></Units>");
            ContentPackEditorStorage.Save(project);

            ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                folder
            );

            Assert.AreEqual(
                units.Text,
                reopened.Documents.Single(document => document.Address == units.Address).Text
            );
            Assert.AreEqual(
                "<Units />",
                File.ReadAllText(
                    Path.Combine(contentRoot, "Packs", "BasePack", "Data", "units.xml")
                )
            );
            Assert.IsFalse(Directory.Exists(Path.Combine(contentRoot, "Packs", "MyPack")));
        }

        [Test]
        public void Undo_CompletedFieldEdit_RestoresOriginalDocumentAndCleanState()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorDocument units = project.Documents.Single(document =>
                document.Address == "Pack/Data/units.xml"
            );
            string original = units.Text;
            units.SetText("<Units><Unit/></Units>");

            Assert.IsTrue(project.CanUndo);
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(original, units.Text);
            Assert.IsFalse(project.IsDirty);
            Assert.IsTrue(project.CanRedo);
            Assert.IsTrue(project.Redo());
            Assert.AreEqual("<Units><Unit/></Units>", units.Text);
            Assert.IsTrue(project.IsDirty);
        }

        [Test]
        public void BeginEdit_ContinuousChangesAcrossDocuments_UndoesAsOneOperation()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorDocument units = project.Documents.Single(document =>
                document.Address == "Pack/Data/units.xml"
            );
            ContentPackEditorDocument manifest = project.Documents.Single(document =>
                document.Address == "Pack/pack.xml"
            );
            string originalManifest = manifest.Text;
            using (project.BeginEdit("Move sector"))
            {
                units.SetText("<Units><Unit X='1'/></Units>");
                units.SetText("<Units><Unit X='2'/></Units>");
                manifest.SetText(manifest.Text.Replace("Sample pack", "Edited pack"));
            }

            Assert.AreEqual("Move sector", project.UndoDescription);
            project.Undo();
            Assert.IsFalse(project.CanUndo);
            Assert.AreEqual("<Units />", units.Text);
            Assert.AreEqual(originalManifest, manifest.Text);
            project.Redo();
            Assert.AreEqual("<Units><Unit X='2'/></Units>", units.Text);
            StringAssert.Contains("Edited pack", manifest.Text);
        }

        [Test]
        public void Undo_AfterSave_MarksRestoredValuesUnsaved()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorDocument units = project.Documents.Single(document =>
                document.Address == "Pack/Data/units.xml"
            );
            units.SetText("<Units><Unit/></Units>");
            ContentPackEditorStorage.Save(project);

            project.Undo();

            Assert.AreEqual("<Units />", units.Text);
            Assert.IsTrue(project.IsDirty);
            Assert.AreEqual("<Units><Unit/></Units>", File.ReadAllText(units.TargetPath));
        }

        [Test]
        public void SaveAs_UnsavedProject_PreservesSourceAndWritesIndependentCopy()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorDocument units = project.Documents.Single(document =>
                document.Address == "Pack/Data/units.xml"
            );
            units.SetText("<Units><Unit ID='edited'/></Units>");
            string destination = Path.Combine(contentRoot, "Copies", "Copy");

            ContentPackEditorProject copy = ContentPackEditorStorage.SaveAs(
                project,
                destination,
                "copy-pack",
                "Copied pack",
                "2.0.0"
            );

            Assert.AreEqual("copy-pack", copy.Definition.ID);
            Assert.AreEqual("base-pack", copy.Definition.BasePackID);
            Assert.IsFalse(copy.IsDirty);
            Assert.IsTrue(project.IsDirty);
            Assert.AreEqual(
                units.Text,
                copy.Documents.Single(document => document.Address == units.Address).Text
            );
            Assert.IsFalse(File.Exists(units.TargetPath));
            Assert.AreEqual(
                "sample-pack",
                ContentPackEditorStorage.OpenFolder(contentRoot, project.PackRootPath).Definition.ID
            );
        }

        [Test]
        [Timeout(600000)]
        public async Task ExportAsync_UnsavedEditsAndLocalMedia_IncludesOverridesWithoutCopyingBaseContentAsync()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorRecord officer = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First();
            ContentPackEditorDocument units = officer.Catalog.Document;
            project.SetField(officer.GetField("DisplayName"), "Edited officer");
            string source = Path.Combine(contentRoot, "sound.wav");
            byte[] media = { 1, 2, 3, 4, 5 };
            File.WriteAllBytes(source, media);
            ContentPackEditorStorage.ImportAsset(project, source);
            string archivePath = Path.Combine(contentRoot, "export.zip");

            int checks = 0;
            await ContentPackEditorExporter.WriteArchiveAsync(
                project,
                archivePath,
                message =>
                {
                    if (++checks % 100 == 0)
                        TestContext.Progress.WriteLine($"Validation check {checks}: {message}");
                }
            );

            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            Assert.IsNotNull(archive.GetEntry("pack.xml"));
            Assert.AreEqual(3, archive.Entries.Count);
            using StreamReader text = new StreamReader(archive.GetEntry(units.Address[5..]).Open());
            Assert.AreEqual(units.Text, text.ReadToEnd());
            using Stream asset = archive.GetEntry("Assets/sound.wav").Open();
            using MemoryStream buffer = new MemoryStream();
            asset.CopyTo(buffer);
            CollectionAssert.AreEqual(media, buffer.ToArray());
            Assert.IsTrue(project.IsDirty);
        }

        [Test]
        public async Task ExportAsync_UnsavedStartupFailure_PreservesTheExistingArchiveAndWorkingProjectAsync()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorDocument generation = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Starting setup")
                .Document;
            System.Xml.Linq.XDocument xml = System.Xml.Linq.XDocument.Parse(generation.Text);
            xml.Descendants("OfficerInstanceID").First().Value = "missing-export-officer";
            project.SetDocumentText(generation, xml.ToString());
            string output = Path.Combine(contentRoot, "existing.zip");
            File.WriteAllText(output, "existing archive");

            InvalidDataException error = await ExpectFailureAsync<InvalidDataException>(() =>
                ContentPackEditorExporter.WriteArchiveAsync(project, output)
            );

            StringAssert.Contains("missing-export-officer", error.Message);
            Assert.AreEqual("existing archive", File.ReadAllText(output));
            Assert.IsFalse(File.Exists(generation.TargetPath));
            Assert.IsTrue(project.IsDirty);
        }

        [Test]
        public async Task ExportAsync_CorruptReferencedImage_PreservesTheArchiveAndUnsavedProjectAsync()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            string badFile = Path.Combine(contentRoot, "corrupt.png");
            File.WriteAllText(badFile, "Not a PNG file.");
            string address = ContentPackEditorStorage.ImportAsset(project, badFile);
            ContentPackEditorRecord officer = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First();
            project.SetField(officer.GetField("DisplayImagePath"), address);
            string output = Path.Combine(contentRoot, "existing.zip");
            File.WriteAllText(output, "existing archive");

            InvalidDataException failure = await ExpectFailureAsync<InvalidDataException>(() =>
                ContentPackEditorExporter.WriteArchiveAsync(project, output)
            );

            StringAssert.Contains(address, failure.Message);
            Assert.AreEqual("existing archive", File.ReadAllText(output));
            Assert.IsTrue(project.IsDirty);
            Assert.IsFalse(File.Exists(officer.Catalog.Document.TargetPath));
        }

        [Test]
        public async Task ValidateAsync_CancelledBeforeStartup_ReturnsControlAndPreservesTheProjectAsync()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            Task pending = ContentEditorPackValidator.ValidateAsync(
                project,
                cancellationToken: cancellation.Token
            );
            Assert.IsFalse(pending.IsCompleted);

            cancellation.Cancel();
            try
            {
                await pending;
                Assert.Fail("Validation must observe cancellation.");
            }
            catch (OperationCanceledException) { }

            Assert.IsFalse(project.IsDirty);
            Assert.AreEqual(
                1,
                Directory
                    .EnumerateFiles(project.PackRootPath, "*", SearchOption.AllDirectories)
                    .Count()
            );
        }

        [Test]
        public async Task ExportAsync_CancelledAfterStarting_PreservesExistingOutputAndUnsavedEditsAsync()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorRecord officer = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First();
            project.SetField(officer.GetField("DisplayName"), "Unsaved officer");
            string output = Path.Combine(contentRoot, "existing.zip");
            File.WriteAllText(output, "existing archive");
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            Task pending = ContentPackEditorExporter.WriteArchiveAsync(
                project,
                output,
                cancellationToken: cancellation.Token
            );
            Assert.IsFalse(pending.IsCompleted);

            cancellation.Cancel();
            try
            {
                await pending;
                Assert.Fail("Export must observe cancellation.");
            }
            catch (OperationCanceledException) { }

            Assert.AreEqual("existing archive", File.ReadAllText(output));
            Assert.AreEqual("Unsaved officer", officer.Name);
            Assert.IsTrue(project.IsDirty);
            Assert.IsFalse(File.Exists(officer.Catalog.Document.TargetPath));
            Assert.IsFalse(Directory.EnumerateFiles(contentRoot, "existing.zip.*.tmp").Any());
        }

        [Test]
        public async Task ExportAsync_MalformedWorkingDocument_PreservesExistingArchiveAsync()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            project
                .Documents.Single(document => document.Address == "Pack/Data/units.xml")
                .SetText("<Units>");
            string output = Path.Combine(contentRoot, "export.zip");
            File.WriteAllText(output, "existing archive");

            await ExpectFailureAsync<System.Xml.XmlException>(() =>
                ContentPackEditorExporter.WriteArchiveAsync(project, output)
            );

            Assert.AreEqual("existing archive", File.ReadAllText(output));
        }

        [Test]
        public void SaveAs_DestinationInsideSource_RejectsRecursiveCopy()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            string output = Path.Combine(project.PackRootPath, "Nested");

            Assert.Throws<IOException>(() =>
                ContentPackEditorStorage.SaveAs(project, output, "copy", "Copy", "1.0.0")
            );

            Assert.IsFalse(Directory.Exists(output));
        }

        [Test]
        public void SetField_DefaultRules_CreatesReversibleLocalOverrideWithoutChangingApplication()
        {
            string rulesPath = Path.Combine(contentRoot, "Application", "Rules", "game.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(rulesPath));
            string defaults =
                "<GameConfig><Movement><DistanceDivisor>5</DistanceDivisor></Movement></GameConfig>";
            File.WriteAllText(rulesPath, defaults);
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorCatalog rules = project
                .GetCatalogs()
                .Single(catalog => catalog.Name == "Rules");
            ContentPackEditorField field = rules
                .Records[0]
                .GetFields()
                .Single(candidate => candidate.MemberName == "DistanceDivisor");

            project.SetField(field, "9");

            StringAssert.Contains(
                "Shared/Data/game-config.xml",
                project.Documents.Single(document => document.Address == "Pack/pack.xml").Text
            );
            Assert.AreEqual(defaults, File.ReadAllText(rulesPath));
            project.Undo();
            Assert.IsFalse(project.IsDirty);
            Assert.IsFalse(project.CanUndo);
            project.Redo();
            ContentPackEditorStorage.Save(project);
            Assert.AreEqual("Shared/Data/game-config.xml", project.Definition.GameConfigPath);
            StringAssert.Contains(
                ">9<",
                File.ReadAllText(
                    Path.Combine(project.PackRootPath, "Shared", "Data", "game-config.xml")
                )
            );
            Assert.AreEqual(defaults, File.ReadAllText(rulesPath));
        }

        [Test]
        public void Save_ExternallyCreatedOverride_PreservesExternalFileAndPendingEdits()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorDocument document = project.Documents.Single(candidate =>
                candidate.Address == "Pack/Data/units.xml"
            );
            document.SetText("<Units><Unit /></Units>");
            Directory.CreateDirectory(Path.GetDirectoryName(document.TargetPath));
            File.WriteAllText(document.TargetPath, "<Units><Other /></Units>");

            Assert.Throws<IOException>(() => ContentPackEditorStorage.Save(project));

            Assert.AreEqual("<Units><Other /></Units>", File.ReadAllText(document.TargetPath));
            Assert.IsTrue(document.IsDirty);
        }

        [Test]
        public void DuplicateRecord_IdentityUsedInAnotherCatalog_AllocatesAnUnusedIdentityAndSupportsUndo()
        {
            string data = Path.Combine(contentRoot, "Packs", "BasePack", "Data");
            File.WriteAllText(
                Path.Combine(data, "units.xml"),
                "<Officers><Officer><TypeID>UNIT</TypeID></Officer></Officers>"
            );
            File.WriteAllText(
                Path.Combine(data, "ships.xml"),
                "<CapitalShips><CapitalShip><TypeID>UNIT_COPY</TypeID></CapitalShip></CapitalShips>"
            );
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorRecord source = project
                .GetCatalogs()
                .Single(catalog => catalog.Name == "Officers")
                .Records[0];

            ContentPackEditorRecord copy = project.DuplicateRecord(source);

            Assert.AreNotEqual("UNIT", copy.ID);
            Assert.AreNotEqual("UNIT_COPY", copy.ID);
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(
                1,
                project.GetCatalogs().Single(catalog => catalog.Name == "Officers").Records.Count
            );
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void SetField_UnscopedArtworkPath_RejectsItBeforeChangingTheDocument()
        {
            File.WriteAllText(
                Path.Combine(contentRoot, "Packs", "BasePack", "Data", "units.xml"),
                "<Officers><Officer><TypeID>UNIT</TypeID><DisplayImagePath>Pack/Art/unit</DisplayImagePath></Officer></Officers>"
            );
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorField field = project
                .GetCatalogs()
                .Single(catalog => catalog.Name == "Officers")
                .Records[0]
                .GetField("DisplayImagePath");

            Assert.Throws<ArgumentException>(() => project.SetField(field, "unscoped/image.png"));
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void Save_ChangedInheritedSource_RefusesToWriteStaleOverride()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorDocument document = project.Documents.Single(candidate =>
                candidate.Address == "Pack/Data/units.xml"
            );
            document.SetText("<Units><Unit /></Units>");
            File.WriteAllText(document.SourcePath, "<Units><Updated /></Units>");

            Assert.Throws<IOException>(() => ContentPackEditorStorage.Save(project));

            Assert.IsFalse(File.Exists(document.TargetPath));
            Assert.IsTrue(document.IsDirty);
        }

        [Test]
        public void OpenFolder_InstalledBasePack_RejectsEditingWithoutChangingFiles()
        {
            string folder = Path.Combine(contentRoot, "Packs", "BasePack");
            string before = File.ReadAllText(Path.Combine(folder, "pack.xml"));
            Assert.Throws<IOException>(() =>
                ContentPackEditorStorage.OpenFolder(contentRoot, folder)
            );
            Assert.AreEqual(before, File.ReadAllText(Path.Combine(folder, "pack.xml")));
        }

        [Test]
        public void Save_InstalledReferenceProject_RejectsWritingBaseData()
        {
            ContentPackEditorProject reference = ContentPackEditorStorage.Open(
                contentRoot,
                "base-pack"
            );
            ContentPackEditorDocument document = reference.Documents.Single(item =>
                item.Address == "Pack/Data/units.xml"
            );
            document.SetText("<Units><Unit /></Units>");
            Assert.Throws<IOException>(() => ContentPackEditorStorage.Save(reference));
            Assert.AreEqual("<Units />", File.ReadAllText(document.SourcePath));
        }

        [Test]
        public void ImportAsset_InstalledReferenceProject_RejectsWritingAssets()
        {
            ContentPackEditorProject reference = ContentPackEditorStorage.Open(
                contentRoot,
                "base-pack"
            );
            Assert.Throws<IOException>(() =>
                ContentPackEditorStorage.ImportAsset(
                    reference,
                    Path.Combine(contentRoot, "file.png")
                )
            );
            Assert.IsFalse(Directory.Exists(Path.Combine(reference.PackRootPath, "Assets")));
        }

        [Test]
        public void CreateInFolder_InsideInstalledContent_RejectsCreation()
        {
            Assert.Throws<IOException>(() =>
                ContentPackEditorStorage.CreateInFolder(
                    contentRoot,
                    "base-pack",
                    Path.Combine(contentRoot, "Packs", "NewPack"),
                    "new-pack",
                    "New pack",
                    "1.0.0"
                )
            );
        }

        [TestCase("field")]
        [TestCase("duplicate")]
        [TestCase("remove")]
        public void Undo_StaleRecordHandle_RejectsMutationWithoutChangingRestoredText(
            string operation
        )
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorRecord officer = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First();
            ContentPackEditorField name = officer.GetField("DisplayName");
            string before = officer.Catalog.Document.Text;
            project.SetField(name, "Edited officer");
            Assert.IsTrue(project.Undo());
            Assert.Throws<ArgumentException>(() =>
            {
                if (operation == "field")
                    project.SetField(name, "Stale edit");
                else if (operation == "duplicate")
                    project.DuplicateRecord(officer);
                else
                    project.RemoveRecord(officer);
            });
            Assert.AreEqual(before, officer.Catalog.Document.Text);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void Move_UndoThenStaleSectorHandle_RejectsTheDiscardedCoordinates()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorRecord sector = project
                .GetCatalogs()
                .Single(catalog => catalog.IsMap)
                .Records.First();
            string before = sector.Catalog.Document.Text;
            project.Move(sector, sector.X + 3, sector.Y + 4);
            Assert.IsTrue(project.Undo());
            Assert.Throws<ArgumentException>(() => project.Move(sector, 100, 100));
            Assert.AreEqual(before, sector.Catalog.Document.Text);
        }

        [Test]
        public void ValidateField_UnchangedInvalidScalar_RejectsTheAuthoredTypeMismatch()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorRecord ship = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Capital ships")
                .Records.First();
            XDocument xml = XDocument.Parse(ship.Catalog.Document.Text);
            xml.Root.Elements().First().SetElementValue("Hyperdrive", "invalid");
            project.SetDocumentText(ship.Catalog.Document, xml.ToString());
            ship = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Capital ships")
                .Records.First();
            Assert.Throws<ArgumentException>(() =>
                project.ValidateField(ship.GetField("Hyperdrive"))
            );
        }

        /// <summary>
        /// Captures an expected failure through the asynchronous authoring API.
        /// </summary>
        /// <param name="operation">The operation expected to fail.</param>
        /// <typeparam name="TException">The expected failure type.</typeparam>
        /// <returns>The captured failure.</returns>
        private static async Task<TException> ExpectFailureAsync<TException>(Func<Task> operation)
            where TException : Exception
        {
            try
            {
                await operation();
            }
            catch (TException exception)
            {
                return exception;
            }
            Assert.Fail("The invalid authoring operation must fail.");
            return null;
        }

        /// <summary>
        /// Creates a derived pack without changing its inheritance source.
        /// </summary>
        /// <returns>The working project.</returns>
        private ContentPackEditorProject CreateLoadableProject()
        {
            string liveContentRoot = ContentPackLoader.ResolveContentRootPath();
            ContentPackDefinition source = ContentPackLoader.DiscoverPacks(liveContentRoot).First();
            return ContentPackEditorStorage.CreateInFolder(
                liveContentRoot,
                source.ID,
                Path.Combine(Directory.GetParent(contentRoot).FullName, "LiveProject"),
                "export-test-" + Guid.NewGuid().ToString("N"),
                "Export test",
                "1.0.0"
            );
        }

        /// <summary>
        /// Creates an inherited working project over the isolated fixture catalogs.
        /// </summary>
        /// <returns>The editable project.</returns>
        private ContentPackEditorProject CreateWorkingProject()
        {
            return ContentPackEditorStorage.Create(
                contentRoot,
                "base-pack",
                "sample-pack",
                "Sample pack",
                "1.0.0"
            );
        }

        /// <summary>
        /// Writes an isolated inherited fixture document.
        /// </summary>
        /// <param name="relative">The pack-relative filename.</param>
        /// <param name="text">The fixture contents.</param>
        private void WriteFixtureFile(string relative, string text)
        {
            string path = Path.Combine(contentRoot, "Packs", "BasePack", relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }

        /// <summary>
        /// Writes the minimal installed inheritance source for each test.
        /// </summary>
        private void WriteBasePack()
        {
            string packRoot = Path.Combine(contentRoot, "Packs", "BasePack");
            ContentDefinitionSerializer.Serialize(
                Path.Combine(packRoot, "pack.xml"),
                new ContentPackDefinition
                {
                    ID = "base-pack",
                    Version = "1.0.0",
                    DisplayName = "Base pack",
                    DefaultScenarioID = "standard",
                    MainMenuMusicPath = "Application/Audio/menu",
                    PlanetSectorsPath = "Data/units.xml",
                    BuildingsPath = "Data/units.xml",
                    GameEventsPath = "Data/units.xml",
                    MessageDefinitionsPath = "Data/units.xml",
                    EncyclopediaEntriesPath = "Data/units.xml",
                    NeutralThemePath = "Data/units.xml",
                }
            );
            File.WriteAllText(Path.Combine(packRoot, "Data", "units.xml"), "<Units />");
        }
    }
}
