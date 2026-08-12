using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rebellion.Game;
using UnityEngine;

/// <summary>
/// Owns main-menu launch state, audio, cutscenes, and scene navigation.
/// </summary>
public sealed class MainMenuController : MonoBehaviour, IOptionsMenuActions
{
    private const string _creditsVideoPath = "Application/Credits/Videos/credits";
    private const string _menuMusicPath = "Application/MainMenu/Audio/battle-of-endor-1-medley";

    [SerializeField]
    private MainMenuView view;

    [SerializeField]
    private OptionsMenuView _optionsMenuPrefab;

    [SerializeField]
    [Min(0f)]
    private float creditsMusicFadeDuration = 0.5f;

    private GameVictoryCondition currentVictoryCondition;
    private Canvas mainMenuCanvas;
    private OptionsMenuController optionsMenuController;
    private bool optionsDirty;
    private FactionThemeLibrary factionThemeLibrary;
    private ContentAssets contentAssets;
    private GameRuntime _settingsRuntime;

    /// <summary>
    /// Resets launch state and renders the authored initial selections.
    /// </summary>
    private void Awake()
    {
        if (Application.isPlaying)
        {
            if (view == null)
                throw new MissingReferenceException($"{name} has no main-menu view.");
        }

        ContentPack contentPack = AppBootstrap.EnsureExists().GetContentPack();
        GameLaunchContext.Reset(contentPack);
        currentVictoryCondition = GameLaunchContext.Summary.VictoryCondition;

        if (view == null)
            return;

        mainMenuCanvas = view.transform.root.GetComponentInChildren<Canvas>(true);
        if (mainMenuCanvas == null)
            throw new MissingReferenceException($"{name} has no main-menu canvas.");
        mainMenuCanvas.enabled = false;

        if (view.TryGetSelectedDifficulty(out GameDifficulty difficulty))
            SelectGameDifficulty(difficulty);
    }

    /// <summary>
    /// Subscribes to semantic view requests while the controller is active.
    /// </summary>
    private void OnEnable()
    {
        if (view == null)
            return;

        AppBootstrap.EnsureExists().GetInputController()?.SetContext(InputContext.Menu);

        view.GalaxySizeSelected += SelectGalaxySize;
        view.DifficultySelected += SelectGameDifficulty;
        view.StartGameRequested += HandleStartGameRequested;
        view.VictoryConditionToggleRequested += HandleVictoryConditionToggleRequested;
        view.LoadGameRequested += OpenLoadGameMenu;
        view.ExitRequested += ExitApplication;
        view.CreditsRequested += ShowCredits;
        view.AudioCueRequested += PlayAudioCue;

        // Options Menu.
        _settingsRuntime = AppBootstrap.EnsureExists().GetRuntime();
        if (_settingsRuntime != null)
            _settingsRuntime.ToggleSettingsMenuRequested += HandleToggleSettingsMenu;
    }

    /// <summary>
    /// Initializes local content, preloads immediate UI cues, and starts main-menu music.
    /// </summary>
    private async void Start()
    {
        try
        {
            AppBootstrap bootstrap = AppBootstrap.EnsureExists();
            Task contentTask = bootstrap.InitializeMainMenuSceneAsync();
            Task modelTask = Task.WhenAll(
                view.transform.root.GetComponentsInChildren<ContentModelBinding>(true)
                    .Select(binding => binding.Ready)
            );
            await Task.WhenAll(contentTask, modelTask);
            ContentPack contentPack = bootstrap.GetContentPack();
            view?.InitializeContent(bootstrap.GetContentAssets());
            view?.RenderVictoryCondition(currentVictoryCondition);
            FactionThemeLibrary themeLibrary = new FactionThemeLibrary(
                contentPack.GameData.FactionThemes
            );
            view?.RenderFactions(
                contentPack.Scenario.PlayableFactionIDs,
                themeLibrary.GetTheme,
                bootstrap.GetContentAssets().GetTexture
            );
            AudioManager audioManager = AudioManager.EnsureExists();
            audioManager.PreloadSfx(view?.GetAudioCuePaths());
            audioManager.PlayTrack(_menuMusicPath, true);
            mainMenuCanvas.enabled = true;
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    /// <summary>
    /// Unsubscribes from semantic view requests when the controller is disabled.
    /// </summary>
    private void OnDisable()
    {
        if (view == null)
            return;

        view.GalaxySizeSelected -= SelectGalaxySize;
        view.DifficultySelected -= SelectGameDifficulty;
        view.StartGameRequested -= HandleStartGameRequested;
        view.VictoryConditionToggleRequested -= HandleVictoryConditionToggleRequested;
        view.LoadGameRequested -= OpenLoadGameMenu;
        view.ExitRequested -= ExitApplication;
        view.CreditsRequested -= ShowCredits;
        view.AudioCueRequested -= PlayAudioCue;

        if (_settingsRuntime != null)
            _settingsRuntime.ToggleSettingsMenuRequested -= HandleToggleSettingsMenu;
    }

    /// <summary>
    /// Destroys the Options menu.
    /// </summary>
    private void OnDestroy()
    {
        if (optionsMenuController == null)
            return;

        AppBootstrap.Instance?.GetCancelStack()?.Unregister(optionsMenuController);
        optionsMenuController.Dispose();
        optionsMenuController = null;
    }

    /// <summary>
    /// Opens the Options menu from its keyboard shortcut.
    /// </summary>
    private void HandleToggleSettingsMenu()
    {
        EnsureOptionsController();
        if (optionsMenuController == null)
            return;

        if (optionsMenuController.IsOpen)
            optionsMenuController.TryCancel();
        else
            OpenOptions(OptionsMenuTab.Graphics);
    }

    /// <summary>
    /// Selects the player faction for the next game.
    /// </summary>
    /// <param name="factionId">The configured faction identifier.</param>
    internal void SelectFaction(string factionId)
    {
        GameLaunchContext.Summary.PlayerFactionID = factionId;
    }

    /// <summary>
    /// Selects the galaxy size for the next game.
    /// </summary>
    /// <param name="size">The selected galaxy size.</param>
    internal void SelectGalaxySize(GameSize size)
    {
        GameLaunchContext.Summary.GalaxySize = size;
    }

    /// <summary>
    /// Selects the victory condition for the next game and refreshes its presentation.
    /// </summary>
    /// <param name="condition">The selected victory condition.</param>
    internal void SelectVictoryCondition(GameVictoryCondition condition)
    {
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
        GameLaunchContext.Summary.Difficulty = difficulty;
    }

    /// <summary>
    /// Applies the requested faction selection and starts a new game.
    /// </summary>
    /// <param name="factionId">The configured faction identifier.</param>
    private void HandleStartGameRequested(string factionId)
    {
        SelectFaction(factionId);
        StartGame();
    }

    /// <summary>
    /// Toggles between the supported victory conditions.
    /// </summary>
    private void HandleVictoryConditionToggleRequested()
    {
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
        AudioManager.EnsureExists().FadeOutMusic(creditsMusicFadeDuration);
        AppBootstrap.EnsureExists().GetCutsceneManager().Play(_creditsVideoPath, OnCreditsFinished);
    }

    /// <summary>
    /// Opens the Options menu.
    /// </summary>
    private void OpenLoadGameMenu()
    {
        OpenOptions(OptionsMenuTab.SaveLoad);
    }

    /// <summary>
    /// Updates the Options menu.
    /// </summary>
    private void Update()
    {
        bool open = optionsMenuController?.IsOpen == true;
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
        factionThemeLibrary = new FactionThemeLibrary(
            bootstrap.GetContentPack().GameData.FactionThemes
        );
        contentAssets = bootstrap.GetContentAssets();
        UIWindowManager windowManager = view.OptionsWindowManager;
        windowManager.SetContentSource(contentAssets);
        optionsMenuController = new OptionsMenuController(
            _optionsMenuPrefab,
            view.OptionsWindowLayer,
            windowManager,
            () => view.GetOptionsWindowPosition(_optionsMenuPrefab),
            windowManager.DestroyWindow,
            bootstrap.GetUserSettingsManager(),
            bootstrap.GetDisplayManager(),
            AudioManager.EnsureExists(),
            bootstrap.GetInputManager(),
            MarkOptionsDirty
        );
        optionsMenuController.Initialize(this);
        bootstrap.GetCancelStack()?.Register(optionsMenuController);
    }

    /// <summary>
    /// Opens the requested Options page over the authored Main Menu dimmer.
    /// </summary>
    /// <param name="tab">The page to show initially.</param>
    private void OpenOptions(OptionsMenuTab tab)
    {
        EnsureOptionsController();
        if (optionsMenuController == null)
            return;

        view.RenderOptionsOverlay(true);
        optionsMenuController.Open(tab);
        optionsMenuController.RenderWindows();
        optionsDirty = false;
    }

    /// <summary>
    /// Gets whether the Main Menu Options overlay can return to a running game.
    /// </summary>
    bool IOptionsMenuActions.CanReturnToGame => false;

    /// <summary>
    /// Gets whether the Main Menu Options overlay needs a separate Main Menu command.
    /// </summary>
    bool IOptionsMenuActions.CanReturnToMainMenu => false;

    /// <summary>
    /// Gets whether a running game is available to create or overwrite saves.
    /// </summary>
    bool IOptionsMenuActions.CanWriteSaves => false;

    /// <summary>
    /// Leaves the already-paused Main Menu unchanged while Options is open.
    /// </summary>
    void IOptionsMenuActions.PauseForOptions() { }

    /// <summary>
    /// Leaves the Main Menu input context unchanged when Options closes.
    /// </summary>
    void IOptionsMenuActions.ResumeFromOptions() { }

    /// <summary>
    /// Builds the save rows available from the Main Menu.
    /// </summary>
    /// <returns>The saved games in manager order.</returns>
    IReadOnlyList<OptionsSaveSlot> IOptionsMenuActions.GetSaveSlots()
    {
        List<OptionsSaveSlot> rows = new List<OptionsSaveSlot>();
        foreach (SaveGameEntry entry in SaveGameManager.Instance.GetSavedGames())
        {
            string displayName = string.IsNullOrEmpty(entry.Metadata?.SaveDisplayName)
                ? entry.FileName
                : entry.Metadata.SaveDisplayName;
            string date =
                entry.Metadata != null
                    ? entry.Metadata.LastSavedUtc.ToLocalTime().ToString("g")
                    : string.Empty;
            rows.Add(
                new OptionsSaveSlot(
                    displayName,
                    date,
                    ResolveSaveFactionIcon(entry.Metadata?.PlayerFactionID),
                    false,
                    entry.FileName
                )
            );
        }

        return rows;
    }

    /// <summary>
    /// Ignores save creation because the Main Menu has no running game state.
    /// </summary>
    /// <param name="displayName">The unused requested display name.</param>
    void IOptionsMenuActions.CreateNamedSave(string displayName) { }

    /// <summary>
    /// Ignores save overwrites because the Main Menu has no running game state.
    /// </summary>
    /// <param name="fileName">The unused save identifier.</param>
    /// <param name="displayName">The unused display name.</param>
    void IOptionsMenuActions.OverwriteSave(string fileName, string displayName) { }

    /// <summary>
    /// Starts the selected saved game through the normal Main Menu launch context.
    /// </summary>
    /// <param name="fileName">The save identifier to load.</param>
    /// <returns>True when the Strategy scene transition was requested.</returns>
    bool IOptionsMenuActions.LoadSave(string fileName)
    {
        AppBootstrap bootstrap = AppBootstrap.Instance;
        if (
            bootstrap == null
            || string.IsNullOrEmpty(fileName)
            || !System.IO.File.Exists(SaveGameManager.Instance.GetSaveFilePath(fileName))
        )
            return false;

        bootstrap.GetRuntime()?.EndGame();
        GameLaunchContext.IsLoadGame = true;
        GameLaunchContext.SaveFileName = fileName;
        GameLaunchContext.PlayIntroCutscene = false;
        bootstrap.LoadScene("StrategyView");
        return true;
    }

    /// <summary>
    /// Deletes a save selected from the Main Menu.
    /// </summary>
    /// <param name="fileName">The save identifier to delete.</param>
    void IOptionsMenuActions.DeleteSave(string fileName)
    {
        SaveGameManager.Instance.DeleteSave(fileName);
    }

    /// <summary>
    /// Renames a save selected from the Main Menu.
    /// </summary>
    /// <param name="fileName">The save identifier to update.</param>
    /// <param name="displayName">The new display name.</param>
    void IOptionsMenuActions.RenameSave(string fileName, string displayName)
    {
        SaveGameManager.Instance.SetSaveDisplayName(fileName, displayName);
    }

    /// <summary>
    /// Closes the overlay because the owning scene is already the Main Menu.
    /// </summary>
    void IOptionsMenuActions.ReturnToMainMenu()
    {
        optionsMenuController?.Close();
    }

    /// <summary>
    /// Exits the application from the Options overlay.
    /// </summary>
    void IOptionsMenuActions.QuitApplication()
    {
        ExitApplication();
    }

    /// <summary>
    /// Resolves a save row's faction icon from installation content.
    /// </summary>
    /// <param name="factionId">The saved faction identifier.</param>
    /// <returns>The faction slot texture, or null when none is configured.</returns>
    private Texture2D ResolveSaveFactionIcon(string factionId)
    {
        if (string.IsNullOrEmpty(factionId) || factionThemeLibrary == null || contentAssets == null)
            return null;

        string path = factionThemeLibrary.GetTheme(factionId)?.SavedGameSlotIconImagePath;
        return string.IsNullOrEmpty(path) ? null : contentAssets.GetTexture(path);
    }

    /// <summary>
    /// Requests that the open Options window render its changed state.
    /// </summary>
    private void MarkOptionsDirty()
    {
        optionsDirty = true;
    }

    /// <summary>
    /// Exits the player, or stops Play Mode when testing the command in the Unity Editor.
    /// </summary>
    private static void ExitApplication()
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
        AudioManager.EnsureExists().PlayTrack(_menuMusicPath, true);
    }

    /// <summary>
    /// Finalizes new-game launch state and opens the strategy scene.
    /// </summary>
    private void StartGame()
    {
        GameLaunchContext.IsLoadGame = false;
        GameLaunchContext.SaveFileName = null;
        GameLaunchContext.PlayIntroCutscene = true;

        // Start a new game session.
        AppBootstrap.Instance.GetRuntime()?.EndGame();

        AudioManager.EnsureExists().StopMusic();
        AppBootstrap.Instance.LoadScene("StrategyView");
    }
}
