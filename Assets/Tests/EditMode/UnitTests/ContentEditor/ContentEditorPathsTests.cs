using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentEditorPathsTests
    {
        private string directory;
        private string contentRoot;
        private string installed;
        private readonly List<string> links = new();

        /// <summary>
        /// Creates an isolated installation and working-pack parent for path checks.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(
                Path.GetTempPath(),
                "editor-paths-" + Guid.NewGuid().ToString("N")
            );
            contentRoot = Path.Combine(directory, "Content");
            installed = Path.Combine(contentRoot, "Packs", "Installed");
            Directory.CreateDirectory(Path.Combine(installed, "Data"));
            File.WriteAllText(
                Path.Combine(installed, "pack.xml"),
                "<ContentPack><ID>installed</ID><Version>1.0.0</Version><DisplayName>Installed</DisplayName></ContentPack>"
            );
            File.WriteAllText(Path.Combine(installed, "Data", "units.xml"), "<Units/>");
            Directory.CreateDirectory(Path.Combine(directory, "Working"));
        }

        /// <summary>
        /// Unlinks fixture aliases before removing their owned directories.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            foreach (string link in links.AsEnumerable().Reverse())
                Directory.Delete(link);
            links.Clear();
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        [Test]
        public void Resolve_NewDestinationUnderAnAlias_RetainsTheSuffixOnThePhysicalParent()
        {
            string physical = Path.Combine(directory, "Physical");
            Directory.CreateDirectory(physical);
            string alias = Link(Path.Combine(directory, "Alias"), physical);
            string target = Path.Combine(alias, "New", "pack.xml");

            Assert.AreEqual(
                Path.Combine(ContentEditorPaths.Resolve(physical), "New", "pack.xml"),
                ContentEditorPaths.Resolve(target)
            );
            Assert.IsFalse(Directory.Exists(Path.Combine(physical, "New")));
        }

        [Test]
        public void Contains_SiblingWithTheSamePrefix_DoesNotTreatItAsAChild()
        {
            Assert.IsFalse(
                ContentEditorPaths.Contains(
                    Path.Combine(directory, "Pack"),
                    Path.Combine(directory, "PackOther", "file.xml")
                )
            );
        }

        [Test]
        public void OpenFolder_AliasToInstalledPack_RejectsWithoutChangingInstalledFiles()
        {
            string alias = Link(Path.Combine(directory, "Working", "Alias"), installed);
            string before = File.ReadAllText(Path.Combine(installed, "Data", "units.xml"));

            Assert.Throws<IOException>(() =>
                ContentPackEditorStorage.OpenFolder(contentRoot, alias)
            );

            Assert.AreEqual(before, File.ReadAllText(Path.Combine(installed, "Data", "units.xml")));
        }

        [Test]
        public void CreateInFolder_AliasToInstalledParent_RejectsBeforeCreatingThePack()
        {
            string alias = Link(
                Path.Combine(directory, "Working", "Alias"),
                Path.Combine(contentRoot, "Packs")
            );
            string target = Path.Combine(alias, "NewPack");

            Assert.Throws<IOException>(() =>
                ContentPackEditorStorage.CreateInFolder(
                    contentRoot,
                    "installed",
                    target,
                    "new-pack",
                    "New pack",
                    "1.0.0"
                )
            );

            Assert.IsFalse(Directory.Exists(Path.Combine(contentRoot, "Packs", "NewPack")));
        }

        [Test]
        public void CreateInFolder_LinkedUserParent_RemainsWritableOutsideInstalledContent()
        {
            string physical = Path.Combine(directory, "Physical");
            Directory.CreateDirectory(physical);
            string alias = Link(Path.Combine(directory, "Working", "Alias"), physical);
            var project = ContentPackEditorStorage.CreateInFolder(
                contentRoot,
                "installed",
                Path.Combine(alias, "NewPack"),
                "new-pack",
                "New pack",
                "1.0.0"
            );
            var document = project.Documents.Single(item => item.Address == "Pack/Data/units.xml");
            project.SetDocumentText(document, "<Units><Unit/></Units>");

            ContentPackEditorStorage.Save(project);

            Assert.AreEqual(
                "<Units><Unit/></Units>",
                File.ReadAllText(Path.Combine(physical, "NewPack", "Data", "units.xml"))
            );
            Assert.AreEqual(
                "<Units/>",
                File.ReadAllText(Path.Combine(installed, "Data", "units.xml"))
            );
        }

        [Test]
        public void Save_LinkedOverrideDirectory_LeavesInstalledFilesAndPendingEditsUntouched()
        {
            var project = CreateProject();
            var document = project.Documents.Single(item => item.Address == "Pack/Data/units.xml");
            project.SetDocumentText(document, "<Units><Unit/></Units>");
            Link(Path.Combine(project.PackRootPath, "Data"), Path.Combine(installed, "Data"));

            Assert.Throws<IOException>(() => ContentPackEditorStorage.Save(project));

            Assert.AreEqual(
                "<Units/>",
                File.ReadAllText(Path.Combine(installed, "Data", "units.xml"))
            );
            Assert.IsTrue(project.IsDirty);
        }

        [Test]
        public void ImportAsset_LinkedAssetDirectory_RejectsCopyingOutsideTheWorkingPack()
        {
            var project = CreateProject();
            string source = Path.Combine(directory, "image.png");
            File.WriteAllText(source, "fixture image");
            Link(Path.Combine(project.PackRootPath, "Assets"), installed);

            Assert.Throws<IOException>(() => ContentPackEditorStorage.ImportAsset(project, source));

            Assert.IsFalse(File.Exists(Path.Combine(installed, "image.png")));
        }

        [Test]
        public void ImportAsset_InstalledContentInsideTheWorkingFolder_RemainsReadOnly()
        {
            string source = Path.Combine(directory, "image.png");
            File.WriteAllText(source, "fixture image");
            File.WriteAllText(
                Path.Combine(directory, "pack.xml"),
                "<ContentPack><ID>working</ID><Version>1.0.0</Version><DisplayName>Working</DisplayName></ContentPack>"
            );
            var project = ContentPackEditorStorage.OpenFolder(contentRoot, directory);
            Link(Path.Combine(project.PackRootPath, "Assets"), installed);

            Assert.Throws<IOException>(() => ContentPackEditorStorage.ImportAsset(project, source));

            Assert.IsFalse(File.Exists(Path.Combine(installed, "image.png")));
        }

        [Test]
        public void WriteArchiveAsync_AliasedNestedDestination_RejectsBeforeCreatingAnArchive()
        {
            var project = CreateProject();
            string alias = Link(Path.Combine(directory, "OutputAlias"), project.PackRootPath);
            string target = Path.Combine(alias, "export.zip");

            Assert.ThrowsAsync<IOException>(() =>
                ContentPackEditorExporter.WriteArchiveAsync(project, target)
            );

            Assert.IsFalse(File.Exists(Path.Combine(project.PackRootPath, "export.zip")));
        }

        [Test]
        public void VerifyWritableLocation_IndividuallyLinkedInstalledPack_ProtectsItsPhysicalFolder()
        {
            string physical = Path.Combine(directory, "PhysicalPack");
            Directory.CreateDirectory(physical);
            Link(Path.Combine(contentRoot, "Packs", "LinkedPack"), physical);

            Assert.Throws<IOException>(() =>
                ContentEditorPaths.VerifyWritableLocation(contentRoot, physical)
            );
        }

        /// <summary>
        /// Creates a derived working pack with no local data overrides.
        /// </summary>
        /// <returns>The editable project.</returns>
        private ContentPackEditorProject CreateProject() =>
            ContentPackEditorStorage.CreateInFolder(
                contentRoot,
                "installed",
                Path.Combine(directory, "Working", "Pack"),
                "working-pack",
                "Working pack",
                "1.0.0"
            );

        /// <summary>
        /// Creates a native directory alias owned by this fixture.
        /// </summary>
        /// <param name="path">The new alias path.</param>
        /// <param name="target">The fixture-owned target directory.</param>
        /// <returns>The alias path.</returns>
        private string Link(string path, string target)
        {
            bool windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            ProcessStartInfo info = new(windows ? "cmd.exe" : "ln")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            if (windows)
                info.Arguments = "/c mklink /J \"" + path + "\" \"" + target + "\"";
            else
            {
                info.ArgumentList.Add("-s");
                info.ArgumentList.Add(target);
                info.ArgumentList.Add(path);
            }
            using Process process = Process.Start(info);
            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.AreEqual(0, process.ExitCode, output);
            links.Add(path);
            return path;
        }
    }
}
