using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game;
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
        /// <returns>The play-mode setup sequence.</returns>
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

            LoadingScreenController.StartCustomGame(previous, selected, summary);

            LoadingScreenController loading = Object.FindAnyObjectByType<LoadingScreenController>();
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                foreach (Camera camera in loading.GetComponentsInChildren<Camera>(true))
                    camera.enabled = false;
            }
            Button continueButton = loading
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "ContinueButton");
            float deadline = Time.realtimeSinceStartup + 60f;
            while (
                !continueButton.gameObject.activeInHierarchy && Time.realtimeSinceStartup < deadline
            )
                yield return null;

            Assert.IsTrue(
                continueButton.gameObject.activeInHierarchy,
                "The custom game never became ready."
            );
            Assert.AreSame(selected, AppBootstrap.Instance.GetContentPack());
            Assert.IsTrue(AppBootstrap.Instance.GetRuntime().HasActiveGame);
            Assert.AreEqual(
                GameDifficulty.Hard,
                AppBootstrap.Instance.GetRuntime().GetActiveGameSession().Game.Summary.Difficulty
            );
            Assert.IsNull(
                Object.FindAnyObjectByType<CutscenePlayer>(),
                "The intro must wait until Continue releases the loading overlay."
            );
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
