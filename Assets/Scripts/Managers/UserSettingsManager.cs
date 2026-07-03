using System;
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
    {
        _audioManager = audioManager;
        _inputManager = inputManager;
    }

    /// <summary>
    /// Returns the path used for user settings persistence.
    /// </summary>
    /// <returns>The absolute user settings file path.</returns>
    public string GetSettingsFilePath()
    {
        return Path.Combine(Application.persistentDataPath, _settingsFileName);
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
        File.WriteAllText(path, JsonUtility.ToJson(settings, true));
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
