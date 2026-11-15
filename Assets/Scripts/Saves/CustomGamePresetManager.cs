using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rebellion.Game;
using Rebellion.Util.Serialization;
using UnityEngine;

/// <summary>
/// Describes one saved custom-game configuration.
/// </summary>
public sealed class CustomGamePresetEntry
{
    public string FileName { get; }
    public string DisplayName { get; }
    public DateTime LastSavedUtc { get; }
    public GameSummary Summary { get; }

    /// <summary>
    /// Creates a discovered custom-game preset entry.
    /// </summary>
    /// <param name="fileName">The stable preset file identity.</param>
    /// <param name="displayName">The player-facing preset name.</param>
    /// <param name="lastSavedUtc">The last saved timestamp.</param>
    /// <param name="summary">The saved launch configuration.</param>
    public CustomGamePresetEntry(
        string fileName,
        string displayName,
        DateTime lastSavedUtc,
        GameSummary summary
    )
    {
        FileName = fileName;
        DisplayName = displayName;
        LastSavedUtc = lastSavedUtc;
        Summary = summary;
    }
}

/// <summary>
/// Stores named custom-game configurations independently from campaign saves.
/// </summary>
public sealed class CustomGamePresetManager
{
    private const string _fileExtension = ".customgame.xml";
    private readonly string _directoryPath;

    /// <summary>
    /// Creates a manager using the application's persistent custom-game directory.
    /// </summary>
    public CustomGamePresetManager()
        : this(Path.Combine(UserDataPaths.Root, "custom-games")) { }

    /// <summary>
    /// Creates a manager using an explicit storage directory.
    /// </summary>
    /// <param name="directoryPath">The directory containing custom-game presets.</param>
    internal CustomGamePresetManager(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("A preset directory is required.", nameof(directoryPath));

        _directoryPath = directoryPath;
    }

    /// <summary>
    /// Returns every readable preset ordered from newest to oldest.
    /// </summary>
    /// <returns>The discovered preset entries.</returns>
    public IReadOnlyList<CustomGamePresetEntry> GetPresets()
    {
        if (!Directory.Exists(_directoryPath))
            return Array.Empty<CustomGamePresetEntry>();

        List<CustomGamePresetEntry> entries = new List<CustomGamePresetEntry>();
        foreach (string path in Directory.GetFiles(_directoryPath, "*" + _fileExtension))
        {
            try
            {
                CustomGamePresetData data = Read(path);
                if (data?.Summary == null || string.IsNullOrWhiteSpace(data.DisplayName))
                    continue;

                entries.Add(
                    new CustomGamePresetEntry(
                        Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path)),
                        data.DisplayName,
                        data.LastSavedUtc,
                        data.Summary
                    )
                );
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Failed to read custom-game preset '{Path.GetFileName(path)}': {exception.Message}"
                );
            }
        }

        return entries
            .OrderByDescending(entry => entry.LastSavedUtc)
            .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Creates a named preset with a generated file identity.
    /// </summary>
    /// <param name="displayName">The player-facing preset name.</param>
    /// <param name="summary">The launch configuration to save.</param>
    /// <returns>The generated preset file identity.</returns>
    public string CreatePreset(string displayName, GameSummary summary)
    {
        string fileName = "preset_" + Guid.NewGuid().ToString("N");
        SavePreset(fileName, displayName, summary);
        return fileName;
    }

    /// <summary>
    /// Writes a launch configuration to an existing or new preset identity.
    /// </summary>
    /// <param name="fileName">The stable preset file identity.</param>
    /// <param name="displayName">The player-facing preset name.</param>
    /// <param name="summary">The launch configuration to save.</param>
    public void SavePreset(string fileName, string displayName, GameSummary summary)
    {
        ValidateFileName(fileName);
        if (summary == null)
            throw new ArgumentNullException(nameof(summary));

        string normalizedName = NormalizeDisplayName(displayName);
        if (normalizedName.Length == 0)
            throw new ArgumentException("A preset name is required.", nameof(displayName));

        Directory.CreateDirectory(_directoryPath);
        string path = GetPresetPath(fileName);
        string temporaryPath = path + ".tmp";
        CustomGamePresetData data = new CustomGamePresetData
        {
            DisplayName = normalizedName,
            LastSavedUtc = DateTime.UtcNow,
            Summary = summary,
        };
        GameSerializer serializer = new GameSerializer(typeof(CustomGamePresetData));
        try
        {
            using (
                FileStream stream = new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None
                )
            )
            {
                serializer.Serialize(stream, data);
                stream.Flush(true);
            }

            if (File.Exists(path))
                File.Replace(temporaryPath, path, null);
            else
                File.Move(temporaryPath, path);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    /// <summary>
    /// Loads one preset's launch configuration.
    /// </summary>
    /// <param name="fileName">The stable preset file identity.</param>
    /// <returns>The saved launch configuration.</returns>
    public GameSummary LoadPreset(string fileName)
    {
        ValidateFileName(fileName);
        CustomGamePresetData data = Read(GetPresetPath(fileName));
        return data?.Summary
            ?? throw new InvalidDataException($"Custom-game preset '{fileName}' has no summary.");
    }

    /// <summary>
    /// Changes one preset's display name without changing its launch configuration.
    /// </summary>
    /// <param name="fileName">The stable preset file identity.</param>
    /// <param name="displayName">The replacement player-facing name.</param>
    public void RenamePreset(string fileName, string displayName)
    {
        ValidateFileName(fileName);
        CustomGamePresetData data = Read(GetPresetPath(fileName));
        SavePreset(fileName, displayName, data.Summary);
    }

    /// <summary>
    /// Deletes one preset if it exists.
    /// </summary>
    /// <param name="fileName">The stable preset file identity.</param>
    public void DeletePreset(string fileName)
    {
        ValidateFileName(fileName);
        string path = GetPresetPath(fileName);
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>
    /// Reads one preset document from disk.
    /// </summary>
    /// <param name="path">The absolute preset path.</param>
    /// <returns>The deserialized preset data.</returns>
    private static CustomGamePresetData Read(string path)
    {
        GameSerializer serializer = new GameSerializer(typeof(CustomGamePresetData));
        using FileStream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );
        return (CustomGamePresetData)serializer.Deserialize(stream);
    }

    /// <summary>
    /// Returns the absolute path for one validated preset identity.
    /// </summary>
    /// <param name="fileName">The stable preset file identity.</param>
    /// <returns>The absolute preset path.</returns>
    private string GetPresetPath(string fileName)
    {
        return Path.Combine(_directoryPath, fileName + _fileExtension);
    }

    /// <summary>
    /// Rejects blank or path-shaped preset identities.
    /// </summary>
    /// <param name="fileName">The preset identity to validate.</param>
    private static void ValidateFileName(string fileName)
    {
        if (
            string.IsNullOrWhiteSpace(fileName)
            || !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal)
        )
            throw new ArgumentException(
                "A valid preset file identity is required.",
                nameof(fileName)
            );
    }

    /// <summary>
    /// Trims and constrains one preset display name.
    /// </summary>
    /// <param name="displayName">The supplied display name.</param>
    /// <returns>The normalized display name.</returns>
    private static string NormalizeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return string.Empty;

        string normalized = displayName.Trim();
        return normalized.Length <= SaveGameManager.MaxDisplayNameLength
            ? normalized
            : normalized.Substring(0, SaveGameManager.MaxDisplayNameLength).TrimEnd();
    }
}

/// <summary>
/// Defines the persisted contents of one custom-game preset.
/// </summary>
[PersistableObject]
public sealed class CustomGamePresetData
{
    public string DisplayName { get; set; }
    public DateTime LastSavedUtc { get; set; }
    public GameSummary Summary { get; set; }
}
