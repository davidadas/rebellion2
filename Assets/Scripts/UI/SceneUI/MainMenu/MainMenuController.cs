using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Multiplayer;
using Rebellion.UI.SceneUI.MainMenu.Multiplayer;
using Rebellion.Util.Logging;
using UnityEngine;

/// <summary>
/// Owns main-menu launch state, audio, cutscenes, and scene navigation.
/// </summary>
public sealed class MainMenuController : MonoBehaviour, ICancelable
{
    private const string _creditsVideoPath = "Application/Credits/Videos/credits";
    private const string _selectSfxPath = "Application/MainMenu/Audio/select";

    [SerializeField]
    private MainMenuView view;

    [SerializeField]
    private ModelPreviewAtlas _iconPreviews;

    [SerializeField]
    private OptionsMenuView _optionsMenuPrefab;

    [SerializeField]
    private NewGameSettingsView _newGameSettingsPrefab;

    [SerializeField]
    private MultiplayerMenuOverlay _multiplayerMenuPrefab;

    [SerializeField]
    private AnalyticsConsentView _analyticsConsentView;

    [SerializeField]
    private LegalDocumentsView _legalDocumentsView;

    [SerializeField]
    private ConfirmationDialogView _confirmationTemplate;

    private UIWindow _newGameSettingsWindow;
    private NewGameSettingsView _newGameSettingsView;
    private IReadOnlyList<ContentPackDefinition> _customGamePacks;
    private CustomGamePresetManager _customGamePresetManager;

    [SerializeField]
    [Min(0f)]
    private float creditsMusicFadeDuration = 0.5f;

    private GameVictoryCondition currentVictoryCondition;
    private Canvas mainMenuCanvas;
    private OptionsMenuController optionsMenuController;
    private bool optionsDirty;
    private AppInputController _appInputController;
    private AppBootstrap _appBootstrap;
    private MultiplayerCoordinator _multiplayerCoordinator;
    private UIWindow _multiplayerWindow;
    private MultiplayerMenuOverlay _multiplayerMenuView;
    private UIWindow _confirmationWindow;
    private ConfirmationDialogView _confirmationDialog;
    private Action _pendingConfirmAction;
    private Action _pendingCancelAction;
    private Action _pendingMultiplayerEntry;
    private bool _menuReady;
    private bool _reconnectOffered;
    private bool _reconnecting;

    private bool IsStartupConsentBlocking =>
        _legalDocumentsView?.IsVisible == true
        || _analyticsConsentView?.IsVisible == true
        || _confirmationWindow != null;

    /// <summary>
    /// Resets launch state and renders the authored initial selections.
    /// </summary>
    private void Awake()
    {
        _customGamePresetManager = new CustomGamePresetManager();
        if (Application.isPlaying)
        {
            if (view == null)
                throw new MissingReferenceException($"{name} has no main-menu view.");
        }

        _appBootstrap = AppBootstrap.EnsureExists();
        _appBootstrap.LoadStarting += HandleLoadStarting;
        ContentPack contentPack = _appBootstrap.GetContentPack();
        GameLaunchContext.Reset(contentPack);
        currentVictoryCondition = GameLaunchContext.Summary.VictoryCondition;
        _multiplayerCoordinator = AppBootstrap.EnsureExists().GetMultiplayerCoordinator();
        _multiplayerCoordinator.Disconnect();

        if (view == null)
            return;

        mainMenuCanvas = view.transform.root.GetComponentInChildren<Canvas>(true);
        if (mainMenuCanvas == null)
            throw new MissingReferenceException($"{name} has no main-menu canvas.");
        mainMenuCanvas.enabled = false;
        view.RenderGalaxySize(GameLaunchContext.Summary.GalaxySize);
        view.RenderDifficulty(GameLaunchContext.Summary.Difficulty);
    }

    /// <summary>
    /// Subscribes to semantic view requests while the controller is active.
    /// </summary>
    private void OnEnable()
    {
        if (view == null)
            return;

        _appInputController = AppBootstrap.EnsureExists().GetInputController();
        _appInputController?.SetContext(InputContext.Menu);
        if (_appInputController != null)
            _appInputController.OptionsMenuRequested += HandleOptionsMenuRequested;

        view.GalaxySizeSelected += SelectGalaxySize;
        view.DifficultySelected += SelectGameDifficulty;
        view.StartGameRequested += HandleStartGameRequested;
        view.VictoryConditionToggleRequested += HandleVictoryConditionToggleRequested;
        view.SaveLoadMenuRequested += OpenSaveLoadMenu;
        view.ExitRequested += ExitApplication;
        view.CreditsRequested += ShowCredits;
        view.NewGameSettingsRequested += OpenNewGameSettings;
        view.MultiplayerRequested += OpenMultiplayerMenu;
        view.AudioCueRequested += PlayAudioCue;
        _multiplayerCoordinator.LaunchRequested += HandleMultiplayerLaunchRequested;
        _multiplayerCoordinator.StateChanged += HandleMultiplayerStateChanged;
        _analyticsConsentView.Allowed += HandleAnalyticsAllowed;
        _analyticsConsentView.Declined += HandleAnalyticsDeclined;
        _legalDocumentsView.MultiplayerAccepted += HandleMultiplayerAgreementAccepted;
        _legalDocumentsView.Closed += HandleLegalDocumentsClosed;
    }

    /// <summary>
    /// Initializes local content, preloads immediate UI cues, and starts main-menu music.
    /// </summary>
    private async void Start()
    {
        AppBootstrap bootstrap = null;
        try
        {
            bootstrap = AppBootstrap.EnsureExists();
            Task contentTask = bootstrap.InitializeMainMenuSceneAsync();
            Task modelTask = Task.WhenAll(
                view.transform.root.GetComponentsInChildren<ContentModelBinding>(true)
                    .Select(binding => binding.Ready)
            );
            await Task.WhenAll(contentTask, modelTask);
            if (this == null || view == null || mainMenuCanvas == null)
                return;

            _iconPreviews.Initialize();
            ContentPack contentPack = bootstrap.GetContentPack();
            view.InitializeContent(bootstrap.GetContentAssets());
            view.RenderVictoryCondition(currentVictoryCondition);
            view.RenderFactions(contentPack.Scenario.PlayableFactionIDs);
            view.ResetNewGameSettingsPresentation();
            AudioManager audioManager = bootstrap.GetAudioManager();
            audioManager.PreloadSfx(view.GetAudioCuePaths());
            if (LoadingScreenController.IsLoadInProgress)
            {
                if (
                    !LoadingScreenController.CompletePendingLoad(
                        bootstrap,
                        () => audioManager.PlayTrack(GetMenuMusicPath(contentPack.Definition), true)
                    )
                )
                    return;
            }
            else
                audioManager.PlayTrack(GetMenuMusicPath(contentPack.Definition), true);
            RenderAnalyticsConsentGate(bootstrap);
            mainMenuCanvas.enabled = true;
            _menuReady = true;
            await _multiplayerCoordinator.CheckForRecoveryAsync();
        }
        catch (Exception exception)
        {
            if (!LoadingScreenController.CompleteFailedRecovery(bootstrap, exception))
            {
                if (mainMenuCanvas != null)
                {
                    RenderAnalyticsConsentGate(bootstrap);
                    mainMenuCanvas.enabled = true;
                }
                GameLogger.LogException(exception);
            }
        }
    }

    /// <summary>
    /// Unsubscribes from semantic view requests when the controller is disabled.
    /// </summary>
    private void OnDisable()
    {
        _menuReady = false;
        if (_multiplayerCoordinator != null)
            _multiplayerCoordinator.StateChanged -= HandleMultiplayerStateChanged;
        if (_reconnecting)
            CancelReconnect();
        CloseConfirmation();
        _pendingMultiplayerEntry = null;
        _appBootstrap?.GetCancelStack()?.Unregister(this);
        CloseMultiplayerMenu(true);
        CloseNewGameSettings();
        if (view == null)
            return;

        view.GalaxySizeSelected -= SelectGalaxySize;
        view.DifficultySelected -= SelectGameDifficulty;
        view.StartGameRequested -= HandleStartGameRequested;
        view.VictoryConditionToggleRequested -= HandleVictoryConditionToggleRequested;
        view.SaveLoadMenuRequested -= OpenSaveLoadMenu;
        view.ExitRequested -= ExitApplication;
        view.CreditsRequested -= ShowCredits;
        view.NewGameSettingsRequested -= OpenNewGameSettings;
        view.MultiplayerRequested -= OpenMultiplayerMenu;
        view.AudioCueRequested -= PlayAudioCue;
        if (_multiplayerCoordinator != null)
            _multiplayerCoordinator.LaunchRequested -= HandleMultiplayerLaunchRequested;
        _analyticsConsentView.Allowed -= HandleAnalyticsAllowed;
        _analyticsConsentView.Declined -= HandleAnalyticsDeclined;
        _legalDocumentsView.MultiplayerAccepted -= HandleMultiplayerAgreementAccepted;
        _legalDocumentsView.Closed -= HandleLegalDocumentsClosed;
        _legalDocumentsView.Hide();

        if (_appInputController != null)
        {
            _appInputController.OptionsMenuRequested -= HandleOptionsMenuRequested;
            _appInputController = null;
        }
    }

    /// <summary>
    /// Destroys the Options menu.
    /// </summary>
    private void OnDestroy()
    {
        if (_appBootstrap != null)
        {
            _appBootstrap.LoadStarting -= HandleLoadStarting;
            _appBootstrap = null;
        }

        if (optionsMenuController == null)
            return;

        optionsMenuController.Dispose();
        optionsMenuController = null;
    }

    /// <summary>
    /// Conceals the outgoing menu before a loading transition begins.
    /// </summary>
    private void HandleLoadStarting()
    {
        if (mainMenuCanvas != null)
            mainMenuCanvas.enabled = false;
    }

    /// <summary>
    /// Opens the Options menu from its keyboard shortcut.
    /// </summary>
    private void HandleOptionsMenuRequested()
    {
        if (mainMenuCanvas?.enabled != true || IsStartupConsentBlocking)
            return;

        if (TryCancel())
            return;
        EnsureOptionsController();
        if (optionsMenuController == null)
            return;

        if (optionsMenuController.IsOpen)
            optionsMenuController.TryCancel();
        else
            OpenOptions(OptionsMenuTab.Gameplay);
    }

    /// <summary>
    /// Selects the player faction for the next game.
    /// </summary>
    /// <param name="factionId">The configured faction identifier.</param>
    internal void SelectFaction(string factionId)
    {
        if (IsStartupConsentBlocking)
            return;
        GameLaunchContext.Summary.PlayerFactionID = factionId;
    }

    /// <summary>
    /// Selects the galaxy size for the next game.
    /// </summary>
    /// <param name="size">The selected galaxy size.</param>
    internal void SelectGalaxySize(GameSize size)
    {
        if (IsStartupConsentBlocking)
            return;
        GameLaunchContext.Summary.GalaxySize = size;
    }

    /// <summary>
    /// Selects the victory condition for the next game and refreshes its presentation.
    /// </summary>
    /// <param name="condition">The selected victory condition.</param>
    internal void SelectVictoryCondition(GameVictoryCondition condition)
    {
        if (IsStartupConsentBlocking)
            return;
        currentVictoryCondition = condition;
        GameLaunchContext.Summary.VictoryCondition = condition;
        view?.RenderVictoryCondition(condition);
    }

    /// <summary>
    /// Selects the difficulty for the next game.
    /// </summary>
    /// <param name="difficulty">The selected difficulty.</param>
    internal void SelectGameDifficulty(GameDifficulty difficulty)
    {
        if (IsStartupConsentBlocking)
            return;
        GameLaunchContext.Summary.Difficulty = difficulty;
    }

    /// <summary>
    /// Applies the requested faction selection and starts a new game.
    /// </summary>
    /// <param name="factionId">The configured faction identifier.</param>
    private void HandleStartGameRequested(string factionId)
    {
        if (IsStartupConsentBlocking)
            return;
        SelectFaction(factionId);
        StartGame();
    }

    /// <summary>
    /// Applies negotiated multiplayer settings and enters the strategy scene.
    /// </summary>
    /// <param name="configuration">The synchronized launch configuration.</param>
    private void HandleMultiplayerLaunchRequested(MultiplayerLaunchConfiguration configuration)
    {
        _reconnecting = false;
        CloseConfirmation();
        CloseMultiplayerMenu(false);
    }

    /// <summary>
    /// Toggles between the supported victory conditions.
    /// </summary>
    private void HandleVictoryConditionToggleRequested()
    {
        if (IsStartupConsentBlocking)
            return;
        SelectVictoryCondition(
            currentVictoryCondition == GameVictoryCondition.Conquest
                ? GameVictoryCondition.Headquarters
                : GameVictoryCondition.Conquest
        );
    }

    /// <summary>
    /// Plays a UI audio cue emitted by the view.
    /// </summary>
    /// <param name="resourcePath">The audio content address.</param>
    private void PlayAudioCue(string resourcePath)
    {
        AudioManager.EnsureExists().PlaySfx(resourcePath);
    }

    /// <summary>
    /// Plays the credits cutscene and restores menu music when it finishes.
    /// </summary>
    private void ShowCredits()
    {
        if (IsStartupConsentBlocking)
            return;
        AudioManager.EnsureExists().FadeOutMusic(creditsMusicFadeDuration);
        AppBootstrap.EnsureExists().GetCutsceneManager().Play(_creditsVideoPath, OnCreditsFinished);
    }

    /// <summary>
    /// Opens the Options menu.
    /// </summary>
    private void OpenSaveLoadMenu()
    {
        if (IsStartupConsentBlocking)
            return;
        PlayAudioCue(_selectSfxPath);
        OpenOptions(OptionsMenuTab.SaveLoad);
    }

    /// <summary>
    /// Updates the Options menu.
    /// </summary>
    private void Update()
    {
        bool open =
            optionsMenuController?.IsOpen == true
            || _newGameSettingsWindow != null
            || _multiplayerWindow != null;
        view?.RenderOptionsOverlay(open);
        TryOfferReconnect();
        if (optionsMenuController?.IsOpen != true || !optionsDirty)
            return;

        optionsDirty = false;
        optionsMenuController.RenderWindows();
    }

    /// <summary>
    /// Creates the Options menu when it is first opened.
    /// </summary>
    private void EnsureOptionsController()
    {
        if (optionsMenuController != null)
            return;
        if (_optionsMenuPrefab == null)
        {
            GameLogger.Warning(
                "MainMenu options prefab is not assigned; run Build Main Menu UI to wire it."
            );
            return;
        }

        AppBootstrap bootstrap = AppBootstrap.EnsureExists();
        UIWindowManager windowManager = view.OptionsWindowManager;
        windowManager.SetContentSource(bootstrap.GetContentAssets());
        optionsMenuController = new OptionsMenuController(
            _optionsMenuPrefab,
            view.OptionsWindowLayer,
            windowManager,
            () => view.GetOptionsWindowPosition(_optionsMenuPrefab),
            windowManager.DestroyWindow,
            bootstrap,
            bootstrap.GetGameSessionManager().SaveGameAsync,
            bootstrap.LoadSaveAsync,
            MarkOptionsDirty,
            SaveGameManager.Instance,
            showModsTab: true
        );
    }

    /// <summary>
    /// Opens the requested Options page over the authored Main Menu dimmer.
    /// </summary>
    /// <param name="tab">The page to show initially.</param>
    private void OpenOptions(OptionsMenuTab tab)
    {
        if (
            _analyticsConsentView.IsVisible
            || _newGameSettingsWindow != null
            || _multiplayerWindow != null
        )
            return;
        EnsureOptionsController();
        if (optionsMenuController == null)
            return;

        view.RenderOptionsOverlay(true);
        try
        {
            optionsMenuController.Open(tab);
            optionsMenuController.RenderWindows();
            optionsDirty = false;
        }
        catch
        {
            view.RenderOptionsOverlay(false);
            throw;
        }
    }

    /// <summary>
    /// Requests that the open Options window render its changed state.
    /// </summary>
    private void MarkOptionsDirty()
    {
        optionsDirty = true;
    }

    /// <summary>
    /// Opens the custom-game editor through the existing main-menu modal surface.
    /// </summary>
    private void OpenNewGameSettings()
    {
        if (
            _analyticsConsentView.IsVisible
            || _newGameSettingsWindow != null
            || _multiplayerWindow != null
            || optionsMenuController?.IsOpen == true
        )
            return;
        AppBootstrap bootstrap = AppBootstrap.EnsureExists();
        UIWindowManager manager = view.OptionsWindowManager;
        manager.SetContentSource(bootstrap.GetContentAssets());
        Vector2Int position = view.GetOptionsWindowPosition(_newGameSettingsPrefab);
        Vector2 size = ((RectTransform)_newGameSettingsPrefab.transform).sizeDelta;
        view.RenderOptionsOverlay(true);
        try
        {
            _newGameSettingsWindow = manager.CreateWindow(
                _newGameSettingsPrefab,
                view.OptionsWindowLayer,
                "NewGameSettings",
                position.x,
                position.y,
                new Vector2Int((int)size.x, (int)size.y),
                true,
                true,
                false,
                false,
                out _newGameSettingsView
            );
            _newGameSettingsView.CloseRequested += CloseNewGameSettings;
            _newGameSettingsView.StartRequested += StartCustomGame;
            _newGameSettingsView.ContentPackRequested += SelectCustomGameContentPack;
            ContentPack contentPack = bootstrap.GetContentPack();
            _customGamePacks = ContentPackLoader.DiscoverPacks(contentPack.ContentRootPath);
            _newGameSettingsView.Initialize(
                contentPack,
                GameLaunchContext.Summary,
                _optionsMenuPrefab,
                _customGamePresetManager,
                _customGamePacks
            );
            bootstrap.GetCancelStack()?.Register(this);
        }
        catch
        {
            CloseNewGameSettings();
            throw;
        }
    }

    /// <summary>
    /// Loads a selected content pack and resets content-dependent custom-game choices.
    /// </summary>
    /// <param name="packID">The selected installed content-pack identity.</param>
    private void SelectCustomGameContentPack(string packID)
    {
        if (_newGameSettingsView == null || string.IsNullOrWhiteSpace(packID))
            return;

        ContentPackDefinition definition = _customGamePacks?.FirstOrDefault(candidate =>
            string.Equals(candidate.ID, packID, StringComparison.Ordinal)
        );
        if (definition == null)
            throw new InvalidDataException($"Content pack '{packID}' is not installed.");

        ContentPack current = _appBootstrap.GetContentPack();
        string[] disabledModIDs = _appBootstrap
            .GetUserSettingsManager()
            .Settings.Content.DisabledModIDs;
        ContentPack selected = ContentPackLoader.OpenExact(
            current.ContentRootPath,
            definition.ID,
            definition.DefaultScenarioID,
            disabledModIDs
        );
        _newGameSettingsView.Initialize(
            selected,
            GameLaunchContext.CreateDefaultSummary(selected),
            _optionsMenuPrefab,
            _customGamePresetManager,
            _customGamePacks
        );
    }

    /// <summary>
    /// Applies the complete custom-game configuration and starts its strategy scene.
    /// </summary>
    /// <param name="contentPack">The selected content pack.</param>
    /// <param name="summary">The configured launch summary.</param>
    private void StartCustomGame(ContentPack contentPack, GameSummary summary)
    {
        currentVictoryCondition = summary.VictoryCondition;
        if (_appBootstrap.GetContentPack().MatchesContentIdentity(summary))
        {
            GameLaunchContext.ApplyCustomGameSummary(summary);
            StartGame();
            return;
        }

        LoadingScreenController.StartCustomGame(_appBootstrap, contentPack, summary);
        CloseNewGameSettings();
    }

    /// <summary>
    /// Closes the custom-game editor.
    /// </summary>
    private void CloseNewGameSettings()
    {
        if (_newGameSettingsWindow == null)
            return;
        if (_newGameSettingsView != null)
        {
            _newGameSettingsView.CloseRequested -= CloseNewGameSettings;
            _newGameSettingsView.StartRequested -= StartCustomGame;
            _newGameSettingsView.ContentPackRequested -= SelectCustomGameContentPack;
        }
        AppBootstrap.Instance?.GetCancelStack()?.Unregister(this);
        if (view?.OptionsWindowManager != null)
        {
            view.OptionsWindowManager.DestroyWindow(_newGameSettingsWindow);
            view.RenderOptionsOverlay(false);
        }
        _newGameSettingsWindow = null;
        _newGameSettingsView = null;
    }

    /// <summary>
    /// Presents the required documents when their current version has not been accepted.
    /// </summary>
    private void OpenMultiplayerMenu()
    {
        if (
            IsStartupConsentBlocking
            || _multiplayerWindow != null
            || _newGameSettingsWindow != null
            || optionsMenuController?.IsOpen == true
        )
            return;
        if (_multiplayerMenuPrefab == null)
        {
            GameLogger.Warning(
                "MainMenu multiplayer prefab is not assigned; run Build Main Menu UI to wire it."
            );
            return;
        }

        RequestMultiplayerEntry(ContinueToMultiplayer);
    }

    /// <summary>
    /// Persists explicit agreement before closing the prompt and entering multiplayer.
    /// </summary>
    private void HandleMultiplayerAgreementAccepted()
    {
        try
        {
            _appBootstrap.GetUserSettingsManager().AcceptAgreement(_legalDocumentsView.Agreement);
        }
        catch (Exception exception)
        {
            _legalDocumentsView.ShowError("Your acceptance could not be saved. Please try again.");
            GameLogger.LogException(exception);
            return;
        }

        _legalDocumentsView.Hide();
        Action continueEntry = _pendingMultiplayerEntry;
        _pendingMultiplayerEntry = null;
        continueEntry?.Invoke();
    }

    /// <summary>
    /// Opens multiplayer discovery after the current documents have been accepted.
    /// </summary>
    private void ContinueToMultiplayer()
    {
        _appBootstrap.GetCancelStack()?.Unregister(this);
        AppBootstrap bootstrap = AppBootstrap.EnsureExists();
        UIWindowManager manager = view.OptionsWindowManager;
        manager.SetContentSource(bootstrap.GetContentAssets());
        Vector2Int position = view.GetOptionsWindowPosition(_multiplayerMenuPrefab);
        Vector2 size = ((RectTransform)_multiplayerMenuPrefab.transform).sizeDelta;
        view.RenderOptionsOverlay(true);
        try
        {
            _multiplayerWindow = manager.CreateWindow(
                _multiplayerMenuPrefab,
                view.OptionsWindowLayer,
                "MultiplayerMenu",
                position.x,
                position.y,
                new Vector2Int((int)size.x, (int)size.y),
                true,
                true,
                false,
                false,
                out _multiplayerMenuView
            );
            _multiplayerMenuView.CloseRequested += HandleMultiplayerCloseRequested;
            _multiplayerMenuView.ConfirmationRequested += RequestConfirmation;
            _multiplayerMenuView.ReconnectRequested += ReconnectPreviousGame;
            _multiplayerMenuView.Initialize(_multiplayerCoordinator, GameLaunchContext.Summary);
            bootstrap.GetCancelStack()?.Register(this);
        }
        catch
        {
            CloseMultiplayerMenu(true);
            throw;
        }
    }

    /// <summary>
    /// Releases the privacy notice's cancellation handler when the player returns to the menu.
    /// </summary>
    private void HandleLegalDocumentsClosed()
    {
        _pendingMultiplayerEntry = null;
        _appBootstrap.GetCancelStack()?.Unregister(this);
    }

    /// <summary>
    /// Closes the multiplayer modal in response to its Back command.
    /// </summary>
    private void HandleMultiplayerCloseRequested()
    {
        CloseMultiplayerMenu(true);
    }

    /// <summary>
    /// Removes a multiplayer window still owned by this menu and optionally ends its session.
    /// </summary>
    /// <param name="disconnect">Whether to close the peer and directory listing.</param>
    private void CloseMultiplayerMenu(bool disconnect)
    {
        if (_multiplayerWindow == null)
            return;

        if (_multiplayerMenuView != null)
        {
            _multiplayerMenuView.CloseRequested -= HandleMultiplayerCloseRequested;
            _multiplayerMenuView.ConfirmationRequested -= RequestConfirmation;
            _multiplayerMenuView.ReconnectRequested -= ReconnectPreviousGame;
        }
        AppBootstrap.Instance?.GetCancelStack()?.Unregister(this);
        if (disconnect)
            _multiplayerCoordinator?.Disconnect();
        if (view?.OptionsWindowManager != null)
            view.OptionsWindowManager.DestroyWindow(_multiplayerWindow);
        _multiplayerWindow = null;
        _multiplayerMenuView = null;
        bool anotherModalOpen =
            optionsMenuController?.IsOpen == true || _newGameSettingsWindow != null;
        view?.RenderOptionsOverlay(anotherModalOpen);
    }

    /// <summary>
    /// Handles cancellation only while this scene owns the custom-game editor.
    /// </summary>
    /// <returns>Whether the preview consumed cancellation.</returns>
    public bool TryCancel()
    {
        if (_confirmationDialog?.TryCancel() == true)
            return true;
        if (_legalDocumentsView?.TryCancel() == true)
            return true;
        if (_analyticsConsentView.IsVisible)
            return true;
        if (_multiplayerMenuView != null)
            return _multiplayerMenuView.TryCancel();
        if (_newGameSettingsWindow == null)
            return false;
        CloseNewGameSettings();
        return true;
    }

    /// <summary>
    /// Presents a saved continuation once the main menu and authenticated account are ready.
    /// </summary>
    private void TryOfferReconnect()
    {
        if (
            !_menuReady
            || mainMenuCanvas?.enabled != true
            || _reconnectOffered
            || !_multiplayerCoordinator.HasRecovery
            || _multiplayerCoordinator.IsSigningIn
            || _multiplayerCoordinator.IsConnecting
            || IsStartupConsentBlocking
            || optionsMenuController?.IsOpen == true
            || _newGameSettingsWindow != null
            || view.OptionsWindowManager.Windows.Any(window =>
                window?.Modal == true && window != _multiplayerWindow
            )
        )
            return;
        _reconnectOffered = true;
        string message =
            _multiplayerCoordinator.RecoveryRole == MultiplayerRole.Host
                ? "Resume your previous game as host?"
                : "Reconnect to your previous game?";
        RequestConfirmation(message, ResumePreviousGame);
        _pendingCancelAction = DiscardPreviousGame;
    }

    /// <summary>
    /// Uses the existing multiplayer connection presentation after explicit reconnection approval.
    /// </summary>
    private void ResumePreviousGame()
    {
        RequestMultiplayerEntry(() => ReconnectPreviousGame(null));
    }

    /// <summary>
    /// Restores an interrupted campaign directly from the menu with cancellable progress.
    /// </summary>
    /// <param name="selected">The explicitly selected recovery room, when reconnecting from discovery.</param>
    private async void ReconnectPreviousGame(MultiplayerGameListing selected)
    {
        if (_reconnecting)
            return;
        CloseMultiplayerMenu(true);
        _multiplayerCoordinator.ClearFailure();
        _reconnectOffered = true;
        _reconnecting = true;
        RenderReconnect();
        try
        {
            await _multiplayerCoordinator.ResumeAsync(selected);
            if (this != null)
                RenderReconnect();
        }
        catch (Exception exception)
        {
            if (this != null && _reconnecting)
            {
                Debug.LogException(exception);
                ShowReconnectFailure(exception);
            }
        }
    }

    /// <summary>
    /// Routes connection changes to their active presentation owner.
    /// </summary>
    private void HandleMultiplayerStateChanged()
    {
        if (_reconnecting)
            RenderReconnect();
        else
            TryOfferReconnect();
    }

    /// <summary>
    /// Updates cancellable reconnection progress or presents its failure in the shared dialog.
    /// </summary>
    private void RenderReconnect()
    {
        if (!_reconnecting)
            return;
        if (_multiplayerCoordinator.Failure is Exception failure)
        {
            ShowReconnectFailure(failure);
            return;
        }
        string message = _multiplayerCoordinator.IsRejoining
            ? _multiplayerCoordinator.Status
            : "Reconnecting to your previous game...";
        EnsureConfirmationWindow();
        _pendingConfirmAction = null;
        _pendingCancelAction = CancelReconnect;
        _confirmationDialog.ShowProgress(message);
    }

    /// <summary>
    /// Leaves a failed campaign available for retry and shows its reason outside discovery.
    /// </summary>
    /// <param name="failure">The failure returned by loading or connecting the saved campaign.</param>
    private void ShowReconnectFailure(Exception failure)
    {
        _reconnecting = false;
        _multiplayerCoordinator.ClearFailure();
        RequestConfirmation("Unable to reconnect.\n\n" + failure.Message, null);
    }

    /// <summary>
    /// Cancels this connection attempt while retaining the saved campaign for a later retry.
    /// </summary>
    private void CancelReconnect()
    {
        _reconnecting = false;
        CloseConfirmation();
        _multiplayerCoordinator.Disconnect();
    }

    /// <summary>
    /// Clears a declined reconnection offer while leaving ordinary saved games intact.
    /// </summary>
    private void DiscardPreviousGame()
    {
        try
        {
            _multiplayerCoordinator.DiscardRecovery();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RequestConfirmation(
                "The reconnect offer could not be cleared. Please try again.",
                null
            );
        }
    }

    /// <summary>
    /// Continues an approved multiplayer entry after any required document acceptance.
    /// </summary>
    /// <param name="continueEntry">The selected multiplayer entry action.</param>
    private void RequestMultiplayerEntry(Action continueEntry)
    {
        if (
            _legalDocumentsView.Agreement.IsAccepted(
                _appBootstrap.GetUserSettingsManager().Settings
            )
        )
        {
            continueEntry();
            return;
        }
        _pendingMultiplayerEntry = continueEntry;
        _legalDocumentsView.ShowMultiplayerAgreement();
        _appBootstrap.GetCancelStack()?.Register(this);
    }

    /// <summary>
    /// Hosts the shared confirmation artwork in the menu's full-screen modal canvas.
    /// </summary>
    /// <param name="message">The displayed message.</param>
    /// <param name="onConfirmed">The action to approve, or null for an acknowledgment.</param>
    private void RequestConfirmation(string message, Action onConfirmed)
    {
        _pendingConfirmAction = onConfirmed;
        _pendingCancelAction = null;
        EnsureConfirmationWindow();
        if (onConfirmed == null)
            _confirmationDialog.ShowAcknowledgment(message);
        else
            _confirmationDialog.Show(message);
    }

    /// <summary>
    /// Creates the shared full-screen modal host once for this menu's current notice.
    /// </summary>
    private void EnsureConfirmationWindow()
    {
        UIWindowManager manager = view.OptionsWindowManager;
        if (_confirmationWindow == null)
        {
            manager.SetContentSource(_appBootstrap.GetContentAssets());
            _confirmationWindow = manager.CreateWindow(
                _confirmationTemplate,
                _confirmationTemplate.transform.parent,
                "MainMenuConfirmationDialog",
                0,
                0,
                Vector2Int.RoundToInt(((RectTransform)_confirmationTemplate.transform).sizeDelta),
                true,
                true,
                false,
                false,
                out _confirmationDialog
            );
            RectTransform rectangle = (RectTransform)_confirmationDialog.transform;
            rectangle.anchorMin = Vector2.zero;
            rectangle.anchorMax = Vector2.one;
            rectangle.offsetMin = rectangle.offsetMax = Vector2.zero;
            _confirmationDialog.InitializeContent(_appBootstrap.GetContentAssets());
            _confirmationDialog.Confirmed += HandleConfirmationAccepted;
            _confirmationDialog.Canceled += HandleConfirmationDeclined;
        }
        _appBootstrap.GetCancelStack()?.Register(this);
    }

    /// <summary>
    /// Runs the approved action after releasing the modal window.
    /// </summary>
    private void HandleConfirmationAccepted()
    {
        Action confirmed = _pendingConfirmAction;
        CloseConfirmation();
        confirmed?.Invoke();
    }

    /// <summary>
    /// Runs any decline action after releasing the modal window.
    /// </summary>
    private void HandleConfirmationDeclined()
    {
        Action declined = _pendingCancelAction;
        CloseConfirmation();
        declined?.Invoke();
    }

    /// <summary>
    /// Releases the full-screen modal without interpreting scene teardown as a player decision.
    /// </summary>
    private void CloseConfirmation()
    {
        _pendingConfirmAction = _pendingCancelAction = null;
        if (_confirmationDialog != null)
        {
            _confirmationDialog.Confirmed -= HandleConfirmationAccepted;
            _confirmationDialog.Canceled -= HandleConfirmationDeclined;
        }
        UIWindow window = _confirmationWindow;
        _confirmationWindow = null;
        _confirmationDialog = null;
        if (window != null && view?.OptionsWindowManager != null)
            view.OptionsWindowManager.DestroyWindow(window);
        if (
            _multiplayerWindow == null
            && _newGameSettingsWindow == null
            && _legalDocumentsView?.IsVisible != true
        )
            _appBootstrap?.GetCancelStack()?.Unregister(this);
    }

    /// <summary>
    /// Persists and applies an affirmative analytics decision.
    /// </summary>
    private void HandleAnalyticsAllowed()
    {
        ApplyAnalyticsConsent(UserAnalyticsConsent.Granted);
    }

    /// <summary>
    /// Persists and applies a negative analytics decision.
    /// </summary>
    private void HandleAnalyticsDeclined()
    {
        ApplyAnalyticsConsent(UserAnalyticsConsent.Denied);
    }

    /// <summary>
    /// Saves an explicit analytics decision and removes the first-run input gate.
    /// </summary>
    /// <param name="consent">The explicit player decision.</param>
    private void ApplyAnalyticsConsent(UserAnalyticsConsent consent)
    {
        UserSettingsManager settingsManager = _appBootstrap.GetUserSettingsManager();
        settingsManager.Settings.Analytics.Consent = consent;
        settingsManager.Save();
        settingsManager.Apply();
        _analyticsConsentView.Render(false);
    }

    /// <summary>
    /// Shows the first-run input gate whenever no explicit analytics choice can be established.
    /// </summary>
    /// <param name="bootstrap">The initialized application bootstrap, or null after startup failure.</param>
    private void RenderAnalyticsConsentGate(AppBootstrap bootstrap)
    {
        UserSettings settings = bootstrap?.GetUserSettingsManager()?.Settings;
        UserAnalyticsConsent? consent = settings?.Analytics?.Consent;
        _analyticsConsentView.Render(consent == null || consent == UserAnalyticsConsent.Undecided);
    }

    /// <summary>
    /// Exits the player, or stops Play Mode when testing the command in the Unity Editor.
    /// </summary>
    private void ExitApplication()
    {
        if (IsStartupConsentBlocking)
            return;
        QuitApplication();
    }

    /// <summary>
    /// Ends the application when the player exits.
    /// </summary>
    private void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Restarts main-menu music after the credits cutscene.
    /// </summary>
    private void OnCreditsFinished()
    {
        ContentPack contentPack = AppBootstrap.EnsureExists().GetContentPack();
        AudioManager.EnsureExists().PlayTrack(GetMenuMusicPath(contentPack.Definition), true);
    }

    /// <summary>
    /// Resolves the active pack's main-menu music address.
    /// </summary>
    /// <param name="definition">The active content-pack definition.</param>
    /// <returns>The configured audio address.</returns>
    internal static string GetMenuMusicPath(ContentPackDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition?.MainMenuMusicPath))
            throw new InvalidDataException("ContentPack.MainMenuMusicPath is required.");

        return definition.MainMenuMusicPath.Trim();
    }

    /// <summary>
    /// Finalizes new-game launch state and opens the strategy scene.
    /// </summary>
    private void StartGame()
    {
        if (IsStartupConsentBlocking)
            return;
        GameLaunchContext.IsLoadGame = false;
        GameLaunchContext.SaveFileName = null;
        GameLaunchContext.PlayIntroCutscene = !GameLaunchContext.IsMultiplayer;

        // Start a new game session.
        AppBootstrap.Instance.GetGameSessionManager()?.EndGame();

        AudioManager.EnsureExists().StopMusic();
        AppBootstrap.Instance.LoadScene("StrategyView");
    }
}
