using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Gameplay;
using Rebellion.Persistence;
using Rebellion.PlayerRequests;
using Rebellion.Util.Logging;
using Rebellion.Util.Reflection;
using UnityEngine;

/// <summary>
/// Owns the active game-session lifecycle, hot loading, and content-identity validation.
/// </summary>
public sealed class GameRuntime
{
    private readonly ContentPack _contentPack;
    private readonly SaveGameManager _saveGameManager;
    private readonly Func<UserGameplaySettings> _getGameplaySettings;
    private GameSession _activeGameSession;
    private GameManager _gameManager;
    private bool _operationsBlocked;
    private IGamePersistenceCoordinator _persistenceCoordinator;
    private SessionPlayerRequestDispatcher _playerRequests;
    private IGameSessionOwner _sessionOwner;

    /// <summary>
    /// Gets whether persistence is owned by a coordinated session rather than the local process.
    /// </summary>
    public bool HasCoordinatedPersistence => _persistenceCoordinator != null;

    /// <summary>
    /// Gets the active player's ordered application action boundary.
    /// </summary>
    public IPlayerRequestDispatcher PlayerRequests => _playerRequests;

    public event Action<GameRoot> GameReplaced;

    /// <summary>
    /// Raised when an ordered remote operation changes the existing live graph.
    /// </summary>
    public event Action GameStateChanged;

    /// <summary>
    /// Reports a storage failure belonging to the session that requested the save or load.
    /// </summary>
    public event Action<GameRuntime, PersistenceOperation, Exception> PersistenceFailed;

    /// <summary>
    /// Gets whether a game session is currently active.
    /// </summary>
    public bool HasActiveGame => _activeGameSession != null;

    /// <summary>
    /// Gets whether the persistence owner can accept a request without requiring an immediate snapshot.
    /// </summary>
    public bool CanRequestSave =>
        _persistenceCoordinator != null
            ? !_operationsBlocked && _persistenceCoordinator.CanRequestSave
            : CanSave;

    /// <summary>
    /// Gets whether a complete settled snapshot can be captured immediately.
    /// </summary>
    public bool CanSave =>
        !_operationsBlocked && _activeGameSession?.CanCaptureSettledState == true;

    /// <summary>
    /// Creates an application runtime backed by the active content pack.
    /// </summary>
    /// <param name="contentPack">The active content pack.</param>
    /// <param name="saveGameManager">The save manager, or null to use the application singleton.</param>
    /// <param name="getGameplaySettings">Returns the current gameplay settings.</param>
    internal GameRuntime(
        ContentPack contentPack,
        SaveGameManager saveGameManager = null,
        Func<UserGameplaySettings> getGameplaySettings = null
    )
    {
        _contentPack = contentPack ?? throw new ArgumentNullException(nameof(contentPack));
        _saveGameManager = saveGameManager ?? SaveGameManager.Instance;
        _getGameplaySettings = getGameplaySettings;
    }

    /// <summary>
    /// Get the current active game instance.
    /// </summary>
    /// <returns>The active game, or null when no game session is active.</returns>
    public GameRoot GetActiveGame()
    {
        return _activeGameSession?.Game;
    }

    /// <summary>
    /// Gets the active game manager.
    /// </summary>
    /// <returns>The active game manager, or null when no game session is active.</returns>
    public GameManager GetActiveGameManager()
    {
        return _gameManager;
    }

    /// <summary>
    /// Gets the active simulation for strategy composition.
    /// </summary>
    /// <returns>The active session, or null when no game is running.</returns>
    public GameSession GetActiveGameSession() => _activeGameSession;

    /// <summary>
    /// Start a new game session.
    /// Creates and owns the simulation and its application clock.
    /// </summary>
    /// <param name="game">The game instance to manage.</param>
    /// <param name="owner">The coordinated owner, required for multiplayer campaigns.</param>
    /// <returns>The created game session.</returns>
    public GameSession StartGame(GameRoot game, IGameSessionOwner owner = null)
    {
        return ReplaceSession(game, owner);
    }

    /// <summary>
    /// Starts a loaded session, leaving coordinated encounter detection to its ordered game turns.
    /// </summary>
    /// <param name="game">The loaded game instance to manage.</param>
    /// <param name="owner">The coordinated owner, required for multiplayer campaigns.</param>
    /// <returns>The created game session.</returns>
    public GameSession StartLoadedGame(GameRoot game, IGameSessionOwner owner = null)
    {
        GameSession session = ReplaceSession(game, owner);
        ReconcileLoadedSession();
        return session;
    }

    /// <summary>
    /// Reconciles local encounters immediately while coordinated sessions wait for an agreed turn.
    /// </summary>
    private void ReconcileLoadedSession()
    {
        if (_sessionOwner == null)
            _activeGameSession.Tick.ReconcileLoadedState();
    }

    /// <summary>
    /// Replaces the active game session with one backed by the supplied game.
    /// </summary>
    /// <param name="game">The game instance to manage.</param>
    /// <param name="owner">The execution and persistence owner chosen before composition.</param>
    /// <returns>The created game session.</returns>
    private GameSession ReplaceSession(GameRoot game, IGameSessionOwner owner)
    {
        ValidateSessionOwner(game, owner);
        if (_activeGameSession != null)
        {
            EndGame();
        }

        owner?.PrepareGame(game);
        ValidateGameContent(game);
        ReconcileStaticContent(game);
        game.Metadata ??= new GameMetadata();
        if (string.IsNullOrEmpty(game.Metadata.CampaignID))
            game.Metadata.CampaignID = Guid.NewGuid().ToString("N");
        GameSession session = new GameSession(game, _contentPack.GameData);
        return BindSession(session, owner);
    }

    /// <summary>
    /// Connects application clock, player actions, and persistence to an initialized simulation owner.
    /// </summary>
    /// <param name="session">The initialized simulation session.</param>
    /// <param name="owner">The declared coordinated owner, or null for local execution.</param>
    /// <returns>The active session.</returns>
    private GameSession BindSession(GameSession session, IGameSessionOwner owner)
    {
        GameManager clock = new GameManager(() => session.Game, session.Tick);
        _activeGameSession = session;
        _gameManager = clock;
        _operationsBlocked = false;
        _sessionOwner = owner;
        _persistenceCoordinator = owner;
        _playerRequests = new SessionPlayerRequestDispatcher(
            owner
                ?? (IPlayerRequestDispatcher)
                    new LocalPlayerRequestDispatcher(
                        new PlayerRequestExecutor(session, clock),
                        () => session.Game.GetPlayerFaction()?.InstanceID,
                        () =>
                        {
                            if (ReferenceEquals(_activeGameSession, session))
                                NotifyGameStateChanged();
                        }
                    ),
            () => ReferenceEquals(_activeGameSession, session)
        );
        _activeGameSession.Tick.TickCompleted += HandleTickCompleted;
        try
        {
            owner?.BindRuntime(this);
        }
        catch
        {
            EndGame();
            throw;
        }
        return _activeGameSession;
    }

    /// <summary>
    /// Requires saved multiplayer campaigns to declare their execution owner before composition.
    /// </summary>
    /// <param name="game">The graph being started.</param>
    /// <param name="owner">The declared execution owner.</param>
    private static void ValidateSessionOwner(GameRoot game, IGameSessionOwner owner)
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));
        if (game.Metadata?.MultiplayerCampaign != null && owner == null)
            throw new InvalidOperationException(
                "A multiplayer campaign requires its session owner before starting."
            );
    }

    /// <summary>
    /// Reports completed presentation setup to the owner selected when the session was created.
    /// </summary>
    /// <param name="session">The originating session whose presentation finished.</param>
    /// <returns>False when an outgoing presentation no longer belongs to the active session.</returns>
    public bool NotifyPresentationReady(GameSession session)
    {
        if (!HasActiveGame || !ReferenceEquals(_activeGameSession, session))
            return false;
        _sessionOwner?.NotifyPresentationReady(this);
        return true;
    }

    /// <summary>
    /// Admits the next tick through this session's existing execution owner.
    /// </summary>
    /// <param name="elapsedSeconds">The elapsed application frame interval.</param>
    /// <returns>True when the current session should begin its next tick.</returns>
    public bool TryBeginGameTick(float elapsedSeconds)
    {
        return HasActiveGame
            && (
                _sessionOwner != null
                    ? _sessionOwner.TryBeginGameTick(elapsedSeconds)
                    : _gameManager.TryAdvanceTickTimer(elapsedSeconds)
            );
    }

    /// <summary>
    /// End the current game session.
    /// Stops game logic and clears the active session.
    /// </summary>
    public void EndGame()
    {
        if (_activeGameSession == null)
            return;

        try
        {
            _sessionOwner?.ReleaseRuntime(this);
        }
        finally
        {
            _sessionOwner = null;
            _activeGameSession.Tick.TickCompleted -= HandleTickCompleted;
            _gameManager.ApplyGameSpeed(TickSpeed.Paused);
            _activeGameSession.Dispose();
            _activeGameSession = null;
            _gameManager = null;
            _playerRequests = null;
            _persistenceCoordinator = null;
        }
    }

    /// <summary>
    /// Invalidates strategy projections without replacing windows or services.
    /// </summary>
    internal void NotifyGameStateChanged()
    {
        GameStateChanged?.Invoke();
    }

    /// <summary>
    /// Prevents saving or autosaving a session whose simulation could be partially applied.
    /// </summary>
    internal void BlockGameOperations()
    {
        _operationsBlocked = true;
    }

    /// <summary>
    /// Reopens persistence after the session owner establishes a writable multiplayer boundary.
    /// </summary>
    internal void ResumeMultiplayerOperations()
    {
        if (_sessionOwner == null || _activeGameSession?.CanCaptureSettledState != true)
            throw new InvalidOperationException("A settled coordinated session is required.");
        _operationsBlocked = false;
    }

    /// <summary>
    /// Assigns the current session's save and load policy to its persistence owner.
    /// </summary>
    /// <param name="coordinator">The owner of save and load permissions and completion.</param>
    internal void BindPersistenceCoordinator(IGamePersistenceCoordinator coordinator)
    {
        if (!HasActiveGame)
            throw new InvalidOperationException("No active game can coordinate persistence.");
        _persistenceCoordinator =
            coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    /// <summary>
    /// Restores local persistence ownership after the coordinated session has released its requests.
    /// </summary>
    internal void UnbindPersistenceCoordinator()
    {
        _persistenceCoordinator = null;
    }

    /// <summary>
    /// Returns the actual persistence store owned by this runtime, including injected stores.
    /// </summary>
    /// <returns>The runtime's save manager.</returns>
    internal SaveGameManager GetSaveGameManager() => _saveGameManager;

    /// <summary>
    /// Saves the quicksave through the current persistence owner.
    /// </summary>
    /// <returns>True only after the quicksave has completed.</returns>
    public async Task<bool> QuickSaveAsync()
    {
        bool coordinated = HasCoordinatedPersistence;
        bool saved = await ExecutePersistenceAsync(
            PersistenceOperation.Save,
            () =>
                coordinated
                    ? _persistenceCoordinator.SaveGameAsync(SaveGameManager.QuickSaveFileName, null)
                    : SaveGameLocallyAsync(SaveGameManager.QuickSaveFileName, null)
        );
        if (saved && !coordinated)
            GameLogger.Log(
                $"Quick save completed: {_saveGameManager.GetSaveFilePath(SaveGameManager.QuickSaveFileName)}"
            );
        return saved;
    }

    /// <summary>
    /// Loads the quicksave through the current persistence owner.
    /// </summary>
    /// <returns>True only after the quickload has completed.</returns>
    public Task<bool> QuickLoadAsync()
    {
        return ExecutePersistenceAsync(
            PersistenceOperation.Load,
            () =>
                HasCoordinatedPersistence
                    ? _persistenceCoordinator.LoadGameAsync(SaveGameManager.QuickSaveFileName)
                    : Task.FromResult(QuickLoadLocally())
        );
    }

    /// <summary>
    /// Saves through the shared coordinator when bound, otherwise writes a local settled checkpoint.
    /// </summary>
    /// <param name="fileName">The save identifier without its extension.</param>
    /// <param name="displayName">The requested display name.</param>
    /// <returns>The persistence owner's completed save outcome.</returns>
    public Task<bool> SaveGameAsync(string fileName, string displayName)
    {
        return ExecutePersistenceAsync(
            PersistenceOperation.Save,
            () =>
                _persistenceCoordinator != null
                    ? _persistenceCoordinator.SaveGameAsync(fileName, displayName)
                    : SaveGameLocallyAsync(fileName, displayName)
        );
    }

    /// <summary>
    /// Loads through the shared coordinator when bound, otherwise restores the active local session.
    /// </summary>
    /// <param name="fileName">The save identifier without its extension.</param>
    /// <returns>The persistence owner's completed load outcome.</returns>
    public Task<bool> LoadGameAsync(string fileName)
    {
        return ExecutePersistenceAsync(
            PersistenceOperation.Load,
            () =>
                _persistenceCoordinator != null
                    ? _persistenceCoordinator.LoadGameAsync(fileName)
                    : Task.FromResult(LoadGameLocally(fileName))
        );
    }

    /// <summary>
    /// Reports storage failures without leaving async input callbacks with an unobserved exception.
    /// Departed sessions cannot publish a notice into their replacement.
    /// </summary>
    /// <param name="operation">The operation used for user-facing failure reporting.</param>
    /// <param name="execute">Starts the existing local or coordinated operation.</param>
    /// <returns>The operation's result, or false after a reported storage failure.</returns>
    private async Task<bool> ExecutePersistenceAsync(
        PersistenceOperation operation,
        Func<Task<bool>> execute
    )
    {
        GameSession requestedSession = _activeGameSession;
        try
        {
            return await execute();
        }
        catch (Exception exception)
            when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogException(exception);
            if (ReferenceEquals(_activeGameSession, requestedSession))
                PersistenceFailed?.Invoke(this, operation, exception);
            return false;
        }
    }

    /// <summary>
    /// Captures a permitted settled checkpoint and awaits its durable publication.
    /// </summary>
    /// <param name="fileName">The save identifier without its extension.</param>
    /// <param name="displayName">The requested display name.</param>
    /// <returns>True after the local checkpoint has been published.</returns>
    internal async Task<bool> SaveGameLocallyAsync(string fileName, string displayName)
    {
        if (!CanWriteLocalSave(fileName))
            return false;
        await _saveGameManager.SaveGameDataAsync(GetActiveGame(), fileName, displayName);
        return true;
    }

    /// <summary>
    /// Checks that a local publication can capture the active model.
    /// </summary>
    /// <param name="fileName">The requested extension-free save identifier.</param>
    /// <returns>True when the active model is at a permitted writable boundary.</returns>
    private bool CanWriteLocalSave(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        if (!CanSave)
        {
            LogUnsettledSaveWarning();
            return false;
        }

        return true;
    }

    /// <summary>
    /// Restores the local quicksave and reports the same completion diagnostics as the shortcut.
    /// </summary>
    /// <returns>True when the quicksave replaced the active local graph.</returns>
    private bool QuickLoadLocally()
    {
        bool loaded = LoadGameLocally(SaveGameManager.QuickSaveFileName);
        if (loaded)
            Debug.Log("Quick load completed.");
        else
            Debug.LogWarning("Quick load skipped because no quick save exists.");
        return loaded;
    }

    /// <summary>
    /// Forwards completed simulation ticks to the save manager's autosave scheduler.
    /// </summary>
    private void HandleTickCompleted()
    {
        if (!CanSave)
            return;

        UserGameplaySettings settings = _getGameplaySettings?.Invoke();
        if (_persistenceCoordinator != null)
            _persistenceCoordinator.ProcessAutosaveTick(settings);
        else
            ProcessAutosaveLocally(settings);
    }

    /// <summary>
    /// Runs the local autosave scheduler after the persistence owner has established the checkpoint.
    /// </summary>
    /// <param name="settings">The automatic-save cadence and retention settings.</param>
    /// <returns>True when the local automatic save was written.</returns>
    internal bool ProcessAutosaveLocally(UserGameplaySettings settings)
    {
        return CanSave && _saveGameManager.ProcessAutosaveTick(GetActiveGame(), settings);
    }

    /// <summary>
    /// Logs that a requested save cannot capture an unsettled simulation tick.
    /// </summary>
    private static void LogUnsettledSaveWarning()
    {
        GameLogger.Warning("Save skipped because the active game is not at a stable boundary.");
    }

    /// <summary>
    /// Restores a local save after the persistence owner has established the load boundary.
    /// </summary>
    /// <param name="fileName">The save identifier without its extension.</param>
    /// <returns>True when an existing save replaced the active local graph.</returns>
    internal bool LoadGameLocally(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;

        string savePath = _saveGameManager.GetSaveFilePath(fileName);
        if (!File.Exists(savePath))
            return false;

        if (!HasActiveGame)
            return false;

        ReplaceGame(_saveGameManager.LoadGameData(fileName));

        return true;
    }

    /// <summary>
    /// Replaces the active graph with prepared game state while preserving the
    /// scene's stable session and presentation subscriptions.
    /// </summary>
    /// <param name="game">The incoming game state, including its prepared player presentation.</param>
    internal void ReplaceGame(GameRoot game)
    {
        if (_activeGameSession == null)
            throw new InvalidOperationException("No active game can receive replacement state.");

        ValidateGameContent(game);
        ReconcileStaticContent(game);
        _activeGameSession.ReplaceGame(game);
        _gameManager.Reset();
        _operationsBlocked = false;
        GameReplaced?.Invoke(_activeGameSession.Game);
        ReconcileLoadedSession();
    }

    /// <summary>
    /// Verifies that a game was created by the active pack, version, and scenario.
    /// </summary>
    /// <param name="game">The game whose content identity is being validated.</param>
    internal void ValidateGameContent(GameRoot game)
    {
        if (game?.Summary == null)
            throw new InvalidOperationException("Game content identity is missing.");

        GameSummary summary = game.Summary;
        if (!_contentPack.MatchesContentIdentity(summary))
        {
            string requiredMods = FormatModIdentity(summary.ModIDs, summary.ModVersions);
            string activeMods = FormatModIdentity(
                _contentPack.FileResolver.Mods.Select(mod => mod.ID).ToArray(),
                _contentPack.FileResolver.Mods.Select(mod => mod.Version).ToArray()
            );
            throw new InvalidOperationException(
                $"Save requires content pack '{summary.PackID}' version '{summary.PackVersion}' "
                    + $"scenario '{summary.ScenarioID}' with mods [{requiredMods}], but "
                    + $"'{_contentPack.Definition.ID}' version '{_contentPack.Definition.Version}' "
                    + $"scenario '{_contentPack.Scenario.ID}' with mods [{activeMods}] is active."
            );
        }
    }

    /// <summary>
    /// Restores immutable authored state from the active content pack.
    /// </summary>
    /// <param name="game">The game whose authored state will be restored.</param>
    private void ReconcileStaticContent(GameRoot game)
    {
        ReconcileFactionSettings(game);
        ReconcileGalaxyLayout(game);
    }

    /// <summary>
    /// Restores faction configuration from saved launch rules or the active content pack.
    /// </summary>
    /// <param name="game">The game whose faction settings will be restored.</param>
    private void ReconcileFactionSettings(GameRoot game)
    {
        Dictionary<string, FactionSettings> overrides = GetFactionSettingOverrides(game);
        foreach (Faction faction in game.GetFactions())
        {
            if (faction == null || string.IsNullOrWhiteSpace(faction.InstanceID))
                throw new InvalidOperationException(
                    "Loaded game contains an unidentified faction."
                );

            Faction template = _contentPack.GameData.Factions.FirstOrDefault(candidate =>
                candidate?.InstanceID == faction.InstanceID
            );
            if (template == null)
                throw new InvalidOperationException(
                    $"Loaded faction '{faction.InstanceID}' is missing from the active content pack."
                );

            FactionSettings settings = overrides.TryGetValue(
                faction.InstanceID,
                out FactionSettings factionOverride
            )
                ? factionOverride
                : template.Settings;
            faction.Settings = settings.GetDeepCopy();
        }
    }

    /// <summary>
    /// Reads and validates launch-specific faction settings stored with the game.
    /// </summary>
    /// <param name="game">The game whose launch configuration will be read.</param>
    /// <returns>Faction settings indexed by faction instance ID.</returns>
    private Dictionary<string, FactionSettings> GetFactionSettingOverrides(GameRoot game)
    {
        Dictionary<string, FactionSettings> overrides = new Dictionary<string, FactionSettings>(
            StringComparer.Ordinal
        );
        GameGenerationConfig config = game.Summary.CustomSettings?.GenerationConfig;
        if (config == null)
            return overrides;

        foreach (
            FactionModifierConfig modifier in config.FactionModifiers
                ?? Enumerable.Empty<FactionModifierConfig>()
        )
        {
            if (modifier == null || string.IsNullOrWhiteSpace(modifier.FactionID))
                throw new InvalidOperationException(
                    "Game generation config contains an unidentified faction modifier."
                );
            if (modifier.Settings == null)
                throw new InvalidOperationException(
                    $"Faction modifier '{modifier.FactionID}' has no settings."
                );
            if (
                !_contentPack.GameData.Factions.Any(faction =>
                    faction?.InstanceID == modifier.FactionID
                )
            )
                throw new InvalidOperationException(
                    $"Faction modifier '{modifier.FactionID}' is missing from the active content pack."
                );
            if (overrides.ContainsKey(modifier.FactionID))
                throw new InvalidOperationException(
                    $"Game generation config contains duplicate faction modifier '{modifier.FactionID}'."
                );

            overrides.Add(modifier.FactionID, modifier.Settings);
        }

        return overrides;
    }

    /// <summary>
    /// Restores authored sector and planet coordinates from the active content pack.
    /// </summary>
    /// <param name="game">The game whose galaxy layout will be restored.</param>
    private void ReconcileGalaxyLayout(GameRoot game)
    {
        foreach (PlanetSector sector in game.GetGalaxyMap().GetChildren<PlanetSector>())
        {
            PlanetSector template = _contentPack.GameData.PlanetSectors.FirstOrDefault(candidate =>
                candidate?.TypeID == sector.TypeID
            );
            if (template == null)
                continue;

            sector.PositionX = template.PositionX;
            sector.PositionY = template.PositionY;
            foreach (Planet planet in sector.GetChildren<Planet>())
            {
                Planet planetTemplate = template
                    .GetChildren<Planet>()
                    .FirstOrDefault(candidate => candidate?.InstanceID == planet.InstanceID);
                if (planetTemplate == null)
                    continue;

                planet.PositionX = planetTemplate.PositionX;
                planet.PositionY = planetTemplate.PositionY;
            }
        }
    }

    /// <summary>
    /// Formats ordered mod identifiers and versions for a compatibility error.
    /// </summary>
    /// <param name="modIDs">The ordered mod identifiers.</param>
    /// <param name="modVersions">The versions corresponding to the identifiers.</param>
    /// <returns>A comma-separated ordered mod identity.</returns>
    private static string FormatModIdentity(string[] modIDs, string[] modVersions)
    {
        string[] ids = modIDs ?? Array.Empty<string>();
        string[] versions = modVersions ?? Array.Empty<string>();
        return string.Join(
            ", ",
            ids.Select((id, index) => $"{id}@{(index < versions.Length ? versions[index] : "?")}")
        );
    }
}
