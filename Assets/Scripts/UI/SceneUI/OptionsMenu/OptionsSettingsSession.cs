using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages pending changes to graphics, audio, and input settings.
/// </summary>
internal sealed class OptionsSettingsSession
{
    // Display Modes.
    private static readonly int[] _fullScreenModes =
    {
        (int)FullScreenMode.ExclusiveFullScreen,
        (int)FullScreenMode.FullScreenWindow,
        (int)FullScreenMode.Windowed,
    };

    // Settings.
    private readonly UserSettingsManager _userSettings;
    private readonly DisplayManager _displayManager;
    private readonly AudioManager _audioManager;
    private readonly InputManager _inputManager;

    // Display Settings.
    private readonly List<Vector2Int> _resolutions = new List<Vector2Int>();
    private int _resolutionIndex;

    // Original Settings.
    private readonly Dictionary<UserTacticalOption, bool> _snapshotTactical =
        new Dictionary<UserTacticalOption, bool>();

    private float[] _snapshotVolumes = Array.Empty<float>();
    private int _snapshotResolutionWidth;
    private int _snapshotResolutionHeight;
    private int _snapshotFullScreenMode;
    private string _snapshotBindingOverrides = string.Empty;

    /// <summary>
    /// Creates a pending settings session over the runtime service owners.
    /// </summary>
    internal OptionsSettingsSession(
        UserSettingsManager userSettings,
        DisplayManager displayManager,
        AudioManager audioManager,
        InputManager inputManager
    )
    {
        _userSettings = userSettings ?? throw new ArgumentNullException(nameof(userSettings));
        _displayManager = displayManager ?? throw new ArgumentNullException(nameof(displayManager));
        _audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
        _inputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
    }

    internal bool IsDirty { get; private set; }

    internal UserVideoSettings Video => _userSettings.Settings.Video;

    internal UserAudioSettings Audio => _userSettings.Settings.Audio;

    internal string ResolutionLabel => $"{Video.ResolutionWidth} x {Video.ResolutionHeight}";

    internal string FullScreenLabel => GetFullScreenLabel(Video.FullScreenMode);

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
        foreach (KeyValuePair<UserTacticalOption, bool> entry in _snapshotTactical)
            Video.SetEnabled(entry.Key, entry.Value);

        _inputManager.LoadBindingOverrides(_snapshotBindingOverrides);
        ApplyAllVolumes();
        ApplyResolution();
        RebuildResolutions();
        IsDirty = false;
    }

    /// <summary>
    /// Restores defaults for one Options tab and applies its live effects.
    /// </summary>
    internal void RestoreDefaults(OptionsMenuTab tab)
    {
        switch (tab)
        {
            case OptionsMenuTab.Graphics:
                Video.ResolutionWidth = 0;
                Video.ResolutionHeight = 0;
                Video.FullScreenMode = (int)FullScreenMode.ExclusiveFullScreen;
                Video.RestoreTacticalDefaults();
                ApplyResolution();
                RebuildResolutions();
                break;
            case OptionsMenuTab.Audio:
                for (int channel = 0; channel < 5; channel++)
                    SetVolumeValue(channel, 1f);
                ApplyAllVolumes();
                break;
            case OptionsMenuTab.Controls:
                _inputManager.Asset.RemoveAllBindingOverrides();
                break;
            default:
                return;
        }

        IsDirty = true;
    }

    internal Dictionary<UserTacticalOption, bool> GetTacticalStates()
    {
        Dictionary<UserTacticalOption, bool> states = new Dictionary<UserTacticalOption, bool>();
        foreach (UserTacticalOption option in Enum.GetValues(typeof(UserTacticalOption)))
            states[option] = Video.IsEnabled(option);
        return states;
    }

    /// <summary>
    /// Returns the five displayed audio-channel volumes in menu order.
    /// </summary>
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
    internal void ToggleTactical(UserTacticalOption option)
    {
        Video.SetEnabled(option, !Video.IsEnabled(option));
        IsDirty = true;
    }

    /// <summary>
    /// Selects and immediately applies an adjacent supported resolution.
    /// </summary>
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
        _displayManager.Apply(Video);
        IsDirty = true;
    }

    /// <summary>
    /// Selects and immediately applies an adjacent fullscreen mode.
    /// </summary>
    internal void StepFullScreen(int delta)
    {
        int current = Array.IndexOf(_fullScreenModes, Video.FullScreenMode);
        if (current < 0)
            current = 0;
        int next =
            ((current + delta) % _fullScreenModes.Length + _fullScreenModes.Length)
            % _fullScreenModes.Length;
        Video.FullScreenMode = _fullScreenModes[next];
        ApplyResolution();
        IsDirty = true;
    }

    /// <summary>
    /// Stores and immediately applies a normalized audio-channel volume.
    /// </summary>
    internal void SetVolume(int channel, float value)
    {
        if (!SetVolumeValue(channel, value))
            return;

        ApplyVolume(channel, Mathf.Clamp01(value));
        IsDirty = true;
    }

    /// <summary>
    /// Marks the session dirty after a binding override changes.
    /// </summary>
    internal void MarkInputChanged()
    {
        IsDirty = true;
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
        _snapshotBindingOverrides = _inputManager.SaveBindingOverrides();
        _snapshotTactical.Clear();
        foreach (UserTacticalOption option in Enum.GetValues(typeof(UserTacticalOption)))
            _snapshotTactical[option] = Video.IsEnabled(option);
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
    /// Delegates the current video settings to the display owner.
    /// </summary>
    private void ApplyResolution()
    {
        _displayManager.Apply(Video);
    }

    /// <summary>
    /// Resolves the persisted selection against currently supported display modes.
    /// </summary>
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
}
