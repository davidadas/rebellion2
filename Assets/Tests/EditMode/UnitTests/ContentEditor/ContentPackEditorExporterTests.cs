using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentPackEditorExporterTests : ContentPackEditorTestBase
    {
        [Test]
        public async Task WriteSnapshotAsync_UnsavedProject_PreservesSourceAndWritesIndependentCopyAsync()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorDocument units = project.Documents.Single(document =>
                document.Address == "Pack/Data/units.xml"
            );
            units.SetText("<Units><Unit ID='edited'/></Units>");
            string destination = Path.Combine(contentRoot, "Copies", "Copy");

            Directory.CreateDirectory(destination);
            await ContentPackEditorExporter.WriteSnapshotAsync(
                project,
                destination,
                CancellationToken.None
            );
            ContentPackEditorProject copy = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                destination
            );

            Assert.AreEqual("sample-pack", copy.Definition.ID);
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
        public async Task WriteArchiveAsync_UnsavedEditsAndLocalMedia_IncludesOverridesWithoutCopyingBaseContentAsync()
        {
            ContentPackEditorProject project = ContentEditorTestData.CreateLoadableProject(
                contentRoot
            );
            string[] baselineFiles = Directory
                .GetFiles(project.PackRootPath, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(project.PackRootPath, path).Replace('\\', '/'))
                .ToArray();
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
            CollectionAssert.AreEquivalent(
                baselineFiles.Concat(new[] { units.Address[5..], "Assets/sound.wav" }).Distinct(),
                archive.Entries.Select(entry => entry.FullName)
            );
            using StreamReader text = new StreamReader(archive.GetEntry(units.Address[5..]).Open());
            Assert.AreEqual(units.Text, text.ReadToEnd());
            using Stream asset = archive.GetEntry("Assets/sound.wav").Open();
            using MemoryStream buffer = new MemoryStream();
            asset.CopyTo(buffer);
            CollectionAssert.AreEqual(media, buffer.ToArray());
            Assert.IsTrue(project.IsDirty);
        }

        [Test]
        public async Task WriteArchiveAsync_UnsavedStartupFailure_PreservesTheExistingArchiveAndWorkingProjectAsync()
        {
            ContentPackEditorProject project = ContentEditorTestData.CreateLoadableProject(
                contentRoot
            );
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
        public async Task WriteArchiveAsync_CorruptReferencedImage_PreservesTheArchiveAndUnsavedProjectAsync()
        {
            ContentPackEditorProject project = ContentEditorTestData.CreateLoadableProject(
                contentRoot
            );
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
        public async Task WriteArchiveAsync_CancelledAfterStarting_PreservesExistingOutputAndUnsavedEditsAsync()
        {
            ContentPackEditorProject project = ContentEditorTestData.CreateLoadableProject(
                contentRoot
            );
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
        public async Task WriteArchiveAsync_MalformedWorkingDocument_PreservesExistingArchiveAsync()
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
        public void WriteArchiveAsync_DestinationInsideSource_RejectsRecursiveCopy()
        {
            ContentPackEditorProject project = CreateWorkingProject();
            string output = Path.Combine(project.PackRootPath, "Nested.zip");

            Assert.ThrowsAsync<IOException>(() =>
                ContentPackEditorExporter.WriteArchiveAsync(project, output)
            );

            Assert.IsFalse(File.Exists(output));
        }
    }
}
