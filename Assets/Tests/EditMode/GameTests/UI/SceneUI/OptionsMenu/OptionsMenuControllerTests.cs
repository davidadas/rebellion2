using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.OptionsMenu
{
    [TestFixture]
    public sealed class OptionsMenuControllerTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/OptionsMenu/OptionsMenu.prefab";

        private GameObject _bootstrapObject;
        private GameObject _windowRoot;
        private AppBootstrap _bootstrap;
        private UIWindowManager _windowManager;
        private OptionsMenuController _controller;
        private SaveGameManager _saveGameManager;
        private string _saveDirectoryPath;
        private int _dirtyCount;
        private bool _loadResult;
        private string _loadedFileName;

        [SetUp]
        public void SetUp()
        {
            DestroyAudioManagers();
            GameLaunchContext.Reset(TestContent.Pack);
            _saveDirectoryPath = Path.Combine(
                Path.GetTempPath(),
                nameof(OptionsMenuControllerTests),
                Guid.NewGuid().ToString("N")
            );
            _saveGameManager = new SaveGameManager(_saveDirectoryPath);

            _bootstrapObject = new GameObject("OptionsBootstrapUnderTest");
            _bootstrapObject.SetActive(false);
            _bootstrap = _bootstrapObject.AddComponent<AppBootstrap>();
            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");

            _windowRoot = new GameObject(
                "OptionsWindowRootUnderTest",
                typeof(RectTransform),
                typeof(UIWindowManager)
            );
            _windowManager = _windowRoot.GetComponent<UIWindowManager>();
            _windowManager.SetContentSource(_bootstrap.GetContentAssets());
            _controller = CreateController(GetPrefab());
        }

        [TearDown]
        public void TearDown()
        {
            _controller?.Dispose();
            if (_windowRoot != null)
                UnityEngine.Object.DestroyImmediate(_windowRoot);
            if (_bootstrapObject != null)
                UnityEngine.Object.DestroyImmediate(_bootstrapObject);

            DestroyAudioManagers();
            if (Directory.Exists(_saveDirectoryPath))
                Directory.Delete(_saveDirectoryPath, true);
            GameLaunchContext.Reset(TestContent.Pack);
        }

        [Test]
        public void Open_ClosedController_CreatesAndRendersSingleWindow()
        {
            _controller.Open();
            _controller.RenderWindows();
            OptionsMenuView view = GetOpenView();

            _controller.Open();

            Assert.IsTrue(_controller.IsOpen);
            Assert.AreEqual(1, _windowManager.Windows.Count);
            Assert.IsTrue(view.gameObject.activeSelf);
            Assert.GreaterOrEqual(_dirtyCount, 1);
        }

        [Test]
        public void Open_MissingPrefab_LeavesControllerClosed()
        {
            using OptionsMenuController controller = CreateController(null);

            controller.Open();

            Assert.IsFalse(controller.IsOpen);
        }

        [Test]
        public void TryCancel_OpenWindow_ClosesWindow()
        {
            Assert.IsFalse(_controller.TryCancel());
            _controller.Open();

            bool canceled = _controller.TryCancel();

            Assert.IsTrue(canceled);
            Assert.IsFalse(_controller.IsOpen);
            Assert.IsEmpty(_windowManager.Windows);
        }

        [Test]
        public void ClosedController_CloseAndRender_AreNoOps()
        {
            _controller.Close();
            _controller.RenderWindows();

            Assert.IsFalse(_controller.IsOpen);
            Assert.IsEmpty(_windowManager.Windows);
        }

        [Test]
        public void Navigation_CleanSettings_SwitchesAllTabs()
        {
            OptionsMenuView view = OpenAndRender();
            Button[] tabs = GetField<Button[]>(view, "_tabButtons");
            int initialDirtyCount = _dirtyCount;

            Assert.IsTrue(GetField<GameObject>(view, "_gameplayPage").activeSelf);
            tabs[(int)OptionsMenuTab.Gameplay].onClick.Invoke();
            Assert.AreEqual(initialDirtyCount, _dirtyCount);

            tabs[(int)OptionsMenuTab.Graphics].onClick.Invoke();
            tabs[(int)OptionsMenuTab.Audio].onClick.Invoke();
            _controller.RenderWindows();
            Assert.IsTrue(GetField<GameObject>(view, "_audioPage").activeSelf);

            tabs[(int)OptionsMenuTab.Controls].onClick.Invoke();
            _controller.RenderWindows();
            Assert.IsTrue(GetField<GameObject>(view, "_controlsPage").activeSelf);

            tabs[(int)OptionsMenuTab.SaveLoad].onClick.Invoke();
            _controller.RenderWindows();
            Assert.IsTrue(GetField<GameObject>(view, "_saveLoadPage").activeSelf);
        }

        [Test]
        public void GameplayActions_ToggleAutomaticPausingOption()
        {
            OptionsMenuView view = OpenAndRender();
            OptionsToggleRowView gameplayRow = GetField<OptionsToggleRowView[]>(
                    view,
                    "_gameplayRows"
                )
                .Single(row =>
                    row.OptionIndex == (int)UserGameplayOption.PauseAfterEnemyBombardment
                );

            GetField<Button>(gameplayRow, "_button").onClick.Invoke();

            Assert.IsFalse(
                _bootstrap.GetUserSettingsManager().Settings.Gameplay.PauseAfterEnemyBombardment
            );
        }

        [Test]
        public void GraphicsActions_ChangePreviewAndRestoreDefaultsAfterConfirmation()
        {
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.Graphics);
            int initialDirtyCount = _dirtyCount;

            GetField<Button>(view, "_resolutionNextButton").onClick.Invoke();
            GetField<Button>(view, "_fullScreenNextButton").onClick.Invoke();
            OptionsToggleRowView tacticalRow = GetField<OptionsToggleRowView[]>(
                    view,
                    "_tacticalRows"
                )
                .First();
            GetField<Button>(tacticalRow, "_button").onClick.Invoke();
            GetField<Button>(view, "_defaultsButton").onClick.Invoke();
            ConfirmationDialogView confirmation = GetField<ConfirmationDialogView>(
                view,
                "_confirmDialog"
            );
            Assert.IsTrue(confirmation.gameObject.activeSelf);

            GetField<Button>(confirmation, "confirmButton").onClick.Invoke();

            Assert.IsFalse(confirmation.gameObject.activeSelf);
            Assert.Greater(_dirtyCount, initialDirtyCount);
        }

        [Test]
        public void ActiveGame_OpenAndBackToGame_PausesAndRestoresSpeed()
        {
            GameManager gameManager = _bootstrap.GetRuntime().StartGame(CreateGame());
            gameManager.SetGameSpeed(TickSpeed.Fast);

            OptionsMenuView view = OpenAndRender();

            Assert.AreEqual(TickSpeed.Paused, gameManager.GetGameSpeed());
            GetField<Button>(view, "_backToGameButton").onClick.Invoke();
            Assert.IsFalse(_controller.IsOpen);
            Assert.AreEqual(TickSpeed.Fast, gameManager.GetGameSpeed());
        }

        [Test]
        public void ActiveGame_ReturnToMainMenu_WarnsAboutUnsavedProgress()
        {
            _bootstrap.GetRuntime().StartGame(CreateGame());
            OptionsMenuView view = OpenAndRender();
            ConfirmationDialogView confirmation = GetField<ConfirmationDialogView>(
                view,
                "_confirmDialog"
            );

            GetField<Button>(view, "_mainMenuButton").onClick.Invoke();

            Assert.IsTrue(confirmation.gameObject.activeSelf);
            Assert.AreEqual(
                "Return to the Main Menu? Any unsaved progress will be lost.",
                GetField<TextMeshProUGUI>(confirmation, "messageTextField").text
            );
        }

        [Test]
        public void ActiveGame_Quit_WarnsAboutUnsavedProgress()
        {
            _bootstrap.GetRuntime().StartGame(CreateGame());
            OptionsMenuView view = OpenAndRender();
            ConfirmationDialogView confirmation = GetField<ConfirmationDialogView>(
                view,
                "_confirmDialog"
            );

            GetField<Button>(view, "_quitButton").onClick.Invoke();

            Assert.IsTrue(confirmation.gameObject.activeSelf);
            Assert.AreEqual(
                "Quit to desktop? Any unsaved progress will be lost.",
                GetField<TextMeshProUGUI>(confirmation, "messageTextField").text
            );
        }

        [Test]
        public void AudioActions_ChangeVolumeThenDiscardTabChange()
        {
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.Audio);
            NormalizedSliderView slider = GetField<NormalizedSliderView[]>(view, "_volumeSliders")
                .First();
            GetField<Slider>(slider, "slider").onValueChanged.Invoke(0.37f);

            GetField<Button[]>(view, "_tabButtons")[(int)OptionsMenuTab.Graphics].onClick.Invoke();
            ConfirmationDialogView confirmation = GetField<ConfirmationDialogView>(
                view,
                "_confirmDialog"
            );
            Assert.IsTrue(confirmation.gameObject.activeSelf);

            GetField<Button>(confirmation, "cancelButton").onClick.Invoke();

            Assert.IsFalse(confirmation.gameObject.activeSelf);
            Assert.IsTrue(GetField<GameObject>(view, "_audioPage").activeSelf);
        }

        [Test]
        public void ControlsActions_RestoreBindingAndCancelRebind()
        {
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.Controls);
            Button restore = view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "BindingRestore0");
            Button badge = view.GetComponentsInChildren<Button>(true)
                .First(button =>
                    button.interactable
                    && button.name.StartsWith("BindingPrimaryBadge", StringComparison.Ordinal)
                );

            badge.onClick.Invoke();
            bool canceled = _controller.TryCancel();
            restore.onClick.Invoke();

            Assert.IsTrue(canceled);
            Assert.IsTrue(_controller.IsOpen);
        }

        [Test]
        public void SaveLoadActions_WithoutSelection_DoNotLoadOrClose()
        {
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.SaveLoad);
            OptionsSaveListView saveList = GetField<OptionsSaveListView>(view, "_saveListView");

            GetField<Button>(saveList, "_saveButton").onClick.Invoke();
            GetField<Button>(saveList, "_loadButton").onClick.Invoke();

            Assert.IsTrue(_controller.IsOpen);
        }

        [Test]
        public void SaveLoadActions_SelectedSave_OverwritesAndLoadsThroughHost()
        {
            GameRoot game = CreateGame();
            _bootstrap.GetRuntime().StartGame(game);
            _saveGameManager.SaveGameData(game, "existing_save", "Existing Save");
            _loadResult = true;
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.SaveLoad);
            OptionsSaveListView saveList = GetField<OptionsSaveListView>(view, "_saveListView");

            view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "SlotRow1")
                .onClick.Invoke();
            _controller.RenderWindows();
            GetField<Button>(saveList, "_saveButton").onClick.Invoke();
            GetField<Button>(saveList, "_loadButton").onClick.Invoke();

            Assert.AreEqual("existing_save", _loadedFileName);
            Assert.IsFalse(_controller.IsOpen);
            Assert.AreEqual(
                "Existing Save",
                _saveGameManager.GetSavedGames().Single().Metadata.SaveDisplayName
            );
        }

        [Test]
        public void SaveLoadActions_RenameCreateAndDelete_RefreshesPersistedSlots()
        {
            GameRoot game = CreateGame();
            _bootstrap.GetRuntime().StartGame(game);
            _saveGameManager.SaveGameData(game, "existing_save", "Existing Save");
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.SaveLoad);
            OptionsSaveListView saveList = GetField<OptionsSaveListView>(view, "_saveListView");
            TMP_InputField rename = GetField<TMP_InputField>(saveList, "_renameField");
            Button existingRow = view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "SlotRow1");

            existingRow.onClick.Invoke();
            existingRow.onClick.Invoke();
            rename.onEndEdit.Invoke("Renamed Save");
            _controller.RenderWindows();
            Assert.AreEqual(
                "Renamed Save",
                _saveGameManager.GetSavedGames().Single().Metadata.SaveDisplayName
            );

            view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "SlotRow0")
                .onClick.Invoke();
            rename.onSubmit.Invoke("Created Save");
            _controller.RenderWindows();
            Assert.AreEqual(2, _saveGameManager.GetSavedGames().Count);

            view.GetComponentsInChildren<Button>(true)
                .First(button =>
                    button.name.StartsWith("SlotDelete", StringComparison.Ordinal)
                    && button.gameObject.activeSelf
                )
                .onClick.Invoke();
            ConfirmationDialogView confirmation = GetField<ConfirmationDialogView>(
                view,
                "_confirmDialog"
            );
            GetField<Button>(confirmation, "confirmButton").onClick.Invoke();

            Assert.AreEqual(1, _saveGameManager.GetSavedGames().Count);
        }

        [Test]
        public void MainMenuFooter_BackToMainMenuClosesOverlay()
        {
            OptionsMenuView view = OpenAndRender();

            GetField<Button>(view, "_mainMenuButton").onClick.Invoke();

            Assert.IsFalse(_controller.IsOpen);
        }

        [Test]
        public void MainMenu_Quit_CancelDismissesPromptWithoutUnsavedProgressWarning()
        {
            OptionsMenuView view = OpenAndRender();
            ConfirmationDialogView confirmation = GetField<ConfirmationDialogView>(
                view,
                "_confirmDialog"
            );

            GetField<Button>(view, "_quitButton").onClick.Invoke();
            Assert.IsTrue(confirmation.gameObject.activeSelf);
            Assert.AreEqual(
                "Quit to desktop?",
                GetField<TextMeshProUGUI>(confirmation, "messageTextField").text
            );
            GetField<Button>(confirmation, "cancelButton").onClick.Invoke();

            Assert.IsFalse(confirmation.gameObject.activeSelf);
            Assert.IsTrue(_controller.IsOpen);
        }

        [Test]
        public void DestroyedView_ClearsControllerWindowState()
        {
            OptionsMenuView view = OpenAndRender();

            UnityEngine.Object.DestroyImmediate(view.gameObject);

            Assert.IsFalse(_controller.IsOpen);
        }

        [Test]
        public void Dispose_ThenOpen_ThrowsObjectDisposedException()
        {
            _controller.Open();
            _controller.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _controller.Open());
        }

        private OptionsMenuController CreateController(OptionsMenuView prefab)
        {
            return new OptionsMenuController(
                prefab,
                _windowRoot.transform,
                _windowManager,
                () => new Vector2Int(12, 34),
                _windowManager.DestroyWindow,
                _bootstrap,
                fileName =>
                {
                    _loadedFileName = fileName;
                    return _loadResult;
                },
                () => _dirtyCount++,
                _saveGameManager
            );
        }

        private OptionsMenuView OpenAndRender(OptionsMenuTab initialTab = OptionsMenuTab.Gameplay)
        {
            _controller.Open(initialTab);
            OptionsMenuView view = GetOpenView();
            UIComponentTestHelper.InvokeLifecycle(
                GetField<OptionsSaveListView>(view, "_saveListView"),
                "Awake"
            );
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            _controller.RenderWindows();
            return view;
        }

        private OptionsMenuView GetOpenView()
        {
            return (OptionsMenuView)_windowManager.Windows.Single().Content;
        }

        private static OptionsMenuView GetPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            OptionsMenuView view = prefab == null ? null : prefab.GetComponent<OptionsMenuView>();
            if (view == null)
                throw new InvalidOperationException($"Missing OptionsMenuView at {_prefabPath}.");

            return view;
        }

        private static T GetField<T>(object target, string fieldName)
        {
            return (T)
                target
                    .GetType()
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(target);
        }

        private GameRoot CreateGame()
        {
            ContentPack contentPack = _bootstrap.GetContentPack();
            return new GameRoot
            {
                Summary = new GameSummary
                {
                    PackID = contentPack.Definition.ID,
                    PackVersion = contentPack.Definition.Version,
                    ScenarioID = contentPack.Scenario.ID,
                    PlayerFactionID = contentPack.Scenario.DefaultPlayerFactionID,
                },
                Factions = new List<Faction>(),
                Galaxy = new GalaxyMap(),
            };
        }

        private static void DestroyAudioManagers()
        {
            foreach (
                AudioManager manager in UnityEngine.Object.FindObjectsByType<AudioManager>(
                    FindObjectsInactive.Include
                )
            )
            {
                UnityEngine.Object.DestroyImmediate(manager.gameObject);
            }
        }
    }
}
