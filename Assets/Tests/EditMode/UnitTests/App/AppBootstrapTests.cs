using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Simulation;
using Rebellion.Util.Reflection;
using UnityEditor;
using UnityEngine;

namespace Rebellion.Tests.App
{
    [TestFixture]
    public sealed class AppBootstrapTests
    {
        private AppBootstrap _bootstrap;
        private GameObject _gameObject;
        private string _modDirectory;
        private string _packDirectory;
        private string _settingsPath;
        private byte[] _settingsBytes;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _settingsPath = Path.Combine(Application.persistentDataPath, "user-settings.json");
            _settingsBytes = File.Exists(_settingsPath) ? File.ReadAllBytes(_settingsPath) : null;
            DestroyAudioManagers();
            GameLaunchContext.Reset(TestContent.Pack);
            GameLaunchContext.Summary.PlayerFactionID = null;
            GameLaunchContext.Summary.PackID = null;
            GameLaunchContext.Summary.PackVersion = null;
            GameLaunchContext.Summary.ScenarioID = null;

            _gameObject = new GameObject("AppBootstrapUnderTest");
            _gameObject.SetActive(false);
            _bootstrap = _gameObject.AddComponent<AppBootstrap>();
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            try
            {
                LoadingScreenController loadingController =
                    Object.FindAnyObjectByType<LoadingScreenController>(
                        FindObjectsInactive.Include
                    );
                if (loadingController != null)
                    Object.DestroyImmediate(loadingController.gameObject);
                Object.DestroyImmediate(_gameObject);
                DestroyAudioManagers();
                if (_packDirectory != null && Directory.Exists(_packDirectory))
                    Directory.Delete(_packDirectory, true);
                if (_modDirectory != null && Directory.Exists(_modDirectory))
                    Directory.Delete(_modDirectory, true);
                _modDirectory = null;
                _packDirectory = null;
                GameLaunchContext.Reset(TestContent.Pack);
            }
            finally
            {
                if (_settingsBytes == null)
                    File.Delete(_settingsPath);
                else
                    File.WriteAllBytes(_settingsPath, _settingsBytes);
            }
        }

        [Test]
        public void CreateContentLoadOperation_DerivedPack_PreparesSelectionWithoutReplacingActiveContent()
        {
            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");
            ContentPack active = _bootstrap.GetContentPack();
            string packID = CreateDerivedPack(active);

            ContentLoadOperation operation = _bootstrap.CreateContentLoadOperation();
            operation
                .PrepareContentReloadAsync(packID, System.Array.Empty<string>())
                .GetAwaiter()
                .GetResult();

            Assert.AreEqual(packID, operation.Pack.Definition.ID);
            Assert.AreEqual(active.Scenario.ID, operation.Pack.Scenario.ID);
            Assert.AreSame(active, _bootstrap.GetContentPack());
            Assert.IsFalse(LoadingScreenController.IsLoadInProgress);
        }

        [TestCase("main-menu")]
        [TestCase("strategy")]
        public void CreateContentLoadOperation_MalformedApplicationPreload_RejectsSelection(
            string preloadID
        )
        {
            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");
            ContentPack active = _bootstrap.GetContentPack();
            string modContentRoot = CreateContentMod(active);
            string preloadDirectory = Path.Combine(modContentRoot, "Application", "Preload");
            Directory.CreateDirectory(preloadDirectory);
            File.WriteAllText(
                Path.Combine(preloadDirectory, preloadID + ".xml"),
                "<ContentPreload><TexturesPerFrame>0</TexturesPerFrame>"
                    + "<Textures/><TextureDirectories/><Audio/><Models/></ContentPreload>"
            );

            Assert.Throws<InvalidDataException>(() =>
                _bootstrap
                    .CreateContentLoadOperation()
                    .PrepareContentReloadAsync(active.Definition.ID, System.Array.Empty<string>())
                    .GetAwaiter()
                    .GetResult()
            );

            Assert.AreSame(active, _bootstrap.GetContentPack());
        }

        [Test]
        public void CreateContentLoadOperation_MissingPack_RejectsSelectionWithoutFallingBack()
        {
            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");
            ContentPack active = _bootstrap.GetContentPack();

            Assert.Throws<System.InvalidOperationException>(() =>
                _bootstrap
                    .CreateContentLoadOperation()
                    .PrepareContentReloadAsync("not-installed", System.Array.Empty<string>())
                    .GetAwaiter()
                    .GetResult()
            );

            Assert.AreSame(active, _bootstrap.GetContentPack());
        }

        [Test]
        public void ReloadContent_PendingSelection_LeavesContentPreferencesUnchanged()
        {
            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");
            string[] original = _bootstrap.GetUserSettingsManager().Settings.Content.DisabledModIDs;
            string[] disabledModIDs = { "other-pack-mod", "selected-pack-mod" };

            bool started = _bootstrap.ReloadContent(
                _bootstrap.GetContentPack().Definition.ID,
                disabledModIDs
            );

            Assert.IsTrue(started);
            CollectionAssert.AreEqual(
                original,
                _bootstrap.GetUserSettingsManager().Settings.Content.DisabledModIDs
            );
            Assert.AreEqual(_settingsBytes != null, File.Exists(_settingsPath));
            if (_settingsBytes != null)
                CollectionAssert.AreEqual(_settingsBytes, File.ReadAllBytes(_settingsPath));
        }

        [Test]
        public void ReloadContent_ActiveCampaign_LeavesCampaignRunning()
        {
            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");
            GameRuntime runtime = _bootstrap.GetRuntime();
            GameRoot game = CreateCompatibleGame(_bootstrap.GetContentPack(), 42);
            runtime.StartGame(game);

            bool started = _bootstrap.ReloadContent(
                TestContent.Pack.Definition.ID,
                System.Array.Empty<string>()
            );

            Assert.IsFalse(started);
            Assert.AreSame(game, runtime.GetActiveGame());
            Assert.IsFalse(LoadingScreenController.IsLoadInProgress);
        }

        [Test]
        public void ReloadContent_MainMenu_StartsContentTransitionWithoutSaveRequest()
        {
            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");
            int transitions = 0;
            _bootstrap.LoadStarting += () => transitions++;

            bool started = _bootstrap.ReloadContent(
                _bootstrap.GetContentPack().Definition.ID,
                System.Array.Empty<string>()
            );

            Assert.IsTrue(started);
            Assert.IsTrue(LoadingScreenController.IsLoadInProgress);
            Assert.AreEqual(1, transitions);
            Assert.IsFalse(GameLaunchContext.IsLoadGame);
            Assert.IsNull(GameLaunchContext.SaveFileName);
        }

        [Test]
        public void ReloadContent_PendingTransition_RejectsDuplicateRequest()
        {
            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");
            string packID = _bootstrap.GetContentPack().Definition.ID;
            Assert.IsTrue(_bootstrap.ReloadContent(packID, System.Array.Empty<string>()));

            Assert.IsFalse(_bootstrap.ReloadContent(packID, System.Array.Empty<string>()));
        }

        [Test]
        public void CompletePendingLoad_DifferentBootstrap_LeavesTransitionPending()
        {
            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");
            _bootstrap.ReloadContent(
                _bootstrap.GetContentPack().Definition.ID,
                System.Array.Empty<string>()
            );
            GameObject otherObject = new GameObject("UnrelatedBootstrap");
            otherObject.SetActive(false);
            try
            {
                AppBootstrap other = otherObject.AddComponent<AppBootstrap>();

                Assert.IsFalse(LoadingScreenController.CompletePendingLoad(other));

                Assert.IsTrue(LoadingScreenController.IsLoadInProgress);
            }
            finally
            {
                Object.DestroyImmediate(otherObject);
            }
        }

        [Test]
        public void InitializeRuntimeCore_BlankLaunchContext_SetsActiveContentDefaults()
        {
            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");
            ContentPack activeContent = _bootstrap.GetContentPack();

            Assert.AreEqual(
                activeContent.Scenario.DefaultPlayerFactionID,
                GameLaunchContext.Summary.PlayerFactionID
            );
            Assert.AreEqual(activeContent.Definition.ID, GameLaunchContext.Summary.PackID);
            Assert.AreEqual(
                activeContent.Definition.Version,
                GameLaunchContext.Summary.PackVersion
            );
            Assert.AreEqual(activeContent.Scenario.ID, GameLaunchContext.Summary.ScenarioID);
        }

        [Test]
        public void StartCustomGame_SelectedContent_PreservesLaunchConfigurationThroughLoadingPresentation()
        {
            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");
            ContentPack selected = _bootstrap.GetContentPack();
            GameSummary summary = GameLaunchContext.CreateDefaultSummary(selected);
            summary.Difficulty = GameDifficulty.Hard;
            summary.GenerationConfigOverride = selected.GameData.GenerationConfig.GetDeepCopy();
            summary.GenerationConfigOverride.FacilityGeneration.CoreMineMultiplier = 17;
            int transitionCount = 0;
            _bootstrap.LoadStarting += () => transitionCount++;

            LoadingScreenController.StartCustomGame(_bootstrap, selected, summary);
            summary.GenerationConfigOverride.FacilityGeneration.CoreMineMultiplier = 999;

            Assert.AreEqual(1, transitionCount);
            Assert.IsTrue(LoadingScreenController.IsLoadInProgress);
            Assert.IsFalse(GameLaunchContext.IsLoadGame);
            Assert.IsTrue(GameLaunchContext.PlayIntroCutscene);
            Assert.AreEqual(GameDifficulty.Hard, GameLaunchContext.Summary.Difficulty);
            Assert.AreEqual(
                17,
                GameLaunchContext
                    .Summary
                    .GenerationConfigOverride
                    .FacilityGeneration
                    .CoreMineMultiplier
            );
        }

        [Test]
        public void LoadSave_ActiveContent_BeginsPresentedSceneTransition()
        {
            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");
            ContentPack contentPack = _bootstrap.GetContentPack();
            GameRuntime runtime = _bootstrap.GetRuntime();
            GameRoot activeGame = CreateCompatibleGame(contentPack, 1);
            runtime.StartGame(activeGame);
            string fileName = $"bootstrap-hot-load-{System.Guid.NewGuid():N}";
            SaveGameManager saveGameManager = SaveGameManager.Instance;
            int transitionCount = 0;
            _bootstrap.LoadStarting += () => transitionCount++;
            AudioManager audioManager = AudioManager.EnsureExists();
            AudioClip music = AudioClip.Create("Outgoing Music", 1, 1, 44100, false);
            audioManager.PlayTrack(music, true);
            AudioSource musicSource = audioManager
                .GetComponents<AudioSource>()
                .Single(source => source.clip == music);

            try
            {
                saveGameManager.SaveGameData(
                    CreateCompatibleGame(contentPack, 42),
                    fileName,
                    "Bootstrap Hot Load"
                );

                bool loaded = _bootstrap.LoadSave(fileName);

                Assert.IsTrue(loaded);
                Assert.AreEqual(1, transitionCount);
                Assert.IsFalse(runtime.HasActiveGame);
                Assert.AreEqual(fileName, GameLaunchContext.SaveFileName);
                Assert.IsTrue(GameLaunchContext.IsLoadGame);
                Assert.IsNull(musicSource.clip);
                Assert.IsNotNull(
                    Object.FindAnyObjectByType<LoadingScreenController>(FindObjectsInactive.Include)
                );
            }
            finally
            {
                File.Delete(saveGameManager.GetSaveFilePath(fileName));
                File.Delete(saveGameManager.GetSaveMetadataFilePath(fileName));
                Object.DestroyImmediate(music);
            }
        }

        [Test]
        public void RecoverFromLoadFailure_ActiveTransition_ClearsLoadingState()
        {
            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");
            ContentPack contentPack = _bootstrap.GetContentPack();
            string fileName = $"bootstrap-failed-load-{System.Guid.NewGuid():N}";
            SaveGameManager saveGameManager = SaveGameManager.Instance;

            try
            {
                saveGameManager.SaveGameData(
                    CreateCompatibleGame(contentPack, 42),
                    fileName,
                    "Bootstrap Failed Load"
                );
                Assert.IsTrue(_bootstrap.LoadSave(fileName));
                Assert.IsTrue(LoadingScreenController.IsLoadInProgress);

                Assert.IsTrue(LoadingScreenController.RecoverFromLoadFailure());

                Assert.IsFalse(LoadingScreenController.IsLoadInProgress);
            }
            finally
            {
                File.Delete(saveGameManager.GetSaveFilePath(fileName));
                File.Delete(saveGameManager.GetSaveMetadataFilePath(fileName));
            }
        }

        [Test]
        public void CompleteFailedRecovery_ActiveTransition_ClearsLoadingState()
        {
            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");
            ContentPack contentPack = _bootstrap.GetContentPack();
            string fileName = $"bootstrap-menu-failed-load-{System.Guid.NewGuid():N}";
            SaveGameManager saveGameManager = SaveGameManager.Instance;

            try
            {
                saveGameManager.SaveGameData(
                    CreateCompatibleGame(contentPack, 42),
                    fileName,
                    "Bootstrap Menu Failed Load"
                );
                Assert.IsTrue(_bootstrap.LoadSave(fileName));
                Assert.IsTrue(LoadingScreenController.IsLoadInProgress);

                Assert.IsTrue(LoadingScreenController.CompleteFailedRecovery(_bootstrap, null));

                Assert.IsFalse(LoadingScreenController.IsLoadInProgress);
            }
            finally
            {
                File.Delete(saveGameManager.GetSaveFilePath(fileName));
                File.Delete(saveGameManager.GetSaveMetadataFilePath(fileName));
            }
        }

        [Test]
        public async Task InitializeMainMenuContentAsync_DestroyedDuringPreload_CompletesSafelyAsync()
        {
            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");
            Task preload = _bootstrap.InitializeMainMenuContentAsync();

            Object.DestroyImmediate(_gameObject);
            _gameObject = null;

            await preload;

            Assert.IsNull(AppBootstrap.Instance);
        }

        /// <summary>
        /// Creates a distinct installed pack that inherits the active pack's content.
        /// </summary>
        /// <param name="active">The installed base pack.</param>
        /// <returns>The derived pack identifier.</returns>
        private string CreateDerivedPack(ContentPack active)
        {
            string packID = $"reload-test-{System.Guid.NewGuid():N}";
            _packDirectory = Path.Combine(active.ContentRootPath, "Packs", packID);
            Directory.CreateDirectory(_packDirectory);
            XDocument definition = XDocument.Load(Path.Combine(active.PackRootPath, "pack.xml"));
            definition.Root.SetElementValue("ID", packID);
            definition.Root.SetElementValue("DisplayName", "Reload test pack");
            definition.Root.SetElementValue("BasePackID", active.Definition.ID);
            definition.Save(Path.Combine(_packDirectory, "pack.xml"));
            return packID;
        }

        /// <summary>
        /// Creates an enabled content mod compatible with the supplied pack.
        /// </summary>
        /// <param name="active">The pack the test mod extends.</param>
        /// <returns>The test mod's content root.</returns>
        private string CreateContentMod(ContentPack active)
        {
            string modID = $"reload-test-mod-{System.Guid.NewGuid():N}";
            _modDirectory = Path.Combine(
                Directory.GetParent(active.ContentRootPath).FullName,
                "Mods",
                modID
            );
            Directory.CreateDirectory(_modDirectory);
            File.WriteAllText(
                Path.Combine(_modDirectory, "mod.xml"),
                $"<ContentModDefinition><ID>{modID}</ID>"
                    + "<Version>1.0.0</Version><DisplayName>Reload test mod</DisplayName>"
                    + $"<BasePackID>{active.Definition.ID}</BasePackID>"
                    + $"<BasePackVersion>{active.Definition.Version}</BasePackVersion>"
                    + "</ContentModDefinition>"
            );
            string contentRoot = Path.Combine(_modDirectory, "Content");
            Directory.CreateDirectory(contentRoot);
            return contentRoot;
        }

        /// <summary>
        /// Removes persistent audio managers created by bootstrap initialization.
        /// </summary>
        private static void DestroyAudioManagers()
        {
            foreach (
                AudioManager manager in Object.FindObjectsByType<AudioManager>(
                    FindObjectsInactive.Include
                )
            )
            {
                Object.DestroyImmediate(manager.gameObject);
            }
        }

        /// <summary>
        /// Creates a minimal game carrying the supplied content identity.
        /// </summary>
        /// <param name="contentPack">The content pack the game belongs to.</param>
        /// <param name="tick">The current turn value.</param>
        /// <returns>The compatible game.</returns>
        private static GameRoot CreateCompatibleGame(ContentPack contentPack, int tick)
        {
            return new GameRoot
            {
                CurrentTick = tick,
                Summary = new GameSummary
                {
                    PackID = contentPack.Definition.ID,
                    PackVersion = contentPack.Definition.Version,
                    ScenarioID = contentPack.Scenario.ID,
                    ModIDs = contentPack.FileResolver.Mods.Select(mod => mod.ID).ToArray(),
                    ModVersions = contentPack
                        .FileResolver.Mods.Select(mod => mod.Version)
                        .ToArray(),
                },
            };
        }
    }
}
