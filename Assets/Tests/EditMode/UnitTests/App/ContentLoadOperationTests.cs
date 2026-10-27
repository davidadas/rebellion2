using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Util.Reflection;
using UnityEngine;

namespace Rebellion.Tests.App
{
    [TestFixture]
    public sealed class ContentLoadOperationTests
    {
        private string _directory;
        private string _settingsPath;
        private string _modDirectory;
        private UserSettingsManager _settings;
        private SaveGameManager _saves;
        private ContentPack _previous;

        /// <summary>
        /// Creates isolated preferences and saves with a distinct previous content identity.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                "content-load-" + Guid.NewGuid().ToString("N")
            );
            _settingsPath = Path.Combine(_directory, "user-settings.json");
            _settings = CreateSettings(_settingsPath);
            _settings.Settings.Content.ActivePackID = "previous";
            _settings.Settings.Content.ActiveScenarioID = "previous-scenario";
            _settings.Settings.Content.DisabledModIDs = new[] { "other-pack-mod", "previous-mod" };
            _settings.Save();
            _saves = new SaveGameManager(Path.Combine(_directory, "saves"));
            ContentPack source = TestContent.Pack;
            _previous = new ContentPack(
                source.ContentRootPath,
                source.PackRootPath,
                source.FileResolver,
                new ContentPackDefinition { ID = "previous" },
                source.Scenario,
                source.Factions,
                source.GameData,
                new Dictionary<string, ContentPreloadManifest>()
            );
        }

        /// <summary>
        /// Deletes only files and content owned by this fixture.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_modDirectory != null && Directory.Exists(_modDirectory))
                Directory.Delete(_modDirectory, true);
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public async Task PrepareContentReloadAsync_PendingSelection_LeavesSettingsFileUnchangedAsync()
        {
            byte[] before = File.ReadAllBytes(_settingsPath);
            ContentLoadOperation operation = CreateOperation();

            await operation.PrepareContentReloadAsync(
                TestContent.Pack.Definition.ID,
                Array.Empty<string>()
            );
            _settings.Save();

            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
            Assert.IsTrue(operation.RequiresContentRestart);
            Assert.AreEqual("MainMenu", operation.DestinationScene);
            Assert.IsNull(operation.SaveFileName);
        }

        [Test]
        public void Complete_InitializedCandidate_PersistsCompleteModSelection()
        {
            ContentLoadOperation operation = CreatePreparedOperation();

            operation.Complete(operation.Pack, _settings);

            UserSettingsManager reloaded = CreateSettings(_settingsPath);
            Assert.AreEqual(operation.Pack.Definition.ID, reloaded.Settings.Content.ActivePackID);
            CollectionAssert.AreEqual(
                new[] { "candidate-mod", "other-pack-mod" },
                reloaded.Settings.Content.DisabledModIDs
            );
        }

        [Test]
        public void Complete_DifferentContent_RejectsCompletionWithoutWritingSettings()
        {
            ContentLoadOperation operation = CreatePreparedOperation();
            byte[] before = File.ReadAllBytes(_settingsPath);

            Assert.Throws<InvalidOperationException>(() =>
                operation.Complete(_previous, _settings)
            );

            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
        }

        [Test]
        public void Complete_SettingsWriteFailure_RestoresPreferencesBeforeShutdownSave()
        {
            ContentLoadOperation operation = CreatePreparedOperation();
            string blockedPath = Path.Combine(_directory, "blocked.json");
            Directory.CreateDirectory(blockedPath);
            UserSettingsManager blocked = CreateSettings(blockedPath);

            Assert.Throws<IOException>(() => operation.Complete(operation.Pack, blocked));

            Directory.Delete(blockedPath);
            blocked.Save();
            UserSettingsManager reloaded = CreateSettings(blockedPath);
            Assert.AreEqual("previous", reloaded.Settings.Content.ActivePackID);
            CollectionAssert.AreEqual(
                new[] { "other-pack-mod", "previous-mod" },
                reloaded.Settings.Content.DisabledModIDs
            );
            Assert.IsTrue(operation.BeginRecovery());
        }

        [Test]
        public async Task BeginRecovery_PendingSavedGame_SelectsPreviousContentWithoutSaveRequestAsync()
        {
            SaveGame(TestContent.Pack);
            ContentLoadOperation operation = CreateOperation();
            await operation.PrepareSavedGameAsync(_saves, "campaign");

            Assert.IsTrue(operation.BeginRecovery());

            Assert.AreSame(_previous, operation.Pack);
            Assert.AreEqual("MainMenu", operation.DestinationScene);
            Assert.IsNull(operation.SaveFileName);
        }

        [Test]
        public void BeginRecovery_RecoveryAlreadyStarted_RejectsAnotherAttempt()
        {
            ContentLoadOperation operation = CreatePreparedOperation();
            operation.BeginRecovery();

            Assert.IsFalse(operation.BeginRecovery());
        }

        [Test]
        public void Complete_RestoredContent_PreservesOriginalSettingsFile()
        {
            ContentLoadOperation operation = CreatePreparedOperation();
            byte[] before = File.ReadAllBytes(_settingsPath);
            operation.BeginRecovery();
            _settings.Settings.Content.DisabledModIDs[0] = "uncommitted-change";

            operation.Complete(_previous, _settings);

            Assert.AreEqual("previous", _settings.Settings.Content.ActivePackID);
            CollectionAssert.AreEqual(
                new[] { "other-pack-mod", "previous-mod" },
                _settings.Settings.Content.DisabledModIDs
            );
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
            Assert.IsFalse(operation.BeginRecovery());
        }

        [Test]
        public void Complete_AlreadyCompleted_DoesNotWriteSettingsAgain()
        {
            ContentLoadOperation operation = CreatePreparedOperation();
            operation.Complete(operation.Pack, _settings);
            byte[] before = File.ReadAllBytes(_settingsPath);
            _settings.Settings.Content.ActivePackID = "later-selection";

            operation.Complete(operation.Pack, _settings);

            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
            Assert.IsFalse(operation.BeginRecovery());
        }

        [Test]
        public async Task PrepareSavedGameAsync_PackScopedModSelection_PreservesUnrelatedModPreferencesAsync()
        {
            string modID = CreateMod();
            ContentPack required = ContentPackLoader.OpenExact(
                TestContent.Pack.ContentRootPath,
                TestContent.Pack.Definition.ID,
                TestContent.Pack.Scenario.ID,
                Array.Empty<string>()
            );
            SaveGame(required);
            _settings.Settings.Content.DisabledModIDs = new[] { "other-pack-mod", modID };
            ContentLoadOperation operation = CreateOperation();

            await operation.PrepareSavedGameAsync(_saves, "campaign");
            operation.Complete(operation.Pack, _settings);

            Assert.IsTrue(operation.Pack.FileResolver.Mods.Any(mod => mod.ID == modID));
            CollectionAssert.AreEqual(
                new[] { "other-pack-mod" },
                _settings.Settings.Content.DisabledModIDs
            );
        }

        [Test]
        public async Task PrepareSavedGameAsync_MatchingContent_ReusesContentWithoutSavingPreferencesAsync()
        {
            ContentPack current = TestContent.Pack;
            SaveGame(current);
            ContentLoadOperation operation = new ContentLoadOperation(
                current,
                _settings.Settings.Content,
                new[] { "main-menu", "strategy" }
            );
            byte[] before = File.ReadAllBytes(_settingsPath);

            await operation.PrepareSavedGameAsync(_saves, "campaign");
            operation.Complete(current, _settings);

            Assert.IsFalse(operation.RequiresContentRestart);
            Assert.AreSame(current, operation.Pack);
            Assert.AreEqual("campaign", operation.SaveFileName);
            Assert.AreEqual("StrategyView", operation.DestinationScene);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
        }

        [Test]
        public async Task PrepareContentReloadAsync_CallerChangesPreferences_CommitsOriginalSelectionAsync()
        {
            ContentLoadOperation operation = CreateOperation();
            string[] disabled = { "selected-mod" };
            Task preparation = operation.PrepareContentReloadAsync(
                TestContent.Pack.Definition.ID,
                disabled
            );
            disabled[0] = "changed-mod";
            await preparation;

            operation.Complete(operation.Pack, _settings);

            CollectionAssert.AreEqual(
                new[] { "selected-mod" },
                _settings.Settings.Content.DisabledModIDs
            );
        }

        [Test]
        public void PrepareContentReloadAsync_AlreadyPrepared_RejectsAnotherRequest()
        {
            ContentLoadOperation operation = CreatePreparedOperation();

            Assert.ThrowsAsync<InvalidOperationException>(() =>
                operation.PrepareContentReloadAsync(
                    TestContent.Pack.Definition.ID,
                    Array.Empty<string>()
                )
            );
        }

        [Test]
        public void PrepareContentReloadAsync_MissingPack_RecoversWithPreviousContent()
        {
            ContentLoadOperation operation = CreateOperation();

            Assert.ThrowsAsync<InvalidOperationException>(() =>
                operation.PrepareContentReloadAsync("missing-pack", Array.Empty<string>())
            );

            Assert.AreSame(_previous, operation.Pack);
            Assert.IsFalse(operation.RequiresContentRestart);
            Assert.IsTrue(operation.BeginRecovery());
            operation.Complete(_previous, _settings);
        }

        [Test]
        public void Fail_RecoveryCannotInitialize_RejectsFurtherRecoveryAndCompletion()
        {
            ContentLoadOperation operation = CreatePreparedOperation();
            operation.BeginRecovery();

            operation.Fail();

            Assert.IsFalse(operation.BeginRecovery());
            Assert.Throws<InvalidOperationException>(() =>
                operation.Complete(_previous, _settings)
            );
        }

        [Test]
        public async Task BeginRecovery_PreparationStillRunning_RetainsPreviousSelectionAfterPreparationAsync()
        {
            ContentLoadOperation operation = CreateOperation();
            Task preparation = operation.PrepareContentReloadAsync(
                TestContent.Pack.Definition.ID,
                Array.Empty<string>()
            );

            Assert.IsTrue(operation.BeginRecovery());
            await preparation;

            Assert.AreSame(_previous, operation.Pack);
            Assert.IsTrue(operation.IsRecovering);
            operation.Complete(_previous, _settings);
        }

        [Test]
        public async Task BeginRecovery_SameContentSceneReady_ReturnsToMenuWithoutReconstructionAsync()
        {
            ContentPack current = TestContent.Pack;
            SaveGame(current);
            ContentLoadOperation operation = new ContentLoadOperation(
                current,
                _settings.Settings.Content,
                new[] { "main-menu", "strategy" }
            );
            await operation.PrepareSavedGameAsync(_saves, "campaign");
            operation.Complete(current, _settings);

            Assert.IsTrue(operation.BeginRecovery());

            Assert.IsFalse(operation.RequiresContentRestart);
            Assert.AreEqual("MainMenu", operation.DestinationScene);
            Assert.IsNull(operation.SaveFileName);
            operation.Complete(current, _settings);
            Assert.IsFalse(operation.BeginRecovery());
        }

        [Test]
        public void PrepareCustomGame_ValidSelection_PreservesDetachedConfigurationWithoutSaving()
        {
            ContentPack selected = TestContent.Pack;
            GameSummary summary = GameLaunchContext.CreateDefaultSummary(selected);
            summary.Difficulty = GameDifficulty.Hard;
            summary.CustomSettings = new CustomGameSettings
            {
                GenerationConfig = selected.GameData.GenerationConfig.GetDeepCopy(),
            };
            byte[] before = File.ReadAllBytes(_settingsPath);
            ContentLoadOperation operation = CreateOperation();

            operation.PrepareCustomGame(selected, summary);
            summary.Difficulty = GameDifficulty.Easy;
            summary.CustomSettings.GenerationConfig.FacilityGeneration.CoreMineMultiplier = 999;

            Assert.AreSame(selected, operation.Pack);
            Assert.IsTrue(operation.RequiresContentRestart);
            Assert.AreEqual("StrategyView", operation.DestinationScene);
            Assert.IsNull(operation.SaveFileName);
            Assert.AreEqual(GameDifficulty.Hard, operation.CustomGameSummary.Difficulty);
            Assert.AreNotEqual(
                999,
                operation
                    .CustomGameSummary
                    .CustomSettings
                    .GenerationConfig
                    .FacilityGeneration
                    .CoreMineMultiplier
            );
            GameSummary exposed = operation.CustomGameSummary;
            exposed.Difficulty = GameDifficulty.Easy;
            Assert.AreEqual(GameDifficulty.Hard, operation.CustomGameSummary.Difficulty);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
        }

        [Test]
        public void PrepareCustomGame_MismatchedIdentity_RejectsLaunchWithoutSaving()
        {
            GameSummary summary = GameLaunchContext.CreateDefaultSummary(TestContent.Pack);
            summary.PackID = "missing";
            byte[] before = File.ReadAllBytes(_settingsPath);
            ContentLoadOperation operation = CreateOperation();

            Assert.Throws<InvalidOperationException>(() =>
                operation.PrepareCustomGame(TestContent.Pack, summary)
            );

            Assert.AreSame(_previous, operation.Pack);
            Assert.IsNull(operation.CustomGameSummary);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
        }

        [Test]
        public void BeginRecovery_CustomGame_DiscardsLaunchAndPreservesPreviousPreferences()
        {
            ContentLoadOperation operation = CreateOperation();
            operation.PrepareCustomGame(
                TestContent.Pack,
                GameLaunchContext.CreateDefaultSummary(TestContent.Pack)
            );
            byte[] before = File.ReadAllBytes(_settingsPath);

            Assert.IsTrue(operation.BeginRecovery());
            operation.Complete(_previous, _settings);

            Assert.AreSame(_previous, operation.Pack);
            Assert.AreEqual("MainMenu", operation.DestinationScene);
            Assert.IsNull(operation.CustomGameSummary);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_settingsPath));
        }

        [Test]
        public void Complete_CustomGame_PreservesOtherPacksModPreferences()
        {
            string modID = CreateMod();
            ContentPack selected = ContentPackLoader.OpenExact(
                TestContent.Pack.ContentRootPath,
                TestContent.Pack.Definition.ID,
                TestContent.Pack.Scenario.ID,
                new[] { modID }
            );
            ContentLoadOperation operation = CreateOperation();
            operation.PrepareCustomGame(selected, GameLaunchContext.CreateDefaultSummary(selected));

            operation.Complete(operation.Pack, _settings);

            UserContentSettings content = CreateSettings(_settingsPath).Settings.Content;
            Assert.AreEqual(TestContent.Pack.Definition.ID, content.ActivePackID);
            Assert.AreEqual(TestContent.Pack.Scenario.ID, content.ActiveScenarioID);
            CollectionAssert.AreEquivalent(
                new[] { "other-pack-mod", "previous-mod", modID },
                content.DisabledModIDs
            );
        }

        /// <summary>
        /// Creates a pending operation over the fixture's committed content preferences.
        /// </summary>
        /// <returns>The operation.</returns>
        private ContentLoadOperation CreateOperation()
        {
            return new ContentLoadOperation(
                _previous,
                _settings.Settings.Content,
                new[] { "main-menu", "strategy" }
            );
        }

        /// <summary>
        /// Prepares installed replacement content for lifecycle tests without mutating preferences.
        /// </summary>
        /// <returns>The prepared operation.</returns>
        private ContentLoadOperation CreatePreparedOperation()
        {
            ContentLoadOperation operation = CreateOperation();
            operation
                .PrepareContentReloadAsync(
                    TestContent.Pack.Definition.ID,
                    new[] { "other-pack-mod", "candidate-mod" }
                )
                .GetAwaiter()
                .GetResult();
            return operation;
        }

        /// <summary>
        /// Writes a minimal save declaring the supplied content selection.
        /// </summary>
        /// <param name="pack">The content required by the save.</param>
        private void SaveGame(ContentPack pack)
        {
            _saves.SaveGameData(
                new GameRoot
                {
                    Summary = new GameSummary
                    {
                        PackID = pack.Definition.ID,
                        PackVersion = pack.Definition.Version,
                        ScenarioID = pack.Scenario.ID,
                        ModIDs = pack.FileResolver.Mods.Select(mod => mod.ID).ToArray(),
                        ModVersions = pack.FileResolver.Mods.Select(mod => mod.Version).ToArray(),
                    },
                },
                "campaign"
            );
        }

        /// <summary>
        /// Creates a compatible mod used to verify saved-game preference reconciliation.
        /// </summary>
        /// <returns>The mod identifier.</returns>
        private string CreateMod()
        {
            ContentPack pack = TestContent.Pack;
            string modID = "content-load-test-" + Guid.NewGuid().ToString("N");
            _modDirectory = Path.Combine(
                Directory.GetParent(pack.ContentRootPath).FullName,
                "Mods",
                modID
            );
            Directory.CreateDirectory(_modDirectory);
            File.WriteAllText(
                Path.Combine(_modDirectory, "mod.xml"),
                $"<ContentModDefinition><ID>{modID}</ID><Version>1.0.0</Version>"
                    + $"<DisplayName>Content load test</DisplayName><BasePackID>{pack.Definition.ID}</BasePackID>"
                    + $"<BasePackVersion>{pack.Definition.Version}</BasePackVersion></ContentModDefinition>"
            );
            return modID;
        }

        /// <summary>
        /// Loads isolated preferences without applying changes to the real display.
        /// </summary>
        /// <param name="path">The fixture-owned preference path.</param>
        /// <returns>The preference service.</returns>
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
    }
}
