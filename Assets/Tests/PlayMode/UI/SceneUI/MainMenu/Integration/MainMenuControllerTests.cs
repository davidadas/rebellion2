using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Multiplayer;
using Rebellion.UI.SceneUI.MainMenu.Multiplayer;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rebellion.Tests.UI.SceneUI.MainMenu.Integration
{
    [TestFixture]
    public sealed class MainMenuControllerTests
    {
        private const string _settingsBackupKey = "MainMenuControllerAgreementTests.SettingsBackup";
        private GameObject _gameObject;
        private MainMenuView _view;
        private LegalDocumentsView _documents;
        private UserSettingsManager _settings;
        private Button _multiplayer;
        private Button _accept;
        private string _blockedSettingsPath;

        /// <summary>
        /// Starts the authored controls with isolated consent state and suspended decorative rendering.
        /// </summary>
        /// <returns>The menu startup sequence.</returns>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            string path = Path.Combine(UserDataPaths.Root, "user-settings.json");
            SessionState.SetString(
                _settingsBackupKey,
                File.Exists(path) ? Convert.ToBase64String(File.ReadAllBytes(path)) : "-"
            );
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            global::UserSettings settings = new global::UserSettings();
            settings.Analytics.Consent = UserAnalyticsConsent.Denied;
            File.WriteAllText(path, JsonUtility.ToJson(settings));
            _blockedSettingsPath = null;
            yield return StartMenu();
        }

        /// <summary>
        /// Releases the menu and runtime services, then restores the user's settings for the next test.
        /// </summary>
        /// <returns>The cleanup sequence.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_blockedSettingsPath != null && Directory.Exists(_blockedSettingsPath))
                Directory.Delete(_blockedSettingsPath);
            if (Application.isPlaying)
            {
                Object.Destroy(_gameObject);
                if (AppBootstrap.Instance != null)
                    Object.Destroy(AppBootstrap.Instance.gameObject);
                if (MultiplayerCoordinator.Instance != null)
                    Object.Destroy(MultiplayerCoordinator.Instance.gameObject);
                yield return null;
            }
            else
                Object.DestroyImmediate(_gameObject);

            string backup = SessionState.GetString(_settingsBackupKey, "not-backed-up");
            if (backup != "not-backed-up")
            {
                string path = Path.Combine(UserDataPaths.Root, "user-settings.json");
                if (backup == "-")
                    File.Delete(path);
                else
                    File.WriteAllBytes(path, Convert.FromBase64String(backup));
                SessionState.EraseString(_settingsBackupKey);
            }
            GameLaunchContext.Reset(TestContent.Pack);
        }

        [Test]
        public void MultiplayerRequested_MatchingSavedVersion_OpensWithoutPrompt()
        {
            _settings.AcceptAgreement(_documents.Agreement);
            _settings.Load();

            _multiplayer.onClick.Invoke();

            Assert.IsFalse(_documents.IsVisible);
            AssertMultiplayerOpen();
        }

        [Test]
        public void MultiplayerRequested_SignedOut_ShowsSignInPromptAndBrowserGuidance()
        {
            _settings.AcceptAgreement(_documents.Agreement);
            _settings.Load();

            _multiplayer.onClick.Invoke();

            MultiplayerMenuOverlay overlay = GetMultiplayerOverlay();
            ConfirmationDialogView confirmation = _view
                .OptionsWindowManager.Windows.Select(window =>
                    window.GetComponentInChildren<ConfirmationDialogView>(true)
                )
                .Single(dialog => dialog != null);
            Assert.AreEqual(
                "You are not signed in. Sign in now to play online?",
                confirmation
                    .GetComponentsInChildren<TMP_Text>(true)
                    .Single(text => text.name == "MessageTextField")
                    .text
            );
            Assert.IsTrue(
                overlay
                    .GetComponentsInChildren<TMP_Text>(true)
                    .Any(text => text.text == "Sign in to play online.")
            );
            Assert.IsFalse(
                overlay
                    .GetComponentsInChildren<TMP_Text>(true)
                    .Any(text => text.text == "No games found.")
            );
        }

        [Test]
        public void MultiplayerRequested_ChangedAgreement_RequiresAcceptanceAgain()
        {
            _settings.AcceptAgreement(
                new PlayerAgreement(
                    _documents.Agreement.Text + "\nEarlier agreement",
                    _documents.Agreement.PrivacyNotice
                )
            );
            _settings.Load();

            _multiplayer.onClick.Invoke();

            Assert.IsTrue(_documents.IsVisible);
            Assert.IsEmpty(_view.OptionsWindowManager.Windows);
            Assert.IsFalse(_documents.Agreement.IsAccepted(_settings.Settings));
        }

        [UnityTest]
        public IEnumerator MultiplayerClosed_SameFrameConnectionChange_DoesNotRenderClosedBrowser()
        {
            _settings.AcceptAgreement(_documents.Agreement);
            _multiplayer.onClick.Invoke();
            AssertMultiplayerOpen();

            MainMenuController controller =
                _gameObject.GetComponentInChildren<MainMenuController>();
            if (
                _view.OptionsWindowManager.Windows.Any(window =>
                    window.GetComponentInChildren<ConfirmationDialogView>(true) != null
                )
            )
                Assert.IsTrue(controller.TryCancel());
            Assert.IsTrue(controller.TryCancel());
            Assert.DoesNotThrow(MultiplayerCoordinator.Instance.Disconnect);
            yield return null;

            Assert.IsEmpty(_view.OptionsWindowManager.Windows);
        }

        [Test]
        public void MultiplayerRequested_ChangedPrivacyNotice_RequiresAcceptanceAgain()
        {
            _settings.AcceptAgreement(
                new PlayerAgreement(
                    _documents.Agreement.Text,
                    _documents.Agreement.PrivacyNotice + "\nEarlier privacy notice"
                )
            );
            _settings.Load();

            _multiplayer.onClick.Invoke();

            Assert.IsTrue(_documents.IsVisible);
            Assert.IsEmpty(_view.OptionsWindowManager.Windows);
            Assert.IsFalse(_documents.Agreement.IsAccepted(_settings.Settings));
        }

        [Test]
        public void MultiplayerAccepted_BothDocumentsAccepted_PersistsBeforeOpening()
        {
            _multiplayer.onClick.Invoke();

            _accept.onClick.Invoke();

            Assert.IsTrue(_documents.IsVisible);
            Assert.IsEmpty(_view.OptionsWindowManager.Windows);
            Assert.IsFalse(_documents.Agreement.IsAccepted(_settings.Settings));

            _accept.onClick.Invoke();

            Assert.IsFalse(_documents.IsVisible);
            AssertMultiplayerOpen();
            AssertAcceptanceSaved();
        }

        [UnityTest]
        public IEnumerator MultiplayerAccepted_NewApplicationInstance_OpensWithoutPrompt()
        {
            _multiplayer.onClick.Invoke();
            _accept.onClick.Invoke();
            _accept.onClick.Invoke();
            _settings.Save();
            Object.Destroy(_gameObject);
            Object.Destroy(AppBootstrap.Instance.gameObject);
            Object.Destroy(MultiplayerCoordinator.Instance.gameObject);
            yield return null;

            yield return StartMenu();
            _multiplayer.onClick.Invoke();

            Assert.IsFalse(_documents.IsVisible);
            AssertMultiplayerOpen();
            AssertAcceptanceSaved();
        }

        [Test]
        public void MultiplayerAccepted_PersistenceFailure_KeepsClosedUntilSuccessfulRetry()
        {
            _multiplayer.onClick.Invoke();
            string path = _settings.GetSettingsFilePath();
            File.Delete(path);
            Directory.CreateDirectory(path);
            _blockedSettingsPath = path;

            _accept.onClick.Invoke();
            LogAssert.Expect(LogType.Error, new Regex("IOException|UnauthorizedAccessException"));
            _accept.onClick.Invoke();

            Assert.IsTrue(_documents.IsVisible);
            Assert.IsEmpty(_view.OptionsWindowManager.Windows);
            Assert.IsFalse(_documents.Agreement.IsAccepted(_settings.Settings));
            Assert.AreEqual(
                "Your acceptance could not be saved. Please try again.",
                _documents
                    .transform.Find("DocumentPanel/Console/Status")
                    .GetComponent<TMP_Text>()
                    .text
            );

            Directory.Delete(path);
            _blockedSettingsPath = null;
            _accept.onClick.Invoke();

            Assert.IsFalse(_documents.IsVisible);
            AssertMultiplayerOpen();
            AssertAcceptanceSaved();
        }

        /// <summary>
        /// Starts a fresh runtime and the authored menu using the current saved settings.
        /// </summary>
        /// <returns>The menu initialization sequence.</returns>
        private IEnumerator StartMenu()
        {
            AppBootstrap.EnsureExists(TestContent.Pack);
            _gameObject = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/UI/MainMenu/MainMenuRoot.prefab"
                )
            );
            foreach (Camera camera in _gameObject.GetComponentsInChildren<Camera>(true))
                camera.enabled = false;
            _view = _gameObject.GetComponentInChildren<MainMenuView>(true);
            _documents = _gameObject.GetComponentInChildren<LegalDocumentsView>(true);
            _settings = AppBootstrap.Instance.GetUserSettingsManager();
            _multiplayer = _gameObject
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "MultiplayerButton");
            _accept = _documents
                .transform.Find("DocumentPanel/Console/AcceptButton")
                .GetComponent<Button>();
            Canvas canvas = _gameObject.transform.Find("UI/Canvas").GetComponent<Canvas>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (!canvas.enabled && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(canvas.enabled, "The main-menu controller did not finish starting.");
            Assert.IsFalse(_documents.IsVisible);
        }

        /// <summary>
        /// Checks that the controller created the actual multiplayer window.
        /// </summary>
        private void AssertMultiplayerOpen()
        {
            Assert.IsNotNull(GetMultiplayerOverlay());
        }

        /// <summary>
        /// Returns the multiplayer overlay hosted by the menu's current window stack.
        /// </summary>
        /// <returns>The open multiplayer overlay.</returns>
        private MultiplayerMenuOverlay GetMultiplayerOverlay()
        {
            return _view
                .OptionsWindowManager.Windows.Select(window =>
                    window.GetComponentInChildren<MultiplayerMenuOverlay>(true)
                )
                .Single(overlay => overlay != null);
        }

        /// <summary>
        /// Reads the persisted user preferences independently of the live settings instance.
        /// </summary>
        private void AssertAcceptanceSaved()
        {
            global::UserSettings saved = JsonUtility.FromJson<global::UserSettings>(
                File.ReadAllText(_settings.GetSettingsFilePath())
            );
            Assert.AreEqual(_documents.Agreement.Version, saved.AcceptedAgreementVersion);
        }
    }
}
