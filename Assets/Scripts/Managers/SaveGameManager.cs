using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rebellion.Game;
using Rebellion.Util.Serialization;
using UnityEngine;

/// <summary>
///
/// </summary>
public sealed class SaveGameEntry
{
    public string FileName { get; }
    public GameMetadata Metadata { get; }

    public SaveGameEntry(string fileName, GameMetadata metadata)
    {
        FileName = fileName;
        Metadata = metadata;
    }
}

/// <summary>
/// The SaveGameManager is responsible for managing the saving and loading of the game state.
/// </summary>
public class SaveGameManager
{
    // Singleton instance.
    private static SaveGameManager instance;

    // Initialize singleton.
    public static SaveGameManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new SaveGameManager();
            }
            return instance;
        }
    }

    /// <summary>
    /// Get the directory path for saving game data.
    /// </summary>
    /// <returns>The directory path for saving game data.</returns>
    public string GetSaveDirectoryPath()
    {
        return Path.Combine(Application.persistentDataPath, "saves");
    }

    /// <summary>
    /// Get the full path to the save file.
    /// </summary>
    /// <param name="fileName">The name of the save file.</param>
    /// <returns>The full path to the save file.</returns>
    public string GetSaveFilePath(string fileName)
    {
        return Path.Combine(Application.persistentDataPath, "saves", $"{fileName}.sav");
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<SaveGameEntry> GetSavedGames()
    {
        string saveDirectory = GetSaveDirectoryPath();

        if (!Directory.Exists(saveDirectory))
            return Array.Empty<SaveGameEntry>();

        FileInfo[] files = new DirectoryInfo(saveDirectory).GetFiles(
            "*.sav",
            SearchOption.TopDirectoryOnly
        );

        List<SaveGameEntry> saves = new List<SaveGameEntry>(files.Length);

        GameSerializer serializer = new GameSerializer(typeof(GameRoot));

        foreach (FileInfo file in files)
        {
            try
            {
                using FileStream stream = new FileStream(
                    file.FullName,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read
                );

                GameMetadata metadata = serializer.DeserializeNode<GameMetadata>(stream);

                saves.Add(new SaveGameEntry(Path.GetFileNameWithoutExtension(file.Name), metadata));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to read save metadata for {file.Name}: {ex.Message}");
                // Skip corrupted/bad files
            }
        }

        // Sort newest first (based on metadata, not file system)
        return saves.OrderByDescending(s => s.Metadata.LastSavedUtc).ToList();
    }

    /// <summary>
    /// Save game data to file using XML serialization.
    /// </summary>
    /// <param name="game">The game data to save.</param>
    /// <param name="fileName">The name of the save file.</param>
    public void SaveGameData(GameRoot game, string fileName)
    {
        string saveDirectory = GetSaveDirectoryPath();

        // Create save directory if it does not exist.
        if (!Directory.Exists(saveDirectory))
        {
            Directory.CreateDirectory(saveDirectory);
        }

        // Serialize the data to a file.
        string saveFilePath = GetSaveFilePath(fileName);
        GameSerializer serializer = new GameSerializer(typeof(GameRoot));
        using FileStream fileStream = new FileStream(saveFilePath, FileMode.Create);
        serializer.Serialize(fileStream, game);
    }

    /// <summary>
    /// Load game data from file using XML deserialization.
    /// </summary>
    /// <param name="fileName">The name of the save file.</param>
    /// <returns>The loaded game data.</returns>
    public GameRoot LoadGameData(string fileName)
    {
        string saveFilePath = GetSaveFilePath(fileName);

        GameSerializer serializer = new GameSerializer(typeof(GameRoot));
        using FileStream fileStream = new FileStream(saveFilePath, FileMode.Open);
        return (GameRoot)serializer.Deserialize(fileStream);
    }
}
