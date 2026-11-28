using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class SaveContentResolverTests
    {
        private string _directory;
        private ContentPack _fixturePack;

        /// <summary>
        /// Installs the pack used for exact save-content matching.
        /// </summary>
        [OneTimeSetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            _fixturePack = TestPackFixture.Install(Path.Combine(_directory, "Content"));
        }

        /// <summary>
        /// Removes the temporary content installation.
        /// </summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public void Open_MatchingLaunchContent_RetainsActivePack()
        {
            ContentPack activePack = _fixturePack;
            GameSummary summary = GameLaunchContext.CreateDefaultSummary(activePack);

            Assert.AreSame(activePack, SaveContentResolver.Open(activePack, summary));
        }

        [Test]
        public void Open_UnavailableLaunchVersion_RejectsContent()
        {
            ContentPack activePack = _fixturePack;
            GameSummary summary = GameLaunchContext.CreateDefaultSummary(activePack);
            summary.PackVersion = "unavailable-version";

            Assert.Throws<InvalidOperationException>(() =>
                SaveContentResolver.Open(activePack, summary)
            );
        }

        [Test]
        public void Resolve_MatchingInstalledContent_ReturnsExactSelection()
        {
            ContentPack activePack = _fixturePack;
            GameMetadata metadata = CreateMetadata(activePack);

            SaveContentSelection selection = SaveContentResolver.Resolve(activePack, metadata);

            Assert.IsTrue(selection.Pack.MatchesContentIdentity(metadata));
            CollectionAssert.AreEquivalent(
                activePack
                    .FileResolver.AvailableMods.Where(mod =>
                        !activePack.FileResolver.Mods.Any(active => active.ID == mod.ID)
                    )
                    .Select(mod => mod.ID),
                selection.DisabledModIDs
            );
        }

        [Test]
        public void Resolve_SavedOrderDiffersFromFolders_PreservesWinningAsset()
        {
            GameMetadata metadata = CreateMetadata(_fixturePack);
            metadata.ModIDs = new[] { "later", "earlier" };
            metadata.ModVersions = new[] { "1.0.0", "1.0.0" };
            string mods = Path.Combine(_directory, "Mods");
            try
            {
                WriteMod(mods, "earlier");
                WriteMod(mods, "later");
                WriteMod(mods, "newly-installed");

                SaveContentSelection selection = SaveContentResolver.Resolve(
                    _fixturePack,
                    metadata
                );

                Assert.That(selection.Pack.MatchesContentIdentity(metadata), Is.True);
                Assert.That(
                    File.ReadAllText(selection.Pack.FileResolver.ResolveFile("Pack/overlap.txt")),
                    Is.EqualTo("earlier")
                );
            }
            finally
            {
                Directory.Delete(mods, true);
            }
        }

        [Test]
        public void Resolve_DuplicateIDWithDifferentVersions_LoadsTheSavedVersion()
        {
            string mods = Path.Combine(_directory, "Mods");
            GameMetadata metadata = CreateMetadata(_fixturePack);
            metadata.ModIDs = new[] { "rebalance" };
            metadata.ModVersions = new[] { "2.0.0" };
            try
            {
                WriteMod(mods, "rebalance", folderName: "first-copy");
                WriteMod(mods, "rebalance", "2.0.0", folderName: "second-copy");

                ContentPack selected = SaveContentResolver.Resolve(_fixturePack, metadata).Pack;

                Assert.That(
                    File.ReadAllText(selected.FileResolver.ResolveFile("Pack/overlap.txt")),
                    Is.EqualTo("2.0.0")
                );
            }
            finally
            {
                Directory.Delete(mods, true);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Resolve_MultipleCopiesOfSavedVersion_ReportsEveryMatchingFolder(
            bool archiveSecond
        )
        {
            string mods = Path.Combine(_directory, "Mods");
            GameMetadata metadata = CreateMetadata(_fixturePack);
            metadata.ModIDs = new[] { "rebalance" };
            metadata.ModVersions = new[] { "1.0.0" };
            try
            {
                WriteMod(mods, "rebalance", folderName: "first-copy");
                WriteMod(
                    archiveSecond ? Path.Combine(mods, ".versions") : mods,
                    "rebalance",
                    folderName: "second-copy"
                );

                var error = Assert.Throws<InvalidOperationException>(() =>
                    SaveContentResolver.Resolve(_fixturePack, metadata)
                );

                Assert.That(error.Message, Does.Contain("first-copy").And.Contain("second-copy"));
            }
            finally
            {
                Directory.Delete(mods, true);
            }
        }

        [Test]
        public void Resolve_OlderModReleaseIsRetained_LoadsItsFiles()
        {
            string mods = Path.Combine(_directory, "Mods");
            GameMetadata metadata = CreateMetadata(_fixturePack);
            metadata.ModIDs = new[] { "rebalance" };
            metadata.ModVersions = new[] { "1.0.0" };
            try
            {
                WriteMod(mods, "rebalance", "2.0.0");
                WriteMod(Path.Combine(mods, ".versions"), "rebalance");

                ContentPack selected = SaveContentResolver.Resolve(_fixturePack, metadata).Pack;

                Assert.That(selected.MatchesContentIdentity(metadata), Is.True);
                Assert.That(
                    File.ReadAllText(selected.FileResolver.ResolveFile("Pack/overlap.txt")),
                    Is.EqualTo("rebalance")
                );
            }
            finally
            {
                Directory.Delete(mods, true);
            }
        }

        [Test]
        public void Resolve_OlderPackReleaseIsRetained_UsesItsExactVersion()
        {
            string mods = Path.Combine(_directory, "Mods");
            ContentPack retained = TestPackFixture.Install(
                Path.Combine(_directory, "RetainedContent")
            );
            ContentPackDefinition previous = retained.Definition;
            previous.Version = "0.9.0";
            ContentDefinitionSerializer.Serialize(
                Path.Combine(retained.PackRootPath, "pack.xml"),
                previous
            );
            string archive = Path.Combine(mods, ".versions");
            Directory.CreateDirectory(archive);
            Directory.Move(retained.PackRootPath, Path.Combine(archive, "previous-pack"));
            try
            {
                GameMetadata metadata = CreateMetadata(_fixturePack);
                metadata.PackVersion = "0.9.0";

                ContentPack selected = SaveContentResolver.Resolve(_fixturePack, metadata).Pack;

                Assert.That(selected.MatchesContentIdentity(metadata), Is.True);
            }
            finally
            {
                Directory.Delete(mods, true);
            }
        }

        [Test]
        public void Resolve_DifferentPackVersion_ThrowsInvalidOperationException()
        {
            ContentPack activePack = _fixturePack;
            GameMetadata metadata = CreateMetadata(activePack);
            metadata.PackVersion = "unavailable-version";

            Assert.Throws<InvalidOperationException>(() =>
                SaveContentResolver.Resolve(activePack, metadata)
            );
        }

        [Test]
        public void Open_SharedSelectionChanges_PreservesLocalArtwork()
        {
            string mods = Path.Combine(_directory, "Mods");
            try
            {
                WriteMod(mods, "local", clientOnly: true);
                ContentPack local = ContentPackLoader.OpenExact(
                    _fixturePack.ContentRootPath,
                    _fixturePack.Definition.ID,
                    _fixturePack.Scenario.ID,
                    null
                );
                WriteMod(mods, "shared");
                GameSummary host = _fixturePack.CreateDefaultGameSummary();
                host.ModIDs = new[] { "shared" };
                host.ModVersions = new[] { "1.0.0" };

                ContentPack joined = SaveContentResolver.Open(local, host);

                Assert.That(joined.MatchesContentIdentity(host), Is.True);
                Assert.That(
                    File.ReadAllText(joined.FileResolver.ResolveFile("Pack/overlap.png")),
                    Is.EqualTo("local")
                );
            }
            finally
            {
                Directory.Delete(mods, true);
            }
        }

        [Test]
        public void Resolve_ClientOnlyMods_PreservesLocalSelectionWithoutSharingIt()
        {
            string mods = Path.Combine(_directory, "Mods");
            try
            {
                WriteMod(mods, "enabled", clientOnly: true);
                WriteMod(mods, "disabled", clientOnly: true);
                ContentPack local = ContentPackLoader.OpenExact(
                    _fixturePack.ContentRootPath,
                    _fixturePack.Definition.ID,
                    _fixturePack.Scenario.ID,
                    new[] { "disabled" }
                );
                GameMetadata metadata = CreateMetadata(_fixturePack);

                SaveContentSelection restored = SaveContentResolver.Resolve(local, metadata);

                Assert.That(restored.Pack.CreateDefaultGameSummary().ModIDs, Is.Empty);
                Assert.That(
                    restored.Pack.FileResolver.Mods.Select(mod => mod.ID),
                    Is.EqualTo(new[] { "enabled" })
                );
                Assert.That(
                    restored.DisabledModIDs,
                    Does.Contain("disabled").And.Not.Contain("enabled")
                );
            }
            finally
            {
                Directory.Delete(mods, true);
            }
        }

        [Test]
        public void Resolve_ClientOnlyReplacesSavedSharedMod_RejectsSelection()
        {
            string mods = Path.Combine(_directory, "Mods");
            try
            {
                WriteMod(mods, "required", clientOnly: true);
                GameMetadata metadata = CreateMetadata(_fixturePack);
                metadata.ModIDs = new[] { "required" };
                metadata.ModVersions = new[] { "1.0.0" };

                Assert.Throws<InvalidDataException>(() =>
                    SaveContentResolver.Resolve(_fixturePack, metadata)
                );
            }
            finally
            {
                Directory.Delete(mods, true);
            }
        }

        [Test]
        public void Resolve_MismatchedModIdentityArrays_ThrowsInvalidOperationException()
        {
            ContentPack activePack = _fixturePack;
            GameMetadata metadata = CreateMetadata(activePack);
            metadata.ModIDs = new[] { "mod" };
            metadata.ModVersions = Array.Empty<string>();

            Assert.Throws<InvalidOperationException>(() =>
                SaveContentResolver.Resolve(activePack, metadata)
            );
        }

        /// <summary>
        /// Creates one compatible test mod with a version-specific replacement file.
        /// </summary>
        /// <param name="mods">The active or retained mod directory.</param>
        /// <param name="id">The saved mod identity.</param>
        /// <param name="version">The installed version.</param>
        /// <param name="folderName">An optional directory name distinct from the mod ID.</param>
        /// <param name="clientOnly">Whether the replacement is local artwork.</param>
        private void WriteMod(
            string mods,
            string id,
            string version = "1.0.0",
            string folderName = null,
            bool clientOnly = false
        )
        {
            string folder = Path.Combine(mods, folderName ?? id);
            string files = Path.Combine(folder, "Content", "Pack");
            Directory.CreateDirectory(files);
            File.WriteAllText(
                Path.Combine(folder, "mod.xml"),
                $"<ContentModDefinition><ID>{id}</ID><DisplayName>{id}</DisplayName><Version>{version}</Version>"
                    + $"<BasePackID>{_fixturePack.Definition.ID}</BasePackID>"
                    + $"<BasePackVersion>{_fixturePack.Definition.Version}</BasePackVersion><ClientOnly>{clientOnly.ToString().ToLowerInvariant()}</ClientOnly></ContentModDefinition>"
            );
            File.WriteAllText(
                Path.Combine(files, clientOnly ? "overlap.png" : "overlap.txt"),
                version == "1.0.0" ? id : version
            );
        }

        /// <summary>
        /// Creates metadata matching one installed content pack.
        /// </summary>
        /// <param name="pack">The content pack to describe.</param>
        /// <returns>Metadata containing the pack's exact content identity.</returns>
        private static GameMetadata CreateMetadata(ContentPack pack)
        {
            return new GameMetadata
            {
                PackID = pack.Definition.ID,
                PackVersion = pack.Definition.Version,
                ScenarioID = pack.Scenario.ID,
                ModIDs = pack.FileResolver.Mods.Select(mod => mod.ID).ToArray(),
                ModVersions = pack.FileResolver.Mods.Select(mod => mod.Version).ToArray(),
            };
        }
    }
}
