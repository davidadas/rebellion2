using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Multiplayer;
using Rebellion.Util.IO;
using Rebellion.Util.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
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
                    ? System.Convert.ToBase64String(
                        Encoding.UTF8.GetBytes(AtomicTextFile.ReadAllText(settingsPath))
                    )
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
                AtomicTextFile.WriteAllText(
                    settingsPath,
                    Encoding.UTF8.GetString(System.Convert.FromBase64String(settingsBackup))
                );
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
        public IEnumerator RecoverInterruptedMultiplayerLoad_CommittedReady_ReturnsToMenuWithCurrentContent()
        {
            yield return RecoverCommittedMultiplayerLoad(false);
        }

        [UnityTest]
        public IEnumerator RecoverInterruptedMultiplayerLoad_ContinueDismissed_ReturnsToMenuWithCurrentContent()
        {
            yield return RecoverCommittedMultiplayerLoad(true);
        }

        /// <summary>
        /// Interrupts initial multiplayer reconstruction at its actual application-destruction boundary.
        /// </summary>
        /// <returns>The recovery sequence verifying one service replacement and one menu navigation.</returns>
        [UnityTest]
        public IEnumerator RecoverInterruptedMultiplayerLoad_ReconstructingInitialLaunch_ReturnsToMenuOnce()
        {
            int menuLoads = 0;
            int strategyLoads = 0;
            bool recoveryAccepted = false;
            BootstrapDestructionObserver observer = null;
            System.Action requestRecovery = () =>
                recoveryAccepted = AppBootstrap.RecoverInterruptedMultiplayerLoad();
            List<AppBootstrap> replacements = new();
            UnityAction<Scene, LoadSceneMode> observeScene = (scene, mode) =>
            {
                DisableSceneRendering(scene, mode);
                if (scene.name == "MainMenu")
                    menuLoads++;
                if (scene.name == "StrategyView")
                    strategyLoads++;
            };
            SceneManager.sceneLoaded += observeScene;
            try
            {
                AppBootstrap previous = AppBootstrap.EnsureExists();
                ContentPack previousPack = previous.GetContentPack();
                ContentPack selected = ContentPackLoader.OpenExact(
                    previousPack.ContentRootPath,
                    previousPack.Definition.ID,
                    previousPack.Scenario.ID,
                    previous.GetUserSettingsManager().Settings.Content.DisabledModIDs
                );
                GameSummary summary = GameLaunchContext.CreateDefaultSummary(selected);
                summary.GalaxySize = GameSize.Small;
                string hostFactionID = selected.Scenario.DefaultPlayerFactionID;
                string guestFactionID = selected.Scenario.PlayableFactionIDs.First(id =>
                    id != hostFactionID
                );
                observer = previous.gameObject.AddComponent<BootstrapDestructionObserver>();
                observer.Destroyed += requestRecovery;

                LoadingScreenController.StartMultiplayer(
                    previous,
                    selected,
                    new MultiplayerLaunchConfiguration(
                        summary,
                        hostFactionID,
                        hostFactionID,
                        guestFactionID
                    )
                );
                LoadingScreenController loading =
                    Object.FindAnyObjectByType<LoadingScreenController>();
                DisableRendering(loading.gameObject);

                float deadline = Time.realtimeSinceStartup + 60f;
                while (
                    (
                        !recoveryAccepted
                        || SceneManager.GetActiveScene().name != "MainMenu"
                        || LoadingScreenController.IsLoadInProgress
                    )
                    && Time.realtimeSinceStartup < deadline
                )
                {
                    AppBootstrap current = AppBootstrap.Instance;
                    if (
                        current != null
                        && !ReferenceEquals(current, previous)
                        && !replacements.Any(item => ReferenceEquals(item, current))
                    )
                        replacements.Add(current);
                    yield return null;
                }

                Assert.IsTrue(
                    recoveryAccepted,
                    "Reconstruction never reached its destruction boundary."
                );
                Assert.IsFalse(LoadingScreenController.IsLoadInProgress);
                Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);
                Assert.AreEqual(1, replacements.Count);
                Assert.AreEqual(1, menuLoads);
                Assert.AreEqual(0, strategyLoads);
                Assert.AreSame(previousPack, AppBootstrap.Instance.GetContentPack());
                Assert.IsFalse(AppBootstrap.Instance.GetRuntime().HasActiveGame);
                Assert.IsFalse(GameLaunchContext.IsMultiplayer);
                Assert.IsFalse(GameLaunchContext.IsLoadGame);
                Assert.IsNull(GameLaunchContext.LocalPlayerFactionID);
                Assert.IsNull(GameLaunchContext.SaveFileName);
            }
            finally
            {
                if (observer != null)
                    observer.Destroyed -= requestRecovery;
                SceneManager.sceneLoaded -= observeScene;
            }
        }

        /// <summary>
        /// Interrupts an accepted Continue fade before its deferred gameplay activation can execute.
        /// </summary>
        /// <returns>The recovery sequence verifying obsolete activation callbacks remain invalid.</returns>
        [UnityTest]
        public IEnumerator RecoverInterruptedMultiplayerLoad_ContinueFading_DiscardsActivationCallback()
        {
            yield return RecoverCommittedMultiplayerLoad(true, true);
        }

        /// <summary>
        /// Exercises committed-load interruption through the actual ready view and application scene router.
        /// </summary>
        /// <param name="dismissContinue">Whether the local player has already dismissed the loading view.</param>
        /// <param name="interruptDuringFade">Whether recovery supersedes the accepted Continue before its exit fade finishes.</param>
        /// <returns>The real scene-transition verification sequence.</returns>
        private static IEnumerator RecoverCommittedMultiplayerLoad(
            bool dismissContinue,
            bool interruptDuringFade = false
        )
        {
            SceneManager.sceneLoaded += DisableSceneRendering;
            List<(FactionTheme Theme, string IntroPath)> introPaths = new();
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
                foreach (FactionTheme theme in selected.GameData.FactionThemes)
                {
                    introPaths.Add((theme, theme.IntroCutscenePath));
                    theme.IntroCutscenePath = null;
                }
                GameSummary summary = GameLaunchContext.CreateDefaultSummary(selected);
                summary.GalaxySize = GameSize.Small;
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
                    "The replacement strategy never became ready."
                );
                AppBootstrap current = AppBootstrap.Instance;
                GameRuntime runtime = current.GetRuntime();
                Assert.IsTrue(runtime.HasActiveGame);
                runtime.GetActiveGameManager().ApplyGameSpeed(TickSpeed.Paused);
                int activationCallbacks = 0;
                int continueCallbacks = 0;
                Assert.IsTrue(
                    LoadingScreenController.CompletePendingLoad(
                        current,
                        () => activationCallbacks++,
                        continueAccepted: () => continueCallbacks++
                    )
                );
                if (dismissContinue)
                {
                    continueButton.onClick.Invoke();
                    if (interruptDuringFade)
                        Assert.IsTrue(LoadingScreenController.IsLoadInProgress);
                    else
                    {
                        deadline = Time.realtimeSinceStartup + 15f;
                        while (
                            LoadingScreenController.IsLoadInProgress
                            && Time.realtimeSinceStartup < deadline
                        )
                            yield return null;
                        Assert.IsFalse(
                            LoadingScreenController.IsLoadInProgress,
                            "Continue never released the loading presentation."
                        );
                    }
                }
                string packID = current.GetUserSettingsManager().Settings.Content.ActivePackID;
                string scenarioID = current
                    .GetUserSettingsManager()
                    .Settings.Content.ActiveScenarioID;
                string[] disabledMods = current
                    .GetUserSettingsManager()
                    .Settings.Content.DisabledModIDs.ToArray();
                string hostFactionID = selected.Scenario.DefaultPlayerFactionID;
                string guestFactionID = selected.Scenario.PlayableFactionIDs.First(id =>
                    id != hostFactionID
                );
                GameLaunchContext.PrepareMultiplayer(
                    new MultiplayerLaunchConfiguration(
                        summary,
                        hostFactionID,
                        hostFactionID,
                        guestFactionID
                    )
                );

                Assert.IsTrue(AppBootstrap.RecoverInterruptedMultiplayerLoad());

                Assert.IsFalse(runtime.HasActiveGame);
                Assert.IsFalse(GameLaunchContext.IsMultiplayer);
                Assert.IsFalse(GameLaunchContext.IsLoadGame);
                Assert.IsNull(GameLaunchContext.LocalPlayerFactionID);
                Assert.IsNull(GameLaunchContext.SaveFileName);
                deadline = Time.realtimeSinceStartup + 60f;
                while (
                    (
                        SceneManager.GetActiveScene().name != "MainMenu"
                        || LoadingScreenController.IsLoadInProgress
                    )
                    && Time.realtimeSinceStartup < deadline
                )
                    yield return null;
                Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);
                Assert.IsFalse(LoadingScreenController.IsLoadInProgress);
                Assert.AreSame(current, AppBootstrap.Instance);
                Assert.AreSame(selected, AppBootstrap.Instance.GetContentPack());
                Assert.AreEqual(
                    packID,
                    current.GetUserSettingsManager().Settings.Content.ActivePackID
                );
                Assert.AreEqual(
                    scenarioID,
                    current.GetUserSettingsManager().Settings.Content.ActiveScenarioID
                );
                CollectionAssert.AreEqual(
                    disabledMods,
                    current.GetUserSettingsManager().Settings.Content.DisabledModIDs
                );
                Assert.IsFalse(current.GetRuntime().HasActiveGame);
                Assert.AreEqual(
                    dismissContinue && !interruptDuringFade ? 1 : 0,
                    activationCallbacks
                );
                Assert.AreEqual(dismissContinue ? 1 : 0, continueCallbacks);
            }
            finally
            {
                foreach ((FactionTheme theme, string introPath) in introPaths)
                    theme.IntroCutscenePath = introPath;
                SceneManager.sceneLoaded -= DisableSceneRendering;
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

        /// <summary>
        /// Exposes the real destruction boundary of the bootstrap being replaced by a loading operation.
        /// </summary>
        private sealed class BootstrapDestructionObserver : MonoBehaviour
        {
            public event System.Action Destroyed;

            /// <summary>
            /// Notifies the integration test while the outgoing application's destruction completes.
            /// </summary>
            private void OnDestroy()
            {
                Destroyed?.Invoke();
            }
        }
    }
}
