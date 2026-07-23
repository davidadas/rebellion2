using System;
using System.Collections.Generic;
using System.IO;
using Rebellion.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns save-menu state, game operations, audio settings, and scene navigation.
/// </summary>
public sealed class SaveMenuSceneController : MonoBehaviour
{
    private const string _exitConfirmationMessage = "Are you sure you want to quit?";

    [SerializeField]
    private RectTransform contentHost;

    [SerializeField]
    private SaveMenuWindowView saveMenuWindow;

    private SaveMenuDataBuilder dataBuilder;
    private SaveGameManager saveGameManager;
    private AudioManager audioManager;
    private GameRuntime runtime;
    private UserSettingsManager userSettingsManager;
    private UserVideoSettings videoSettings;
    private bool exitConfirmationPending;
    private bool userSettingsDirty;
    private bool viewBound;

    /// <summary>
    /// Resolves scene dependencies, initializes menu state, and binds semantic view events.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        AppBootstrap bootstrap = AppBootstrap.EnsureExists();
        runtime = bootstrap.GetRuntime();
        audioManager = bootstrap.GetAudioManager();
        userSettingsManager = bootstrap.GetUserSettingsManager();
        videoSettings = userSettingsManager.Settings.Video;
        saveGameManager = SaveGameManager.Instance;
        dataBuilder = new SaveMenuDataBuilder(
            new FactionThemeLibrary(),
            saveGameManager,
            ResourceManager.TryGetTexture,
            GetVersionText()
        );
    }

    /// <summary>
    /// Subscribes to semantic view requests while the scene controller is active.
    /// </summary>
    private void OnEnable()
    {
        BindView();
    }

    /// <summary>
    /// Fits the authored source canvas to the current viewport and performs the initial render.
    /// </summary>
    private void Start()
    {
        Render();
    }

    /// <summary>
    /// Removes every view subscription while the scene controller is inactive.
    /// </summary>
    private void OnDisable()
    {
        UnbindView();
        SaveUserSettings();
    }

    /// <summary>
    /// Recalculates source-space scaling when the viewport dimensions change.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator.CSharp", "RCS1213")]
    private void OnRectTransformDimensionsChange()
    {
        UpdateContentHostLayout();
    }

    /// <summary>
    /// Subscribes the controller to semantic requests from the authored window.
    /// </summary>
    private void BindView()
    {
        if (viewBound)
            return;

        saveMenuWindow.ReturnCockpitRequested += ReturnToMainMenu;
        saveMenuWindow.ExitRequested += BeginExitConfirmation;
        saveMenuWindow.ReturnStrategyRequested += ReturnToLaunchScene;
        saveMenuWindow.MusicToggleRequested += ToggleMusicVolume;
        saveMenuWindow.MusicVolumeChanged += SetMusicVolume;
        saveMenuWindow.SfxVolumeChanged += SetSfxVolume;
        saveMenuWindow.TacticalOptionToggleRequested += ToggleTacticalOption;
        saveMenuWindow.SaveRequested += SaveSlot;
        saveMenuWindow.LoadRequested += LoadSlot;
        saveMenuWindow.ConfirmationAccepted += ConfirmExit;
        saveMenuWindow.ConfirmationCanceled += CancelExit;
        viewBound = true;
    }

    /// <summary>
    /// Unsubscribes the controller from all semantic view requests.
    /// </summary>
    private void UnbindView()
    {
        if (!viewBound)
            return;

        saveMenuWindow.ReturnCockpitRequested -= ReturnToMainMenu;
        saveMenuWindow.ExitRequested -= BeginExitConfirmation;
        saveMenuWindow.ReturnStrategyRequested -= ReturnToLaunchScene;
        saveMenuWindow.MusicToggleRequested -= ToggleMusicVolume;
        saveMenuWindow.MusicVolumeChanged -= SetMusicVolume;
        saveMenuWindow.SfxVolumeChanged -= SetSfxVolume;
        saveMenuWindow.TacticalOptionToggleRequested -= ToggleTacticalOption;
        saveMenuWindow.SaveRequested -= SaveSlot;
        saveMenuWindow.LoadRequested -= LoadSlot;
        saveMenuWindow.ConfirmationAccepted -= ConfirmExit;
        saveMenuWindow.ConfirmationCanceled -= CancelExit;
        viewBound = false;
    }

    /// <summary>
    /// Builds a fresh presentation snapshot and renders the save-menu window.
    /// </summary>
    private void Render()
    {
        UpdateContentHostLayout();
        saveMenuWindow.Render(
            dataBuilder.CreateRenderData(
                GetPlayerFactionID(),
                IsSavingAvailable(),
                audioManager.MusicVolume,
                audioManager.SfxVolume,
                CreateTacticalOptionSnapshot(),
                exitConfirmationPending ? _exitConfirmationMessage : null
            )
        );
    }

    /// <summary>
    /// Captures the persisted tactical options for immutable menu presentation.
    /// </summary>
    /// <returns>The current tactical option states.</returns>
    private IReadOnlyDictionary<UserTacticalOption, bool> CreateTacticalOptionSnapshot()
    {
        Dictionary<UserTacticalOption, bool> snapshot = new Dictionary<UserTacticalOption, bool>();
        foreach (UserTacticalOption option in Enum.GetValues(typeof(UserTacticalOption)))
            snapshot.Add(option, videoSettings.IsEnabled(option));

        return snapshot;
    }

    /// <summary>
    /// Determines whether the active launch context permits save operations.
    /// </summary>
    /// <returns>True when an active game may be saved.</returns>
    private bool IsSavingAvailable()
    {
        return SaveMenuLaunchContext.CanSave && runtime?.HasActiveGame == true;
    }

    /// <summary>
    /// Persists the active game to a numbered slot and refreshes the menu.
    /// </summary>
    /// <param name="slot">The zero-based save-slot index.</param>
    /// <param name="displayName">The requested save display name.</param>
    private void SaveSlot(int slot, string displayName)
    {
        if (!saveGameManager.IsValidSaveSlot(slot) || !IsSavingAvailable())
            return;

        saveGameManager.SaveSlotGameData(runtime.GetActiveGame(), slot, displayName);
        Render();
    }

    /// <summary>
    /// Loads a numbered slot and enters the strategy scene when successful.
    /// </summary>
    /// <param name="slot">The zero-based save-slot index.</param>
    private void LoadSlot(int slot)
    {
        if (!TryLoadSlot(slot))
            Render();
    }

    /// <summary>
    /// Attempts to load a valid numbered slot into the active game runtime.
    /// </summary>
    /// <param name="slot">The zero-based save-slot index.</param>
    /// <returns>True when the slot loaded and scene navigation began.</returns>
    private bool TryLoadSlot(int slot)
    {
        if (!saveGameManager.IsValidSaveSlot(slot))
            return false;

        string fileName = saveGameManager.GetSaveSlotFileName(slot);
        if (!File.Exists(saveGameManager.GetSaveFilePath(fileName)))
            return false;
        if (runtime == null)
            return false;

        if (!runtime.LoadGame(fileName))
            return false;
        SaveMenuLaunchContext.OpenFromStrategyView();
        SceneManager.LoadScene(SaveMenuLaunchContext.StrategyViewSceneName);
        return true;
    }

    /// <summary>
    /// Starts the modal application-exit confirmation flow.
    /// </summary>
    private void BeginExitConfirmation()
    {
        exitConfirmationPending = true;
        Render();
    }

    /// <summary>
    /// Executes application exit when a confirmation request is pending.
    /// </summary>
    private void ConfirmExit()
    {
        if (!exitConfirmationPending)
            return;

        exitConfirmationPending = false;
        ExitApplication();
    }

    /// <summary>
    /// Cancels the modal application-exit confirmation flow.
    /// </summary>
    private void CancelExit()
    {
        if (!exitConfirmationPending)
            return;

        exitConfirmationPending = false;
        Render();
    }

    /// <summary>
    /// Toggles music between muted and full volume and refreshes the menu.
    /// </summary>
    private void ToggleMusicVolume()
    {
        audioManager.SetMusicVolume(audioManager.MusicVolume > 0f ? 0f : 1f);
        userSettingsDirty = true;
        RefreshAudioPresentation();
    }

    /// <summary>
    /// Applies a normalized music volume and refreshes the menu.
    /// </summary>
    /// <param name="value">The normalized music volume.</param>
    private void SetMusicVolume(float value)
    {
        float volume = Mathf.Clamp01(value);
        if (Mathf.Approximately(audioManager.MusicVolume, volume))
            return;

        audioManager.SetMusicVolume(volume);
        userSettingsDirty = true;
        RefreshAudioPresentation();
    }

    /// <summary>
    /// Applies a normalized sound-effect volume and refreshes the menu.
    /// </summary>
    /// <param name="value">The normalized sound-effect volume.</param>
    private void SetSfxVolume(float value)
    {
        float volume = Mathf.Clamp01(value);
        if (Mathf.Approximately(audioManager.SfxVolume, volume))
            return;

        audioManager.SetSfxVolume(volume);
        userSettingsDirty = true;
        RefreshAudioPresentation();
    }

    /// <summary>
    /// Persists changed user settings without writing on every slider update.
    /// </summary>
    private void SaveUserSettings()
    {
        if (!userSettingsDirty || userSettingsManager == null)
            return;

        userSettingsManager.Save();
        userSettingsDirty = false;
    }

    /// <summary>
    /// Refreshes audio controls without rebuilding save-slot data from disk.
    /// </summary>
    private void RefreshAudioPresentation()
    {
        saveMenuWindow.RenderAudioSettings(audioManager.MusicVolume, audioManager.SfxVolume);
    }

    /// <summary>
    /// Toggles one typed tactical presentation option and refreshes the menu.
    /// </summary>
    /// <param name="option">The tactical option to toggle.</param>
    private void ToggleTacticalOption(UserTacticalOption option)
    {
        videoSettings.SetEnabled(option, !videoSettings.IsEnabled(option));
        userSettingsDirty = true;
        Render();
    }

    /// <summary>
    /// Returns to the scene that opened the save menu when its game is still active.
    /// </summary>
    private void ReturnToLaunchScene()
    {
        if (
            SaveMenuLaunchContext.ReturnSceneName == SaveMenuLaunchContext.StrategyViewSceneName
            && runtime?.HasActiveGame == true
        )
        {
            SceneManager.LoadScene(SaveMenuLaunchContext.StrategyViewSceneName);
            return;
        }

        ReturnToMainMenu();
    }

    /// <summary>
    /// Ends the active game and returns to the main menu scene.
    /// </summary>
    private void ReturnToMainMenu()
    {
        runtime?.EndGame();
        SaveMenuLaunchContext.Reset();
        SceneManager.LoadScene(SaveMenuLaunchContext.MainMenuSceneName);
    }

    /// <summary>
    /// Ends the active game and exits the application or editor play session.
    /// </summary>
    private void ExitApplication()
    {
        runtime?.EndGame();
        SaveMenuLaunchContext.Reset();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Resolves the current player's faction identifier from the active game.
    /// </summary>
    /// <returns>The current player's faction identifier, or null.</returns>
    private string GetPlayerFactionID()
    {
        GameRoot game = runtime?.GetActiveGame();
        return game?.Summary?.PlayerFactionID;
    }

    /// <summary>
    /// Fits the authored source canvas within the current viewport without stretching it.
    /// </summary>
    private void UpdateContentHostLayout()
    {
        saveMenuWindow.FitWithinViewport(contentHost);
    }

    /// <summary>
    /// Builds the version label displayed by the authored save menu.
    /// </summary>
    /// <returns>The current application version label.</returns>
    private static string GetVersionText()
    {
        return string.IsNullOrEmpty(Application.version)
            ? "Version: Development"
            : "Version: " + Application.version;
    }

    /// <summary>
    /// Verifies the authored scene references required by the controller.
    /// </summary>
    private void VerifyReferences()
    {
        if (contentHost == null)
            throw new MissingReferenceException("ContentHost is missing.");
        if (saveMenuWindow == null)
            throw new MissingReferenceException("SaveMenuWindow is missing.");
    }
}
