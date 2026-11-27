using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.Content.Packages
{
    [TestFixture]
    public sealed class ModPackageStoreTests
    {
        private string folder;
        private string contentRoot;
        private ModPackageStore store;

        /// <summary>
        /// Creates an isolated installation and cache.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(
                Path.GetTempPath(),
                "package-store-" + Guid.NewGuid().ToString("N")
            );
            contentRoot = Path.Combine(folder, "Content");
            Directory.CreateDirectory(contentRoot);
            store = new ModPackageStore(contentRoot, new ThunderstoreClient());
        }

        /// <summary>
        /// Removes only this fixture's installation and archives.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        [TestCase("<ContentPack />")]
        [TestCase(
            "<ContentPack xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:nil=\"true\" />"
        )]
        public void GetInstalled_UnreadableLocalManifest_KeepsOtherPackagesAvailable(string xml)
        {
            string root = ContentFileResolver.ResolveModsRootPath(contentRoot);
            string invalid = Path.Combine(root, "Broken");
            string valid = Path.Combine(root, "Valid");
            Directory.CreateDirectory(invalid);
            Directory.CreateDirectory(valid);
            File.WriteAllText(Path.Combine(invalid, "pack.xml"), xml);
            File.WriteAllText(
                Path.Combine(valid, "pack.xml"),
                "<ContentPack><ID>valid</ID><Version>1.0.0</Version><DisplayName>Valid</DisplayName></ContentPack>"
            );

            var installed = store.GetInstalled();

            Assert.That(installed.Count, Is.EqualTo(2));
            Assert.That(
                installed.Single(item => item.Name == "Broken").Description,
                Does.Contain("could not be read")
            );
            Assert.That(
                installed.Single(item => item.Name == "Valid").Content.Single().ID,
                Is.EqualTo("valid")
            );
        }

        [Test]
        public void GetCompatibilityIssue_ManifestBecomesNull_ReturnsIssue()
        {
            string root = ContentFileResolver.ResolveModsRootPath(contentRoot);
            string local = Path.Combine(root, "Edited");
            Directory.CreateDirectory(local);
            File.WriteAllText(
                Path.Combine(local, "pack.xml"),
                "<ContentPack xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:nil=\"true\" />"
            );
            var package = new InstalledModPackage
            {
                Content = new[]
                {
                    new InstalledModContent
                    {
                        Folder = "Edited",
                        ID = "edited",
                        Version = "1.0.0",
                        IsPack = true,
                    },
                },
            };

            Assert.That(
                store.GetCompatibilityIssue(package, System.Array.Empty<ContentPackDefinition>()),
                Is.Not.Empty
            );
        }

        [Test]
        public async Task RecoverInterruptedInstallations_UnchangedReceiptDuringDependencyRepair_RestoresPreviousFilesAsync()
        {
            await InstallDependencyPairAsync();
            string state = Path.GetDirectoryName(store.Catalog.CachePath);
            string registry = File.ReadAllText(Path.Combine(state, "installed.json"));
            string transaction = Path.Combine(state, "transactions", "interrupted-repair");
            string backup = new string('A', 64);
            Directory.CreateDirectory(Path.Combine(transaction, "previous"));
            Directory.Move(
                Path.GetDirectoryName(PayloadPath("Dependency")),
                Path.Combine(transaction, "previous", backup)
            );
            var journal = new RecoveryJournal
            {
                PreviousRegistry = registry,
                NextRegistry = registry,
                RetainPrevious = false,
                Changes = new[]
                {
                    new RecoveryChange
                    {
                        Folder = "Dependency",
                        Backup = backup,
                        HadPrevious = true,
                    },
                    new RecoveryChange
                    {
                        Folder = "Parent",
                        Backup = new string('B', 64),
                        HadPrevious = true,
                    },
                },
            };
            File.WriteAllText(
                Path.Combine(transaction, "pending.json"),
                JsonUtility.ToJson(journal)
            );

            store.RecoverInterruptedInstallations();

            Assert.That(
                File.ReadAllText(PayloadPath("Dependency")),
                Is.EqualTo("Original payload")
            );
            Assert.That(File.ReadAllText(PayloadPath("Parent")), Is.EqualTo("Original payload"));
            Assert.That(store.GetInstalled().Count, Is.EqualTo(2));
            Assert.That(Directory.Exists(transaction), Is.False);
        }

        [Test]
        public async Task InstallAsync_RepairLeavesPackageMetadataUnchanged_PublishesDistinctCommitIdentityAsync()
        {
            ThunderstorePackage package = await ImportAsync("Root");
            File.Delete(PayloadPath("Root"));
            await InstallAsync(package);
            string path = Path.Combine(
                Path.GetDirectoryName(store.Catalog.CachePath),
                "installed.json"
            );
            string previous = File.ReadAllText(path);
            File.Delete(PayloadPath("Root"));

            await InstallAsync(package);

            Assert.That(File.ReadAllText(PayloadPath("Root")), Is.EqualTo("Original payload"));
            Assert.That(
                File.ReadAllText(path),
                Is.Not.EqualTo(previous),
                "Recovery must distinguish a completed repair from its initial state."
            );
        }

        [Test]
        public async Task InstallAsync_DeepArtworkPath_RestoresArtworkFromCachedReleaseAsync()
        {
            const string name = "DeepArtwork";
            const string artwork = "Factions/Alliance/Missions/Abduction/encyclopedia.png";
            const string nativeFolder = "Content/Packs/DeepArtwork/";
            const string artworkData = "Fixture artwork";
            string archivePath = Path.Combine(folder, "deep-artwork.zip");
            using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                using (var manifest = new StreamWriter(archive.CreateEntry("manifest.json").Open()))
                    manifest.Write("{\"name\":\"DeepArtwork\",\"version_number\":\"1.0.0\"}");
                using (
                    var pack = new StreamWriter(
                        archive.CreateEntry(nativeFolder + "pack.xml").Open()
                    )
                )
                    pack.Write(
                        "<ContentPack><ID>DeepArtwork</ID><DisplayName>DeepArtwork</DisplayName><Version>1.0.0</Version></ContentPack>"
                    );
                using var image = new StreamWriter(
                    archive.CreateEntry(nativeFolder + artwork).Open()
                );
                image.Write(artworkData);
            }
            await store.InstallLocalAsync(
                archivePath,
                Array.Empty<string>(),
                null,
                CancellationToken.None
            );
            InstalledModPackage receipt = store.GetInstalled().Single();
            var package = new ThunderstorePackage
            {
                name = name,
                owner = "Fixture",
                full_name = receipt.PackageID,
                versions = new[]
                {
                    new ThunderstorePackageVersion
                    {
                        version_number = receipt.Version,
                        file_size = new FileInfo(archivePath).Length,
                        dependencies = Array.Empty<string>(),
                    },
                },
            };
            string installedImage = Path.Combine(
                ContentFileResolver.ResolveModsRootPath(contentRoot),
                name,
                artwork
            );
            File.Delete(installedImage);

            await InstallAsync(package);

            Assert.That(File.ReadAllText(installedImage), Is.EqualTo(artworkData));
        }

        [Test]
        public async Task InstallAsync_PartiallyMissingDependency_RestoresMissingFilesAsync()
        {
            var (dependency, parent) = await InstallDependencyPairAsync();
            File.Delete(PayloadPath("Dependency"));

            await InstallAsync(parent, dependency);

            Assert.AreEqual("Original payload", File.ReadAllText(PayloadPath("Dependency")));
            Assert.AreEqual(2, store.GetInstalled().Count);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task InstallAsync_LocallyEditedDependency_PreservesEditsAsync(bool missingFile)
        {
            var (dependency, parent) = await InstallDependencyPairAsync();
            File.WriteAllText(PayloadPath("Dependency"), "Local changes");
            if (missingFile)
                File.Delete(
                    Path.Combine(Path.GetDirectoryName(PayloadPath("Dependency")), "second.txt")
                );

            await ExpectFailureAsync<InvalidOperationException>(() =>
                InstallAsync(parent, dependency)
            );

            Assert.AreEqual("Local changes", File.ReadAllText(PayloadPath("Dependency")));
        }

        [Test]
        public async Task InstallAsync_AdditionalLocalFile_PreservesFileAndBlocksReplacementAsync()
        {
            var (dependency, parent) = await InstallDependencyPairAsync();
            string extra = Path.Combine(
                Path.GetDirectoryName(PayloadPath("Dependency")),
                "custom.txt"
            );
            File.WriteAllText(extra, "Keep this");

            await ExpectFailureAsync<InvalidOperationException>(() =>
                InstallAsync(parent, dependency)
            );

            Assert.AreEqual("Keep this", File.ReadAllText(extra));
        }

        [Test]
        public async Task InstallAsync_ProtectedIncompleteDependency_LeavesInstallationUntouchedAsync()
        {
            var (dependency, parent) = await InstallDependencyPairAsync();
            File.Delete(PayloadPath("Dependency"));

            await ExpectFailureAsync<InvalidOperationException>(() =>
                store.InstallAsync(
                    parent,
                    parent.Latest,
                    new[] { dependency, parent },
                    new[] { "Dependency" },
                    null,
                    CancellationToken.None
                )
            );

            Assert.IsFalse(File.Exists(PayloadPath("Dependency")));
            Assert.AreEqual("Original payload", File.ReadAllText(PayloadPath("Parent")));
        }

        [Test]
        public async Task InstallAsync_CorruptCacheWithMatchingSize_DiscardsCacheBeforeFreshDownloadAsync()
        {
            ThunderstorePackage package = await ImportAsync("Sample");
            string cache = Directory
                .GetFiles(
                    Path.GetDirectoryName(store.Catalog.CachePath),
                    "*.zip",
                    SearchOption.AllDirectories
                )
                .Single();
            byte[] corrupt = new byte[new FileInfo(cache).Length];
            File.WriteAllBytes(cache, corrupt);

            await ExpectFailureAsync<InvalidDataException>(() => InstallAsync(package));

            Assert.IsFalse(File.Exists(cache));
            Assert.AreEqual("Original payload", File.ReadAllText(PayloadPath("Sample")));
        }

        [Test]
        public async Task InstallAsync_CanceledBeforePreparation_RetainsCachedArchiveAsync()
        {
            ThunderstorePackage package = await ImportAsync("Sample");
            string cache = Directory
                .GetFiles(
                    Path.GetDirectoryName(store.Catalog.CachePath),
                    "*.zip",
                    SearchOption.AllDirectories
                )
                .Single();
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            await ExpectFailureAsync<OperationCanceledException>(() =>
                store.InstallAsync(
                    package,
                    package.Latest,
                    new[] { package },
                    Array.Empty<string>(),
                    null,
                    cancellation.Token
                )
            );

            Assert.IsTrue(File.Exists(cache));
        }

        [Test]
        public async Task GetRequiredPackages_TransitiveDependencies_ReturnsEveryRequiredPackageOnceAsync()
        {
            var (dependency, parent) = await InstallDependencyPairAsync();
            ThunderstorePackage root = await ImportAsync("Root");
            root.Latest.dependencies = new[]
            {
                parent.full_name + "-1.0.0",
                dependency.full_name + "-1.0.0",
            };
            await InstallAsync(root, dependency, parent);

            CollectionAssert.AreEqual(
                new[] { dependency.full_name, parent.full_name, root.full_name },
                store
                    .GetRequiredPackages(
                        store.GetInstalled().Single(item => item.PackageID == root.full_name)
                    )
                    .Select(item => item.PackageID)
            );
        }

        [Test]
        public void Uninstall_RequiredNativeMod_LeavesItInstalled()
        {
            string dependency = WriteLocalMod("colors", "1.0.0", "");
            WriteLocalMod(
                "layout",
                "2.0.0",
                "<Requires><Mod><ID>colors</ID><Version>1.0.0</Version></Mod></Requires>"
            );
            InstalledModPackage selected = store
                .GetInstalled()
                .Single(item => item.Name == "colors");

            var error = Assert.Throws<InvalidOperationException>(() =>
                store.Uninstall(selected, Array.Empty<string>())
            );

            StringAssert.Contains("layout requires colors", error.Message);
            Assert.IsTrue(File.Exists(Path.Combine(dependency, "pack.xml")));
        }

        [Test]
        public async Task Uninstall_IntactAddOn_KeepsItsExactReleaseAvailableForSavesAsync()
        {
            string source = Path.Combine(folder, "add-on-source");
            string payload = Path.Combine(source, "Content", "Pack");
            Directory.CreateDirectory(payload);
            File.WriteAllText(
                Path.Combine(source, "mod.xml"),
                "<ContentModDefinition><ID>retired</ID><DisplayName>Retired</DisplayName><Version>1.0.0</Version><BasePackID>base</BasePackID><BasePackVersion>1.0.0</BasePackVersion></ContentModDefinition>"
            );
            File.WriteAllText(Path.Combine(payload, "payload.txt"), "Retained artwork data");
            string archive = Path.Combine(folder, "retired.zip");
            ZipFile.CreateFromDirectory(source, archive);
            await store.InstallLocalAsync(
                archive,
                Array.Empty<string>(),
                null,
                CancellationToken.None
            );

            store.Uninstall(store.GetInstalled().Single(), Array.Empty<string>());

            ContentFileResolver resolver = ContentFileResolver.Discover(
                contentRoot,
                Path.Combine(contentRoot, "Packs", "base"),
                "base",
                "1.0.0",
                appliedModIDs: new[] { "retired" },
                appliedModVersions: new[] { "1.0.0" }
            );
            Assert.That(
                File.ReadAllText(resolver.ResolveFile("Pack/payload.txt")),
                Is.EqualTo("Retained artwork data")
            );
        }

        [Test]
        public async Task InstallLocalAsync_DeclaredExtractionSizeTooLarge_RejectsBeforeInstallingFilesAsync()
        {
            string archivePath = CreateArchive("Oversized");
            using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Update))
                for (int index = 0; index < 6; index++)
                    archive.CreateEntry("large-" + index + ".bin");
            byte[] bytes = File.ReadAllBytes(archivePath);
            for (int index = 0; index < bytes.Length - 46; index++)
            {
                if (BitConverter.ToUInt32(bytes, index) != 0x02014b50)
                    continue;
                int nameLength = BitConverter.ToUInt16(bytes, index + 28);
                string name = Encoding.UTF8.GetString(bytes, index + 46, nameLength);
                if (name.StartsWith("large-", StringComparison.Ordinal))
                    Array.Copy(
                        BitConverter.GetBytes(3u * 1024 * 1024 * 1024),
                        0,
                        bytes,
                        index + 24,
                        4
                    );
            }
            File.WriteAllBytes(archivePath, bytes);

            InvalidDataException failure = await ExpectFailureAsync<InvalidDataException>(() =>
                store.InstallLocalAsync(
                    archivePath,
                    Array.Empty<string>(),
                    null,
                    CancellationToken.None
                )
            );

            StringAssert.Contains("limit", failure.Message);
            Assert.IsEmpty(store.GetInstalled());
        }

        [Test]
        public async Task InstallLocalAsync_TooManyArchiveEntries_RejectsBeforeInstallingFilesAsync()
        {
            string archivePath = Path.Combine(folder, "many.zip");
            using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
                for (int index = 0; index < 100001; index++)
                    archive.CreateEntry("entry-" + index);

            InvalidDataException failure = await ExpectFailureAsync<InvalidDataException>(() =>
                store.InstallLocalAsync(
                    archivePath,
                    Array.Empty<string>(),
                    null,
                    CancellationToken.None
                )
            );

            StringAssert.Contains("limit", failure.Message);
            Assert.IsEmpty(store.GetInstalled());
        }

        [Test]
        public async Task RemoveConflictingInstallation_UnpackedCopy_AllowsInstallingNewVersionAsync()
        {
            string existing = WritePackCopy("SeparateFolder", "Duplicate", "0.9.0");
            File.WriteAllText(Path.Combine(existing, "artwork.txt"), "Existing artwork");
            string archive = CreateArchive("Duplicate");
            var conflict = await ExpectFailureAsync<ModPackageStore.InstallationConflict>(() =>
                store.InstallLocalAsync(
                    archive,
                    Array.Empty<string>(),
                    null,
                    CancellationToken.None
                )
            );

            store.RemoveConflictingInstallation(conflict, Array.Empty<string>());
            await store.InstallLocalAsync(
                archive,
                Array.Empty<string>(),
                null,
                CancellationToken.None
            );

            string recovered = Directory
                .EnumerateFiles(
                    Path.Combine(Path.GetDirectoryName(store.Catalog.CachePath), "removed"),
                    "artwork.txt",
                    SearchOption.AllDirectories
                )
                .Single();
            Assert.That(File.ReadAllText(recovered), Is.EqualTo("Existing artwork"));
            Assert.That(Directory.Exists(existing), Is.False);
            Assert.That(store.GetInstalled().Single().Version, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task InstallLocalAsync_DefaultPackConflict_DoesNotOfferRemovalAsync()
        {
            string existing = WritePackCopy("DefaultFolder", "Default", "0.9.0");
            File.WriteAllText(
                Path.Combine(contentRoot, "catalog.xml"),
                "<ContentCatalog><ActivePackID>Default</ActivePackID></ContentCatalog>"
            );
            string archive = CreateArchive("Default");

            var exception = await ExpectFailureAsync<InvalidOperationException>(() =>
                store.InstallLocalAsync(
                    archive,
                    Array.Empty<string>(),
                    null,
                    CancellationToken.None
                )
            );

            Assert.That(exception, Is.Not.InstanceOf<ModPackageStore.InstallationConflict>());
            Assert.That(Directory.Exists(existing), Is.True);
        }

        [Test]
        public async Task RemoveConflictingInstallation_CopyIsNowInUse_LeavesCopyInstalledAsync()
        {
            string existing = WritePackCopy("SeparateFolder", "Duplicate", "0.9.0");
            string archive = CreateArchive("Duplicate");
            var conflict = await ExpectFailureAsync<ModPackageStore.InstallationConflict>(() =>
                store.InstallLocalAsync(
                    archive,
                    Array.Empty<string>(),
                    null,
                    CancellationToken.None
                )
            );

            Assert.Throws<InvalidOperationException>(() =>
                store.RemoveConflictingInstallation(conflict, new[] { "Duplicate" })
            );
            Assert.That(Directory.Exists(existing), Is.True);
        }

        [Test]
        public async Task InstallLocalAsync_PackCopyHasDependent_DoesNotOfferRemovalAsync()
        {
            string existing = WritePackCopy("SeparateFolder", "Duplicate", "0.9.0");
            WritePackCopy("ExpansionFolder", "Expansion", "1.0.0", "Duplicate");
            string archive = CreateArchive("Duplicate");

            var exception = await ExpectFailureAsync<InvalidOperationException>(() =>
                store.InstallLocalAsync(
                    archive,
                    Array.Empty<string>(),
                    null,
                    CancellationToken.None
                )
            );

            Assert.That(exception.Message, Does.Contain("requires"));
            Assert.That(exception, Is.Not.InstanceOf<ModPackageStore.InstallationConflict>());
            Assert.That(Directory.Exists(existing), Is.True);
        }

        /// <summary>
        /// Awaits an expected failure without blocking Unity's continuation queue.
        /// </summary>
        /// <typeparam name="T">The expected exception type.</typeparam>
        /// <param name="action">The asynchronous operation.</param>
        /// <returns>The expected exception.</returns>
        private string WriteLocalMod(string id, string version, string rules)
        {
            string root = Path.Combine(ContentFileResolver.ResolveModsRootPath(contentRoot), id);
            Directory.CreateDirectory(root);
            File.WriteAllText(
                Path.Combine(root, "pack.xml"),
                $"""<ContentPackage Type="Mod"><ID>{id}</ID><DisplayName>{id}</DisplayName><Version>{version}</Version><BasePackID>base</BasePackID><BasePackVersion>1.0.0</BasePackVersion>{rules}</ContentPackage>"""
            );
            return root;
        }

        private static async Task<T> ExpectFailureAsync<T>(Func<Task> action)
            where T : Exception
        {
            try
            {
                await action();
            }
            catch (T exception)
            {
                return exception;
            }
            Assert.Fail("Expected " + typeof(T).Name);
            return null;
        }

        private string WritePackCopy(
            string name,
            string id,
            string version,
            string basePackID = null
        )
        {
            string catalog = Path.Combine(contentRoot, "catalog.xml");
            if (!File.Exists(catalog))
                File.WriteAllText(
                    catalog,
                    "<ContentCatalog><ActivePackID>Default</ActivePackID></ContentCatalog>"
                );
            string existing = Path.Combine(contentRoot, "Packs", name);
            Directory.CreateDirectory(existing);
            ContentDefinitionSerializer.Serialize(
                Path.Combine(existing, "pack.xml"),
                new ContentPackDefinition
                {
                    ID = id,
                    DisplayName = id,
                    Version = version,
                    BasePackID = basePackID,
                }
            );
            return existing;
        }

        /// <summary>
        /// Installs two cached packages and records the parent's exact dependency.
        /// </summary>
        /// <returns>The installed dependency and its parent listing.</returns>
        private async Task<(
            ThunderstorePackage Dependency,
            ThunderstorePackage Parent
        )> InstallDependencyPairAsync()
        {
            ThunderstorePackage dependency = await ImportAsync("Dependency");
            ThunderstorePackage parent = await ImportAsync("Parent");
            parent.Latest.dependencies = new[] { dependency.full_name + "-1.0.0" };
            await InstallAsync(parent, dependency);
            return (dependency, parent);
        }

        /// <summary>
        /// Imports a package through the public path to create both its receipt and cached ZIP.
        /// </summary>
        /// <param name="name">The fixture's unique content identity.</param>
        /// <returns>The listing corresponding to that cached package.</returns>
        private async Task<ThunderstorePackage> ImportAsync(string name)
        {
            string archive = CreateArchive(name);
            await store.InstallLocalAsync(
                archive,
                Array.Empty<string>(),
                null,
                CancellationToken.None
            );
            InstalledModPackage receipt = store.GetInstalled().Single(item => item.Name == name);
            return new ThunderstorePackage
            {
                name = name,
                owner = "Fixture",
                full_name = receipt.PackageID,
                versions = new[]
                {
                    new ThunderstorePackageVersion
                    {
                        version_number = "1.0.0",
                        file_size = new FileInfo(archive).Length,
                        download_url = "http://not-a-download.invalid/fixture.zip",
                        dependencies = Array.Empty<string>(),
                    },
                },
            };
        }

        /// <summary>
        /// Builds a small native pack with two independently repairable content files.
        /// </summary>
        /// <param name="name">The content identity and archive name.</param>
        /// <returns>The fixture-owned archive path.</returns>
        private string CreateArchive(string name)
        {
            string source = Path.Combine(folder, name + "-source");
            Directory.CreateDirectory(source);
            ContentDefinitionSerializer.Serialize(
                Path.Combine(source, "pack.xml"),
                new ContentPackDefinition
                {
                    ID = name,
                    DisplayName = name,
                    Version = "1.0.0",
                }
            );
            File.WriteAllText(
                Path.Combine(source, "manifest.json"),
                "{\"name\":\"" + name + "\",\"version_number\":\"1.0.0\"}"
            );
            File.WriteAllText(Path.Combine(source, "payload.txt"), "Original payload");
            File.WriteAllText(Path.Combine(source, "second.txt"), "Second file");
            string archive = Path.Combine(folder, name + ".zip");
            ZipFile.CreateFromDirectory(source, archive);
            return archive;
        }

        /// <summary>
        /// Installs the selected fixture release without using a remote service.
        /// </summary>
        /// <param name="package">The cached package to install.</param>
        /// <param name="dependencies">The other cached dependency listings.</param>
        /// <returns>The installation completion.</returns>
        private Task InstallAsync(
            ThunderstorePackage package,
            params ThunderstorePackage[] dependencies
        ) =>
            store.InstallAsync(
                package,
                package.Latest,
                dependencies.Append(package).ToArray(),
                Array.Empty<string>(),
                null,
                CancellationToken.None
            );

        /// <summary>
        /// Locates the installed data file through the shared content path convention.
        /// </summary>
        /// <param name="name">The content folder identity.</param>
        /// <returns>The installed payload path.</returns>
        private string PayloadPath(string name) =>
            Path.Combine(ContentFileResolver.ResolveModsRootPath(contentRoot), name, "payload.txt");

        [Serializable]
        private sealed class RecoveryJournal
        {
            public string PreviousRegistry;
            public string NextRegistry;
            public RecoveryChange[] Changes;
            public bool RetainPrevious;
        }

        [Serializable]
        private sealed class RecoveryChange
        {
            public string Folder;
            public string Backup;
            public bool HadPrevious;
        }
    }
}
