using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rebellion.Util.Serialization;
using UnityEngine;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class ContentReplacementTests
    {
        private string _directory;
        private string _settingsPath;
        private UserSettingsManager _settings;
        private ContentPack _previous;
        private ContentPack _candidate;

        /// <summary>
        /// Creates independent content identities and a settings file owned by this test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                "content-replacement-" + Guid.NewGuid().ToString("N")
            );
            _settingsPath = Path.Combine(_directory, "user-settings.json");
            _settings = CreateSettings(_settingsPath);
            _settings.Settings.Content.ActivePackID = "previous";
            _settings.Settings.Content.ActiveScenarioID = "previous-scenario";
            _settings.Settings.Content.DisabledModIDs = new[] { "other-pack-mod", "previous-mod" };
            _settings.Save();
            _previous = CreatePack("previous");
            _candidate = CreatePack("candidate");
        }

        /// <summary>
        /// Clears staged launch state and deletes only the settings owned by this test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            GameLaunchContext.PrepareContentRestart(TestContent.Pack);
            GameLaunchContext.ConsumeContentPackAfterRestart();
            GameLaunchContext.Reset(TestContent.Pack);
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public void Constructor_PendingSelection_LeavesSettingsFileUnchanged()
        {
            byte[] before = File.ReadAllBytes(_settingsPath);

            CreateReplacement();
            _settings.Save();

            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
        }

        [Test]
        public void Complete_InitializedCandidate_PersistsCompleteModSelection()
        {
            ContentReplacement replacement = CreateReplacement();

            replacement.Complete(_candidate, _settings);

            UserSettingsManager reloaded = CreateSettings(_settingsPath);
            Assert.AreEqual("candidate", reloaded.Settings.Content.ActivePackID);
            CollectionAssert.AreEqual(
                new[] { "candidate-mod", "other-pack-mod" },
                reloaded.Settings.Content.DisabledModIDs
            );
        }

        [Test]
        public void Complete_DifferentContent_RejectsCompletionWithoutWritingSettings()
        {
            ContentReplacement replacement = CreateReplacement();
            byte[] before = File.ReadAllBytes(_settingsPath);

            Assert.Throws<InvalidOperationException>(() =>
                replacement.Complete(_previous, _settings)
            );

            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
        }

        [Test]
        public void Complete_SettingsWriteFailure_RestoresPreferencesBeforeShutdownSave()
        {
            ContentReplacement replacement = CreateReplacement();
            string blockedPath = Path.Combine(_directory, "blocked.json");
            Directory.CreateDirectory(blockedPath);
            UserSettingsManager blocked = CreateSettings(blockedPath);

            Assert.Throws<IOException>(() => replacement.Complete(_candidate, blocked));

            Directory.Delete(blockedPath);
            blocked.Save();
            UserSettingsManager reloaded = CreateSettings(blockedPath);
            Assert.AreEqual("previous", reloaded.Settings.Content.ActivePackID);
            CollectionAssert.AreEqual(
                new[] { "other-pack-mod", "previous-mod" },
                reloaded.Settings.Content.DisabledModIDs
            );
        }

        [Test]
        public void BeginRecovery_PendingSavedGame_StagesPreviousContentWithoutSavedGameRequest()
        {
            ContentReplacement replacement = CreateReplacement();
            GameLaunchContext.PrepareSaveLoadAfterContentRestart("failed-campaign", _candidate);

            Assert.IsTrue(replacement.BeginRecovery());
            ContentPack restored = GameLaunchContext.ConsumeContentPackAfterRestart();
            GameLaunchContext.Reset(restored);
            GameLaunchContext.RestoreSaveLoadAfterContentRestart();

            Assert.AreSame(_previous, restored);
            Assert.IsFalse(GameLaunchContext.IsLoadGame);
            Assert.IsNull(GameLaunchContext.SaveFileName);
        }

        [Test]
        public void BeginRecovery_RecoveryAlreadyStarted_RejectsAnotherAttempt()
        {
            ContentReplacement replacement = CreateReplacement();
            replacement.BeginRecovery();

            Assert.IsFalse(replacement.BeginRecovery());
        }

        [Test]
        public void Complete_RestoredContent_PreservesOriginalSettingsFile()
        {
            ContentReplacement replacement = CreateReplacement();
            byte[] before = File.ReadAllBytes(_settingsPath);
            replacement.BeginRecovery();
            _settings.Settings.Content.DisabledModIDs[0] = "uncommitted-change";

            replacement.Complete(_previous, _settings);

            Assert.AreEqual("previous", _settings.Settings.Content.ActivePackID);
            CollectionAssert.AreEqual(
                new[] { "other-pack-mod", "previous-mod" },
                _settings.Settings.Content.DisabledModIDs
            );
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
        }

        [Test]
        public void Complete_AlreadyCompleted_DoesNotWriteSettingsAgain()
        {
            ContentReplacement replacement = CreateReplacement();
            replacement.Complete(_candidate, _settings);
            byte[] before = File.ReadAllBytes(_settingsPath);
            _settings.Settings.Content.ActivePackID = "later-selection";

            replacement.Complete(_candidate, _settings);

            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
        }

        [Test]
        public void ForSavedGame_PackScopedModSelection_PreservesUnrelatedModPreferences()
        {
            ContentReplacement replacement = ContentReplacement.ForSavedGame(
                _previous,
                _settings.Settings.Content,
                new SaveContentSelection(_candidate, Array.Empty<string>())
            );

            replacement.Complete(_candidate, _settings);

            CollectionAssert.AreEqual(
                new[] { "other-pack-mod" },
                _settings.Settings.Content.DisabledModIDs
            );
        }

        /// <summary>
        /// Creates the verified pending content selection used by lifecycle tests.
        /// </summary>
        /// <returns>The pending replacement.</returns>
        private ContentReplacement CreateReplacement()
        {
            return new ContentReplacement(
                _previous,
                _settings.Settings.Content,
                new ContentReloadPreparation(
                    _candidate,
                    new[] { "other-pack-mod", "candidate-mod" }
                )
            );
        }

        /// <summary>
        /// Loads settings without mutating the display or using the application's settings file.
        /// </summary>
        /// <param name="path">The test-owned settings path.</param>
        /// <returns>The settings manager.</returns>
        private static UserSettingsManager CreateSettings(string path)
        {
            DisplayManager display = new DisplayManager(
                () => new[] { new Vector2Int(1920, 1080) },
                () => new Vector2Int(1920, 1080),
                (_, _, _) => { }
            );
            UserSettingsManager settings = new UserSettingsManager(null, display, null, path);
            settings.Load();
            return settings;
        }

        /// <summary>
        /// Creates a distinct content identity without loading another scene or decoding assets.
        /// </summary>
        /// <param name="id">The content-pack identifier.</param>
        /// <returns>The test content pack.</returns>
        private static ContentPack CreatePack(string id)
        {
            ContentPack source = TestContent.Pack;
            return new ContentPack(
                source.ContentRootPath,
                source.PackRootPath,
                new ContentFileResolver(
                    source.ContentRootPath,
                    source.PackRootPath,
                    availableMods: new[] { CreateMod("previous-mod"), CreateMod("candidate-mod") }
                ),
                new ContentPackDefinition { ID = id },
                source.Scenario,
                source.Factions,
                source.GameData,
                new Dictionary<string, ContentPreloadManifest>()
            );
        }

        /// <summary>
        /// Reads a mod identity through its persisted public contract.
        /// </summary>
        /// <param name="id">The mod identifier.</param>
        /// <returns>The mod definition.</returns>
        private static ContentModDefinition CreateMod(string id)
        {
            using StringReader reader = new StringReader(
                $"<ContentModDefinition><ID>{id}</ID></ContentModDefinition>"
            );
            return (ContentModDefinition)
                new GameSerializer(typeof(ContentModDefinition)).Deserialize(reader);
        }
    }
}
