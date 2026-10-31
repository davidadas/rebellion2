using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentPackEditorStorageTests : ContentPackEditorTestBase
    {
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
