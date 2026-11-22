using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentPackEditorStorageTests : ContentPackEditorTestBase
    {
        [Test]
        [Category("RequiresMedia")]
        public void Save_HeadquartersFacilityList_PreservesOrderedQuantitiesWhenLoaded()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorDocument generation = project.Documents.Single(document =>
                document.Address.EndsWith("/generation.xml", StringComparison.Ordinal)
            );
            XDocument xml = XDocument.Parse(generation.Text);
            xml.Descendants("FacilityTypeIDs").First().RemoveNodes();
            project.SetDocumentText(generation, xml.ToString());
            ContentPackEditorField facilities = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Where(record => record.Kind == "GameGenerationConfig")
                .SelectMany(record => record.GetFields())
                .First(field => field.MemberName == "FacilityTypeIDs");
            string[] buildingIDs = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Where(record => record.Kind == "Building")
                .Select(record => record.ID)
                .Take(2)
                .ToArray();
            string[] expected = { buildingIDs[1], buildingIDs[0], buildingIDs[1] };

            project.SetFieldValues(facilities, expected);
            ContentPackEditorStorage.Save(project);
            ContentPack loaded = ContentPackLoader.OpenFolder(contentRoot, project.PackRootPath);

            CollectionAssert.AreEqual(
                expected,
                loaded.GameData.GenerationConfig.FacilityGeneration.HQLoadouts[0].FacilityTypeIDs
            );
        }

        [Test]
        public void ImportAsset_RelativeAdvisorAudio_KeepsTheDeclaredRootAndClipName()
        {
            WriteFixtureFile(
                "Data/theme.xml",
                "<FactionTheme><StrategyAdvisor><AudioRoot>Pack/Advisor/Audio</AudioRoot><InvalidOrderRejected><AudioOptions><Path>existing</Path></AudioOptions></InvalidOrderRejected></StrategyAdvisor></FactionTheme>"
            );
            string manifestPath = Path.Combine(contentRoot, "Packs", "BasePack", "pack.xml");
            ContentPackDefinition definition =
                ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(manifestPath);
            definition.NeutralThemePath = "Data/theme.xml";
            ContentDefinitionSerializer.Serialize(manifestPath, definition);
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

        [TestCase("Pack/Assets/current.png")]
        [TestCase("Pack/Assets/current")]
        [Category("RequiresMedia")]
        public void ReplaceAsset_PackOwnedImage_OverwritesFileAndPreservesAddress(string address)
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Starfighter")
                .GetField("DisplayImagePath");
            string target = Path.Combine(project.PackRootPath, "Assets", "current.png");
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, "old image");
            project.SetField(field, address);
            string source = Path.Combine(Directory.GetParent(contentRoot).FullName, "new.png");
            File.WriteAllText(source, "new image");

            Assert.IsTrue(ContentPackEditorStorage.CanReplaceAsset(project, field));
            ContentPackEditorStorage.ReplaceAsset(project, source, field);

            Assert.AreEqual("new image", File.ReadAllText(target));
            Assert.AreEqual(address, field.Value);
        }

        [Test]
        [Category("RequiresMedia")]
        public void ReadGeneratedLayers_ReplacedImage_IgnoresThePreviousRecipe()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Starfighter")
                .GetField("DamagedSmallImagePath");
            string address = ContentPackEditorStorage.WriteGeneratedPng(
                project,
                field,
                new byte[] { 1, 2, 3 },
                preservePrevious: true,
                layerRecipe: "{}"
            );
            project.SetField(field, address);
            string source = Path.Combine(Directory.GetParent(contentRoot).FullName, "new.png");
            File.WriteAllBytes(source, new byte[] { 4, 5, 6 });

            ContentPackEditorStorage.ReplaceAsset(project, source, field);

            Assert.IsNull(ContentPackEditorStorage.ReadGeneratedLayers(project, field));
        }

        [Test]
        [Category("RequiresMedia")]
        public void ReplaceAsset_DifferentFileType_RejectsWithoutChangingCurrentFile()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Starfighter")
                .GetField("DisplayImagePath");
            string target = Path.Combine(project.PackRootPath, "Assets", "current.png");
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, "old image");
            project.SetField(field, "Pack/Assets/current.png");
            string source = Path.Combine(Directory.GetParent(contentRoot).FullName, "new.jpg");
            File.WriteAllText(source, "new image");

            Assert.Throws<ArgumentException>(() =>
                ContentPackEditorStorage.ReplaceAsset(project, source, field)
            );

            Assert.AreEqual("old image", File.ReadAllText(target));
        }

        [Test]
        [Category("RequiresMedia")]
        public void WriteGeneratedPng_UnitOutput_UsesStablePackPathAndOverwritesIt()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Starfighter")
                .GetField("DisplayImagePath");

            string first = ContentPackEditorStorage.WriteGeneratedPng(
                project,
                field,
                new byte[] { 1, 2, 3 }
            );
            string second = ContentPackEditorStorage.WriteGeneratedPng(
                project,
                field,
                new byte[] { 4, 5 }
            );

            Assert.AreEqual(first, second);
            StringAssert.StartsWith(
                "Pack/Assets/Generated/starfighter/" + field.Record.ID.ToLowerInvariant() + "/",
                first
            );
            StringAssert.EndsWith("/display.png", first);
            CollectionAssert.AreEqual(
                new byte[] { 4, 5 },
                File.ReadAllBytes(Path.Combine(project.PackRootPath, first[5..]))
            );
        }

        [Test]
        [Category("RequiresMedia")]
        public void WriteGeneratedPng_LayerRecipe_ReopensWithTheSelectedArtworkVersion()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Starfighter")
                .GetField("DamagedSmallImagePath");
            const string firstRecipe = "{\"Version\":1,\"Seed\":8}";
            const string nextRecipe = "{\"Version\":1,\"Seed\":9}";
            byte[] image = { 1, 2, 3 };
            string first = ContentPackEditorStorage.WriteGeneratedPng(
                project,
                field,
                image,
                preservePrevious: true,
                layerRecipe: firstRecipe
            );
            string next = ContentPackEditorStorage.WriteGeneratedPng(
                project,
                field,
                image,
                preservePrevious: true,
                layerRecipe: nextRecipe
            );
            Assert.AreNotEqual(first, next);

            project.SetField(field, first);
            ContentPackEditorStorage.Save(project);
            ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                project.ContentRootPath,
                project.PackRootPath
            );
            ContentPackEditorField saved = reopened
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record => record.Kind == "Starfighter" && record.ID == field.Record.ID)
                .GetField("DamagedSmallImagePath");
            Assert.AreEqual(
                firstRecipe,
                ContentPackEditorStorage.ReadGeneratedLayers(reopened, saved)
            );
        }

        [Test]
        [Category("RequiresMedia")]
        public void WriteGeneratedPng_PreservedVariations_KeepsPreviousBytesAtTheirAddress()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Starfighter")
                .GetField("DamagedSmallImagePath");
            byte[] firstImage = { 1, 2, 3 };
            byte[] nextImage = { 4, 5 };

            string first = ContentPackEditorStorage.WriteGeneratedPng(
                project,
                field,
                firstImage,
                preservePrevious: true
            );
            string second = ContentPackEditorStorage.WriteGeneratedPng(
                project,
                field,
                nextImage,
                preservePrevious: true
            );

            Assert.AreNotEqual(first, second);
            CollectionAssert.AreEqual(
                firstImage,
                File.ReadAllBytes(Path.Combine(project.PackRootPath, first[5..]))
            );
            Assert.AreEqual(
                first,
                ContentPackEditorStorage.WriteGeneratedPng(
                    project,
                    field,
                    firstImage,
                    preservePrevious: true
                )
            );
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

        [Test]
        public void OpenFolder_UnreferencedSettingOverride_IgnoresAndPreservesTheFile()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            string unrelatedPath = Path.Combine(
                project.PackRootPath,
                "KelsFiles",
                "Data",
                "renamed.xml"
            );
            Directory.CreateDirectory(Path.GetDirectoryName(unrelatedPath));
            const string unrelated = "<Buildings Inherit=\"true\"><Building /></Buildings>";
            File.WriteAllText(unrelatedPath, unrelated);

            ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                project.PackRootPath
            );
            ContentPackEditorStorage.Save(reopened);

            Assert.IsFalse(
                reopened.Documents.Any(document =>
                    string.Equals(
                        document.TargetPath,
                        unrelatedPath,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            );
            Assert.AreEqual(unrelated, File.ReadAllText(unrelatedPath));
        }

        [Test]
        public void Create_ReferencedDefinitions_OpensTheirNestedDocuments()
        {
            string packRoot = Path.Combine(contentRoot, "Packs", "BasePack");
            ContentPackDefinition definition =
                ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(
                    Path.Combine(packRoot, "pack.xml")
                );
            definition.FactionPaths.Add("Definitions/faction.xml");
            definition.ScenarioPaths.Add("Definitions/scenario.xml");
            definition.Preloads.Add(
                new ContentPreloadDefinition { ID = "strategy", Path = "Definitions/preload.xml" }
            );
            ContentDefinitionSerializer.Serialize(Path.Combine(packRoot, "pack.xml"), definition);
            Directory.CreateDirectory(Path.Combine(packRoot, "Definitions"));
            ContentDefinitionSerializer.Serialize(
                Path.Combine(packRoot, "Definitions", "faction.xml"),
                new ContentFactionDefinition
                {
                    ID = "faction",
                    FactionDataPath = "Nested/faction-data.xml",
                    CapitalShipsPath = "Nested/capital-ships.xml",
                    StarfightersPath = "Nested/starfighters.xml",
                    RegimentsPath = "Nested/regiments.xml",
                    SpecialForcesPath = "Nested/special-forces.xml",
                    OfficersPath = "Nested/officers.xml",
                    EncyclopediaEntriesPath = "Nested/encyclopedia.xml",
                    ThemePath = "Nested/theme.xml",
                }
            );
            ContentDefinitionSerializer.Serialize(
                Path.Combine(packRoot, "Definitions", "scenario.xml"),
                new ContentScenarioDefinition
                {
                    ID = "standard",
                    GenerationConfigPath = "Nested/generation.xml",
                }
            );
            ContentDefinitionSerializer.Serialize(
                Path.Combine(packRoot, "Definitions", "preload.xml"),
                new ContentPreloadManifest()
            );
            string[] nestedPaths =
            {
                "faction-data.xml",
                "capital-ships.xml",
                "starfighters.xml",
                "regiments.xml",
                "special-forces.xml",
                "officers.xml",
                "encyclopedia.xml",
                "theme.xml",
                "generation.xml",
            };
            foreach (string path in nestedPaths)
                WriteFixtureFile("Nested/" + path, "<Fixture />");

            ContentPackEditorProject project = CreateWorkingProject();

            CollectionAssert.IsSubsetOf(
                new[]
                {
                    "Pack/Definitions/faction.xml",
                    "Pack/Definitions/scenario.xml",
                    "Pack/Definitions/preload.xml",
                    "Pack/Nested/faction-data.xml",
                    "Pack/Nested/capital-ships.xml",
                    "Pack/Nested/starfighters.xml",
                    "Pack/Nested/regiments.xml",
                    "Pack/Nested/special-forces.xml",
                    "Pack/Nested/officers.xml",
                    "Pack/Nested/encyclopedia.xml",
                    "Pack/Nested/theme.xml",
                    "Pack/Nested/generation.xml",
                },
                project.Documents.Select(document => document.Address)
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
                    PlanetSectorsPath = "Data/new.xml",
                    BuildingsPath = "Data/units.xml",
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
                manifest
                    .Text.Replace(
                        "<BasePackID>base-pack</BasePackID>",
                        "<BasePackID>another-base</BasePackID>"
                    )
                    .Replace(
                        "<PlanetSectorsPath>Data/units.xml</PlanetSectorsPath>",
                        "<PlanetSectorsPath>Data/new.xml</PlanetSectorsPath>"
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
                System
                    .Xml.Linq.XElement.Parse(
                        localOverride
                            ? "<Units><Unit ID=\"local\" /></Units>"
                            : "<Units><Unit ID=\"base\" /></Units>"
                    )
                    .ToString(),
                System.Xml.Linq.XElement.Parse(effective.Text).ToString()
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
            Assert.IsTrue(ContentXmlOverrides.IsOverlay(File.ReadAllText(document.TargetPath)));
            Assert.AreEqual(1, System.Xml.Linq.XElement.Parse(document.Text).Elements().Count());
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

            Assert.Throws<XmlException>(() => ContentPackEditorStorage.Save(project));
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

            Assert.Throws<XmlException>(() => ContentPackEditorStorage.Save(project));
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
                System.Xml.Linq.XElement.Parse(units.Text).ToString(),
                System
                    .Xml.Linq.XElement.Parse(
                        reopened
                            .Documents.Single(document => document.Address == units.Address)
                            .Text
                    )
                    .ToString()
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
    }
}
