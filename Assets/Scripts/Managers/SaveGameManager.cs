using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rebellion.Game;
using Rebellion.Util.Serialization;

/// <summary>
/// Represents a discovered save file paired with its deserialized metadata.
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
    private static SaveGameManager _instance;

    // Initialize singleton.
    public static SaveGameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new SaveGameManager();
            }
            return _instance;
        }
    }

    /// <summary>
    /// Get the directory path for saving game data.
    /// </summary>
    /// <returns>The directory path for saving game data.</returns>
    public string GetSaveDirectoryPath()
    {
        return Path.Combine(UnityEngine.Application.persistentDataPath, "saves");
    }

    /// <summary>
    /// Get the full path to the save file.
    /// </summary>
    /// <param name="fileName">The name of the save file.</param>
    /// <returns>The full path to the save file.</returns>
    public string GetSaveFilePath(string fileName)
    {
        return Path.Combine(UnityEngine.Application.persistentDataPath, "saves", $"{fileName}.sav");
    }

    /// <summary>
    /// Returns all valid save files in the save directory, sorted newest first.
    /// </summary>
    /// <returns>A read-only list of save game entries.</returns>
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

                GameMetadata metadata = serializer.DeserializeNode<GameMetadata>(
                    stream,
                    _metadataElementName
                );
                if (metadata == null)
                    throw new InvalidOperationException("Save metadata is missing.");

                ValidateSaveVersion(metadata.SaveVersion);

                saves.Add(new SaveGameEntry(Path.GetFileNameWithoutExtension(file.Name), metadata));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    $"Failed to read save metadata for {file.Name}: {ex.Message}"
                );
                // Skip corrupted/bad files.
            }
        }

        // Sort newest first (based on metadata, not file system)
        return saves.OrderByDescending(s => s.Metadata.LastSavedUtc).ToList();
    }

    /// <summary>
    /// Save game data to file using XML serialization.
    /// Stamps the current schema version and save timestamp onto the metadata.
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

        if (game.Metadata == null)
            game.Metadata = new GameMetadata();

        game.Metadata.SaveVersion = GameMetadata.CurrentSaveVersion;
        game.Metadata.LastSavedUtc = DateTime.UtcNow;

        // Serialize the data to a file.
        string saveFilePath = GetSaveFilePath(fileName);
        GameSerializer serializer = new GameSerializer(typeof(GameRoot));
        using FileStream fileStream = new FileStream(saveFilePath, FileMode.Create);
        serializer.Serialize(fileStream, game);
    }

    /// <summary>
    /// Load game data from file using XML deserialization.
    /// Peeks the save version first and refuses saves written by newer clients.
    /// </summary>
    /// <param name="fileName">The name of the save file.</param>
    /// <returns>The loaded game data.</returns>
    public GameRoot LoadGameData(string fileName)
    {
        string saveFilePath = GetSaveFilePath(fileName);

        GameSerializer serializer = new GameSerializer(typeof(GameRoot));

        int saveVersion = PeekSaveVersion(saveFilePath, serializer);
        ValidateSaveVersion(saveVersion);

        using FileStream fileStream = new FileStream(saveFilePath, FileMode.Open);
        return (GameRoot)serializer.Deserialize(fileStream);
    }

    /// <summary>
    /// Reads the save version from a save file's metadata.
    /// </summary>
    /// <param name="saveFilePath">The save file path.</param>
    /// <param name="serializer">The serializer used to read the save metadata.</param>
    /// <returns>The save version.</returns>
    private int PeekSaveVersion(string saveFilePath, GameSerializer serializer)
    {
        using FileStream stream = new FileStream(
            saveFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );

        GameMetadata metadata = serializer.DeserializeNode<GameMetadata>(
            stream,
            _metadataElementName
        );
        if (metadata == null)
            throw new InvalidOperationException("Save metadata is missing.");

        return metadata.SaveVersion;
    }

    /// <summary>
    /// Validates that a save version can be loaded by this client.
    /// </summary>
    /// <param name="saveVersion">The save version to validate.</param>
    private void ValidateSaveVersion(int saveVersion)
    {
        if (saveVersion != GameMetadata.CurrentSaveVersion)
        {
            throw new InvalidOperationException(
                $"Save version {saveVersion} is not supported by version {GameMetadata.CurrentSaveVersion}."
            );
        }
    }

    private const string _metadataElementName = "Metadata";
}
