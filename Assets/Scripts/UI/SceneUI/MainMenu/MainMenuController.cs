using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Rebellion.Game;
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
    private OptionsMenuView _optionsMenuPrefab;

    [SerializeField]
    private NewGameSettingsView _newGameSettingsPrefab;

    [SerializeField]
    private AnalyticsConsentView _analyticsConsentView;

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

    private bool IsAnalyticsConsentBlocking => _analyticsConsentView?.IsVisible == true;

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
        view.AudioCueRequested += PlayAudioCue;
        _analyticsConsentView.Allowed += HandleAnalyticsAllowed;
        _analyticsConsentView.Declined += HandleAnalyticsDeclined;
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
        }
        catch (System.Exception exception)
        {
            if (!LoadingScreenController.CompleteFailedRecovery(bootstrap, exception))
            {
                if (mainMenuCanvas != null)
                {
                    RenderAnalyticsConsentGate(bootstrap);
                    mainMenuCanvas.enabled = true;
                }
                Debug.LogException(exception);
            }
        }
    }

    /// <summary>
    /// Unsubscribes from semantic view requests when the controller is disabled.
    /// </summary>
    private void OnDisable()
    {
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
        view.AudioCueRequested -= PlayAudioCue;
        _analyticsConsentView.Allowed -= HandleAnalyticsAllowed;
        _analyticsConsentView.Declined -= HandleAnalyticsDeclined;

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
        if (_analyticsConsentView.IsVisible)
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
        if (IsAnalyticsConsentBlocking)
            return;
        GameLaunchContext.Summary.PlayerFactionID = factionId;
    }

    /// <summary>
    /// Selects the galaxy size for the next game.
    /// </summary>
    /// <param name="size">The selected galaxy size.</param>
    internal void SelectGalaxySize(GameSize size)
    {
        if (IsAnalyticsConsentBlocking)
            return;
        GameLaunchContext.Summary.GalaxySize = size;
    }

    /// <summary>
    /// Selects the victory condition for the next game and refreshes its presentation.
    /// </summary>
    /// <param name="condition">The selected victory condition.</param>
    internal void SelectVictoryCondition(GameVictoryCondition condition)
    {
        if (IsAnalyticsConsentBlocking)
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
        if (IsAnalyticsConsentBlocking)
            return;
        GameLaunchContext.Summary.Difficulty = difficulty;
    }

    /// <summary>
    /// Applies the requested faction selection and starts a new game.
    /// </summary>
    /// <param name="factionId">The configured faction identifier.</param>
    private void HandleStartGameRequested(string factionId)
    {
        if (IsAnalyticsConsentBlocking)
            return;
        SelectFaction(factionId);
        StartGame();
    }

    /// <summary>
    /// Toggles between the supported victory conditions.
    /// </summary>
    private void HandleVictoryConditionToggleRequested()
    {
        if (IsAnalyticsConsentBlocking)
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
        if (IsAnalyticsConsentBlocking)
            return;
        AudioManager.EnsureExists().FadeOutMusic(creditsMusicFadeDuration);
        AppBootstrap.EnsureExists().GetCutsceneManager().Play(_creditsVideoPath, OnCreditsFinished);
    }

    /// <summary>
    /// Opens the Options menu.
    /// </summary>
    private void OpenSaveLoadMenu()
    {
        if (IsAnalyticsConsentBlocking)
            return;
        PlayAudioCue(_selectSfxPath);
        OpenOptions(OptionsMenuTab.SaveLoad);
    }

    /// <summary>
    /// Updates the Options menu.
    /// </summary>
    private void Update()
    {
        bool open = optionsMenuController?.IsOpen == true || _newGameSettingsWindow != null;
        view?.RenderOptionsOverlay(open);
        if (!open || !optionsDirty)
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
            Debug.LogWarning(
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
            bootstrap.GetRuntime().SaveGame,
            bootstrap.LoadSave,
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
        if (_analyticsConsentView.IsVisible || _newGameSettingsWindow != null)
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
            string.Equals(candidate.ID, packID, System.StringComparison.Ordinal)
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
    /// Handles cancellation only while this scene owns the custom-game editor.
    /// </summary>
    /// <returns>Whether the preview consumed cancellation.</returns>
    public bool TryCancel()
    {
        if (_analyticsConsentView.IsVisible)
            return true;
        if (_newGameSettingsWindow == null)
            return false;
        CloseNewGameSettings();
        return true;
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
        UserAnalyticsConsent? consent = bootstrap
            ?.GetUserSettingsManager()
            ?.Settings?.Analytics?.Consent;
        _analyticsConsentView.Render(consent == null || consent == UserAnalyticsConsent.Undecided);
    }

    /// <summary>
    /// Exits the player, or stops Play Mode when testing the command in the Unity Editor.
    /// </summary>
    private void ExitApplication()
    {
        if (IsAnalyticsConsentBlocking)
            return;
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
        if (IsAnalyticsConsentBlocking)
            return;
        GameLaunchContext.IsLoadGame = false;
        GameLaunchContext.SaveFileName = null;
        GameLaunchContext.PlayIntroCutscene = true;

        // Start a new game session.
        AppBootstrap.Instance.GetRuntime()?.EndGame();

        AudioManager.EnsureExists().StopMusic();
        AppBootstrap.Instance.LoadScene("StrategyView");
    }
}
