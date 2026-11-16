using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Content.Modio;

namespace Rebellion.Tests.Content.Modio
{
    public sealed class ModioInstallerTests
    {
        private string _root;
        private ModioInstaller _installer;

        /// <summary>
        /// Creates a minimal base-pack installation for each package case.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "reb2-modio-" + Guid.NewGuid());
            Directory.CreateDirectory(Path.Combine(_root, "Content", "Packs", "Base"));
            File.WriteAllText(
                Path.Combine(_root, "Content", "Packs", "Base", "pack.xml"),
                "<ContentPack><ID>base</ID><Version>1</Version></ContentPack>"
            );
            _installer = new ModioInstaller(Path.Combine(_root, "Content"), 7);
        }

        /// <summary>
        /// Removes all isolated package and installation files.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        [Test]
        public async Task InstallAsync_OverlayPackage_UsesExistingModDiscoveryAsync()
        {
            string archive = Archive(("mod.xml", Definition()), ("Content/Pack/value.xml", "new"));

            await _installer.InstallAsync(Mod(), archive, CancellationToken.None);

            ModioInstallation record = _installer.GetInstalled(12);
            Assert.AreEqual("community", record.ContentID);
            Assert.AreEqual(44, record.FileID);
            ContentFileResolver resolver = ContentPackLoader.DiscoverModResolver(
                Path.Combine(_root, "Content"),
                "base",
                null
            );
            Assert.AreEqual("community", resolver.AvailableMods.Single().ID);
        }

        [Test]
        public async Task InstallAsync_WrappedContentPack_UsesExistingPackDiscoveryAsync()
        {
            string archive = Archive(
                (
                    "Wrapper/pack.xml",
                    "<ContentPack><ID>new-pack</ID><Version>1</Version></ContentPack>"
                )
            );

            await _installer.InstallAsync(Mod(), archive, CancellationToken.None);

            Assert.IsTrue(
                ContentPackLoader
                    .DiscoverPacks(Path.Combine(_root, "Content"))
                    .Any(pack => pack.ID == "new-pack")
            );
        }

        [Test]
        public async Task InstallAsync_UpdatedArchive_ReplacesPreviousFilesAsync()
        {
            await _installer.InstallAsync(
                Mod(),
                Archive(("mod.xml", Definition()), ("Content/old.xml", "old")),
                CancellationToken.None
            );
            ModioMod update = Mod();
            update.File.ID = 45;
            string archive = Archive(("mod.xml", Definition("2")), ("Content/new.xml", "new"));

            await _installer.InstallAsync(update, archive, CancellationToken.None);

            Assert.IsFalse(File.Exists(Path.Combine(InstalledDirectory(), "Content", "old.xml")));
            Assert.AreEqual(
                "new",
                File.ReadAllText(Path.Combine(InstalledDirectory(), "Content", "new.xml"))
            );
            Assert.AreEqual(45, _installer.GetInstalled(12).FileID);
        }

        [TestCase("relative")]
        [TestCase("absolute")]
        [TestCase("drive")]
        [TestCase("backslash")]
        public void InstallAsync_UnsafeArchivePath_RejectsPackage(string scenario)
        {
            string path = scenario switch
            {
                "absolute" => Path.Combine(_root, "escaped"),
                "drive" => "C:/escaped",
                "backslash" => "..\\escaped",
                _ => "../escaped",
            };
            string archive = Archive(("mod.xml", Definition()), (path, "unsafe"));

            Assert.ThrowsAsync<InvalidDataException>(() =>
                _installer.InstallAsync(Mod(), archive, CancellationToken.None)
            );

            Assert.IsFalse(Directory.Exists(InstalledDirectory()));
            Assert.IsFalse(File.Exists(Path.Combine(_root, "escaped")));
        }

        [Test]
        public void InstallAsync_ConflictingFileCase_RejectsPackage()
        {
            string archive = Archive(
                ("mod.xml", Definition()),
                ("Content/value.xml", "first"),
                ("Content/VALUE.xml", "second")
            );

            Assert.ThrowsAsync<InvalidDataException>(() =>
                _installer.InstallAsync(Mod(), archive, CancellationToken.None)
            );
        }

        [Test]
        public async Task InstallAsync_ArchiveSuppliesInstallationRecord_WritesCurrentRecordAsync()
        {
            string archive = Archive(
                ("mod.xml", Definition()),
                (".modio.json", "{\"mod_id\":999}"),
                ("Content/value.xml", "new")
            );

            await _installer.InstallAsync(Mod(), archive, CancellationToken.None);

            Assert.AreEqual(12, _installer.GetInstalled(12).ModID);
        }

        [Test]
        public void InstallAsync_UnmanagedDestination_PreservesLocalFiles()
        {
            Directory.CreateDirectory(InstalledDirectory());
            string local = Path.Combine(InstalledDirectory(), "keep.txt");
            File.WriteAllText(local, "local");
            string archive = Archive(("mod.xml", Definition()), ("Content/new.xml", "new"));

            Assert.ThrowsAsync<InvalidDataException>(() =>
                _installer.InstallAsync(Mod(), archive, CancellationToken.None)
            );

            Assert.AreEqual("local", File.ReadAllText(local));
        }

        [Test]
        public void InstallAsync_ExistingLocalIdentity_PreservesManualMod()
        {
            string local = Path.Combine(_root, "Mods", "LocalCopy");
            Directory.CreateDirectory(local);
            File.WriteAllText(Path.Combine(local, "mod.xml"), Definition());
            string archive = Archive(("mod.xml", Definition()), ("Content/new.xml", "new"));

            Assert.ThrowsAsync<InvalidDataException>(() =>
                _installer.InstallAsync(Mod(), archive, CancellationToken.None)
            );

            Assert.IsFalse(Directory.Exists(InstalledDirectory()));
            Assert.IsTrue(File.Exists(Path.Combine(local, "mod.xml")));
        }

        [Test]
        public async Task InstallAsync_IncompatibleUpdate_PreservesInstalledVersionAsync()
        {
            await _installer.InstallAsync(
                Mod(),
                Archive(("mod.xml", Definition()), ("Content/value.xml", "old")),
                CancellationToken.None
            );
            string archive = Archive(
                ("mod.xml", Definition("2", "2")),
                ("Content/value.xml", "new")
            );

            Assert.ThrowsAsync<InvalidDataException>(() =>
                _installer.InstallAsync(Mod(), archive, CancellationToken.None)
            );

            Assert.AreEqual(
                "old",
                File.ReadAllText(Path.Combine(InstalledDirectory(), "Content", "value.xml"))
            );
        }

        [Test]
        public void InstallAsync_CancelledOperation_DoesNotInstallPackage()
        {
            string archive = Archive(("mod.xml", Definition()), ("Content/value.xml", "new"));

            Assert.CatchAsync<OperationCanceledException>(() =>
                _installer.InstallAsync(Mod(), archive, new CancellationToken(true))
            );

            Assert.IsFalse(Directory.Exists(InstalledDirectory()));
        }

        [Test]
        public void GetInstalled_LauncherRecord_ReadsSharedArchiveIdentity()
        {
            Directory.CreateDirectory(InstalledDirectory());
            File.WriteAllText(
                Path.Combine(InstalledDirectory(), ".modio.json"),
                "{\"schema_version\":1,\"game_id\":7,\"mod_id\":12,\"file_id\":44,\"content_id\":\"community\",\"content_version\":\"1\",\"name\":\"Community\"}"
            );

            ModioInstallation record = _installer.GetInstalled(12);

            Assert.AreEqual(44, record.FileID);
            Assert.AreEqual("community", record.ContentID);
        }

        [Test]
        public void GetInstalled_DamagedRecord_ReturnsNoManagedInstallation()
        {
            Directory.CreateDirectory(InstalledDirectory());
            File.WriteAllText(Path.Combine(InstalledDirectory(), ".modio.json"), "invalid");

            Assert.IsNull(_installer.GetInstalled(12));
        }

        [Test]
        public void InstallAsync_BuiltInPackIdentity_PreservesBasePack()
        {
            string archive = Archive(
                ("pack.xml", "<ContentPack><ID>base</ID><Version>2</Version></ContentPack>")
            );

            Assert.ThrowsAsync<InvalidDataException>(() =>
                _installer.InstallAsync(Mod(), archive, CancellationToken.None)
            );

            Assert.IsFalse(Directory.Exists(InstalledDirectory()));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task InstallAsync_ChangedPackageIdentity_PreservesInstalledVersionAsync(
            bool changesType
        )
        {
            await _installer.InstallAsync(
                Mod(),
                Archive(("mod.xml", Definition()), ("Content/value.xml", "old")),
                CancellationToken.None
            );
            string archive = changesType
                ? Archive(
                    (
                        "pack.xml",
                        "<ContentPack><ID>community</ID><Version>2</Version></ContentPack>"
                    )
                )
                : Archive(
                    ("mod.xml", Definition("2").Replace("<ID>community</ID>", "<ID>renamed</ID>")),
                    ("Content/value.xml", "new")
                );

            Assert.ThrowsAsync<InvalidDataException>(() =>
                _installer.InstallAsync(Mod(), archive, CancellationToken.None)
            );

            Assert.AreEqual(
                "old",
                File.ReadAllText(Path.Combine(InstalledDirectory(), "Content", "value.xml"))
            );
            Assert.AreEqual("community", _installer.GetInstalled(12).ContentID);
        }

        [Test]
        public async Task InstallAsync_CircularPackUpdate_PreservesPreviousPackAsync()
        {
            await _installer.InstallAsync(
                Mod(),
                Archive(
                    (
                        "pack.xml",
                        "<ContentPack><ID>community</ID><Version>1</Version></ContentPack>"
                    )
                ),
                CancellationToken.None
            );
            string dependent = Path.Combine(_root, "Content", "Packs", "Dependent");
            Directory.CreateDirectory(dependent);
            File.WriteAllText(
                Path.Combine(dependent, "pack.xml"),
                "<ContentPack><ID>dependent</ID><Version>1</Version><BasePackID>community</BasePackID></ContentPack>"
            );
            string archive = Archive(
                (
                    "pack.xml",
                    "<ContentPack><ID>community</ID><Version>2</Version><BasePackID>dependent</BasePackID></ContentPack>"
                )
            );

            Assert.ThrowsAsync<InvalidDataException>(() =>
                _installer.InstallAsync(Mod(), archive, CancellationToken.None)
            );

            Assert.AreEqual("1", _installer.GetInstalled(12).ContentVersion);
        }

        [Test]
        public async Task InstallAsync_OverlayUsesPackIdentifier_KeepsNamespacesSeparateAsync()
        {
            string archive = Archive(
                ("mod.xml", Definition().Replace("<ID>community</ID>", "<ID>base</ID>")),
                ("Content/value.xml", "new")
            );

            await _installer.InstallAsync(Mod(), archive, CancellationToken.None);

            Assert.AreEqual("base", _installer.GetInstalled(12).ContentID);
        }

        /// <summary>
        /// Returns the cross-application managed directory for the fixture mod.
        /// </summary>
        /// <returns>The expected installation directory.</returns>
        private string InstalledDirectory() => Path.Combine(_root, "Mods", "modio-12");

        /// <summary>
        /// Creates published metadata for the fixture package.
        /// </summary>
        /// <returns>The fixture mod and archive identity.</returns>
        private static ModioMod Mod() =>
            new ModioMod
            {
                ID = 12,
                GameID = 7,
                Name = "Community",
                File = new ModioFile { ID = 44, ModID = 12 },
            };

        /// <summary>
        /// Creates an overlay definition targeting the fixture base pack.
        /// </summary>
        /// <param name="version">The overlay version.</param>
        /// <param name="baseVersion">The required base-pack version.</param>
        /// <returns>The fixture definition XML.</returns>
        private static string Definition(string version = "1", string baseVersion = "1") =>
            $"<ContentModDefinition><ID>community</ID><Version>{version}</Version><DisplayName>Community</DisplayName><BasePackID>base</BasePackID><BasePackVersion>{baseVersion}</BasePackVersion></ContentModDefinition>";

        /// <summary>
        /// Creates a ZIP with the requested entry names and contents.
        /// </summary>
        /// <param name="entries">The archive entry names and contents.</param>
        /// <returns>The isolated ZIP path.</returns>
        private string Archive(params (string Name, string Content)[] entries)
        {
            string path = Path.Combine(_root, Guid.NewGuid() + ".zip");
            using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach ((string name, string content) in entries)
            {
                using StreamWriter writer = new StreamWriter(archive.CreateEntry(name).Open());
                writer.Write(content);
            }
            return path;
        }
    }
}
