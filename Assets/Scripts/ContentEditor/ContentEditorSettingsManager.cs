using System;
using System.IO;
using System.Linq;
using Rebellion.Util.IO;
using Rebellion.Util.Logging;
using UnityEngine;

/// <summary>
/// Stores application preferences independently of content packs and editor build locations.
/// </summary>
public sealed class ContentEditorSettingsManager
{
    private readonly string path;
    private readonly ContentEditorSettings defaults;

    /// <summary>
    /// Selects the user-profile settings file and optional preferences from an earlier installation.
    /// </summary>
    /// <param name="filePath">The profile's settings file.</param>
    /// <param name="initialSettings">Preferences used when no file has been saved.</param>
    public ContentEditorSettingsManager(
        string filePath,
        ContentEditorSettings initialSettings = null
    )
    {
        path = Path.GetFullPath(filePath ?? throw new ArgumentNullException(nameof(filePath)));
        defaults = initialSettings ?? new ContentEditorSettings();
    }

    /// <summary>
    /// Reads normalized application settings without changing any authored content.
    /// </summary>
    /// <returns>The current settings, or defaults when no usable file exists.</returns>
    public ContentEditorSettings Load()
    {
        ContentEditorSettings settings = new ContentEditorSettings();
        try
        {
            if (File.Exists(path))
            {
                JsonUtility.FromJsonOverwrite(AtomicTextFile.ReadAllText(path), settings);
            }
            else
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(defaults), settings);
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            GameLogger.Warning("Editor settings could not be read: " + exception.Message);
            settings = new ContentEditorSettings();
        }
        settings ??= new ContentEditorSettings();
        settings.Normalize();
        return settings;
    }

    /// <summary>
    /// Updates only the selected theme, preserving changes made by another editor window.
    /// </summary>
    /// <param name="mode">The requested presentation.</param>
    public void SaveColorMode(ContentEditorColorMode mode) =>
        Update(settings => settings.ColorMode = mode);

    /// <summary>
    /// Remembers content zoom and pane width for the application profile.
    /// </summary>
    /// <param name="zoom">The content scale.</param>
    /// <param name="split">The navigation-pane width as a window fraction.</param>
    public void SaveLayout(float zoom, float split) =>
        Update(settings =>
        {
            settings.Zoom = zoom;
            settings.Split = split;
        });

    /// <summary>
    /// Remembers the source folder shared by the image and layer file pickers.
    /// </summary>
    /// <param name="directory">The folder of the last imported image.</param>
    internal void SaveImageImportDirectory(string directory) =>
        Update(settings => settings.ImageImportDirectory = directory);

    /// <summary>
    /// Records acknowledgement without affecting another window's presentation preferences.
    /// </summary>
    public void AcknowledgeAlphaNotice() =>
        Update(settings => settings.AlphaNoticeAcknowledged = true);

    /// <summary>
    /// Reads the latest preferences and atomically replaces their file after one change.
    /// </summary>
    /// <param name="change">The preference update.</param>
    private void Update(Action<ContentEditorSettings> change)
    {
        AtomicTextFile.UpdateAllText(
            path,
            () =>
            {
                ContentEditorSettings settings = Load();
                change(settings);
                settings.Normalize();
                return JsonUtility.ToJson(settings, true);
            }
        );
    }

    /// <summary>
    /// Remembers a successfully opened working folder without storing its project contents.
    /// </summary>
    /// <param name="folder">The local folder containing pack.xml.</param>
    public void RememberProject(string folder)
    {
        string path = Path.GetFullPath(folder);
        Update(settings =>
            settings.ProjectFolders = new[] { path }
                .Concat(settings.ProjectFolders)
                .Distinct(
                    System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                        System.Runtime.InteropServices.OSPlatform.Windows
                    )
                        ? StringComparer.OrdinalIgnoreCase
                        : StringComparer.Ordinal
                )
                .ToArray()
        );
    }
}

/// <summary>
/// Defines presentation and acknowledgement preferences for the standalone editor.
/// </summary>
[Serializable]
public sealed class ContentEditorSettings
{
    public ContentEditorColorMode ColorMode = ContentEditorColorMode.Dark;
    public float Zoom = 1;
    public float Split = 0.205f;
    public bool AlphaNoticeAcknowledged;
    public string ImageImportDirectory;
    public string[] ProjectFolders = Array.Empty<string>();

    /// <summary>
    /// Keeps persisted values within the same limits used by the workbench.
    /// </summary>
    public void Normalize()
    {
        ProjectFolders = (ProjectFolders ?? Array.Empty<string>())
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (!Enum.IsDefined(typeof(ContentEditorColorMode), ColorMode))
            ColorMode = ContentEditorColorMode.Dark;
        Zoom = float.IsNaN(Zoom) || float.IsInfinity(Zoom) ? 1 : Mathf.Clamp(Zoom, 0.65f, 1.75f);
        Split =
            float.IsNaN(Split) || float.IsInfinity(Split)
                ? 0.205f
                : Mathf.Clamp(Split, 0.15f, 0.65f);
    }
}
