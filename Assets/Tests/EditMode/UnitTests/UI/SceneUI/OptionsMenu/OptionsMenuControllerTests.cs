using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
        private bool _menuOpenWhenLoadCalled;
        private string _loadedFileName;
        private string _testModDirectoryPath;
        private TaskCompletionSource<bool> _saveCompletion;
        private TaskCompletionSource<bool> _loadCompletion;
        private int _saveRequests;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _saveCompletion = null;
            _loadCompletion = null;
            _saveRequests = 0;
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
            LoadingScreenController loadingController =
                UnityEngine.Object.FindAnyObjectByType<LoadingScreenController>(
                    FindObjectsInactive.Include
                );
            if (loadingController != null)
                UnityEngine.Object.DestroyImmediate(loadingController.gameObject);
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
        public void Open_ModsUnavailable_UsesGameplayAndOmitsModsTab()
        {
            _controller.Dispose();
            _controller = CreateController(GetPrefab(), showModsTab: false);

            OptionsMenuView view = OpenAndRender(OptionsMenuTab.Mods);
            Button[] tabs = GetField<Button[]>(view, "_tabButtons");

            Assert.IsTrue(GetField<GameObject>(view, "_gameplayPage").activeSelf);
            Assert.IsFalse(tabs[(int)OptionsMenuTab.Mods].gameObject.activeSelf);
            Assert.IsTrue(tabs[(int)OptionsMenuTab.SaveLoad].gameObject.activeSelf);
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
        public void RenderWindows_ClosedController_DoesNotCreateWindow()
        {
            _controller.Close();
            _controller.RenderWindows();

            Assert.IsFalse(_controller.IsOpen);
            Assert.IsEmpty(_windowManager.Windows);
        }

        [Test]
        public void HandleTabSelected_CleanSettings_SwitchesAllTabs()
        {
            OptionsMenuView view = OpenAndRender();
            Button[] tabs = GetField<Button[]>(view, "_tabButtons");
            int initialDirtyCount = _dirtyCount;

            Assert.IsTrue(GetField<GameObject>(view, "_gameplayPage").activeSelf);
            tabs[(int)OptionsMenuTab.Gameplay].onClick.Invoke();
            Assert.AreEqual(initialDirtyCount, _dirtyCount);

            tabs[(int)OptionsMenuTab.UserInterface].onClick.Invoke();
            _controller.RenderWindows();
            Assert.IsTrue(GetField<GameObject>(view, "_userInterfacePage").activeSelf);

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

        [Test]
        public void HandleModToggle_CompatibleMod_PreservesDisabledModsForOtherPacks()
        {
            const string compatibleModID = "compatible-mod";
            const string otherPackModID = "other-pack-mod";
            ContentPack contentPack = _bootstrap.GetContentPack();
            _testModDirectoryPath = Path.Combine(
                Directory.GetParent(contentPack.ContentRootPath).FullName,
                "Mods",
                nameof(HandleModToggle_CompatibleMod_PreservesDisabledModsForOtherPacks)
                    + "-"
                    + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(_testModDirectoryPath);
            File.WriteAllText(
                Path.Combine(_testModDirectoryPath, "mod.xml"),
                $"<ContentModDefinition><ID>{compatibleModID}</ID><Version>1.0.0</Version>"
                    + "<DisplayName>Compatible Mod</DisplayName>"
                    + $"<BasePackID>{contentPack.Definition.ID}</BasePackID>"
                    + $"<BasePackVersion>{contentPack.Definition.Version}</BasePackVersion>"
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
                new[] { otherPackModID },
                _bootstrap.GetUserSettingsManager().Settings.Content.DisabledModIDs
            );
            GetField<Button>(view, "_applyButton").onClick.Invoke();

            Assert.IsFalse(_controller.IsOpen);
            Assert.IsTrue(LoadingScreenController.IsLoadInProgress);
            CollectionAssert.AreEqual(
                new[] { otherPackModID },
                _bootstrap.GetUserSettingsManager().Settings.Content.DisabledModIDs
            );
        }

        [Test]
        public void HandleDefaults_ModsConfirmed_RestoresCatalogPackAndEnablesAllMods()
        {
            _bootstrap.GetUserSettingsManager().Settings.Content.DisabledModIDs = new[]
            {
                "disabled-mod",
            };
            _controller.Dispose();
            _controller = CreateController(GetPrefab());
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.Mods);

            GetField<Button>(view, "_defaultsButton").onClick.Invoke();
            ConfirmationDialogView confirmation = GetField<ConfirmationDialogView>(
                view,
                "_confirmDialog"
            );
            GetField<Button>(confirmation, "confirmButton").onClick.Invoke();

            Assert.AreEqual(
                ContentPackLoader.GetDefaultPackID(_bootstrap.GetContentPack().ContentRootPath),
                GetField<string>(_controller, "_selectedPackID")
            );
            Assert.IsEmpty(GetField<HashSet<string>>(_controller, "_selectedDisabledModIDs"));
        }

        [Test]
        public void HandleGameplayToggle_SavedAutomaticPausingState_TogglesOption()
        {
            OptionsMenuView view = OpenAndRender();
            bool initiallyPaused = _bootstrap
                .GetUserSettingsManager()
                .Settings.Gameplay.PauseAfterEnemyBombardment;
            OptionsToggleRowView gameplayRow = GetField<OptionsToggleRowView[]>(
                    view,
                    "_gameplayRows"
                )
                .Single(row =>
                    row.OptionIndex == (int)UserGameplayOption.PauseAfterEnemyBombardment
                );

            GetField<Button>(gameplayRow, "_button").onClick.Invoke();

            Assert.AreEqual(
                !initiallyPaused,
                _bootstrap.GetUserSettingsManager().Settings.Gameplay.PauseAfterEnemyBombardment
            );
        }

        [Test]
        public void HandleGameplayToggle_DisableBriefingsClicked_TogglesOption()
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

        [Test]
        public void HandleUserInterfaceToggle_InterfaceOptionsClicked_TogglesOptions()
        {
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.UserInterface);
            bool initiallyVisible = _bootstrap
                .GetUserSettingsManager()
                .Settings.UserInterface.ShowIdleBar;
            bool initiallyAlwaysOpen = _bootstrap
                .GetUserSettingsManager()
                .Settings.UserInterface.KeepIdleBarOpen;
            bool initiallyAboveWindows = _bootstrap
                .GetUserSettingsManager()
                .Settings.UserInterface.KeepIdleBarAboveWindows;
            bool initiallyShowingStats = _bootstrap
                .GetUserSettingsManager()
                .Settings.UserInterface.ShowUnitCardStats;
            bool initiallyShowingFrameRate = _bootstrap
                .GetUserSettingsManager()
                .Settings.UserInterface.ShowFrameRate;
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
            OptionsToggleRowView layerRow = rows.Single(row =>
                row.OptionIndex == (int)UserInterfaceOption.KeepIdleBarAboveWindows
            );
            OptionsToggleRowView statsRow = rows.Single(row =>
                row.OptionIndex == (int)UserInterfaceOption.ShowUnitCardStats
            );
            OptionsToggleRowView frameRateRow = rows.Single(row =>
                row.OptionIndex == (int)UserInterfaceOption.ShowFrameRate
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

            GetField<Button>(layerRow, "_button").onClick.Invoke();
            Assert.AreEqual(
                !initiallyAboveWindows,
                _bootstrap.GetUserSettingsManager().Settings.UserInterface.KeepIdleBarAboveWindows
            );

            GetField<Button>(statsRow, "_button").onClick.Invoke();
            Assert.AreEqual(
                !initiallyShowingStats,
                _bootstrap.GetUserSettingsManager().Settings.UserInterface.ShowUnitCardStats
            );

            GetField<Button>(frameRateRow, "_button").onClick.Invoke();
            Assert.AreEqual(
                !initiallyShowingFrameRate,
                _bootstrap.GetUserSettingsManager().Settings.UserInterface.ShowFrameRate
            );
        }

        [Test]
        public void HandleIdleBarLocationChanged_DropdownSelection_StagesLocation()
        {
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.UserInterface);
            TMP_Dropdown dropdown = GetField<TMP_Dropdown>(view, "_idleBarLocationDropdown");

            dropdown.value = (int)IdleBarLocation.BottomLeft;

            Assert.AreEqual(
                IdleBarLocation.BottomLeft,
                _bootstrap.GetUserSettingsManager().Settings.UserInterface.IdleBarLocation
            );
        }

        [Test]
        public void HandleGameLaunchSpeedChanged_DropdownSelection_StagesSpeed()
        {
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.Gameplay);
            TMP_Dropdown dropdown = GetField<TMP_Dropdown>(view, "_gameLaunchSpeedDropdown");

            dropdown.value = (int)GameLaunchSpeed.Fast;

            Assert.AreEqual(
                GameLaunchSpeed.Fast,
                _bootstrap.GetUserSettingsManager().Settings.Gameplay.GameLaunchSpeed
            );
        }

        [Test]
        public void HandleDefaults_ModifiedGraphicsConfirmed_ClosesPromptAndMarksSettingsDirty()
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

        [Test]
        public void HandleBackToGame_ActiveGame_RestoresSpeedAndClosesWindow()
        {
            _bootstrap.GetRuntime().StartGame(CreateGame());
            GameManager gameManager = _bootstrap.GetRuntime().GetActiveGameManager();
            gameManager.SetGameSpeed(TickSpeed.Fast);

            OptionsMenuView view = OpenAndRender();

            Assert.AreEqual(TickSpeed.Paused, gameManager.GetGameSpeed());
            GetField<Button>(view, "_backToGameButton").onClick.Invoke();
            Assert.IsFalse(_controller.IsOpen);
            Assert.AreEqual(TickSpeed.Fast, gameManager.GetGameSpeed());
        }

        [TestCase(TickSpeed.Paused, TickSpeed.Paused)]
        [TestCase(TickSpeed.VerySlow, TickSpeed.VerySlow)]
        public void HandleBackToGame_DifferentOtherSelection_RestoresOnlyLocalPreference(
            TickSpeed otherSpeed,
            TickSpeed effectiveAfterClosing
        )
        {
            GameRoot game = CreateGame();
            ContentScenarioDefinition scenario = _bootstrap.GetContentPack().Scenario;
            string hostFactionID = scenario.DefaultPlayerFactionID;
            string localFactionID = scenario.PlayableFactionIDs.First(id => id != hostFactionID);
            game.GetFactions()
                .AddRange(
                    new[]
                    {
                        new Faction { InstanceID = hostFactionID },
                        new Faction { InstanceID = localFactionID },
                    }
                );
            game.SetFactionController(hostFactionID, "first-player", PlayerControllerType.Human);
            game.SetFactionController(localFactionID, "second-player", PlayerControllerType.Human);
            game.LocalPlayerFactionID = localFactionID;
            _bootstrap.GetRuntime().StartGame(game);
            GameManager gameManager = _bootstrap.GetRuntime().GetActiveGameManager();
            gameManager.SetGameSpeed(TickSpeed.Fast);
            gameManager.EnableCoordinatedSpeeds();
            gameManager.ApplyPlayerGameSpeed(hostFactionID, otherSpeed);
            gameManager.BindSpeedRequests(speed =>
                gameManager.ApplyPlayerGameSpeed(localFactionID, speed)
            );

            OptionsMenuView view = OpenAndRender();

            Assert.AreEqual(TickSpeed.Paused, gameManager.GetGameSpeed());
            Assert.AreEqual(
                TickSpeed.Paused,
                game.GetFactionPlayer(localFactionID).RequestedGameSpeed
            );
            Assert.AreEqual(otherSpeed, game.GetFactionPlayer(hostFactionID).RequestedGameSpeed);

            GetField<Button>(view, "_backToGameButton").onClick.Invoke();

            Assert.IsFalse(_controller.IsOpen);
            Assert.AreEqual(
                TickSpeed.Fast,
                game.GetFactionPlayer(localFactionID).RequestedGameSpeed
            );
            Assert.AreEqual(otherSpeed, game.GetFactionPlayer(hostFactionID).RequestedGameSpeed);
            Assert.AreEqual(effectiveAfterClosing, gameManager.GetGameSpeed());

            gameManager.ApplyPlayerGameSpeed(hostFactionID, TickSpeed.Fast);

            Assert.AreEqual(TickSpeed.Fast, gameManager.GetGameSpeed());
        }

        [Test]
        public void HandleBackToGame_UnavailableSavedPack_ClosesWindow()
        {
            _bootstrap.GetUserSettingsManager().Settings.Content.ActivePackID = "missing-pack";
            _controller.Dispose();
            _controller = CreateController(GetPrefab());
            _bootstrap.GetRuntime().StartGame(CreateGame());
            OptionsMenuView view = OpenAndRender();

            GetField<Button>(view, "_backToGameButton").onClick.Invoke();

            Assert.IsFalse(_controller.IsOpen);
        }

        [Test]
        public void HandleMainMenuRequested_ActiveGame_WarnsAboutUnsavedProgress()
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
        public void HandleQuitRequested_ActiveGame_WarnsAboutUnsavedProgress()
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
        public void HandleTabSelected_DirtyAudioSettingsDeclined_KeepsAudioTabOpen()
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
        public void TryCancel_ActiveRebind_CancelsRebindWithoutClosingWindow()
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
        public void HandleLoadRequested_NoSelection_LeavesWindowOpen()
        {
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.SaveLoad);
            OptionsSaveListView saveList = GetField<OptionsSaveListView>(view, "_saveListView");

            GetField<Button>(saveList, "_saveButton").onClick.Invoke();
            GetField<Button>(saveList, "_loadButton").onClick.Invoke();

            Assert.IsTrue(_controller.IsOpen);
        }

        [Test]
        public void HandleLoadRequested_SelectedSaveAfterOverwrite_LoadsThroughHostAndClosesWindow()
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
            ConfirmationDialogView confirmation = GetField<ConfirmationDialogView>(
                view,
                "_confirmDialog"
            );
            GetField<Button>(confirmation, "confirmButton").onClick.Invoke();
            GetField<Button>(saveList, "_loadButton").onClick.Invoke();

            Assert.AreEqual("existing_save", _loadedFileName);
            Assert.IsTrue(_menuOpenWhenLoadCalled);
            Assert.IsFalse(_controller.IsOpen);
            Assert.AreEqual(
                "Existing Save",
                _saveGameManager.GetSavedGames().Single().Metadata.SaveDisplayName
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HandleSaveRequested_ExistingSave_WaitsForConfirmation(bool submitName)
        {
            OptionsMenuView view = RequestExistingSaveOverwrite(submitName);
            ConfirmationDialogView confirmation = GetField<ConfirmationDialogView>(
                view,
                "_confirmDialog"
            );

            Assert.IsTrue(confirmation.gameObject.activeSelf);
            StringAssert.Contains(
                "Overwrite \"Existing Save\"?",
                confirmation.GetComponentInChildren<TextMeshProUGUI>().text
            );
            Assert.AreEqual(7, _saveGameManager.GetSavedGames().Single().Metadata.CurrentTick);
            Assert.AreEqual(
                "Existing Save",
                _saveGameManager.GetSavedGames().Single().Metadata.SaveDisplayName
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HandleConfirmAccepted_SaveOverwrite_WritesRequestedSave(bool submitName)
        {
            OptionsMenuView view = RequestExistingSaveOverwrite(submitName);
            ConfirmationDialogView confirmation = GetField<ConfirmationDialogView>(
                view,
                "_confirmDialog"
            );

            GetField<Button>(confirmation, "confirmButton").onClick.Invoke();

            SaveGameEntry saved = _saveGameManager.GetSavedGames().Single();
            Assert.AreEqual("existing_save", saved.FileName);
            Assert.AreEqual(42, saved.Metadata.CurrentTick);
            Assert.AreEqual(
                submitName ? "Updated Save" : "Existing Save",
                saved.Metadata.SaveDisplayName
            );
            Assert.IsFalse(confirmation.gameObject.activeSelf);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HandleConfirmDeclined_SaveOverwrite_PreservesStoredGame(bool submitName)
        {
            OptionsMenuView view = RequestExistingSaveOverwrite(submitName);
            ConfirmationDialogView confirmation = GetField<ConfirmationDialogView>(
                view,
                "_confirmDialog"
            );
            string path = _saveGameManager.GetSaveFilePath("existing_save");
            byte[] before = File.ReadAllBytes(path);

            GetField<Button>(confirmation, "cancelButton").onClick.Invoke();
            GetField<Button>(confirmation, "confirmButton").onClick.Invoke();

            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
            Assert.AreEqual(7, _saveGameManager.GetSavedGames().Single().Metadata.CurrentTick);
            Assert.IsFalse(confirmation.gameObject.activeSelf);
        }

        [Test]
        public void TryCancel_SaveOverwrite_DismissesWithoutWriting()
        {
            OptionsMenuView view = RequestExistingSaveOverwrite(false);
            ConfirmationDialogView confirmation = GetField<ConfirmationDialogView>(
                view,
                "_confirmDialog"
            );

            Assert.IsTrue(_controller.TryCancel());
            GetField<Button>(confirmation, "confirmButton").onClick.Invoke();

            Assert.IsTrue(_controller.IsOpen);
            Assert.IsFalse(confirmation.gameObject.activeSelf);
            Assert.AreEqual(7, _saveGameManager.GetSavedGames().Single().Metadata.CurrentTick);
        }

        [Test]
        public void HandleSaveRequested_ConsecutiveNewSaves_KeepsBothSaves()
        {
            _bootstrap.GetRuntime().StartGame(CreateGame());
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.SaveLoad);
            OptionsSaveListView saveList = GetField<OptionsSaveListView>(view, "_saveListView");
            TMP_InputField rename = GetField<TMP_InputField>(saveList, "_renameField");

            foreach (string name in new[] { "First Save", "Second Save" })
            {
                view.GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "SlotRow0")
                    .onClick.Invoke();
                rename.text = name;
                GetField<Button>(saveList, "_saveButton").onClick.Invoke();
                _controller.RenderWindows();
            }

            CollectionAssert.AreEquivalent(
                new[] { "First Save", "Second Save" },
                _saveGameManager.GetSavedGames().Select(entry => entry.Metadata.SaveDisplayName)
            );
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task HandleLoadRequested_PendingOwner_ClosesOnlyAfterAcceptedCompletionAsync(
            bool accepted
        )
        {
            GameRoot game = CreateGame();
            _bootstrap.GetRuntime().StartGame(game);
            _saveGameManager.SaveGameData(game, "existing_save", "Existing Save");
            _loadCompletion = new TaskCompletionSource<bool>();
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.SaveLoad);
            view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "SlotRow1")
                .onClick.Invoke();
            OptionsSaveListView saveList = GetField<OptionsSaveListView>(view, "_saveListView");

            GetField<Button>(saveList, "_loadButton").onClick.Invoke();

            Assert.IsTrue(_controller.IsOpen);
            Assert.IsTrue(_menuOpenWhenLoadCalled);
            Assert.AreSame(view, GetOpenView());
            _loadCompletion.SetResult(accepted);
            await _loadCompletion.Task;
            await Task.Yield();

            Assert.AreEqual(!accepted, _controller.IsOpen);
            if (!accepted)
                Assert.AreSame(view, GetOpenView());
        }

        [Test]
        public async Task HandleLoadRequested_CanceledOwner_LeavesViewOpenAndAllowsRetryAsync()
        {
            GameRoot game = CreateGame();
            _bootstrap.GetRuntime().StartGame(game);
            _saveGameManager.SaveGameData(game, "existing_save", "Existing Save");
            _loadCompletion = new TaskCompletionSource<bool>();
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.SaveLoad);
            view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "SlotRow1")
                .onClick.Invoke();
            OptionsSaveListView saveList = GetField<OptionsSaveListView>(view, "_saveListView");
            Button load = GetField<Button>(saveList, "_loadButton");
            load.onClick.Invoke();

            _loadCompletion.SetCanceled();
            await Task.Yield();

            Assert.IsTrue(_controller.IsOpen);
            Assert.AreSame(view, GetOpenView());
            _loadCompletion = new TaskCompletionSource<bool>();
            load.onClick.Invoke();
            _loadCompletion.SetResult(true);
            await _loadCompletion.Task;
            await Task.Yield();

            Assert.IsFalse(_controller.IsOpen);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task HandleSaveRequested_PendingOwner_RefreshesOnlyAfterAcceptedCompletionAsync(
            bool accepted
        )
        {
            GameRoot game = CreateGame();
            _bootstrap.GetRuntime().StartGame(game);
            _saveGameManager.SaveGameData(game, "existing_save", "Existing Save");
            _saveCompletion = new TaskCompletionSource<bool>();
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.SaveLoad);
            view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "SlotRow1")
                .onClick.Invoke();
            OptionsSaveListView saveList = GetField<OptionsSaveListView>(view, "_saveListView");
            Button save = GetField<Button>(saveList, "_saveButton");
            int dirtyCount = _dirtyCount;

            save.onClick.Invoke();
            GetField<Button>(
                GetField<ConfirmationDialogView>(view, "_confirmDialog"),
                "confirmButton"
            )
                .onClick.Invoke();
            save.onClick.Invoke();

            Assert.AreEqual(1, _saveRequests);
            Assert.AreEqual(dirtyCount, _dirtyCount);
            Assert.AreEqual(
                "Existing Save",
                _saveGameManager.GetSavedGames().Single().Metadata.SaveDisplayName
            );
            _saveCompletion.SetResult(accepted);
            await _saveCompletion.Task;
            await Task.Yield();

            Assert.AreEqual(dirtyCount + (accepted ? 1 : 0), _dirtyCount);
            Assert.IsTrue(_controller.IsOpen);
        }

        [Test]
        public async Task HandleSlotRenamed_PendingNewSave_DoesNotReportSuccessBeforeCompletionAsync()
        {
            _bootstrap.GetRuntime().StartGame(CreateGame());
            _saveCompletion = new TaskCompletionSource<bool>();
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.SaveLoad);
            OptionsSaveListView saveList = GetField<OptionsSaveListView>(view, "_saveListView");
            view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "SlotRow0")
                .onClick.Invoke();
            int dirtyCount = _dirtyCount;

            GetField<TMP_InputField>(saveList, "_renameField").onSubmit.Invoke("Pending Save");

            Assert.AreEqual(1, _saveRequests);
            Assert.AreEqual(dirtyCount, _dirtyCount);
            Assert.IsEmpty(_saveGameManager.GetSavedGames());
            _saveCompletion.SetResult(false);
            await _saveCompletion.Task;
            await Task.Yield();

            Assert.AreEqual(dirtyCount, _dirtyCount);
            Assert.IsEmpty(_saveGameManager.GetSavedGames());
        }

        [Test]
        public async Task HandleSaveRequested_WindowReopened_DoesNotRefreshReplacementViewAsync()
        {
            GameRoot game = CreateGame();
            _bootstrap.GetRuntime().StartGame(game);
            _saveGameManager.SaveGameData(game, "existing_save", "Existing Save");
            _saveCompletion = new TaskCompletionSource<bool>();
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.SaveLoad);
            view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "SlotRow1")
                .onClick.Invoke();
            OptionsSaveListView saveList = GetField<OptionsSaveListView>(view, "_saveListView");
            GetField<Button>(saveList, "_saveButton").onClick.Invoke();
            GetField<Button>(
                GetField<ConfirmationDialogView>(view, "_confirmDialog"),
                "confirmButton"
            )
                .onClick.Invoke();
            _controller.Close();
            OptionsMenuView replacement = OpenAndRender();
            int dirtyCount = _dirtyCount;

            _saveCompletion.SetResult(true);
            await _saveCompletion.Task;
            await Task.Yield();

            Assert.AreSame(replacement, GetOpenView());
            Assert.AreEqual(dirtyCount, _dirtyCount);
        }

        [Test]
        public void RefreshSaveSlots_SavedDetails_RendersDifficultyAndTurn()
        {
            GameRoot game = CreateGame();
            game.Summary.Difficulty = GameDifficulty.Hard;
            game.CurrentTick = 42;
            _saveGameManager.SaveGameData(game, "hard_save", "Hard Save");

            OptionsMenuView view = OpenAndRender(OptionsMenuTab.SaveLoad);

            TextMeshProUGUI metadata = view.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == "SlotMetadata0");
            Assert.AreEqual("Hard • Turn 42", metadata.text);
        }

        [Test]
        public void RefreshSaveSlots_CustomGame_RendersCustomAndTurn()
        {
            GameRoot game = CreateGame();
            game.Summary.Difficulty = GameDifficulty.Hard;
            game.Summary.GalaxySize = GameSize.Small;
            game.Summary.CustomSettings = new CustomGameSettings
            {
                GenerationConfig = new GameGenerationConfig(),
            };
            game.CurrentTick = 42;
            _saveGameManager.SaveGameData(game, "custom_save", "Custom Save");

            OptionsMenuView view = OpenAndRender(OptionsMenuTab.SaveLoad);

            TextMeshProUGUI metadata = view.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == "SlotMetadata0");
            Assert.AreEqual("Custom • Turn 42", metadata.text);
        }

        [Test]
        public void HandleSaveRequested_ValidNewSaveName_EnablesButtonAndCreatesSave()
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

        [Test]
        public void RenderWindows_SaveDraftLosesFocus_PreservesDraftUntilExplicitSave()
        {
            _bootstrap.GetRuntime().StartGame(CreateGame());
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.SaveLoad);
            OptionsSaveListView saveList = view.GetComponentInChildren<OptionsSaveListView>(true);
            view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "SlotRow0")
                .onClick.Invoke();
            TMP_InputField field = saveList.GetComponentInChildren<TMP_InputField>(true);
            field.text = "Campaign draft";

            field.onEndEdit.Invoke(field.text);
            _controller.RenderWindows();

            Assert.IsTrue(field.gameObject.activeSelf);
            Assert.AreEqual("Campaign draft", field.text);
            Assert.IsEmpty(_saveGameManager.GetSavedGames());
            saveList.transform.Find("SaveIcon").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(
                "Campaign draft",
                _saveGameManager.GetSavedGames().Single().Metadata.SaveDisplayName
            );
            Assert.IsFalse(field.gameObject.activeSelf);
        }

        [Test]
        public void RefreshSaveSlots_PersistedSlotChanges_ReflectRenameCreationAndDeletion()
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
            rename.onSubmit.Invoke("Renamed Save");
            GetField<Button>(
                GetField<ConfirmationDialogView>(view, "_confirmDialog"),
                "confirmButton"
            )
                .onClick.Invoke();
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
        public void HandleMainMenuRequested_NoActiveGame_ClosesWindow()
        {
            OptionsMenuView view = OpenAndRender();

            GetField<Button>(view, "_mainMenuButton").onClick.Invoke();

            Assert.IsFalse(_controller.IsOpen);
        }

        [Test]
        public void HandleQuitRequested_NoActiveGameDeclined_KeepsWindowOpen()
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
        public void HandleViewDestroyed_DestroyedWindow_ClearsControllerState()
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

        [Test]
        public void RefreshSaveSlots_InjectedRoleQuery_UsesVisibleOwnerCopiesAndSinglePlayerSources()
        {
            GameRoot game = CreateGame();
            _bootstrap.GetRuntime().StartGame(game);
            _saveGameManager.SaveGameData(game, "single-player-source", "Single-player source");
            game.Metadata.MultiplayerCampaign = new MultiplayerCampaignIdentity
            {
                HostUserID = "host-account",
                GuestUserID = "guest-account",
                HostFactionID = "FNALL1",
                GuestFactionID = "FNEMP1",
            };
            string hostName = SaveGameManager.GetMultiplayerSaveFileName(
                game.Metadata.CampaignID,
                true,
                "checkpoint"
            );
            string guestName = SaveGameManager.GetMultiplayerSaveFileName(
                game.Metadata.CampaignID,
                false,
                "checkpoint"
            );
            _saveGameManager.SaveGameData(game, hostName, "Host checkpoint");
            _saveGameManager.SaveGameData(game, guestName, "Guest checkpoint");
            _controller.Dispose();
            _controller = CreateController(
                GetPrefab(),
                getSavedGames: () => _saveGameManager.GetSavedGames(true)
            );

            OptionsMenuView view = OpenAndRender(OptionsMenuTab.SaveLoad);

            Assert.AreEqual(
                3,
                view.GetComponentsInChildren<Button>()
                    .Count(button => button.name.StartsWith("SlotRow", StringComparison.Ordinal))
            );
        }

        /// <summary>
        /// Opens an existing save and requests its replacement through the authored controls.
        /// </summary>
        /// <param name="submitName">Whether to submit an edited name with Return.</param>
        /// <returns>The open Options view.</returns>
        private OptionsMenuView RequestExistingSaveOverwrite(bool submitName)
        {
            GameRoot game = CreateGame();
            _bootstrap.GetRuntime().StartGame(game);
            game.CurrentTick = 7;
            _saveGameManager.SaveGameData(game, "existing_save", "Existing Save");
            game.CurrentTick = 42;
            OptionsMenuView view = OpenAndRender(OptionsMenuTab.SaveLoad);
            OptionsSaveListView saveList = GetField<OptionsSaveListView>(view, "_saveListView");
            Button row = view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "SlotRow1");
            row.onClick.Invoke();
            _controller.RenderWindows();
            if (submitName)
            {
                row.onClick.Invoke();
                GetField<TMP_InputField>(saveList, "_renameField").onSubmit.Invoke("Updated Save");
            }
            else
                GetField<Button>(saveList, "_saveButton").onClick.Invoke();
            return view;
        }

        /// <summary>
        /// Creates a controller using the test's isolated save directory.
        /// </summary>
        /// <param name="prefab">The Options prefab.</param>
        /// <param name="showModsTab">Whether the controller exposes content management.</param>
        /// <param name="getSavedGames">The injected save visibility query.</param>
        /// <returns>The created controller.</returns>
        private OptionsMenuController CreateController(
            OptionsMenuView prefab,
            bool showModsTab = true,
            Func<IReadOnlyList<SaveGameEntry>> getSavedGames = null
        )
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
                        return Task.FromResult(false);

                    _saveRequests++;
                    if (_saveCompletion != null)
                        return _saveCompletion.Task;
                    _saveGameManager.SaveGameData(game, fileName, displayName);
                    return Task.FromResult(true);
                },
                fileName =>
                {
                    _menuOpenWhenLoadCalled = _controller.IsOpen;
                    _loadedFileName = fileName;
                    return _loadCompletion?.Task ?? Task.FromResult(_loadResult);
                },
                () => _dirtyCount++,
                _saveGameManager,
                showModsTab,
                getSavedGames
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
