using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages pending changes to gameplay, graphics, audio, and input settings.
/// </summary>
internal sealed class OptionsSettingsSession
{
    private static readonly int[] _fullScreenModes =
    {
        (int)FullScreenMode.ExclusiveFullScreen,
        (int)FullScreenMode.FullScreenWindow,
        (int)FullScreenMode.Windowed,
    };

    private static readonly int[] _frameRateLimits = { 0, 30, 60, 120, 144, 165, 240 };

    private readonly UserSettingsManager _userSettings;
    private readonly DisplayManager _displayManager;
    private readonly GraphicsManager _graphicsManager;
    private readonly AudioManager _audioManager;
    private readonly InputManager _inputManager;

    private readonly List<Vector2Int> _resolutions = new List<Vector2Int>();
    private int _resolutionIndex;

    private readonly Dictionary<UserTacticalOption, bool> _snapshotTactical =
        new Dictionary<UserTacticalOption, bool>();
    private readonly Dictionary<UserGameplayOption, bool> _snapshotGameplay =
        new Dictionary<UserGameplayOption, bool>();
    private readonly Dictionary<UserInterfaceOption, bool> _snapshotUserInterface =
        new Dictionary<UserInterfaceOption, bool>();

    private float[] _snapshotVolumes = Array.Empty<float>();
    private int _snapshotResolutionWidth;
    private int _snapshotResolutionHeight;
    private int _snapshotFullScreenMode;
    private int _snapshotQualityLevel;
    private bool _snapshotGraphicsPresetCustomized;
    private int _snapshotAntiAliasingMode;
    private int _snapshotFrameRateLimit;
    private bool _snapshotVerticalSync;
    private bool _snapshotRayTracing;
    private bool _snapshotAmbientOcclusion;
    private bool _snapshotBloom;
    private bool _snapshotMotionBlur;
    private bool _snapshotHdrOutput;
    private int _snapshotAutosaveIntervalTicks;
    private int _snapshotAutosavesToKeep;
    private IdleBarLocation _snapshotIdleBarLocation;
    private GameLaunchSpeed _snapshotGameLaunchSpeed;
    private string _snapshotBindingOverrides = string.Empty;
    private bool _inputDiffersFromSnapshot;

    internal bool IsDirty { get; private set; }

    internal UserVideoSettings Video => _userSettings.Settings.Video;

    internal UserGameplaySettings Gameplay => _userSettings.Settings.Gameplay;

    internal UserInterfaceSettings UserInterface => _userSettings.Settings.UserInterface;

    internal UserAudioSettings Audio => _userSettings.Settings.Audio;

    internal string ResolutionLabel => $"{Video.ResolutionWidth} x {Video.ResolutionHeight}";

    internal string FullScreenLabel => GetFullScreenLabel(Video.FullScreenMode);

    /// <summary>
    /// Creates a pending settings session over the runtime service owners.
    /// </summary>
    /// <param name="userSettings">The user settings.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="audioManager">The audio manager.</param>
    /// <param name="inputManager">The input manager.</param>
    /// <param name="graphicsManager">The optional HDRP graphics manager.</param>
    internal OptionsSettingsSession(
        UserSettingsManager userSettings,
        DisplayManager displayManager,
        AudioManager audioManager,
        InputManager inputManager,
        GraphicsManager graphicsManager = null
    )
    {
        _userSettings = userSettings ?? throw new ArgumentNullException(nameof(userSettings));
        _displayManager = displayManager ?? throw new ArgumentNullException(nameof(displayManager));
        _graphicsManager = graphicsManager;
        _audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
        _inputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
    }

    /// <summary>
    /// Captures the current runtime settings as the session's revert point.
    /// </summary>
    internal void Begin()
    {
        RebuildResolutions();
        CaptureSnapshot();
        IsDirty = false;
    }

    /// <summary>
    /// Persists and applies pending settings, then establishes a new revert point.
    /// </summary>
    internal void Commit()
    {
        _userSettings.Save();
        _userSettings.Apply();
        RebuildResolutions();
        CaptureSnapshot();
        IsDirty = false;
    }

    /// <summary>
    /// Restores the settings and input overrides captured when the session began.
    /// </summary>
    internal void Revert()
    {
        for (int channel = 0; channel < _snapshotVolumes.Length; channel++)
            SetVolumeValue(channel, _snapshotVolumes[channel]);

        Video.ResolutionWidth = _snapshotResolutionWidth;
        Video.ResolutionHeight = _snapshotResolutionHeight;
        Video.FullScreenMode = _snapshotFullScreenMode;
        Video.QualityLevel = _snapshotQualityLevel;
        Video.GraphicsPresetCustomized = _snapshotGraphicsPresetCustomized;
        Video.AntiAliasingMode = _snapshotAntiAliasingMode;
        Video.FrameRateLimit = _snapshotFrameRateLimit;
        Video.VerticalSync = _snapshotVerticalSync;
        Video.RayTracing = _snapshotRayTracing;
        Video.AmbientOcclusion = _snapshotAmbientOcclusion;
        Video.Bloom = _snapshotBloom;
        Video.MotionBlur = _snapshotMotionBlur;
        Video.HdrOutput = _snapshotHdrOutput;
        foreach (KeyValuePair<UserTacticalOption, bool> entry in _snapshotTactical)
            Video.SetEnabled(entry.Key, entry.Value);
        foreach (KeyValuePair<UserGameplayOption, bool> entry in _snapshotGameplay)
            Gameplay.SetEnabled(entry.Key, entry.Value);
        foreach (KeyValuePair<UserInterfaceOption, bool> entry in _snapshotUserInterface)
            UserInterface.SetEnabled(entry.Key, entry.Value);
        UserInterface.IdleBarLocation = _snapshotIdleBarLocation;
        Gameplay.GameLaunchSpeed = _snapshotGameLaunchSpeed;
        Gameplay.SetAutosaveIntervalTicks(_snapshotAutosaveIntervalTicks);
        Gameplay.SetAutosavesToKeep(_snapshotAutosavesToKeep);

        _inputManager.LoadBindingOverrides(_snapshotBindingOverrides);
        _inputDiffersFromSnapshot = false;
        ApplyAllVolumes();
        RebuildResolutions();
        IsDirty = false;
    }

    /// <summary>
    /// Restores defaults for one Options tab and applies its live effects.
    /// </summary>
    /// <param name="tab">The tab.</param>
    internal void RestoreDefaults(OptionsMenuTab tab)
    {
        switch (tab)
        {
            case OptionsMenuTab.Gameplay:
                Gameplay.RestoreDefaults();
                break;
            case OptionsMenuTab.UserInterface:
                UserInterface.RestoreDefaults();
                break;
            case OptionsMenuTab.Graphics:
                Video.ResolutionWidth = 0;
                Video.ResolutionHeight = 0;
                Video.FullScreenMode = (int)FullScreenMode.ExclusiveFullScreen;
                Video.RestoreGraphicsDefaults();
                Video.RestoreTacticalDefaults();
                RebuildResolutions();
                break;
            case OptionsMenuTab.Audio:
                for (int channel = 0; channel < 5; channel++)
                    SetVolumeValue(channel, 1f);
                ApplyAllVolumes();
                break;
            default:
                return;
        }

        RefreshDirtyState();
    }

    /// <summary>
    /// Copies the staged gameplay toggles for presentation.
    /// </summary>
    /// <returns>The requested gameplay states.</returns>
    internal Dictionary<UserGameplayOption, bool> GetGameplayStates()
    {
        Dictionary<UserGameplayOption, bool> states = new Dictionary<UserGameplayOption, bool>();
        foreach (UserGameplayOption option in Enum.GetValues(typeof(UserGameplayOption)))
            states[option] = Gameplay.IsEnabled(option);
        return states;
    }

    /// <summary>
    /// Copies the staged user-interface toggles for presentation.
    /// </summary>
    /// <returns>The staged toggle state keyed by user-interface option.</returns>
    internal Dictionary<UserInterfaceOption, bool> GetUserInterfaceStates()
    {
        Dictionary<UserInterfaceOption, bool> states = new Dictionary<UserInterfaceOption, bool>();
        foreach (UserInterfaceOption option in Enum.GetValues(typeof(UserInterfaceOption)))
            states[option] = UserInterface.IsEnabled(option);
        return states;
    }

    /// <summary>
    /// Copies the staged tactical display toggles for presentation.
    /// </summary>
    /// <returns>The staged toggle state keyed by tactical option.</returns>
    internal Dictionary<UserTacticalOption, bool> GetTacticalStates()
    {
        Dictionary<UserTacticalOption, bool> states = new Dictionary<UserTacticalOption, bool>();
        foreach (UserTacticalOption option in Enum.GetValues(typeof(UserTacticalOption)))
            states[option] = Video.IsEnabled(option);
        return states;
    }

    /// <summary>
    /// Projects every staged graphics setting for presentation.
    /// </summary>
    /// <returns>The displayed value and availability keyed by graphics setting.</returns>
    internal Dictionary<UserGraphicsSetting, OptionsGraphicsValue> GetGraphicsValues()
    {
        Dictionary<UserGraphicsSetting, OptionsGraphicsValue> values =
            new Dictionary<UserGraphicsSetting, OptionsGraphicsValue>();
        foreach (UserGraphicsSetting setting in Enum.GetValues(typeof(UserGraphicsSetting)))
        {
            bool available = IsGraphicsSettingAvailable(setting);
            values[setting] = new OptionsGraphicsValue(
                available ? GetGraphicsLabel(setting) : GetUnavailableGraphicsLabel(setting),
                available
            );
        }
        return values;
    }

    /// <summary>
    /// Returns the five displayed audio-channel volumes in menu order.
    /// </summary>
    /// <returns>The requested volumes.</returns>
    internal float[] GetVolumes()
    {
        return new[]
        {
            Audio.MasterVolume,
            Audio.MusicVolume,
            Audio.SfxVolume,
            Audio.AmbienceVolume,
            Audio.VideoVolume,
        };
    }

    /// <summary>
    /// Toggles a tactical display option and marks the session dirty.
    /// </summary>
    /// <param name="option">The option.</param>
    internal void ToggleTactical(UserTacticalOption option)
    {
        Video.SetEnabled(option, !Video.IsEnabled(option));
        RefreshDirtyState();
    }

    /// <summary>
    /// Toggles a gameplay option and marks the session dirty.
    /// </summary>
    /// <param name="option">The option.</param>
    internal void ToggleGameplay(UserGameplayOption option)
    {
        Gameplay.SetEnabled(option, !Gameplay.IsEnabled(option));
        RefreshDirtyState();
    }

    /// <summary>
    /// Toggles a user-interface option and marks the session dirty.
    /// </summary>
    /// <param name="option">The user-interface option to toggle.</param>
    internal void ToggleUserInterface(UserInterfaceOption option)
    {
        UserInterface.SetEnabled(option, !UserInterface.IsEnabled(option));
        RefreshDirtyState();
    }

    /// <summary>
    /// Sets the strategy-desktop corner used by the idle bar.
    /// </summary>
    /// <param name="location">The requested idle-bar corner.</param>
    internal void SetIdleBarLocation(IdleBarLocation location)
    {
        UserInterface.IdleBarLocation = location;
        RefreshDirtyState();
    }

    /// <summary>
    /// Sets the game speed applied when entering the galaxy view.
    /// </summary>
    /// <param name="speed">The requested launch-speed behavior.</param>
    internal void SetGameLaunchSpeed(GameLaunchSpeed speed)
    {
        Gameplay.GameLaunchSpeed = speed;
        RefreshDirtyState();
    }

    /// <summary>
    /// Sets the number of ticks between autosaves within the supported range.
    /// </summary>
    /// <param name="ticks">The requested autosave interval.</param>
    internal void SetAutosaveInterval(int ticks)
    {
        Gameplay.SetAutosaveIntervalTicks(ticks);
        RefreshDirtyState();
    }

    /// <summary>
    /// Sets the number of retained autosaves within the supported range.
    /// </summary>
    /// <param name="count">The requested autosave retention count.</param>
    internal void SetAutosavesToKeep(int count)
    {
        Gameplay.SetAutosavesToKeep(count);
        RefreshDirtyState();
    }

    /// <summary>
    /// Stages an adjacent supported resolution.
    /// </summary>
    /// <param name="delta">The delta.</param>
    internal void StepResolution(int delta)
    {
        if (_resolutions.Count == 0)
            return;

        _resolutionIndex =
            ((_resolutionIndex + delta) % _resolutions.Count + _resolutions.Count)
            % _resolutions.Count;
        Vector2Int resolution = _resolutions[_resolutionIndex];
        Video.ResolutionWidth = resolution.x;
        Video.ResolutionHeight = resolution.y;
        RefreshDirtyState();
    }

    /// <summary>
    /// Stages an adjacent fullscreen mode.
    /// </summary>
    /// <param name="delta">The delta.</param>
    internal void StepFullScreen(int delta)
    {
        int current = Array.IndexOf(_fullScreenModes, Video.FullScreenMode);
        if (current < 0)
            current = 0;
        int next =
            ((current + delta) % _fullScreenModes.Length + _fullScreenModes.Length)
            % _fullScreenModes.Length;
        Video.FullScreenMode = _fullScreenModes[next];
        RefreshDirtyState();
    }

    /// <summary>
    /// Stages an adjacent supported value for one HDRP graphics setting.
    /// </summary>
    /// <param name="setting">The graphics setting to change.</param>
    /// <param name="delta">The requested direction.</param>
    internal void StepGraphics(UserGraphicsSetting setting, int delta)
    {
        if (delta == 0 || !IsGraphicsSettingAvailable(setting))
            return;

        switch (setting)
        {
            case UserGraphicsSetting.QualityPreset:
                Video.ApplyGraphicsPreset(
                    StepWrapped(
                        Video.QualityLevel,
                        delta,
                        Math.Max(1, QualitySettings.names.Length)
                    )
                );
                break;
            case UserGraphicsSetting.AntiAliasing:
                Video.AntiAliasingMode = StepWrapped(
                    Video.AntiAliasingMode,
                    delta,
                    Enum.GetValues(typeof(UserAntiAliasingMode)).Length
                );
                break;
            case UserGraphicsSetting.VerticalSync:
                Video.VerticalSync = !Video.VerticalSync;
                break;
            case UserGraphicsSetting.FrameRateLimit:
                Video.FrameRateLimit = StepValue(_frameRateLimits, Video.FrameRateLimit, delta);
                break;
            case UserGraphicsSetting.RayTracing:
                Video.RayTracing = !Video.RayTracing;
                if (Video.RayTracing)
                    Video.AmbientOcclusion = true;
                break;
            case UserGraphicsSetting.AmbientOcclusion:
                Video.AmbientOcclusion = !Video.AmbientOcclusion;
                if (!Video.AmbientOcclusion)
                    Video.RayTracing = false;
                break;
            case UserGraphicsSetting.Bloom:
                Video.Bloom = !Video.Bloom;
                break;
            case UserGraphicsSetting.MotionBlur:
                Video.MotionBlur = !Video.MotionBlur;
                break;
            case UserGraphicsSetting.HdrOutput:
                Video.HdrOutput = !Video.HdrOutput;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(setting), setting, null);
        }

        RefreshGraphicsPresetCustomization();
        RefreshDirtyState();
    }

    /// <summary>
    /// Stores and immediately applies a normalized audio-channel volume.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="value">The value.</param>
    internal void SetVolume(int channel, float value)
    {
        if (!SetVolumeValue(channel, value))
            return;

        ApplyVolume(channel, Mathf.Clamp01(value));
        RefreshDirtyState();
    }

    /// <summary>
    /// Marks the session dirty after a binding override changes.
    /// </summary>
    internal void MarkInputChanged()
    {
        _inputDiffersFromSnapshot = !string.Equals(
            _inputManager.SaveBindingOverrides(),
            _snapshotBindingOverrides,
            StringComparison.Ordinal
        );
        RefreshDirtyState();
    }

    /// <summary>
    /// Captures every revertible setting and binding override.
    /// </summary>
    private void CaptureSnapshot()
    {
        _snapshotVolumes = GetVolumes();
        _snapshotResolutionWidth = Video.ResolutionWidth;
        _snapshotResolutionHeight = Video.ResolutionHeight;
        _snapshotFullScreenMode = Video.FullScreenMode;
        _snapshotQualityLevel = Video.QualityLevel;
        _snapshotGraphicsPresetCustomized = Video.GraphicsPresetCustomized;
        _snapshotAntiAliasingMode = Video.AntiAliasingMode;
        _snapshotFrameRateLimit = Video.FrameRateLimit;
        _snapshotVerticalSync = Video.VerticalSync;
        _snapshotRayTracing = Video.RayTracing;
        _snapshotAmbientOcclusion = Video.AmbientOcclusion;
        _snapshotBloom = Video.Bloom;
        _snapshotMotionBlur = Video.MotionBlur;
        _snapshotHdrOutput = Video.HdrOutput;
        _snapshotAutosaveIntervalTicks = Gameplay.AutosaveIntervalTicks;
        _snapshotAutosavesToKeep = Gameplay.AutosavesToKeep;
        _snapshotIdleBarLocation = UserInterface.IdleBarLocation;
        _snapshotGameLaunchSpeed = Gameplay.GameLaunchSpeed;
        _snapshotBindingOverrides = _inputManager.SaveBindingOverrides();
        _inputDiffersFromSnapshot = false;
        _snapshotTactical.Clear();
        foreach (UserTacticalOption option in Enum.GetValues(typeof(UserTacticalOption)))
            _snapshotTactical[option] = Video.IsEnabled(option);
        _snapshotGameplay.Clear();
        foreach (UserGameplayOption option in Enum.GetValues(typeof(UserGameplayOption)))
            _snapshotGameplay[option] = Gameplay.IsEnabled(option);
        _snapshotUserInterface.Clear();
        foreach (UserInterfaceOption option in Enum.GetValues(typeof(UserInterfaceOption)))
            _snapshotUserInterface[option] = UserInterface.IsEnabled(option);
    }

    /// <summary>
    /// Recomputes whether the staged settings differ from the last loaded or applied snapshot.
    /// </summary>
    private void RefreshDirtyState()
    {
        float[] volumes = GetVolumes();
        if (volumes.Length != _snapshotVolumes.Length)
        {
            IsDirty = true;
            return;
        }

        if (
            UserInterface.IdleBarLocation != _snapshotIdleBarLocation
            || Gameplay.GameLaunchSpeed != _snapshotGameLaunchSpeed
        )
        {
            IsDirty = true;
            return;
        }

        for (int channel = 0; channel < volumes.Length; channel++)
        {
            if (!Mathf.Approximately(volumes[channel], _snapshotVolumes[channel]))
            {
                IsDirty = true;
                return;
            }
        }

        if (
            Video.ResolutionWidth != _snapshotResolutionWidth
            || Video.ResolutionHeight != _snapshotResolutionHeight
            || Video.FullScreenMode != _snapshotFullScreenMode
            || Video.QualityLevel != _snapshotQualityLevel
            || Video.GraphicsPresetCustomized != _snapshotGraphicsPresetCustomized
            || Video.AntiAliasingMode != _snapshotAntiAliasingMode
            || Video.FrameRateLimit != _snapshotFrameRateLimit
            || Video.VerticalSync != _snapshotVerticalSync
            || Video.RayTracing != _snapshotRayTracing
            || Video.AmbientOcclusion != _snapshotAmbientOcclusion
            || Video.Bloom != _snapshotBloom
            || Video.MotionBlur != _snapshotMotionBlur
            || Video.HdrOutput != _snapshotHdrOutput
        )
        {
            IsDirty = true;
            return;
        }

        foreach (UserTacticalOption option in Enum.GetValues(typeof(UserTacticalOption)))
        {
            if (
                !_snapshotTactical.TryGetValue(option, out bool enabled)
                || Video.IsEnabled(option) != enabled
            )
            {
                IsDirty = true;
                return;
            }
        }

        foreach (UserGameplayOption option in Enum.GetValues(typeof(UserGameplayOption)))
        {
            if (
                !_snapshotGameplay.TryGetValue(option, out bool enabled)
                || Gameplay.IsEnabled(option) != enabled
            )
            {
                IsDirty = true;
                return;
            }
        }

        foreach (UserInterfaceOption option in Enum.GetValues(typeof(UserInterfaceOption)))
        {
            if (
                !_snapshotUserInterface.TryGetValue(option, out bool enabled)
                || UserInterface.IsEnabled(option) != enabled
            )
            {
                IsDirty = true;
                return;
            }
        }

        if (
            Gameplay.AutosaveIntervalTicks != _snapshotAutosaveIntervalTicks
            || Gameplay.AutosavesToKeep != _snapshotAutosavesToKeep
        )
        {
            IsDirty = true;
            return;
        }

        IsDirty = _inputDiffersFromSnapshot;
    }

    /// <summary>
    /// Refreshes supported resolutions and aligns persisted values with the resolved mode.
    /// </summary>
    private void RebuildResolutions()
    {
        _resolutions.Clear();
        _resolutions.AddRange(_displayManager.GetSupportedResolutions());
        Vector2Int current = ResolveResolution();
        Video.ResolutionWidth = current.x;
        Video.ResolutionHeight = current.y;
        _resolutionIndex = _resolutions.IndexOf(current);
    }

    /// <summary>
    /// Resolves the persisted selection against currently supported display modes.
    /// </summary>
    /// <returns>The resolved resolution.</returns>
    private Vector2Int ResolveResolution()
    {
        return _displayManager.ResolveResolution(Video.ResolutionWidth, Video.ResolutionHeight);
    }

    /// <summary>
    /// Applies all persisted audio-channel volumes to the audio owner.
    /// </summary>
    private void ApplyAllVolumes()
    {
        _audioManager.SetMasterVolume(Audio.MasterVolume);
        _audioManager.SetMusicVolume(Audio.MusicVolume);
        _audioManager.SetSfxVolume(Audio.SfxVolume);
        _audioManager.SetAmbienceVolume(Audio.AmbienceVolume);
        _audioManager.SetVideoVolume(Audio.VideoVolume);
    }

    /// <summary>
    /// Applies one menu-indexed audio-channel volume.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="value">The value.</param>
    private void ApplyVolume(int channel, float value)
    {
        switch (channel)
        {
            case 0:
                _audioManager.SetMasterVolume(value);
                break;
            case 1:
                _audioManager.SetMusicVolume(value);
                break;
            case 2:
                _audioManager.SetSfxVolume(value);
                break;
            case 3:
                _audioManager.SetAmbienceVolume(value);
                break;
            case 4:
                _audioManager.SetVideoVolume(value);
                break;
        }
    }

    /// <summary>
    /// Stores one menu-indexed audio-channel volume when the index is valid.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="value">The value.</param>
    /// <returns>True when the channel index is valid and the volume is stored; otherwise false.</returns>
    private bool SetVolumeValue(int channel, float value)
    {
        value = Mathf.Clamp01(value);
        switch (channel)
        {
            case 0:
                Audio.MasterVolume = value;
                return true;
            case 1:
                Audio.MusicVolume = value;
                return true;
            case 2:
                Audio.SfxVolume = value;
                return true;
            case 3:
                Audio.AmbienceVolume = value;
                return true;
            case 4:
                Audio.VideoVolume = value;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Formats a serialized fullscreen mode for display in the Options menu.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The requested full screen label.</returns>
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
    /// Formats one staged graphics setting for display.
    /// </summary>
    /// <param name="setting">The graphics setting to format.</param>
    /// <returns>The displayed value.</returns>
    private string GetGraphicsLabel(UserGraphicsSetting setting)
    {
        return setting switch
        {
            UserGraphicsSetting.QualityPreset => Video.GraphicsPresetCustomized
                ? "Custom"
                : GetQualityLabel(Video.QualityLevel),
            UserGraphicsSetting.AntiAliasing => (
                (UserAntiAliasingMode)Video.AntiAliasingMode
            ) switch
            {
                UserAntiAliasingMode.Fxaa => "FXAA",
                UserAntiAliasingMode.Smaa => "SMAA",
                UserAntiAliasingMode.Taa => "TAA",
                _ => "Off",
            },
            UserGraphicsSetting.VerticalSync => GetToggleLabel(Video.VerticalSync),
            UserGraphicsSetting.FrameRateLimit => Video.FrameRateLimit == 0
                ? "Unlimited"
                : $"{Video.FrameRateLimit} FPS",
            UserGraphicsSetting.RayTracing => GetToggleLabel(Video.RayTracing),
            UserGraphicsSetting.AmbientOcclusion => GetToggleLabel(Video.AmbientOcclusion),
            UserGraphicsSetting.Bloom => GetToggleLabel(Video.Bloom),
            UserGraphicsSetting.MotionBlur => GetToggleLabel(Video.MotionBlur),
            UserGraphicsSetting.HdrOutput => GetToggleLabel(Video.HdrOutput),
            _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, null),
        };
    }

    /// <summary>
    /// Gets whether the active runtime can change one graphics setting.
    /// </summary>
    /// <param name="setting">The graphics setting to inspect.</param>
    /// <returns>True when the setting is available.</returns>
    private bool IsGraphicsSettingAvailable(UserGraphicsSetting setting)
    {
        return setting switch
        {
            UserGraphicsSetting.FrameRateLimit => !Video.VerticalSync,
            UserGraphicsSetting.RayTracing => _graphicsManager?.RayTracingAvailable == true,
            UserGraphicsSetting.HdrOutput => _graphicsManager?.HdrOutputAvailable == true,
            _ => true,
        };
    }

    /// <summary>
    /// Formats the reason one graphics setting cannot currently be changed.
    /// </summary>
    /// <param name="setting">The unavailable graphics setting.</param>
    /// <returns>The player-facing unavailability label.</returns>
    private string GetUnavailableGraphicsLabel(UserGraphicsSetting setting)
    {
        if (setting == UserGraphicsSetting.FrameRateLimit && Video.VerticalSync)
            return "Controlled by VSync";
        return "Unavailable";
    }

    /// <summary>
    /// Reconciles the custom-preset marker with the values owned by the selected quality preset.
    /// </summary>
    private void RefreshGraphicsPresetCustomization()
    {
        UserVideoSettings preset = new UserVideoSettings();
        preset.ApplyGraphicsPreset(Video.QualityLevel);
        Video.GraphicsPresetCustomized =
            Video.AntiAliasingMode != preset.AntiAliasingMode
            || Video.AmbientOcclusion != preset.AmbientOcclusion
            || Video.Bloom != preset.Bloom
            || Video.MotionBlur != preset.MotionBlur;
    }

    /// <summary>
    /// Formats a quality-level index using the active Unity quality names.
    /// </summary>
    /// <param name="qualityLevel">The quality-level index.</param>
    /// <returns>The quality name.</returns>
    private static string GetQualityLabel(int qualityLevel)
    {
        string[] names = QualitySettings.names;
        if (names.Length == 0)
            return "Default";
        return names[Mathf.Clamp(qualityLevel, 0, names.Length - 1)];
    }

    /// <summary>
    /// Formats a boolean graphics value.
    /// </summary>
    /// <param name="enabled">Whether the setting is enabled.</param>
    /// <returns>On or Off.</returns>
    private static string GetToggleLabel(bool enabled)
    {
        return enabled ? "On" : "Off";
    }

    /// <summary>
    /// Steps a zero-based value through a wrapped range.
    /// </summary>
    /// <param name="current">The current value.</param>
    /// <param name="delta">The requested direction.</param>
    /// <param name="count">The number of available values.</param>
    /// <returns>The wrapped value.</returns>
    private static int StepWrapped(int current, int delta, int count)
    {
        return ((current + Math.Sign(delta)) % count + count) % count;
    }

    /// <summary>
    /// Steps through a discrete ordered set of integer values.
    /// </summary>
    /// <param name="values">The supported values.</param>
    /// <param name="current">The current value.</param>
    /// <param name="delta">The requested direction.</param>
    /// <returns>The adjacent wrapped value.</returns>
    private static int StepValue(int[] values, int current, int delta)
    {
        int index = Array.IndexOf(values, current);
        if (index < 0)
            index = values.Length - 1;
        return values[StepWrapped(index, delta, values.Length)];
    }
}
