using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Rebellion.Game;
using Rebellion.Util.IO;
using Rebellion.Util.Logging;
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
    public const string AutosaveFilePrefix = "autosave_";
    public const string QuickSaveFileName = "quicksave";
    private const int _saveSlotCount = 6;
    private const string _quickSaveDisplayName = "Quicksave";
    private const string _saveSlotFilePrefix = "save_slot_";
    private const string _saveSlotDisplayPrefix = "Save Slot ";
    private const string _metadataElementName = "Metadata";
    private const string _metadataFileExtension = ".metadata.xml";
    private const string _saveDirectoryArgument = "-saveDirectory";

    // Singleton instance.
    private static SaveGameManager _instance;
    private readonly string _saveDirectoryPath;

    /// <summary>
    /// Raised immediately before game state is serialized.
    /// </summary>
    public event Action Saving;

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
        : this(
            Path.Combine(UnityEngine.Application.persistentDataPath, "saves"),
            Environment.GetCommandLineArgs()
        ) { }

    /// <summary>
    /// Creates the application's save owner using its process-level directory selection.
    /// </summary>
    /// <param name="defaultSaveDirectoryPath">The directory used when no override is supplied.</param>
    /// <param name="processArguments">The process arguments containing an optional absolute directory.</param>
    internal SaveGameManager(
        string defaultSaveDirectoryPath,
        IReadOnlyList<string> processArguments
    )
        : this(ResolveSaveDirectory(defaultSaveDirectoryPath, processArguments)) { }

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
    /// Resolves the process-level save directory without creating files or directories.
    /// </summary>
    /// <param name="defaultSaveDirectoryPath">The normal application save directory.</param>
    /// <param name="arguments">The process arguments to inspect.</param>
    /// <returns>The default directory or a validated absolute override.</returns>
    private static string ResolveSaveDirectory(
        string defaultSaveDirectoryPath,
        IReadOnlyList<string> arguments
    )
    {
        if (arguments == null)
            throw new ArgumentNullException(nameof(arguments));

        string requestedDirectory = null;
        for (int index = 0; index < arguments.Count; index++)
        {
            if (
                !string.Equals(
                    arguments[index],
                    _saveDirectoryArgument,
                    StringComparison.OrdinalIgnoreCase
                )
            )
                continue;
            if (requestedDirectory != null)
                throw new ArgumentException(
                    "The save directory override was supplied more than once.",
                    nameof(arguments)
                );
            if (++index >= arguments.Count || string.IsNullOrWhiteSpace(arguments[index]))
                throw new ArgumentException(
                    "The save directory override requires an absolute path.",
                    nameof(arguments)
                );
            requestedDirectory = arguments[index];
        }

        if (requestedDirectory == null)
            return defaultSaveDirectoryPath;
        if (!Path.IsPathFullyQualified(requestedDirectory))
            throw new ArgumentException(
                "The save directory override must be an absolute path.",
                nameof(arguments)
            );

        string fullPath = Path.GetFullPath(requestedDirectory);
        string pathWithoutRoot = fullPath.Substring(Path.GetPathRoot(fullPath).Length);
        if (
            pathWithoutRoot
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            || File.Exists(fullPath)
        )
            throw new ArgumentException(
                "The save directory override is not a valid directory path.",
                nameof(arguments)
            );
        return fullPath;
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
        ValidateSaveFileName(fileName);
        return Path.Combine(GetSaveDirectoryPath(), $"{fileName}.sav");
    }

    /// <summary>
    /// Gets the sidecar metadata path for a save file.
    /// </summary>
    /// <param name="fileName">The save file name without its extension.</param>
    /// <returns>The full sidecar metadata path.</returns>
    internal string GetSaveMetadataFilePath(string fileName)
    {
        ValidateSaveFileName(fileName);
        return Path.Combine(GetSaveDirectoryPath(), fileName + _metadataFileExtension);
    }

    /// <summary>
    /// Rejects save identifiers that could escape or alias a file outside the owned save directory.
    /// </summary>
    /// <param name="fileName">The extension-free identifier supplied by a local or remote participant.</param>
    public static void ValidateSaveFileName(string fileName)
    {
        if (
            string.IsNullOrWhiteSpace(fileName)
            || fileName is "." or ".."
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || fileName.IndexOf('/') >= 0
            || fileName.IndexOf('\\') >= 0
            || fileName.EndsWith(".", StringComparison.Ordinal)
            || fileName.EndsWith(" ", StringComparison.Ordinal)
            || Path.IsPathRooted(fileName)
        )
            throw new ArgumentException(
                "A save identifier must name one file inside the save directory.",
                nameof(fileName)
            );
    }

    /// <summary>
    /// Maps a logical save to this campaign and participant's distinct multiplayer history.
    /// </summary>
    /// <param name="campaignID">The saved campaign identity.</param>
    /// <param name="isHost">Whether the file belongs to the host's local copy.</param>
    /// <param name="fileName">The logical identifier or an existing multiplayer copy identifier.</param>
    /// <returns>The isolated local multiplayer filename without its extension.</returns>
    public static string GetMultiplayerSaveFileName(string campaignID, bool isHost, string fileName)
    {
        ValidateSaveFileName(fileName);
        if (!Guid.TryParseExact(campaignID, "N", out _))
            throw new ArgumentException("The campaign identity is invalid.", nameof(campaignID));
        if (TryParseMultiplayerSaveFileName(fileName, out _, out _, out string logicalName))
            fileName = logicalName;
        return "mp_" + campaignID + (isHost ? "_host_" : "_guest_") + fileName;
    }

    /// <summary>
    /// Recognizes the campaign and participant namespace of an existing multiplayer copy.
    /// </summary>
    /// <param name="fileName">The extension-free save identifier.</param>
    /// <param name="campaignID">The parsed campaign identity.</param>
    /// <param name="isHost">Whether the parsed copy belongs to the host.</param>
    /// <param name="logicalName">The shared logical save identifier.</param>
    /// <returns>True when the identifier uses the reserved multiplayer namespace.</returns>
    public static bool TryParseMultiplayerSaveFileName(
        string fileName,
        out string campaignID,
        out bool isHost,
        out string logicalName
    )
    {
        campaignID = null;
        logicalName = null;
        isHost = false;
        if (
            fileName == null
            || fileName.Length < 42
            || !fileName.StartsWith("mp_", StringComparison.Ordinal)
            || fileName[35] != '_'
            || !Guid.TryParseExact(fileName.Substring(3, 32), "N", out _)
        )
            return false;
        string suffix = fileName.Substring(36);
        isHost = suffix.StartsWith("host_", StringComparison.Ordinal);
        if (!isHost && !suffix.StartsWith("guest_", StringComparison.Ordinal))
            return false;
        logicalName = suffix.Substring(isHost ? 5 : 6);
        if (string.IsNullOrEmpty(logicalName))
            return false;
        ValidateSaveFileName(logicalName);
        campaignID = fileName.Substring(3, 32);
        return true;
    }

    /// <summary>
    /// Returns valid saves occupying the configured numbered slots.
    /// </summary>
    /// <returns>The discovered numbered save entries in slot order.</returns>
    public IReadOnlyList<SaveGameEntry> GetSaveSlotEntries()
    {
        return SerialFileOperations.Run(GetSaveDirectoryPath(), GetSaveSlotEntriesCore);
    }

    /// <summary>
    /// Reads numbered entries while the caller owns the directory operation.
    /// </summary>
    /// <returns>The valid numbered save entries.</returns>
    private IReadOnlyList<SaveGameEntry> GetSaveSlotEntriesCore()
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
    /// <param name="multiplayerIsHost">An optional local role filter for multiplayer copies; single-player sources remain visible.</param>
    /// <returns>A read-only list of save game entries.</returns>
    public IReadOnlyList<SaveGameEntry> GetSavedGames(bool? multiplayerIsHost = null)
    {
        return SerialFileOperations.Run(
            GetSaveDirectoryPath(),
            () => GetSavedGamesCore(multiplayerIsHost)
        );
    }

    /// <summary>
    /// Reads visible entries while the caller owns the directory operation.
    /// </summary>
    /// <param name="multiplayerIsHost">The optional participant-role filter.</param>
    /// <returns>The visible entries in descending save order.</returns>
    private IReadOnlyList<SaveGameEntry> GetSavedGamesCore(bool? multiplayerIsHost)
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
            if (
                save?.Metadata?.IsMultiplayer == true
                && multiplayerIsHost.HasValue
                && (
                    !TryParseMultiplayerSaveFileName(
                        fileName,
                        out string campaignID,
                        out bool isHost,
                        out _
                    )
                    || campaignID != save.Metadata.CampaignID
                    || isHost != multiplayerIsHost.Value
                )
            )
                continue;
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

        SerialFileOperations.Run(
            GetSaveDirectoryPath(),
            () =>
            {
                DeleteSaveFiles(GetSaveDirectoryPath(), fileName);
                return true;
            }
        );
    }

    /// <summary>
    /// Deletes a payload and cache inside an already owned directory operation.
    /// </summary>
    /// <param name="directory">The directory containing both files.</param>
    /// <param name="fileName">The extension-free save identifier.</param>
    private static void DeleteSaveFiles(string directory, string fileName)
    {
        ValidateSaveFileName(fileName);
        string savePath = Path.Combine(directory, fileName + ".sav");
        if (File.Exists(savePath))
            File.Delete(savePath);

        string metadataPath = Path.Combine(directory, fileName + _metadataFileExtension);
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
        SerialFileOperations.Run(
            GetSaveDirectoryPath(),
            () =>
            {
                SetSaveDisplayNameCore(fileName, displayName);
                return true;
            }
        );
    }

    /// <summary>
    /// Renames a cache after preceding publications in its directory have finished.
    /// </summary>
    /// <param name="fileName">The extension-free save identifier.</param>
    /// <param name="displayName">The requested local display name.</param>
    private void SetSaveDisplayNameCore(string fileName, string displayName)
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
        PrepareSaveGameData(game, fileName, displayName);
        WriteCheckpointAsync(game, fileName).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Captures a settled model on the caller and awaits durable publication without blocking rendering.
    /// The caller must own the model until this method returns its publication task.
    /// </summary>
    /// <param name="game">The settled model captured synchronously.</param>
    /// <param name="fileName">The extension-free save identifier.</param>
    /// <param name="displayName">The requested display name.</param>
    /// <returns>The complete payload and metadata-cache publication.</returns>
    public Task SaveGameDataAsync(GameRoot game, string fileName, string displayName = null)
    {
        PrepareSaveGameData(game, fileName, displayName);
        return WriteCheckpointAsync(game, fileName);
    }

    /// <summary>
    /// Invokes presentation capture and stamps the existing checkpoint metadata.
    /// </summary>
    /// <param name="game">The model whose checkpoint is being prepared.</param>
    /// <param name="fileName">The extension-free save identifier.</param>
    /// <param name="displayName">The requested display name.</param>
    private void PrepareSaveGameData(GameRoot game, string fileName, string displayName)
    {
        ValidateSaveFileName(fileName);
        Saving?.Invoke();
        EnsureCampaignIdentity(game);
        string saveDirectory = GetSaveDirectoryPath();

        // Create save directory if it does not exist.
        if (!Directory.Exists(saveDirectory))
        {
            Directory.CreateDirectory(saveDirectory);
        }

        game.Metadata.SaveVersion = GameMetadata.CurrentSaveVersion;
        game.Metadata.LastSavedUtc = DateTime.UtcNow;
        game.Metadata.PlayerFactionID = game.Summary?.PlayerFactionID;
        game.Metadata.Difficulty = game.Summary?.Difficulty;
        game.Metadata.IsCustomGame = game.Summary?.IsCustomGame == true;
        game.Metadata.CurrentTick = game.CurrentTick;
        game.Metadata.PackID = game.Summary?.PackID;
        game.Metadata.PackVersion = game.Summary?.PackVersion;
        game.Metadata.ScenarioID = game.Summary?.ScenarioID;
        game.Metadata.ModIDs = game.Summary?.ModIDs ?? Array.Empty<string>();
        game.Metadata.ModVersions = game.Summary?.ModVersions ?? Array.Empty<string>();
        string normalizedDisplayName = NormalizeDisplayName(displayName);
        if (normalizedDisplayName.Length > 0)
            game.Metadata.SaveDisplayName = normalizedDisplayName;
        else if (
            fileName == QuickSaveFileName
            || (
                TryParseMultiplayerSaveFileName(fileName, out _, out _, out string logicalName)
                && logicalName == QuickSaveFileName
            )
        )
            game.Metadata.SaveDisplayName = _quickSaveDisplayName;
    }

    /// <summary>
    /// Writes a peer copy with the exact host checkpoint metadata, without generating local timestamps.
    /// </summary>
    /// <param name="game">The synchronized local model.</param>
    /// <param name="fileName">The participant-specific local filename.</param>
    /// <param name="metadata">The verified shared host metadata.</param>
    public void SaveGameDataWithMetadata(GameRoot game, string fileName, GameMetadata metadata)
    {
        PreparePeerCheckpoint(game, fileName, metadata);
        WriteCheckpointAsync(game, fileName).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Captures a peer checkpoint on the caller and awaits its durable file publication.
    /// </summary>
    /// <param name="game">The settled peer model.</param>
    /// <param name="fileName">The participant-specific save identifier.</param>
    /// <param name="metadata">The exact host checkpoint metadata.</param>
    /// <returns>The complete peer publication, including its metadata cache.</returns>
    public Task SaveGameDataWithMetadataAsync(GameRoot game, string fileName, GameMetadata metadata)
    {
        PreparePeerCheckpoint(game, fileName, metadata);
        return WriteCheckpointAsync(game, fileName);
    }

    /// <summary>
    /// Validates a peer checkpoint and captures presentation before serialization.
    /// </summary>
    /// <param name="game">The settled peer model.</param>
    /// <param name="fileName">The participant-specific save identifier.</param>
    /// <param name="metadata">The exact host checkpoint metadata.</param>
    private void PreparePeerCheckpoint(GameRoot game, string fileName, GameMetadata metadata)
    {
        ValidateSaveFileName(fileName);
        if (game == null)
            throw new ArgumentNullException(nameof(game));
        if (
            metadata?.MultiplayerCampaign == null
            || !Guid.TryParseExact(metadata.SharedSaveToken, "N", out _)
        )
            throw new ArgumentException(
                "A coordinated save requires shared checkpoint metadata.",
                nameof(metadata)
            );
        Saving?.Invoke();
        game.Metadata = metadata;
        EnsureCampaignIdentity(game);
        Directory.CreateDirectory(GetSaveDirectoryPath());
    }

    /// <summary>
    /// Captures payload and cache bytes synchronously, then transfers only filesystem ownership.
    /// </summary>
    /// <param name="game">The settled model to capture on the calling thread.</param>
    /// <param name="fileName">The extension-free destination identifier.</param>
    /// <param name="afterPublication">Optional filesystem cleanup included in completion.</param>
    /// <returns>The complete durable publication and cleanup.</returns>
    private Task WriteCheckpointAsync(
        GameRoot game,
        string fileName,
        Action afterPublication = null
    )
    {
        GameSerializer serializer = new GameSerializer(
            typeof(GameRoot),
            new GameSerializerSettings
            {
                Idendent = false,
                NewLineHandling = NewLineHandling.Entitize,
            }
        );
        CheckpointPublication publication = new CheckpointPublication(
            GetSaveMetadataFilePath(fileName),
            afterPublication
        );
        return AtomicFile.WriteAsync(
            GetSaveFilePath(fileName),
            stream =>
            {
                serializer.Serialize(stream, game);
                using MemoryStream metadata = new MemoryStream();
                CreateMetadataSerializer().Serialize(metadata, game.Metadata);
                publication.Metadata = metadata.ToArray();
            },
            publication.Complete
        );
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
    /// Processes one completed simulation tick, creating an autosave when the configured
    /// cadence is reached.
    /// </summary>
    /// <param name="game">The game whose completed tick is being processed.</param>
    /// <param name="settings">The current gameplay settings.</param>
    /// <returns>True when an autosave was written; otherwise false.</returns>
    public bool ProcessAutosaveTick(GameRoot game, UserGameplaySettings settings)
    {
        if (
            settings?.AutosaveEnabled != true
            || game == null
            || game.CurrentTick <= 0
            || settings.AutosaveIntervalTicks <= 0
            || game.CurrentTick % settings.AutosaveIntervalTicks != 0
        )
            return false;

        try
        {
            if (game.Metadata?.IsMultiplayer == true)
                return ProcessMultiplayerAutosaveTick(game, settings);
            EnsureCampaignIdentity(game);
            string fileName =
                AutosaveFilePrefix
                + game.Metadata.CampaignID
                + "_"
                + game.CurrentTick.ToString("D10");
            SaveGameData(game, fileName, $"Autosave - Tick {game.CurrentTick}");
            PruneAutosaveHistory(game.Metadata.CampaignID, settings.AutosavesToKeep);
            GameLogger.Log($"Autosave completed at tick {game.CurrentTick}.");
            return true;
        }
        catch (Exception exception)
        {
            GameLogger.Warning($"Autosave failed at tick {game.CurrentTick}: {exception.Message}");
            return false;
        }
    }

    /// <summary>
    /// Writes and rotates only host multiplayer restore points, propagating durable-write failures.
    /// </summary>
    /// <param name="game">The host's settled shared model.</param>
    /// <param name="settings">The host's existing cadence and retention settings.</param>
    /// <returns>True when the configured automatic checkpoint was written.</returns>
    public bool ProcessMultiplayerAutosaveTick(GameRoot game, UserGameplaySettings settings)
    {
        return ProcessMultiplayerAutosaveTickAsync(game, settings).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Captures a host restore point, then awaits its durable publication and scoped rotation.
    /// </summary>
    /// <param name="game">The host's settled shared model.</param>
    /// <param name="settings">The host's existing cadence and retention settings.</param>
    /// <returns>True when the configured automatic checkpoint and rotation completed.</returns>
    public async Task<bool> ProcessMultiplayerAutosaveTickAsync(
        GameRoot game,
        UserGameplaySettings settings
    )
    {
        if (
            settings?.AutosaveEnabled != true
            || game == null
            || game.CurrentTick <= 0
            || settings.AutosaveIntervalTicks <= 0
            || game.CurrentTick % settings.AutosaveIntervalTicks != 0
        )
            return false;
        EnsureCampaignIdentity(game);
        if (!game.Metadata.IsMultiplayer)
            throw new ArgumentException(
                "A multiplayer checkpoint requires participant bindings.",
                nameof(game)
            );
        string logicalName = AutosaveFilePrefix + game.CurrentTick.ToString("D10");
        string fileName = GetMultiplayerSaveFileName(game.Metadata.CampaignID, true, logicalName);
        PrepareSaveGameData(game, fileName, $"Autosave - Tick {game.CurrentTick}");
        Action prune = CreateAutosaveRotation(
            GetSaveDirectoryPath(),
            game.Metadata.CampaignID,
            settings.AutosavesToKeep
        );
        await WriteCheckpointAsync(game, fileName, prune).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Captures only immutable filesystem policy for a host checkpoint's rotation.
    /// </summary>
    /// <param name="directory">The directory containing this campaign's restore points.</param>
    /// <param name="campaignID">The campaign namespace captured after save preparation.</param>
    /// <param name="keep">The captured host retention setting.</param>
    /// <returns>The model-free filesystem continuation.</returns>
    private static Action CreateAutosaveRotation(string directory, string campaignID, int keep)
    {
        return () => PruneAutosaveHistory(directory, campaignID, keep, true);
    }

    /// <summary>
    /// Assigns a campaign identity at its first save and validates identities used in filenames.
    /// </summary>
    /// <param name="game">The game whose save history is being identified.</param>
    private static void EnsureCampaignIdentity(GameRoot game)
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));
        game.Metadata ??= new GameMetadata();
        if (string.IsNullOrWhiteSpace(game.Metadata.CampaignID))
            game.Metadata.CampaignID = Guid.NewGuid().ToString("N");
        else if (!Guid.TryParseExact(game.Metadata.CampaignID, "N", out _))
            throw new ArgumentException("The campaign identity is invalid.", nameof(game));
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
    /// Prunes only this campaign's tick-addressed restore points after a new autosave is written.
    /// </summary>
    /// <param name="campaignId">The identity of the campaign whose history may be pruned.</param>
    /// <param name="autosavesToKeep">The maximum number of autosaves to retain.</param>
    /// <param name="multiplayer">Whether only the host multiplayer namespace may rotate.</param>
    private void PruneAutosaveHistory(
        string campaignId,
        int autosavesToKeep,
        bool multiplayer = false
    )
    {
        SerialFileOperations.Run(
            GetSaveDirectoryPath(),
            () =>
            {
                PruneAutosaveHistory(
                    GetSaveDirectoryPath(),
                    campaignId,
                    autosavesToKeep,
                    multiplayer
                );
                return true;
            }
        );
    }

    /// <summary>
    /// Rotates a captured campaign namespace inside an already owned directory operation.
    /// </summary>
    /// <param name="saveDirectory">The owned save directory.</param>
    /// <param name="campaignId">The campaign whose restore points may rotate.</param>
    /// <param name="autosavesToKeep">The captured retention setting.</param>
    /// <param name="multiplayer">Whether rotation is limited to host multiplayer copies.</param>
    private static void PruneAutosaveHistory(
        string saveDirectory,
        string campaignId,
        int autosavesToKeep,
        bool multiplayer
    )
    {
        if (!Directory.Exists(saveDirectory))
            return;

        string campaignPrefix = multiplayer
            ? GetMultiplayerSaveFileName(campaignId, true, AutosaveFilePrefix)
            : AutosaveFilePrefix + campaignId + "_";
        FileInfo[] autosaves = new DirectoryInfo(saveDirectory)
            .GetFiles($"{campaignPrefix}*.sav", SearchOption.TopDirectoryOnly)
            .Where(file => IsTickAddressedAutosave(file.Name, campaignPrefix))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        for (int index = autosavesToKeep; index < autosaves.Length; index++)
            DeleteSaveFiles(saveDirectory, Path.GetFileNameWithoutExtension(autosaves[index].Name));
    }

    /// <summary>
    /// Checks the reserved tick suffix so unrelated files sharing a prefix are never rotated.
    /// </summary>
    /// <param name="fileName">The save filename including its extension.</param>
    /// <param name="campaignPrefix">The already matched campaign-specific prefix.</param>
    /// <returns>True when the suffix contains exactly ten decimal tick digits.</returns>
    private static bool IsTickAddressedAutosave(string fileName, string campaignPrefix)
    {
        const int tickDigitCount = 10;
        const int extensionLength = 4;
        if (fileName.Length != campaignPrefix.Length + tickDigitCount + extensionLength)
            return false;
        for (
            int index = campaignPrefix.Length;
            index < campaignPrefix.Length + tickDigitCount;
            index++
        )
        {
            if (fileName[index] < '0' || fileName[index] > '9')
                return false;
        }
        return true;
    }

    /// <summary>
    /// Load game data from file using XML deserialization.
    /// </summary>
    /// <param name="fileName">The name of the save file.</param>
    /// <returns>The loaded game data.</returns>
    public GameRoot LoadGameData(string fileName)
    {
        string saveFilePath = GetSaveFilePath(fileName);
        return SerialFileOperations.Run(
            GetSaveDirectoryPath(),
            () => DeserializeGame(saveFilePath)
        );
    }

    /// <summary>
    /// Reads and deserializes a saved game without blocking the caller's synchronization context.
    /// </summary>
    /// <param name="fileName">The name of the save file.</param>
    /// <returns>A task containing the loaded game data.</returns>
    public async Task<GameRoot> LoadGameDataAsync(string fileName)
    {
        string saveFilePath = GetSaveFilePath(fileName);
        List<(Type ObjectType, string ElementName)> skippedElements = new();
        GameRoot loadedGame = await Task.Run(() =>
            SerialFileOperations.Run(
                GetSaveDirectoryPath(),
                () =>
                    DeserializeGame(
                        saveFilePath,
                        (objectType, elementName) => skippedElements.Add((objectType, elementName))
                    )
            )
        );
        foreach (
            IGrouping<
                (Type ObjectType, string ElementName),
                (Type ObjectType, string ElementName)
            > group in skippedElements.GroupBy(item => item)
        )
        {
            LogUnknownSaveElement(group.Key.ObjectType, group.Key.ElementName, group.Count());
        }
        return loadedGame;
    }

    /// <summary>
    /// Reads and deserializes one game save from an absolute path.
    /// </summary>
    /// <param name="saveFilePath">The absolute save path.</param>
    /// <param name="unknownElementSkipped">Receives elements omitted for compatibility.</param>
    /// <returns>The deserialized game data.</returns>
    private static GameRoot DeserializeGame(
        string saveFilePath,
        Action<Type, string> unknownElementSkipped = null
    )
    {
        GameSerializer serializer = CreateSaveDeserializer(
            typeof(GameRoot),
            unknownElementSkipped: unknownElementSkipped
        );

        using FileStream fileStream = new FileStream(
            saveFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );
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
            GameMetadata metadata = GetSaveMetadataCore(fileName);
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
    /// Reads current sidecar metadata or extracts the embedded metadata from the save file.
    /// </summary>
    /// <param name="fileName">The save file name without its extension.</param>
    /// <returns>The deserialized save metadata.</returns>
    public GameMetadata GetSaveMetadata(string fileName)
    {
        return SerialFileOperations.Run(
            GetSaveDirectoryPath(),
            () => GetSaveMetadataCore(fileName)
        );
    }

    /// <summary>
    /// Reads embedded or cached metadata inside an already owned directory operation.
    /// </summary>
    /// <param name="fileName">The extension-free save identifier.</param>
    /// <returns>The metadata from the fully published checkpoint.</returns>
    private GameMetadata GetSaveMetadataCore(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("A save file name is required.", nameof(fileName));

        string saveFilePath = GetSaveFilePath(fileName);
        if (!File.Exists(saveFilePath))
            throw new FileNotFoundException(
                "The requested save file does not exist.",
                saveFilePath
            );

        string metadataFilePath = GetSaveMetadataFilePath(fileName);
        if (
            File.Exists(metadataFilePath)
            && File.GetLastWriteTimeUtc(metadataFilePath) >= File.GetLastWriteTimeUtc(saveFilePath)
            && TryDeserializeMetadataFile(metadataFilePath, out GameMetadata sidecarMetadata)
        )
            return sidecarMetadata;

        GameSerializer serializer = CreateSaveDeserializer(typeof(GameRoot));
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
            GameSerializer serializer = CreateSaveDeserializer(
                typeof(GameMetadata),
                _metadataElementName
            );
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
            new GameSerializerSettings
            {
                RootName = _metadataElementName,
                NewLineHandling = NewLineHandling.Entitize,
            }
        );
    }

    /// <summary>
    /// Creates a deserializer that tolerates fields written by another game version.
    /// </summary>
    /// <param name="type">The root type to deserialize.</param>
    /// <param name="rootName">The optional serialized root element name.</param>
    /// <param name="unknownElementSkipped">Receives elements omitted for compatibility.</param>
    /// <returns>The configured save-game deserializer.</returns>
    private static GameSerializer CreateSaveDeserializer(
        Type type,
        string rootName = null,
        Action<Type, string> unknownElementSkipped = null
    )
    {
        return new GameSerializer(
            type,
            new GameSerializerSettings
            {
                RootName = rootName,
                IgnoreWhitespace = false,
                IgnoreUnknownElements = true,
                UnknownElementSkipped = unknownElementSkipped ?? LogUnknownSaveElement,
            }
        );
    }

    /// <summary>
    /// Logs that a save contained an element unknown to the current game version.
    /// </summary>
    /// <param name="objectType">The object type that did not recognize the element.</param>
    /// <param name="elementName">The skipped XML element name.</param>
    private static void LogUnknownSaveElement(Type objectType, string elementName)
    {
        LogUnknownSaveElement(objectType, elementName, 1);
    }

    /// <summary>
    /// Logs a counted group of matching unknown save elements at verbose level.
    /// </summary>
    /// <param name="objectType">The object type that did not recognize the element.</param>
    /// <param name="elementName">The skipped XML element name.</param>
    /// <param name="occurrenceCount">The number of matching skipped elements.</param>
    private static void LogUnknownSaveElement(
        Type objectType,
        string elementName,
        int occurrenceCount
    )
    {
        string occurrenceSuffix = occurrenceCount > 1 ? $" ({occurrenceCount} occurrences)" : "";
        GameLogger.Debug(
            $"Ignored unknown element '{elementName}' while loading {objectType.Name}{occurrenceSuffix}."
        );
    }

    /// <summary>
    /// Owns only captured cache bytes and filesystem cleanup after payload publication.
    /// </summary>
    private sealed class CheckpointPublication
    {
        private readonly string _metadataPath;
        private readonly Action _afterPublication;

        internal byte[] Metadata { get; set; }

        /// <summary>
        /// Captures model-free filesystem completion work.
        /// </summary>
        /// <param name="metadataPath">The sidecar cache destination.</param>
        /// <param name="afterPublication">Optional filesystem cleanup after the cache attempt.</param>
        internal CheckpointPublication(string metadataPath, Action afterPublication)
        {
            _metadataPath = metadataPath;
            _afterPublication = afterPublication;
        }

        /// <summary>
        /// Writes the captured optional cache and completes owned filesystem cleanup.
        /// </summary>
        internal void Complete()
        {
            TryWriteMetadataBytes();
            _afterPublication?.Invoke();
        }

        /// <summary>
        /// Attempts the optional cache without invalidating a complete primary checkpoint.
        /// </summary>
        private void TryWriteMetadataBytes()
        {
            try
            {
                using FileStream stream = new FileStream(
                    _metadataPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None
                );
                stream.Write(Metadata, 0, Metadata.Length);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
