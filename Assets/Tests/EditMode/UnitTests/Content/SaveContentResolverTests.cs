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
