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
        private string _testModDirectoryPath;

        /// <summary>
        /// Sets up.
        /// </summary>
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

        /// <summary>
        /// Executes tear down.
        /// </summary>
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
            if (Directory.Exists(_testModDirectoryPath))
                Directory.Delete(_testModDirectoryPath, true);
            GameLaunchContext.Reset(TestContent.Pack);
        }

        /// <summary>
        /// Verifies open closed controller creates and renders single window.
        /// </summary>
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

        /// <summary>
        /// Verifies open missing prefab leaves controller closed.
        /// </summary>
        [Test]
        public void Open_MissingPrefab_LeavesControllerClosed()
        {
            using OptionsMenuController controller = CreateController(null);

            controller.Open();

            Assert.IsFalse(controller.IsOpen);
        }

        /// <summary>
        /// Verifies try cancel open window closes window.
        /// </summary>
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

        /// <summary>
        /// Verifies closed controller close and render are no ops.
        /// </summary>
        [Test]
        public void ClosedController_CloseAndRender_AreNoOps()
        {
            _controller.Close();
            _controller.RenderWindows();

            Assert.IsFalse(_controller.IsOpen);
            Assert.IsEmpty(_windowManager.Windows);
        }

        /// <summary>
        /// Verifies navigation clean settings switches all tabs.
        /// </summary>
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

            tabs[(int)OptionsMenuTab.Mods].onClick.Invoke();
            _controller.RenderWindows();
            Assert.IsTrue(GetField<GameObject>(view, "_modsPage").activeSelf);

            tabs[(int)OptionsMenuTab.SaveLoad].onClick.Invoke();
            _controller.RenderWindows();
            Assert.IsTrue(GetField<GameObject>(view, "_saveLoadPage").activeSelf);
        }

        /// <summary>
        /// Verifies toggling a compatible mod preserves disabled selections for other packs.
        /// </summary>
        [Test]
        public void ModsPage_ToggleMod_PreservesDisabledModsForOtherPacks()
        {
            const string compatibleModID = "compatible-mod";
            const string otherPackModID = "other-pack-mod";
            ContentPack contentPack = _bootstrap.GetContentPack();
            _testModDirectoryPath = Path.Combine(
                Directory.GetParent(contentPack.ContentRootPath).FullName,
                "Mods",
                nameof(ModsPage_ToggleMod_PreservesDisabledModsForOtherPacks)
                    + "-"
                    + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(_testModDirectoryPath);
            File.WriteAllText(
                Path.Combine(_testModDirectoryPath, "mod.xml"),
                $"<ContentModDefinition><ID>{compatibleModID}</ID><Version>1.0.0</Version>"
                    + "<DisplayName>Compatible Mod</DisplayName>"
                    + $"<BasePackID>{contentPack.Definition.ID}</BasePackID>"
                    + "</ContentModDefinition>"
            );
            _bootstrap.GetUserSettingsManager().Settings.Content.DisabledModIDs = new[]
            {
                otherPackModID,
            };
            _controller.Dispose();
            _controller = CreateController(GetPrefab());
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.Mods);
            OptionsToggleRowView row = view.GetComponentsInChildren<OptionsToggleRowView>(true)
                .Single(candidate => candidate.name == "ModRow1");

            row.GetComponentInChildren<Button>(true).onClick.Invoke();

            CollectionAssert.AreEqual(
                new[] { compatibleModID, otherPackModID },
                _bootstrap.GetUserSettingsManager().Settings.Content.DisabledModIDs
            );
        }

        /// <summary>
        /// Verifies gameplay actions toggle automatic pausing option.
        /// </summary>
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

        /// <summary>
        /// Verifies that the first gameplay toggle disables strategy briefings.
        /// </summary>
        [Test]
        public void GameplayActions_DisableBriefingsClicked_TogglesOption()
        {
            OptionsMenuView view = OpenAndRender();
            OptionsToggleRowView[] gameplayRows = GetField<OptionsToggleRowView[]>(
                view,
                "_gameplayRows"
            );
            bool initiallyDisabled = _bootstrap
                .GetUserSettingsManager()
                .Settings.Gameplay.DisableBriefings;

            Assert.AreEqual((int)UserGameplayOption.DisableBriefings, gameplayRows[0].OptionIndex);

            GetField<Button>(gameplayRows[0], "_button").onClick.Invoke();

            Assert.AreEqual(
                !initiallyDisabled,
                _bootstrap.GetUserSettingsManager().Settings.Gameplay.DisableBriefings
            );
        }

        /// <summary>
        /// Verifies gameplay actions toggle idle bar.
        /// </summary>
        [Test]
        public void UserInterfaceActions_ToggleIdleBarOptions()
        {
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.Gameplay);
            bool initiallyVisible = _bootstrap
                .GetUserSettingsManager()
                .Settings.UserInterface.ShowIdleBar;
            bool initiallyAlwaysOpen = _bootstrap
                .GetUserSettingsManager()
                .Settings.UserInterface.KeepIdleBarOpen;
            OptionsToggleRowView[] rows = GetField<OptionsToggleRowView[]>(
                view,
                "_userInterfaceRows"
            );
            OptionsToggleRowView showRow = rows.Single(row =>
                row.OptionIndex == (int)UserInterfaceOption.ShowIdleBar
            );
            OptionsToggleRowView pinRow = rows.Single(row =>
                row.OptionIndex == (int)UserInterfaceOption.KeepIdleBarOpen
            );

            GetField<Button>(showRow, "_button").onClick.Invoke();
            Assert.AreEqual(
                !initiallyVisible,
                _bootstrap.GetUserSettingsManager().Settings.UserInterface.ShowIdleBar
            );

            GetField<Button>(pinRow, "_button").onClick.Invoke();
            Assert.AreEqual(
                !initiallyAlwaysOpen,
                _bootstrap.GetUserSettingsManager().Settings.UserInterface.KeepIdleBarOpen
            );
        }

        /// <summary>
        /// Verifies graphics actions change preview and restore defaults after confirmation.
        /// </summary>
        [Test]
        public void GraphicsActions_ChangePreviewAndRestoreDefaultsAfterConfirmation()
        {
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.Graphics);
            int initialDirtyCount = _dirtyCount;

            GetField<Button>(view, "_resolutionNextButton").onClick.Invoke();
            GetField<Button>(view, "_fullScreenNextButton").onClick.Invoke();
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

        /// <summary>
        /// Verifies active game open and back to game pauses and restores speed.
        /// </summary>
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

        /// <summary>
        /// Verifies active game return to main menu warns about unsaved progress.
        /// </summary>
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

        /// <summary>
        /// Verifies active game quit warns about unsaved progress.
        /// </summary>
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

        /// <summary>
        /// Verifies audio actions change volume then discard tab change.
        /// </summary>
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

        /// <summary>
        /// Verifies controls actions restore binding and cancel rebind.
        /// </summary>
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

        /// <summary>
        /// Verifies save load actions without selection do not load or close.
        /// </summary>
        [Test]
        public void SaveLoadActions_WithoutSelection_DoNotLoadOrClose()
        {
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.SaveLoad);
            OptionsSaveListView saveList = GetField<OptionsSaveListView>(view, "_saveListView");

            GetField<Button>(saveList, "_saveButton").onClick.Invoke();
            GetField<Button>(saveList, "_loadButton").onClick.Invoke();

            Assert.IsTrue(_controller.IsOpen);
        }

        /// <summary>
        /// Verifies save load actions selected save overwrites and loads through host.
        /// </summary>
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

        /// <summary>
        /// Verifies save load actions rename create and delete refreshes persisted slots.
        /// </summary>
        [Test]
        public void SaveLoadActions_ValidNewSaveName_EnablesSaveButtonAndCreatesSave()
        {
            _bootstrap.GetRuntime().StartGame(CreateGame());
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.SaveLoad);
            OptionsSaveListView saveList = GetField<OptionsSaveListView>(view, "_saveListView");
            Button saveButton = GetField<Button>(saveList, "_saveButton");
            RawImage disabledImage = GetField<RawImage>(saveList, "_saveDisabledImage");
            TMP_InputField rename = GetField<TMP_InputField>(saveList, "_renameField");

            view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "SlotRow0")
                .onClick.Invoke();
            Assert.IsFalse(saveButton.interactable);

            rename.text = "Created Save";
            Assert.IsTrue(saveButton.interactable);
            Assert.IsTrue(saveButton.targetGraphic.enabled);
            Assert.IsFalse(disabledImage.gameObject.activeSelf);

            saveButton.onClick.Invoke();

            Assert.AreEqual(
                "Created Save",
                _saveGameManager.GetSavedGames().Single().Metadata.SaveDisplayName
            );
        }

        /// <summary>
        /// Verifies that save-list mutations refresh the persisted slot presentation.
        /// </summary>
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

        /// <summary>
        /// Verifies main menu footer back to main menu closes overlay.
        /// </summary>
        [Test]
        public void MainMenuFooter_BackToMainMenuClosesOverlay()
        {
            OptionsMenuView view = OpenAndRender();

            GetField<Button>(view, "_mainMenuButton").onClick.Invoke();

            Assert.IsFalse(_controller.IsOpen);
        }

        /// <summary>
        /// Verifies main menu quit cancel dismisses prompt without unsaved progress warning.
        /// </summary>
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

        /// <summary>
        /// Verifies destroyed view clears controller window state.
        /// </summary>
        [Test]
        public void DestroyedView_ClearsControllerWindowState()
        {
            OptionsMenuView view = OpenAndRender();

            UnityEngine.Object.DestroyImmediate(view.gameObject);

            Assert.IsFalse(_controller.IsOpen);
        }

        /// <summary>
        /// Verifies dispose then open throws object disposed exception.
        /// </summary>
        [Test]
        public void Dispose_ThenOpen_ThrowsObjectDisposedException()
        {
            _controller.Open();
            _controller.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _controller.Open());
        }

        /// <summary>
        /// Creates controller.
        /// </summary>
        /// <param name="prefab">The prefab.</param>
        /// <returns>The created controller.</returns>
        private OptionsMenuController CreateController(OptionsMenuView prefab)
        {
            return new OptionsMenuController(
                prefab,
                _windowRoot.transform,
                _windowManager,
                () => new Vector2Int(12, 34),
                _windowManager.DestroyWindow,
                _bootstrap,
                (fileName, displayName) =>
                {
                    GameRoot game = _bootstrap.GetRuntime().GetActiveGame();
                    if (game == null)
                        return false;

                    _saveGameManager.SaveGameData(game, fileName, displayName);
                    return true;
                },
                fileName =>
                {
                    _loadedFileName = fileName;
                    return _loadResult;
                },
                () => _dirtyCount++,
                _saveGameManager
            );
        }

        /// <summary>
        /// Opens and render.
        /// </summary>
        /// <param name="initialTab">The initial tab.</param>
        /// <returns>The result of open and render.</returns>
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

        /// <summary>
        /// Gets open view.
        /// </summary>
        /// <returns>The requested open view.</returns>
        private OptionsMenuView GetOpenView()
        {
            return (OptionsMenuView)_windowManager.Windows.Single().Content;
        }

        /// <summary>
        /// Gets prefab.
        /// </summary>
        /// <returns>The requested prefab.</returns>
        private static OptionsMenuView GetPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            OptionsMenuView view = prefab == null ? null : prefab.GetComponent<OptionsMenuView>();
            if (view == null)
                throw new InvalidOperationException($"Missing OptionsMenuView at {_prefabPath}.");

            return view;
        }

        /// <summary>
        /// Gets field.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="fieldName">The field name.</param>
        /// <typeparam name="T">The t type.</typeparam>
        /// <returns>The requested field.</returns>
        private static T GetField<T>(object target, string fieldName)
        {
            return (T)
                target
                    .GetType()
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(target);
        }

        /// <summary>
        /// Creates game.
        /// </summary>
        /// <returns>The created game.</returns>
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
                Galaxy = new GalaxyMap(),
            };
        }

        /// <summary>
        /// Executes destroy audio managers.
        /// </summary>
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
