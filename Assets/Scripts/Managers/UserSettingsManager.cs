using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Loads, applies, captures, and saves user settings.
/// </summary>
public sealed class UserSettingsManager
{
    private const string _settingsFileName = "user-settings.json";

    private readonly AudioManager _audioManager;
    private readonly InputManager _inputManager;
    private readonly string _settingsFilePathOverride;

    /// <summary>
    /// Gets the active user settings.
    /// </summary>
    public UserSettings Settings { get; private set; }

    /// <summary>
    /// Creates a user settings manager for the supplied runtime systems.
    /// </summary>
    /// <param name="audioManager">The audio manager that receives audio settings.</param>
    /// <param name="inputManager">The input manager that receives binding overrides.</param>
    public UserSettingsManager(AudioManager audioManager, InputManager inputManager)
        : this(audioManager, inputManager, null) { }

    /// <summary>
    /// Creates a user settings manager that uses the given file path.
    /// </summary>
    /// <param name="audioManager">The audio manager that receives audio settings.</param>
    /// <param name="inputManager">The input manager that receives binding overrides.</param>
    /// <param name="settingsFilePath">The exact settings file path.</param>
    internal UserSettingsManager(
        AudioManager audioManager,
        InputManager inputManager,
        string settingsFilePath
    )
    {
        _audioManager = audioManager;
        _inputManager = inputManager;
        _settingsFilePathOverride = settingsFilePath;
    }

    /// <summary>
    /// Returns the path used for user settings persistence.
    /// </summary>
    /// <returns>The absolute user settings file path.</returns>
    public string GetSettingsFilePath()
    {
        return _settingsFilePathOverride
            ?? Path.Combine(Application.persistentDataPath, _settingsFileName);
    }

    /// <summary>
    /// Loads user settings from disk and applies them to runtime systems.
    /// </summary>
    /// <returns>The loaded user settings.</returns>
    public UserSettings Load()
    {
        Settings = LoadFromDisk();
        Apply();
        return Settings;
    }

    /// <summary>
    /// Applies the active user settings to runtime systems.
    /// </summary>
    public void Apply()
    {
        Settings ??= CreateDefaults();
        Settings.Normalize();

        _audioManager?.ApplySettings(Settings.Audio);
        _inputManager?.LoadBindingOverrides(Settings.Input.BindingOverridesJson);
        ApplyVideoSettings(Settings.Video);
    }

    /// <summary>
    /// Captures current runtime settings and writes them to disk.
    /// </summary>
    public void Save()
    {
        Settings ??= CreateDefaults();
        CaptureRuntimeSettings();
        SaveToDisk(Settings);
    }

    /// <summary>
    /// Loads user settings from the settings file.
    /// </summary>
    /// <returns>The loaded settings, or default settings when no usable file exists.</returns>
    private UserSettings LoadFromDisk()
    {
        string path = GetSettingsFilePath();
        if (!File.Exists(path))
            return CreateDefaults();

        try
        {
            UserSettings settings = JsonUtility.FromJson<UserSettings>(File.ReadAllText(path));
            if (settings == null)
                return CreateDefaults();

            settings.Normalize();
            return settings;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to load user settings: {ex.Message}");
            return CreateDefaults();
        }
    }

    /// <summary>
    /// Writes user settings to the settings file.
    /// </summary>
    /// <param name="settings">The settings to write.</param>
    private void SaveToDisk(UserSettings settings)
    {
        settings ??= CreateDefaults();
        settings.Normalize();

        string path = GetSettingsFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonUtility.ToJson(settings, true));
        if (File.Exists(path))
            File.Replace(temporaryPath, path, null);
        else
            File.Move(temporaryPath, path);
    }

    /// <summary>
    /// Captures runtime settings into the active user settings object.
    /// </summary>
    private void CaptureRuntimeSettings()
    {
        Settings ??= CreateDefaults();
        if (_audioManager != null)
            Settings.Audio = _audioManager.CreateSettingsSnapshot();
        if (_inputManager != null)
            Settings.Input.BindingOverridesJson = _inputManager.SaveBindingOverrides();

        Settings.Normalize();
    }

    /// <summary>
    /// Applies the saved display settings when the game starts.
    /// </summary>
    /// <param name="video">The normalized video settings.</param>
    private static void ApplyVideoSettings(UserVideoSettings video)
    {
        List<Vector2Int> supported = DisplayResolutionPolicy.GetSupportedResolutions();
        Vector2Int resolution = DisplayResolutionPolicy.Resolve(
            supported,
            video.ResolutionWidth,
            video.ResolutionHeight,
            Display.main.systemWidth,
            Display.main.systemHeight
        );
        video.ResolutionWidth = resolution.x;
        video.ResolutionHeight = resolution.y;

        if (Application.isEditor)
            return;

        Screen.SetResolution(resolution.x, resolution.y, (FullScreenMode)video.FullScreenMode);
    }

    /// <summary>
    /// Creates normalized default user settings.
    /// </summary>
    /// <returns>The normalized default user settings.</returns>
    private static UserSettings CreateDefaults()
    {
        UserSettings settings = new UserSettings();
        settings.Normalize();
        return settings;
    }
}
