using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Session-level actions the Options overlay delegates to its host scene.
/// </summary>
public interface IOptionsMenuActions
{
    /// <summary>Gets whether saving is currently available.</summary>
    bool CanSave { get; }

    /// <summary>Gets whether loading is currently available.</summary>
    bool CanLoad { get; }

    /// <summary>Gets whether a running game exists to return to.</summary>
    bool CanReturnToGame { get; }

    /// <summary>Pauses the host while the Options overlay is open.</summary>
    void PauseForOptions();

    /// <summary>Resumes the host after the Options overlay closes.</summary>
    void ResumeFromOptions();

    /// <summary>Opens the save menu.</summary>
    void OpenSaveMenu();

    /// <summary>Opens the load menu.</summary>
    void OpenLoadMenu();

    /// <summary>Returns to the main menu.</summary>
    void ReturnToMainMenu();

    /// <summary>Quits the application.</summary>
    void QuitApplication();
}

/// <summary>
/// Owns the in-game Options overlay: modal lifetime, live settings edits, and the
/// read-only key-binding listing. Registered with the cancel stack to close on Escape.
/// </summary>
public sealed class OptionsMenuController : ICancelable
{
    private const int MasterChannel = 0;
    private const int MusicChannel = 1;
    private const int SfxChannel = 2;
    private const int AmbienceChannel = 3;
    private const int VideoChannel = 4;

    private static readonly int[] FullScreenModes =
    {
        (int)FullScreenMode.ExclusiveFullScreen,
        (int)FullScreenMode.FullScreenWindow,
        (int)FullScreenMode.Windowed,
    };

    private readonly StrategyWindowLayerView windowLayer;
    private readonly UIWindowManager windowManager;
    private readonly Func<Vector2Int> getWindowPosition;
    private readonly Action<UIWindow> closeWindow;
    private readonly Action markDirty;
    private readonly UserSettingsManager userSettings;
    private readonly AudioManager audioManager;
    private readonly InputManager inputManager;

    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private readonly List<OptionsBindingRow> bindings = new List<OptionsBindingRow>();

    private IOptionsMenuActions actions;
    private OptionsMenuView view;
    private UIWindow window;
    private OptionsMenuTab activeTab = OptionsMenuTab.Graphics;
    private int resolutionIndex;
    private bool settingsDirty;

    /// <summary>
    /// Creates an Options overlay controller.
    /// </summary>
    /// <param name="windowLayer">Provides the authored Options prefab and modal layer.</param>
    /// <param name="windowManager">Owns strategy-window creation and registration.</param>
    /// <param name="getWindowPosition">Returns the authored Options placement.</param>
    /// <param name="closeWindow">Closes a registered strategy window.</param>
    /// <param name="userSettings">The user-settings store.</param>
    /// <param name="audioManager">The audio manager for live volume changes.</param>
    /// <param name="inputManager">The input manager exposing key bindings.</param>
    /// <param name="markDirty">Invalidates strategy presentation after changes.</param>
    public OptionsMenuController(
        StrategyWindowLayerView windowLayer,
        UIWindowManager windowManager,
        Func<Vector2Int> getWindowPosition,
        Action<UIWindow> closeWindow,
        UserSettingsManager userSettings,
        AudioManager audioManager,
        InputManager inputManager,
        Action markDirty
    )
    {
        this.windowLayer = windowLayer ?? throw new ArgumentNullException(nameof(windowLayer));
        this.windowManager =
            windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        this.getWindowPosition =
            getWindowPosition ?? throw new ArgumentNullException(nameof(getWindowPosition));
        this.closeWindow = closeWindow ?? throw new ArgumentNullException(nameof(closeWindow));
        this.userSettings = userSettings ?? throw new ArgumentNullException(nameof(userSettings));
        this.audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
        this.inputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
    }

    /// <summary>Gets whether the overlay is currently open.</summary>
    public bool IsOpen => window != null;

    /// <summary>
    /// Supplies host actions after the strategy controller graph is constructed.
    /// </summary>
    /// <param name="menuActions">The host-specific Options actions.</param>
    public void Initialize(IOptionsMenuActions menuActions)
    {
        actions = menuActions ?? throw new ArgumentNullException(nameof(menuActions));
    }

    /// <summary>
    /// Opens the Options overlay, or focuses it when already open.
    /// </summary>
    public void Open()
    {
        EnsureInitialized();
        if (window != null)
        {
            windowManager.Focus(window);
            return;
        }

        if (windowLayer.OptionsMenuWindowPrefab == null)
        {
            Debug.LogWarning(
                "OptionsMenu prefab is not assigned; run Build Strategy View UI to generate it."
            );
            return;
        }

        RebuildResolutions();
        RebuildBindings();
        activeTab = OptionsMenuTab.Graphics;
        settingsDirty = false;

        Vector2Int position = getWindowPosition();
        window = windowManager.CreateWindow(
            windowLayer.OptionsMenuWindowPrefab,
            windowLayer.GetWindowParent(true),
            "OptionsMenu",
            position.x,
            position.y,
            windowLayer.GetWindowSize(windowLayer.OptionsMenuWindowPrefab),
            true,
            true,
            false,
            false,
            out view
        );
        BindView(view);
        actions.PauseForOptions();
        markDirty();
    }

    /// <summary>
    /// Closes the Options overlay and persists any pending settings changes.
    /// </summary>
    public void Close()
    {
        if (window == null)
            return;

        PersistIfDirty();
        UIWindow closing = window;
        window = null;
        view = null;
        actions.ResumeFromOptions();
        closeWindow(closing);
        markDirty();
    }

    /// <summary>
    /// Renders the open Options overlay, if any.
    /// </summary>
    public void RenderWindows()
    {
        if (window == null || view == null)
            return;

        view.Render(BuildRenderData(window));
    }

    /// <summary>
    /// Closes the overlay in response to a cancel request.
    /// </summary>
    /// <returns>True when the overlay was open and closed.</returns>
    public bool TryCancel()
    {
        if (window == null)
            return false;

        Close();
        return true;
    }

    /// <summary>
    /// Builds the current presentation snapshot from live settings and cached bindings.
    /// </summary>
    /// <param name="shell">The window shell supplying source-space position.</param>
    /// <returns>The immutable presentation snapshot.</returns>
    private OptionsMenuRenderData BuildRenderData(UIWindow shell)
    {
        UserVideoSettings video = userSettings.Settings.Video;
        UserAudioSettings audio = userSettings.Settings.Audio;

        Dictionary<UserTacticalOption, bool> tactical =
            new Dictionary<UserTacticalOption, bool>();
        foreach (UserTacticalOption option in Enum.GetValues(typeof(UserTacticalOption)))
            tactical[option] = video.IsEnabled(option);

        float[] volumes =
        {
            audio.MasterVolume,
            audio.MusicVolume,
            audio.SfxVolume,
            audio.AmbienceVolume,
            audio.VideoVolume,
        };

        return new OptionsMenuRenderData(
            shell.X,
            shell.Y,
            activeTab,
            GetResolutionLabel(video),
            GetFullScreenLabel(video.FullScreenMode),
            tactical,
            volumes,
            bindings,
            Array.Empty<OptionsSaveSlot>(),
            -1,
            actions.CanSave,
            actions.CanLoad,
            actions.CanReturnToGame
        );
    }

    /// <summary>
    /// Subscribes the controller to one Options view exactly once.
    /// </summary>
    /// <param name="target">The view to bind.</param>
    private void BindView(OptionsMenuView target)
    {
        target.TabSelected += HandleTabSelected;
        target.ResumeRequested += Close;
        target.SaveRequested += HandleSaveRequested;
        target.LoadRequested += HandleLoadRequested;
        target.MainMenuRequested += HandleMainMenuRequested;
        target.QuitRequested += HandleQuitRequested;
        target.TacticalToggleRequested += HandleTacticalToggle;
        target.ResolutionStepRequested += HandleResolutionStep;
        target.FullScreenStepRequested += HandleFullScreenStep;
        target.VolumeChanged += HandleVolumeChanged;
        target.Destroyed += HandleViewDestroyed;
    }

    /// <summary>
    /// Selects a page and requests a re-render.
    /// </summary>
    /// <param name="tab">The selected page.</param>
    private void HandleTabSelected(OptionsMenuTab tab)
    {
        activeTab = tab;
        markDirty();
    }

    /// <summary>
    /// Routes a save request to the host, persisting pending changes first.
    /// </summary>
    private void HandleSaveRequested()
    {
        if (!actions.CanSave)
            return;

        PersistIfDirty();
        actions.OpenSaveMenu();
    }

    /// <summary>
    /// Routes a load request to the host, persisting pending changes first.
    /// </summary>
    private void HandleLoadRequested()
    {
        if (!actions.CanLoad)
            return;

        PersistIfDirty();
        actions.OpenLoadMenu();
    }

    /// <summary>
    /// Routes a main-menu request to the host, persisting pending changes first.
    /// </summary>
    private void HandleMainMenuRequested()
    {
        PersistIfDirty();
        actions.ReturnToMainMenu();
    }

    /// <summary>
    /// Routes a quit request to the host, persisting pending changes first.
    /// </summary>
    private void HandleQuitRequested()
    {
        PersistIfDirty();
        actions.QuitApplication();
    }

    /// <summary>
    /// Toggles a detail option and marks settings dirty.
    /// </summary>
    /// <param name="option">The toggled option.</param>
    private void HandleTacticalToggle(UserTacticalOption option)
    {
        UserVideoSettings video = userSettings.Settings.Video;
        video.SetEnabled(option, !video.IsEnabled(option));
        settingsDirty = true;
        markDirty();
    }

    /// <summary>
    /// Steps the selected resolution and applies it immediately.
    /// </summary>
    /// <param name="delta">The step direction.</param>
    private void HandleResolutionStep(int delta)
    {
        if (resolutions.Count == 0)
            return;

        resolutionIndex = ((resolutionIndex + delta) % resolutions.Count + resolutions.Count)
            % resolutions.Count;
        Vector2Int resolution = resolutions[resolutionIndex];
        UserVideoSettings video = userSettings.Settings.Video;
        video.ResolutionWidth = resolution.x;
        video.ResolutionHeight = resolution.y;
        Screen.SetResolution(resolution.x, resolution.y, (FullScreenMode)video.FullScreenMode);
        settingsDirty = true;
        markDirty();
    }

    /// <summary>
    /// Steps the display mode and applies it immediately.
    /// </summary>
    /// <param name="delta">The step direction.</param>
    private void HandleFullScreenStep(int delta)
    {
        UserVideoSettings video = userSettings.Settings.Video;
        int current = Array.IndexOf(FullScreenModes, video.FullScreenMode);
        if (current < 0)
            current = 0;
        int next = ((current + delta) % FullScreenModes.Length + FullScreenModes.Length)
            % FullScreenModes.Length;
        video.FullScreenMode = FullScreenModes[next];
        int width = video.ResolutionWidth > 0 ? video.ResolutionWidth : Screen.width;
        int height = video.ResolutionHeight > 0 ? video.ResolutionHeight : Screen.height;
        Screen.SetResolution(width, height, (FullScreenMode)video.FullScreenMode);
        settingsDirty = true;
        markDirty();
    }

    /// <summary>
    /// Applies a live volume change for one channel.
    /// </summary>
    /// <param name="channel">The channel index (0..4).</param>
    /// <param name="value">The normalized volume.</param>
    private void HandleVolumeChanged(int channel, float value)
    {
        switch (channel)
        {
            case MasterChannel:
                audioManager.SetMasterVolume(value);
                break;
            case MusicChannel:
                audioManager.SetMusicVolume(value);
                break;
            case SfxChannel:
                audioManager.SetSfxVolume(value);
                break;
            case AmbienceChannel:
                audioManager.SetAmbienceVolume(value);
                break;
            case VideoChannel:
                audioManager.SetVideoVolume(value);
                break;
            default:
                return;
        }

        settingsDirty = true;
    }

    /// <summary>
    /// Releases subscriptions and state for a destroyed Options view.
    /// </summary>
    /// <param name="destroyed">The destroyed view.</param>
    private void HandleViewDestroyed(OptionsMenuView destroyed)
    {
        if (destroyed == null)
            return;

        destroyed.TabSelected -= HandleTabSelected;
        destroyed.ResumeRequested -= Close;
        destroyed.SaveRequested -= HandleSaveRequested;
        destroyed.LoadRequested -= HandleLoadRequested;
        destroyed.MainMenuRequested -= HandleMainMenuRequested;
        destroyed.QuitRequested -= HandleQuitRequested;
        destroyed.TacticalToggleRequested -= HandleTacticalToggle;
        destroyed.ResolutionStepRequested -= HandleResolutionStep;
        destroyed.FullScreenStepRequested -= HandleFullScreenStep;
        destroyed.VolumeChanged -= HandleVolumeChanged;
        destroyed.Destroyed -= HandleViewDestroyed;
        if (ReferenceEquals(destroyed, view))
        {
            view = null;
            window = null;
        }
    }

    /// <summary>
    /// Persists settings to disk when a change is pending.
    /// </summary>
    private void PersistIfDirty()
    {
        if (!settingsDirty)
            return;

        userSettings.Save();
        settingsDirty = false;
    }

    /// <summary>
    /// Rebuilds the available-resolution list and selects the current entry.
    /// </summary>
    private void RebuildResolutions()
    {
        resolutions.Clear();
        foreach (Resolution resolution in Screen.resolutions)
        {
            Vector2Int size = new Vector2Int(resolution.width, resolution.height);
            if (!resolutions.Contains(size))
                resolutions.Add(size);
        }

        if (resolutions.Count == 0)
            resolutions.Add(new Vector2Int(Screen.width, Screen.height));

        UserVideoSettings video = userSettings.Settings.Video;
        Vector2Int current = new Vector2Int(
            video.ResolutionWidth > 0 ? video.ResolutionWidth : Screen.width,
            video.ResolutionHeight > 0 ? video.ResolutionHeight : Screen.height
        );
        resolutionIndex = Mathf.Max(0, resolutions.IndexOf(current));
    }

    /// <summary>
    /// Rebuilds the read-only key-binding listing from the input asset.
    /// </summary>
    private void RebuildBindings()
    {
        bindings.Clear();
        InputActionAsset asset = inputManager.Asset;
        if (asset == null)
            return;

        foreach (InputActionMap map in asset.actionMaps)
        {
            bindings.Add(new OptionsBindingRow(map.name.ToUpperInvariant(), string.Empty));
            foreach (InputAction action in map.actions)
            {
                string keys = action.GetBindingDisplayString(
                    InputBinding.DisplayStringOptions.DontUseShortDisplayNames
                );
                bindings.Add(new OptionsBindingRow(Humanize(action.name), keys));
            }
        }
    }

    /// <summary>
    /// Resolves the display label for the current resolution.
    /// </summary>
    /// <param name="video">The current video settings.</param>
    /// <returns>The resolution label.</returns>
    private string GetResolutionLabel(UserVideoSettings video)
    {
        if (video.ResolutionWidth > 0 && video.ResolutionHeight > 0)
            return $"{video.ResolutionWidth} x {video.ResolutionHeight}";

        return resolutions.Count > 0
            ? $"{resolutions[resolutionIndex].x} x {resolutions[resolutionIndex].y}"
            : $"{Screen.width} x {Screen.height}";
    }

    /// <summary>
    /// Resolves the display label for a full-screen mode value.
    /// </summary>
    /// <param name="mode">The stored full-screen mode.</param>
    /// <returns>The display label.</returns>
    private static string GetFullScreenLabel(int mode)
    {
        return (FullScreenMode)mode switch
        {
            FullScreenMode.ExclusiveFullScreen => "Fullscreen",
            FullScreenMode.FullScreenWindow => "Borderless",
            FullScreenMode.MaximizedWindow => "Maximized",
            FullScreenMode.Windowed => "Windowed",
            _ => "Fullscreen",
        };
    }

    /// <summary>
    /// Inserts spaces between camel-cased words for display.
    /// </summary>
    /// <param name="name">The raw action name.</param>
    /// <returns>The spaced display name.</returns>
    private static string Humanize(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        System.Text.StringBuilder builder = new System.Text.StringBuilder(name.Length + 8);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
                builder.Append(' ');
            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Verifies that host actions are available before use.
    /// </summary>
    private void EnsureInitialized()
    {
        if (actions == null)
            throw new InvalidOperationException(
                $"{nameof(OptionsMenuController)} must be initialized before use."
            );
    }
}
