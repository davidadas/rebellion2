using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Events;
using Rebellion.Generation;
using Rebellion.Util.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Rebellion.Tests.App
{
    [TestFixture]
    public sealed class ContentLoadIntegrationTests
    {
        private const string _settingsBackupKey = "ContentLoadIntegrationTests.SettingsBackup";

        /// <summary>
        /// Enters play mode in an empty scene so only this test creates application services.
        /// </summary>
        /// <returns>The play-mode summary sequence.</returns>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            string settingsPath = Path.Combine(
                Application.persistentDataPath,
                "user-settings.json"
            );
            SessionState.SetString(
                _settingsBackupKey,
                File.Exists(settingsPath)
                    ? System.Convert.ToBase64String(File.ReadAllBytes(settingsPath))
                    : "-"
            );
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
        }

        /// <summary>
        /// Releases application services before leaving play mode to avoid a shutdown settings save.
        /// </summary>
        /// <returns>The cleanup sequence.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!Application.isPlaying)
                yield break;

            foreach (
                LoadingScreenController loading in Object.FindObjectsByType<LoadingScreenController>(
                    FindObjectsInactive.Include
                )
            )
                Object.Destroy(loading.gameObject);
            Scene strategy = SceneManager.GetSceneByName("StrategyView");
            if (strategy.IsValid() && strategy.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("ContentLoadTestCleanup"));
                yield return SceneManager.UnloadSceneAsync(strategy);
            }
            DestroyApplication();
            yield return null;
            yield return new ExitPlayMode();
            string settingsPath = Path.Combine(
                Application.persistentDataPath,
                "user-settings.json"
            );
            string settingsBackup = SessionState.GetString(_settingsBackupKey, "-");
            if (settingsBackup == "-")
                File.Delete(settingsPath);
            else
                File.WriteAllBytes(settingsPath, System.Convert.FromBase64String(settingsBackup));
            SessionState.EraseString(_settingsBackupKey);
        }

        [UnityTest]
        public IEnumerator EnsureExists_PreparedContent_RetainsExactPackAcrossReplacementAndRecovery()
        {
            AppBootstrap previous = AppBootstrap.EnsureExists();
            ContentPack previousPack = previous.GetContentPack();
            ContentLoadOperation operation = previous.CreateContentLoadOperation();
            Task preparation = operation.PrepareContentReloadAsync(
                previousPack.Definition.ID,
                previous.GetUserSettingsManager().Settings.Content.DisabledModIDs
            );
            while (!preparation.IsCompleted)
                yield return null;
            preparation.GetAwaiter().GetResult();
            ContentPack candidate = operation.Pack;
            Assert.AreNotSame(previousPack, candidate);

            DestroyApplication();
            yield return null;
            AppBootstrap replacement = AppBootstrap.EnsureExists(candidate);

            Assert.AreSame(candidate, replacement.GetContentPack());
            Assert.AreEqual(candidate.Definition.ID, GameLaunchContext.Summary.PackID);
            Assert.IsTrue(operation.BeginRecovery());

            DestroyApplication();
            yield return null;
            AppBootstrap restored = AppBootstrap.EnsureExists(operation.Pack);
            operation.Complete(restored.GetContentPack(), restored.GetUserSettingsManager());

            Assert.AreSame(previousPack, restored.GetContentPack());
            Assert.IsFalse(GameLaunchContext.IsLoadGame);
            Assert.IsNull(GameLaunchContext.SaveFileName);
            Assert.IsFalse(operation.BeginRecovery());
        }

        [UnityTest]
        public IEnumerator StartCustomGame_ReplacedContent_PreparesStrategyBeforePlayingIntro()
        {
            SceneManager.sceneLoaded += DisableSceneRendering;
            try
            {
                AppBootstrap previous = AppBootstrap.EnsureExists();
                ContentPack active = previous.GetContentPack();
                ContentPack selected = ContentPackLoader.OpenExact(
                    active.ContentRootPath,
                    active.Definition.ID,
                    active.Scenario.ID,
                    previous.GetUserSettingsManager().Settings.Content.DisabledModIDs
                );
                GameSummary summary = GameLaunchContext.CreateDefaultSummary(selected);
                summary.GalaxySize = GameSize.Small;
                summary.Difficulty = GameDifficulty.Hard;
                summary.CustomSettings = new CustomGameSettings
                {
                    GenerationConfig = selected.GameData.GenerationConfig.GetDeepCopy(),
                };
                summary.CustomSettings.GenerationConfig.DisabledEventCategories =
                    new List<GameEventCategory> { GameEventCategory.Story };

                LoadingScreenController.StartCustomGame(previous, selected, summary);

                LoadingScreenController loading =
                    Object.FindAnyObjectByType<LoadingScreenController>();
                DisableRendering(loading.gameObject);
                Button continueButton = loading
                    .GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "ContinueButton");
                float deadline = Time.realtimeSinceStartup + 60f;
                while (
                    !continueButton.gameObject.activeInHierarchy
                    && Time.realtimeSinceStartup < deadline
                )
                    yield return null;

                Assert.IsTrue(
                    continueButton.gameObject.activeInHierarchy,
                    "The custom game never became ready."
                );
                Assert.AreSame(selected, AppBootstrap.Instance.GetContentPack());
                Assert.IsTrue(AppBootstrap.Instance.GetRuntime().HasActiveGame);
                GameRoot game = AppBootstrap.Instance.GetRuntime().GetActiveGameSession().Game;
                Assert.AreEqual(GameDifficulty.Hard, game.Summary.Difficulty);
                Assert.IsFalse(
                    game.GetEventPool()
                        .Any(gameEvent => gameEvent.Category == GameEventCategory.Story)
                );
                Assert.IsTrue(
                    game.GetEventPool()
                        .Any(gameEvent => gameEvent.Category == GameEventCategory.Disaster)
                );
                Assert.IsNull(
                    Object.FindAnyObjectByType<CutscenePlayer>(),
                    "The intro must wait until Continue releases the loading overlay."
                );
            }
            finally
            {
                SceneManager.sceneLoaded -= DisableSceneRendering;
            }
        }

        [UnityTest]
        public IEnumerator EnsureExists_ReconstructedApplication_CreatesCutscenePlayersAtEveryStage()
        {
            AppBootstrap previous = AppBootstrap.EnsureExists();
            ContentPack previousPack = previous.GetContentPack();
            AssertCutscenePlayback(previous);

            ContentLoadOperation operation = previous.CreateContentLoadOperation();
            Task preparation = operation.PrepareContentReloadAsync(
                previousPack.Definition.ID,
                previous.GetUserSettingsManager().Settings.Content.DisabledModIDs
            );
            while (!preparation.IsCompleted)
                yield return null;
            preparation.GetAwaiter().GetResult();

            DestroyApplication();
            yield return null;
            AppBootstrap replacement = AppBootstrap.EnsureExists(operation.Pack);
            AssertCutscenePlayback(replacement);

            Assert.IsTrue(operation.BeginRecovery());
            DestroyApplication();
            yield return null;
            AppBootstrap restored = AppBootstrap.EnsureExists(operation.Pack);
            AssertCutscenePlayback(restored);
        }

        [UnityTest]
        public IEnumerator ActivatePresentationAsync_ApplicationDestroyedDuringActivation_CancelsPendingInitialization()
        {
            yield return AssertActivationCanceled(destroyApplication: true);
        }

        [UnityTest]
        public IEnumerator ActivatePresentationAsync_SceneDestroyedDuringActivation_CancelsPendingInitialization()
        {
            yield return AssertActivationCanceled(destroyApplication: false);
        }

        [UnityTest]
        public IEnumerator PreloadAsync_ApplicationDestroyedDuringAudioLoad_CancelsPendingPreload()
        {
            AppBootstrap bootstrap = AppBootstrap.EnsureExists();
            ContentAssets assets = bootstrap.GetContentAssets();
            using CancellationTokenSource cancellation =
                CancellationTokenSource.CreateLinkedTokenSource(bootstrap.destroyCancellationToken);
            ContentPreloadManifest manifest = new ContentPreloadManifest
            {
                TexturesPerFrame = 1,
                Audio = { "Application/Audio/pending" },
            };
            Task preload = assets.PreloadAsync(manifest, cancellation.Token);

            DestroyApplication();
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!preload.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(preload.IsCanceled, preload.Exception?.ToString());
        }

        /// <summary>
        /// Verifies that a caller-owned scene lifetime cancels pending strategy activation.
        /// </summary>
        /// <param name="destroyApplication">Whether to destroy the application or the strategy scene root.</param>
        /// <returns>The cancellation regression sequence.</returns>
        private IEnumerator AssertActivationCanceled(bool destroyApplication)
        {
            AppBootstrap bootstrap = AppBootstrap.EnsureExists();
            Task preload = Task.WhenAll(
                bootstrap.InitializeMainMenuContentAsync(),
                bootstrap.InitializeStrategyContentAsync()
            );
            while (!preload.IsCompleted)
                yield return null;
            preload.GetAwaiter().GetResult();

            ContentPack pack = bootstrap.GetContentPack();
            GameSummary summary = GameLaunchContext.CreateDefaultSummary(pack);
            summary.GalaxySize = GameSize.Small;
            GameRoot game = new GameBuilder(summary, pack.GameData).Build();
            GameRuntime runtime = bootstrap.GetRuntime();
            Rebellion.Simulation.GameSession session = runtime.StartGame(game);
            ContentAssets assets = bootstrap.GetContentAssets();
            GameObject root = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab"
                )
            );
            try
            {
                root.GetComponentInChildren<GameFlowController>(true).enabled = false;
                DisableRendering(root);
                StrategyController controller = root.GetComponentInChildren<StrategyController>(
                    true
                );
                UIContext context = new UIContext(
                    game,
                    new FactionThemeLibrary(pack.GameData.FactionThemes),
                    new EncyclopediaCatalog(System.Array.Empty<EncyclopediaEntry>()),
                    assets.GetTexture,
                    assets.GetTextureContentBounds
                );
                using CancellationTokenSource cancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        controller.destroyCancellationToken,
                        bootstrap.destroyCancellationToken
                    );
                controller.Initialize(
                    session,
                    runtime.GetActiveGameManager(),
                    runtime,
                    context,
                    cancellation.Token
                );

                Task activation = controller.ActivatePresentationAsync(startMusic: false);
                if (destroyApplication)
                    DestroyApplication();
                else
                    Object.Destroy(root);

                float deadline = Time.realtimeSinceStartup + 5f;
                while (!activation.IsCompleted && Time.realtimeSinceStartup < deadline)
                    yield return null;

                Assert.IsTrue(activation.IsCanceled, activation.Exception?.ToString());
            }
            finally
            {
                if (root != null)
                    Object.Destroy(root);
            }
        }

        /// <summary>
        /// Prevents scene rendering from adding GPU work to a load-lifecycle integration test.
        /// </summary>
        /// <param name="scene">The scene whose game objects were initialized.</param>
        /// <param name="mode">The scene-loading mode.</param>
        private static void DisableSceneRendering(Scene scene, LoadSceneMode mode)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                DisableRendering(root);
        }

        /// <summary>
        /// Disables GPU rendering on every graphics backend without changing object lifecycle state.
        /// </summary>
        /// <param name="root">The loading presentation or scene root under test.</param>
        private static void DisableRendering(GameObject root)
        {
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                camera.enabled = false;
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                canvas.enabled = false;
        }

        /// <summary>
        /// Exercises player creation through the same runtime resource binding used in player builds.
        /// </summary>
        /// <param name="bootstrap">The initialized application whose playback must work.</param>
        private static void AssertCutscenePlayback(AppBootstrap bootstrap)
        {
            VideoClip clip = AssetDatabase.LoadAssetAtPath<VideoClip>(
                "Assets/Tests/EditMode/UnitTests/UI/SceneUI/Cutscenes/CutsceneTestClip.webm"
            );
            Assert.IsNotNull(clip);
            CutsceneManager manager = bootstrap.GetCutsceneManager();
            try
            {
                Assert.DoesNotThrow(() => manager.Play(clip, null));
                Assert.IsNotNull(Object.FindAnyObjectByType<CutscenePlayer>());
            }
            finally
            {
                manager.Play((VideoClip)null, null);
            }
        }

        /// <summary>
        /// Schedules service destruction using the same Unity lifetime boundary as the loading controller.
        /// </summary>
        private static void DestroyApplication()
        {
            if (AudioManager.Instance != null)
                Object.Destroy(AudioManager.Instance.gameObject);
            if (AppBootstrap.Instance != null)
                Object.Destroy(AppBootstrap.Instance.gameObject);
        }
    }
}
