using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
            GameLaunchContext.Reset(TestContent.Pack);
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
