using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.ContentEditor.Publishing
{
    [TestFixture]
    public sealed class ThunderstorePackageExporterTests : ContentPackEditorTestBase
    {
        [TestCase("Team-Dependency-1.2.3")]
        [TestCase("team-name-pack-name-1.2.3")]
        public async Task WriteArchiveAsync_PinnedDependencies_ExportsMetadataAndExcludesPrivateDraftsAsync(
            string dependency
        )
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            ContentEditorPublishingSettings settings = CreateSettings(project);
            settings.Dependencies = dependency;
            ContentEditorPublishingStorage.Save(project, settings);
            string editor = Path.Combine(
                project.PackRootPath,
                ContentEditorAssetChange.EditorDirectory
            );
            Directory.CreateDirectory(editor);
            File.WriteAllText(Path.Combine(editor, "private.json"), "private");
            string destination = Path.Combine(contentRoot, "release.zip");

            await ThunderstorePackageExporter.WriteArchiveAsync(project, settings, destination);

            using ZipArchive archive = ZipFile.OpenRead(destination);
            string[] paths = archive.Entries.Select(entry => entry.FullName).ToArray();
            CollectionAssert.Contains(paths, "Mods/" + project.Definition.ID + "/pack.xml");
            CollectionAssert.Contains(paths, "icon.png");
            Assert.That(
                paths.Any(path =>
                    path.Contains(".publishing")
                    || path.Contains(ContentEditorAssetChange.EditorDirectory)
                ),
                Is.False
            );
            using var manifestReader = new StreamReader(archive.GetEntry("manifest.json").Open());
            Manifest manifest = JsonUtility.FromJson<Manifest>(manifestReader.ReadToEnd());
            Assert.That(manifest.name, Is.EqualTo(settings.PackageName));
            Assert.That(manifest.version_number, Is.EqualTo(project.Definition.Version));
            Assert.That(manifest.description, Is.EqualTo(settings.Description));
            Assert.That(manifest.website_url, Is.EqualTo(settings.WebsiteUrl));
            using var changelog = new StreamReader(archive.GetEntry("CHANGELOG.md").Open());
            Assert.That(changelog.ReadToEnd(), Is.EqualTo(settings.Changelog));
            CollectionAssert.AreEqual(new[] { dependency }, manifest.dependencies);
            using var readme = new StreamReader(archive.GetEntry("README.md").Open());
            Assert.That(readme.ReadToEnd(), Is.EqualTo(settings.Readme));
        }

        [TestCase("team-name-pack-name-1.2.3-beta")]
        [TestCase("team-name-pack-name-01.2.3")]
        public async Task WriteArchiveAsync_InvalidDependencyVersion_RejectsArchiveAsync(
            string dependency
        )
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            ContentEditorPublishingSettings settings = CreateSettings(project);
            settings.Dependencies = dependency;
            string destination = Path.Combine(contentRoot, "release.zip");

            await ExpectFailureAsync<ArgumentException>(() =>
                ThunderstorePackageExporter.WriteArchiveAsync(project, settings, destination)
            );

            Assert.That(File.Exists(destination), Is.False);
        }

        [Test]
        public async Task WriteArchiveAsync_LocalImport_RetainsPackIdentityAndAuthoredVersionAsync()
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            string destination = Path.Combine(contentRoot, "release.zip");
            await ThunderstorePackageExporter.WriteArchiveAsync(
                project,
                CreateSettings(project),
                destination
            );
            var store = new ModPackageStore(
                Path.Combine(contentRoot, "installation", "Content"),
                new ThunderstoreClient()
            );

            await store.InstallLocalAsync(
                destination,
                Array.Empty<string>(),
                null,
                CancellationToken.None
            );

            InstalledModContent pack = store.GetInstalled().Single().Content.Single();
            Assert.That(pack.IsPack, Is.True);
            Assert.That(pack.ID, Is.EqualTo(project.Definition.ID));
            Assert.That(pack.Version, Is.EqualTo(project.Definition.Version));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task WriteArchiveAsync_ValidationFailsOrCanceled_PreservesDestinationAndUnsavedEditsAsync(
            bool canceled
        )
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            ContentEditorPublishingSettings settings = CreateSettings(project);
            ContentPackEditorRecord building = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record =>
                    record.Kind == "Building" && record.GetField("ProcessRate").Value != "0"
                );
            project.SetField(building.GetField("ProcessRate"), "0");
            string destination = Path.Combine(contentRoot, "release.zip");
            File.WriteAllText(destination, "existing release");
            if (!canceled)
                settings.PackageName = "";
            if (canceled)
                await ExpectFailureAsync<OperationCanceledException>(() =>
                    ThunderstorePackageExporter.WriteArchiveAsync(
                        project,
                        settings,
                        destination,
                        cancellationToken: new CancellationToken(true)
                    )
                );
            else
                await ExpectFailureAsync<ArgumentException>(() =>
                    ThunderstorePackageExporter.WriteArchiveAsync(project, settings, destination)
                );
            Assert.That(File.ReadAllText(destination), Is.EqualTo("existing release"));
            Assert.That(project.IsDirty, Is.True);
            Assert.That(building.GetField("ProcessRate").Value, Is.EqualTo("0"));
        }

        [Test]
        public async Task WriteArchiveAsync_StackableMod_UsesItsOwnReleaseIdentityAsync()
        {
            using ContentPackEditorProject baseline = CreateLoadableProject();
            using ContentPackEditorProject mod = ContentPackEditorStorage.CreateInFolder(
                contentRoot,
                baseline.BasePackID,
                Path.Combine(contentRoot, "authoring-mod"),
                "release-mod",
                "Release mod",
                "3.2.1",
                asMod: true
            );
            ContentEditorPublishingSettings settings = CreateSettings(mod);
            ContentEditorPublishingStorage.Save(mod, settings);
            string destination = Path.Combine(contentRoot, "mod-release.zip");

            await ThunderstorePackageExporter.WriteArchiveAsync(mod, settings, destination);

            using ZipArchive archive = ZipFile.OpenRead(destination);
            using Stream packageManifest = archive.GetEntry("Mods/release-mod/pack.xml").Open();
            Assert.That(
                System.Xml.Linq.XElement.Load(packageManifest).Attribute("Type")?.Value,
                Is.EqualTo("Mod")
            );
            Assert.That(
                archive.Entries.Any(entry => entry.FullName.Contains(".publishing")),
                Is.False
            );
            using var manifestReader = new StreamReader(archive.GetEntry("manifest.json").Open());
            Manifest manifest = JsonUtility.FromJson<Manifest>(manifestReader.ReadToEnd());
            Assert.That(manifest.version_number, Is.EqualTo("3.2.1"));
        }

        [Test]
        public async Task WriteArchiveAsync_NestedDestination_RejectsWritingInsideWorkingProjectAsync()
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            string destination = Path.Combine(project.PackRootPath, "release.zip");
            await ExpectFailureAsync<IOException>(() =>
                ThunderstorePackageExporter.WriteArchiveAsync(
                    project,
                    CreateSettings(project),
                    destination
                )
            );
            Assert.That(File.Exists(destination), Is.False);
        }

        /// <summary>
        /// Creates valid listing metadata with an independently owned icon.
        /// </summary>
        /// <param name="project">The authored project.</param>
        /// <returns>The listing used by export checks.</returns>
        private ContentEditorPublishingSettings CreateSettings(ContentPackEditorProject project)
        {
            Texture2D icon = new(256, 256, TextureFormat.RGBA32, false);
            string path = Path.Combine(contentRoot, "icon.png");
            try
            {
                File.WriteAllBytes(path, icon.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(icon);
            }
            return new ContentEditorPublishingSettings
            {
                PackageName = project.ID.Replace('-', '_'),
                Description = "Sample content",
                WebsiteUrl = "https://example.com",
                Changelog = "Release notes",
                Readme = "# Sample\n",
                IconPath = path,
            };
        }

        [Serializable]
        private sealed class Manifest
        {
            public string name;
            public string version_number;
            public string description;
            public string website_url;
            public string[] dependencies;
        }
    }
}
