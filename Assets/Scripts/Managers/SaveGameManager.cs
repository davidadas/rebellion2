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

    /// <summary>
    /// Creates a save game entry for one discovered save file.
    /// </summary>
    /// <param name="fileName">The save file name without its extension.</param>
    /// <param name="metadata">The metadata read from the save file.</param>
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
    public const int MaxDisplayNameLength = 64;
    public const string QuickSaveFileName = "quicksave";
    private const int _saveSlotCount = 6;
    private const string _quickSaveDisplayName = "Quicksave";
    private const string _saveSlotFilePrefix = "save_slot_";
    private const string _saveSlotDisplayPrefix = "Save Slot ";
    private const string _metadataElementName = "Metadata";
    private const string _metadataFileExtension = ".metadata.xml";

    // Singleton instance.
    private static SaveGameManager _instance;
    private readonly string _saveDirectoryPath;

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

    public int SaveSlotCount => _saveSlotCount;

    /// <summary>
    /// Creates the singleton save manager for the application's persistent save directory.
    /// </summary>
    private SaveGameManager()
        : this(Path.Combine(UnityEngine.Application.persistentDataPath, "saves")) { }

    /// <summary>
    /// Creates a save manager for a specified directory.
    /// </summary>
    /// <param name="saveDirectoryPath">The directory containing save files.</param>
    internal SaveGameManager(string saveDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(saveDirectoryPath))
            throw new ArgumentException(
                "A save directory path is required.",
                nameof(saveDirectoryPath)
            );

        _saveDirectoryPath = saveDirectoryPath;
    }

    /// <summary>
    /// Returns whether the supplied save slot index is valid.
    /// </summary>
    /// <param name="slot">The zero-based save slot index.</param>
    /// <returns>True when the save slot index is valid.</returns>
    public bool IsValidSaveSlot(int slot)
    {
        return slot >= 0 && slot < _saveSlotCount;
    }

    /// <summary>
    /// Returns the save file name for a numbered save slot.
    /// </summary>
    /// <param name="slot">The zero-based save slot index.</param>
    /// <returns>The save file name for the slot.</returns>
    public string GetSaveSlotFileName(int slot)
    {
        if (!IsValidSaveSlot(slot))
            throw new ArgumentOutOfRangeException(nameof(slot));

        return _saveSlotFilePrefix + (slot + 1);
    }

    /// <summary>
    /// Returns the display name for a numbered save slot.
    /// </summary>
    /// <param name="slot">The zero-based save slot index.</param>
    /// <returns>The display name for the slot.</returns>
    public string GetSaveSlotDisplayName(int slot)
    {
        if (!IsValidSaveSlot(slot))
            throw new ArgumentOutOfRangeException(nameof(slot));

        return _saveSlotDisplayPrefix + (slot + 1);
    }

    /// <summary>
    /// Get the directory path for saving game data.
    /// </summary>
    /// <returns>The directory path for saving game data.</returns>
    public string GetSaveDirectoryPath()
    {
        return _saveDirectoryPath;
    }

    /// <summary>
    /// Get the full path to the save file.
    /// </summary>
    /// <param name="fileName">The name of the save file.</param>
    /// <returns>The full path to the save file.</returns>
    public string GetSaveFilePath(string fileName)
    {
        return Path.Combine(GetSaveDirectoryPath(), $"{fileName}.sav");
    }

    /// <summary>
    /// Gets the sidecar metadata path for a save file.
    /// </summary>
    /// <param name="fileName">The save file name without its extension.</param>
    /// <returns>The full sidecar metadata path.</returns>
    internal string GetSaveMetadataFilePath(string fileName)
    {
        return Path.Combine(GetSaveDirectoryPath(), fileName + _metadataFileExtension);
    }

    /// <summary>
    /// Returns valid saves occupying the configured numbered slots.
    /// </summary>
    /// <returns>The discovered numbered save entries in slot order.</returns>
    public IReadOnlyList<SaveGameEntry> GetSaveSlotEntries()
    {
        List<SaveGameEntry> saves = new List<SaveGameEntry>(_saveSlotCount);
        for (int slot = 0; slot < _saveSlotCount; slot++)
        {
            string fileName = GetSaveSlotFileName(slot);
            if (!File.Exists(GetSaveFilePath(fileName)))
                continue;

            SaveGameEntry save = TryReadSaveEntry(fileName);
            if (save != null)
                saves.Add(save);
        }

        return saves;
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

        foreach (FileInfo file in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(file.Name);
            if (string.Equals(fileName, QuickSaveFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            SaveGameEntry save = TryReadSaveEntry(fileName);
            if (save != null)
                saves.Add(save);
        }

        // Sort newest first (based on metadata, not file system)
        return saves.OrderByDescending(save => save.Metadata.LastSavedUtc).ToList();
    }

    /// <summary>
    /// Deletes a save file and its metadata sidecar, if present.
    /// </summary>
    /// <param name="fileName">The save file name without its extension.</param>
    public void DeleteSave(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        string savePath = GetSaveFilePath(fileName);
        if (File.Exists(savePath))
            File.Delete(savePath);

        string metadataPath = GetSaveMetadataFilePath(fileName);
        if (File.Exists(metadataPath))
            File.Delete(metadataPath);
    }

    /// <summary>
    /// Renames a save's stored display name, rewriting its metadata sidecar.
    /// </summary>
    /// <param name="fileName">The save file name without its extension.</param>
    /// <param name="displayName">The new display name.</param>
    public void SetSaveDisplayName(string fileName, string displayName)
    {
        string normalizedDisplayName = NormalizeDisplayName(displayName);
        if (string.IsNullOrWhiteSpace(fileName) || normalizedDisplayName.Length == 0)
            return;

        SaveGameEntry entry = TryReadSaveEntry(fileName);
        if (entry?.Metadata == null)
            return;

        entry.Metadata.SaveDisplayName = normalizedDisplayName;
        TryWriteSaveMetadata(fileName, entry.Metadata);
    }

    /// <summary>
    /// Save game data to file using XML serialization.
    /// Stamps the current schema version and save timestamp onto the metadata.
    /// </summary>
    /// <param name="game">The game data to save.</param>
    /// <param name="fileName">The name of the save file.</param>
    /// <param name="displayName">The display name to store in save metadata.</param>
    public void SaveGameData(GameRoot game, string fileName, string displayName = null)
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
        game.Metadata.PlayerFactionID = game.Summary?.PlayerFactionID;
        game.Metadata.PackID = game.Summary?.PackID;
        game.Metadata.PackVersion = game.Summary?.PackVersion;
        game.Metadata.ScenarioID = game.Summary?.ScenarioID;
        string normalizedDisplayName = NormalizeDisplayName(displayName);
        if (normalizedDisplayName.Length > 0)
            game.Metadata.SaveDisplayName = normalizedDisplayName;
        else if (fileName == QuickSaveFileName)
            game.Metadata.SaveDisplayName = _quickSaveDisplayName;

        string saveFilePath = GetSaveFilePath(fileName);
        GameSerializer serializer = new GameSerializer(typeof(GameRoot));
        WriteSave(saveFilePath, serializer, game);

        TryWriteSaveMetadata(fileName, game.Metadata);
    }

    /// <summary>
    /// Serializes a save beside its destination and atomically publishes it only after the full
    /// payload has reached durable storage.
    /// </summary>
    private static void WriteSave(string saveFilePath, GameSerializer serializer, GameRoot game)
    {
        string temporaryPath = saveFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (
                FileStream temporaryStream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None
                )
            )
            {
                serializer.Serialize(temporaryStream, game);
                temporaryStream.Flush(true);
            }

            if (File.Exists(saveFilePath))
                File.Replace(temporaryPath, saveFilePath, null);
            else
                File.Move(temporaryPath, saveFilePath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    /// <summary>
    /// Saves game data to the quicksave slot.
    /// </summary>
    /// <param name="game">The game data to save.</param>
    public void SaveQuickGameData(GameRoot game)
    {
        SaveGameData(game, QuickSaveFileName, _quickSaveDisplayName);
    }

    /// <summary>
    /// Saves game data to one numbered save slot with a display name.
    /// </summary>
    /// <param name="game">The game data to save.</param>
    /// <param name="slot">The zero-based save slot index.</param>
    /// <param name="displayName">The display name to store in save metadata.</param>
    public void SaveSlotGameData(GameRoot game, int slot, string displayName)
    {
        if (!IsValidSaveSlot(slot))
            throw new ArgumentOutOfRangeException(nameof(slot));

        string resolvedDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? GetSaveSlotDisplayName(slot)
            : displayName.Trim();
        SaveGameData(game, GetSaveSlotFileName(slot), resolvedDisplayName);
    }

    /// <summary>
    /// Trims a save display name and constrains it to the supported length.
    /// </summary>
    /// <param name="displayName">The display name to normalize.</param>
    /// <returns>The normalized display name, or an empty string when no name was supplied.</returns>
    private static string NormalizeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return string.Empty;

        string normalized = displayName.Trim();
        return normalized.Length <= MaxDisplayNameLength
            ? normalized
            : normalized.Substring(0, MaxDisplayNameLength).TrimEnd();
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

    /// <summary>
    /// Attempts to read and validate one save entry without interrupting menu discovery.
    /// </summary>
    /// <param name="fileName">The save file name without its extension.</param>
    /// <returns>The valid save entry, or null when its metadata cannot be read.</returns>
    private SaveGameEntry TryReadSaveEntry(string fileName)
    {
        try
        {
            GameMetadata metadata = ReadSaveMetadata(fileName);
            return new SaveGameEntry(fileName, metadata);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning(
                $"Failed to read save metadata for {fileName}.sav: {ex.Message}"
            );
            return null;
        }
    }

    /// <summary>
    /// Reads current sidecar metadata or extracts it from the full save file.
    /// </summary>
    /// <param name="fileName">The save file name without its extension.</param>
    /// <returns>The deserialized save metadata.</returns>
    private GameMetadata ReadSaveMetadata(string fileName)
    {
        string saveFilePath = GetSaveFilePath(fileName);
        string metadataFilePath = GetSaveMetadataFilePath(fileName);
        if (
            File.Exists(metadataFilePath)
            && File.GetLastWriteTimeUtc(metadataFilePath) >= File.GetLastWriteTimeUtc(saveFilePath)
            && TryDeserializeMetadataFile(metadataFilePath, out GameMetadata sidecarMetadata)
        )
            return sidecarMetadata;

        GameSerializer serializer = new GameSerializer(typeof(GameRoot));
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

        TryWriteSaveMetadata(fileName, metadata);
        return metadata;
    }

    /// <summary>
    /// Attempts to deserialize one metadata sidecar.
    /// </summary>
    /// <param name="metadataFilePath">The sidecar metadata file path.</param>
    /// <param name="metadata">Receives the deserialized metadata.</param>
    /// <returns>True when the sidecar contains valid metadata.</returns>
    private bool TryDeserializeMetadataFile(string metadataFilePath, out GameMetadata metadata)
    {
        try
        {
            GameSerializer serializer = CreateMetadataSerializer();
            using FileStream stream = new FileStream(
                metadataFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read
            );
            metadata = serializer.Deserialize(stream) as GameMetadata;
            return metadata != null;
        }
        catch (Exception)
        {
            metadata = null;
            return false;
        }
    }

    /// <summary>
    /// Attempts to write one metadata sidecar without failing the primary save operation.
    /// </summary>
    /// <param name="fileName">The save file name without its extension.</param>
    /// <param name="metadata">The metadata to serialize.</param>
    /// <returns>True when the sidecar was written.</returns>
    private bool TryWriteSaveMetadata(string fileName, GameMetadata metadata)
    {
        try
        {
            GameSerializer serializer = CreateMetadataSerializer();
            using FileStream stream = new FileStream(
                GetSaveMetadataFilePath(fileName),
                FileMode.Create,
                FileAccess.Write,
                FileShare.None
            );
            serializer.Serialize(stream, metadata);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Creates the serializer used by save metadata sidecars.
    /// </summary>
    /// <returns>The configured metadata serializer.</returns>
    private static GameSerializer CreateMetadataSerializer()
    {
        return new GameSerializer(
            typeof(GameMetadata),
            new GameSerializerSettings { RootName = _metadataElementName }
        );
    }
}
