using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NUnit.Framework;
using Rebellion.Game.Messages;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentPackEditorProjectTests : ContentPackEditorTestBase
    {
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
            ContentPackEditorProject copy = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                snapshot
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
        public void Move_LaterPlacementWouldOverflow_LeavesEveryRecordAndHistoryUnchanged()
        {
            WriteFixtureFile(
                "Data/planets.xml",
                "<PlanetSectors><PlanetSector><TypeID>FIRST</TypeID><PositionX>0</PositionX><PositionY>0</PositionY><Planets/></PlanetSector><PlanetSector><TypeID>SECOND</TypeID><PositionX>0</PositionX><PositionY>0</PositionY><Planets><Planet><TypeID>PLANET</TypeID><PositionX>2147483647</PositionX><PositionY>0</PositionY></Planet></Planets></PlanetSector></PlanetSectors>"
            );
            var project = CreateWorkingProject();
            var catalog = project.GetCatalogs().Single(item => item.IsMap);
            string before = catalog.Document.Text;
            string first = catalog.Records[0].Element.ToString();

            Assert.Throws<ArgumentException>(() =>
                project.Move(new[] { (catalog.Records[0], 10, 10), (catalog.Records[1], 1, 1) })
            );

            Assert.AreEqual(before, catalog.Document.Text);
            Assert.AreEqual(first, catalog.Records[0].Element.ToString());
            Assert.IsFalse(project.CanUndo);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void Move_OverlappingParentAndChildPlacements_RejectsTheAmbiguousGroupWithoutChanges()
        {
            WriteFixtureFile(
                "Data/planets.xml",
                "<PlanetSectors><PlanetSector><TypeID>SECTOR</TypeID><PositionX>0</PositionX><PositionY>0</PositionY><Planets><Planet><TypeID>PLANET</TypeID><PositionX>1</PositionX><PositionY>0</PositionY></Planet></Planets></PlanetSector></PlanetSectors>"
            );
            var project = CreateWorkingProject();
            var catalog = project.GetCatalogs().Single(item => item.IsMap);
            var sector = catalog.Records.Single();
            string before = catalog.Document.Text;

            Assert.Throws<ArgumentException>(() =>
                project.Move(new[] { (sector.Planets.Single(), int.MaxValue, 0), (sector, 1, 0) })
            );

            Assert.AreEqual(before, catalog.Document.Text);
            Assert.IsFalse(project.IsDirty);
            Assert.IsFalse(project.CanUndo);
        }

        [Test]
        public void Move_MultiplePlacements_UndoesTheCompleteGroupOnce()
        {
            WriteFixtureFile(
                "Data/planets.xml",
                "<PlanetSectors><PlanetSector><TypeID>FIRST</TypeID><PositionX>0</PositionX><PositionY>0</PositionY><Planets/></PlanetSector><PlanetSector><TypeID>SECOND</TypeID><PositionX>0</PositionX><PositionY>0</PositionY><Planets/></PlanetSector></PlanetSectors>"
            );
            var project = CreateWorkingProject();
            var catalog = project.GetCatalogs().Single(item => item.IsMap);
            string before = catalog.Document.Text;

            project.Move(new[] { (catalog.Records[0], 10, 20), (catalog.Records[1], 30, 40) });

            Assert.AreEqual(10, catalog.Records[0].X);
            Assert.AreEqual(40, catalog.Records[1].Y);
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(before, catalog.Document.Text);
            Assert.IsFalse(project.CanUndo);
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
        public void SetField_DuplicatePlanetIdentity_RejectsTheEditWithoutChangingTheMap()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorRecord[] planets = project
                .GetCatalogs()
                .Where(catalog => catalog.IsMap)
                .SelectMany(catalog => catalog.Records)
                .SelectMany(record => record.Planets)
                .Take(2)
                .ToArray();
            string before = planets[0].Catalog.Document.Text;

            Assert.Throws<ArgumentException>(() =>
                project.SetField(planets[0].GetField("TypeID"), planets[1].ID)
            );

            Assert.AreEqual(before, planets[0].Catalog.Document.Text);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void GetReferenceOptions_GenerationMine_ShowsBuildingNamesWithoutChangingTheDocument()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Where(record => record.Kind == "GameGenerationConfig")
                .SelectMany(record => record.GetFields())
                .Single(candidate => candidate.MemberName == "MineTypeID");
            string before = field.Document.Text;

            var options = project.GetReferenceOptions(field);

            Assert.IsTrue(options.ContainsKey(field.Value));
            Assert.AreEqual("None", options[string.Empty]);
            Assert.Greater(options.Count, 1);
            Assert.AreEqual(before, field.Document.Text);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void GetReferenceOptions_OrderedFacilityList_ProvidesNamedChoicesWithoutChangingQuantities()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .SelectMany(record => record.GetFields())
                .First(candidate => candidate.MemberName == "FacilityTypeIDs");

            string[] before = field.Values.ToArray();
            Assert.IsNotNull(project.GetReferenceOptions(field));
            CollectionAssert.AreEqual(before, field.Values);
            Assert.IsFalse(project.IsDirty);
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
    }
}
