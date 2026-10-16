using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Simulation;
using UnityEngine;

namespace Rebellion.Tests.App
{
    [TestFixture]
    public sealed class AppBootstrapTests
    {
        private AppBootstrap _bootstrap;
        private GameObject _gameObject;
        private string _packDirectory;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
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
            LoadingScreenController loadingController =
                Object.FindAnyObjectByType<LoadingScreenController>(FindObjectsInactive.Include);
            if (loadingController != null)
                Object.DestroyImmediate(loadingController.gameObject);
            Object.DestroyImmediate(_gameObject);
            DestroyAudioManagers();
            if (_packDirectory != null && Directory.Exists(_packDirectory))
                Directory.Delete(_packDirectory, true);
            _packDirectory = null;
            GameLaunchContext.Reset(TestContent.Pack);
        }

        [Test]
        public void PrepareContentReloadAsync_DerivedPack_LoadsSelectedPackWithoutReplacingActiveContent()
        {
            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");
            ContentPack active = _bootstrap.GetContentPack();
            string packID = CreateDerivedPack(active);

            ContentPack selected = _bootstrap
                .PrepareContentReloadAsync(packID, System.Array.Empty<string>())
                .GetAwaiter()
                .GetResult();

            Assert.AreEqual(packID, selected.Definition.ID);
            Assert.AreEqual(active.Scenario.ID, selected.Scenario.ID);
            Assert.AreSame(active, _bootstrap.GetContentPack());
            Assert.IsFalse(LoadingScreenController.IsLoadInProgress);
        }

        [Test]
        public void PrepareContentReloadAsync_MissingPack_RejectsSelectionWithoutFallingBack()
        {
            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");
            ContentPack active = _bootstrap.GetContentPack();

            Assert.Throws<System.InvalidOperationException>(() =>
                _bootstrap
                    .PrepareContentReloadAsync("not-installed", System.Array.Empty<string>())
                    .GetAwaiter()
                    .GetResult()
            );

            Assert.AreSame(active, _bootstrap.GetContentPack());
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
            _bootstrap.SaveLoadStarting += () => transitions++;

            bool started = _bootstrap.ReloadContent(
                _bootstrap.GetContentPack().Definition.ID,
                System.Array.Empty<string>()
            );

            Assert.IsTrue(started);
            Assert.IsTrue(LoadingScreenController.IsLoadInProgress);
            Assert.AreEqual(1, transitions);
            Assert.IsFalse(GameLaunchContext.IsLoadGame);
            Assert.IsNull(GameLaunchContext.SaveFileName);
            Assert.IsNotNull(GameLaunchContext.ConsumeContentPackAfterRestart());
            LoadingScreenController.CompletePendingLoad();
        }

        [Test]
        public void ReloadContent_PendingTransition_RejectsDuplicateRequest()
        {
            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");
            string packID = _bootstrap.GetContentPack().Definition.ID;
            Assert.IsTrue(_bootstrap.ReloadContent(packID, System.Array.Empty<string>()));

            Assert.IsFalse(_bootstrap.ReloadContent(packID, System.Array.Empty<string>()));

            GameLaunchContext.ConsumeContentPackAfterRestart();
            LoadingScreenController.CompletePendingLoad();
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
        public void InitializeRuntimeCore_PendingSaveAfterContentRestart_RestoresLoadRequest()
        {
            GameLaunchContext.PrepareSaveLoadAfterContentRestart("campaign", TestContent.Pack);

            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");

            Assert.AreEqual("campaign", GameLaunchContext.SaveFileName);
            Assert.IsTrue(GameLaunchContext.IsLoadGame);
            Assert.AreSame(TestContent.Pack, _bootstrap.GetContentPack());
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
            _bootstrap.SaveLoadStarting += () => transitionCount++;
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

                Assert.IsTrue(LoadingScreenController.CompleteFailedRecovery(null));

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
