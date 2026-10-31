using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Stores application preferences independently of content packs and editor build locations.
/// </summary>
public sealed class ContentEditorSettingsManager
{
    private static readonly object updateGate = new();
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
                using FileStream input = new(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read | FileShare.Delete
                );
                using StreamReader reader = new(input);
                JsonUtility.FromJsonOverwrite(reader.ReadToEnd(), settings);
            }
            else
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(defaults), settings);
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Debug.LogWarning("Editor settings could not be read: " + exception.Message);
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
        // File locks can be process-scoped, so threads must also share one gate.
        lock (updateGate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // Retain this file: replacing or deleting it would split locks across different files.
            using FileStream ownership = new(
                path + ".lock",
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite
            );
            AcquireLock(ownership);
            ContentEditorSettings settings = Load();
            change(settings);
            settings.Normalize();
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(settings, true));
                if (File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
    }

    /// <summary>
    /// Serializes updates from separate editor processes without leaving ownership after a crash.
    /// </summary>
    /// <param name="ownership">The shared settings lock file, released when its handle closes.</param>
    private static void AcquireLock(FileStream ownership)
    {
        Stopwatch waiting = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                ownership.Lock(0, 1);
                return;
            }
            catch (IOException exception)
                when ((exception.HResult & 0xffff) == 33 && waiting.ElapsedMilliseconds < 5000)
            {
                Thread.Sleep(10);
            }
        }
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

    /// <summary>
    /// Keeps persisted values within the same limits used by the workbench.
    /// </summary>
    public void Normalize()
    {
        if (!Enum.IsDefined(typeof(ContentEditorColorMode), ColorMode))
            ColorMode = ContentEditorColorMode.Dark;
        Zoom = float.IsNaN(Zoom) || float.IsInfinity(Zoom) ? 1 : Mathf.Clamp(Zoom, 0.65f, 1.75f);
        Split =
            float.IsNaN(Split) || float.IsInfinity(Split)
                ? 0.205f
                : Mathf.Clamp(Split, 0.15f, 0.65f);
    }
}
