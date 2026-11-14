using System;
using System.IO;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Gameplay;
using Rebellion.PlayerRequests;
using Rebellion.Util.Logging;
using UnityEngine;

/// <summary>
/// Owns the active simulation, clock, player request routing, and save/load lifecycle.
/// </summary>
public sealed class GameSessionManager
{
    private static readonly Task<bool> _noAutosave = Task.FromResult(false);
    private readonly ContentPack _contentPack;
    private readonly GameContentBinding _contentBinding;
    private readonly SaveGameManager _saveGameManager;
    private readonly Func<UserGameplaySettings> _getGameplaySettings;
    private readonly IAnalyticsEventSink _analyticsSink;
    private readonly Func<int> _getFrameCount;
    private GameSession _activeGameSession;
    private GameManager _gameManager;
    private CampaignAnalyticsTracker _campaignAnalyticsTracker;
    private bool _operationsBlocked;
    private SessionPlayerRequestDispatcher _playerRequests;
    private IGameSessionOwner _sessionOwner;

    // A due local autosave waits for a later frame, so its indicator is drawn before the capture.
    private UserGameplaySettings _pendingAutosaveSettings;
    private int _pendingAutosaveFrame;
    private int _autosavePublications;
    private int _autosaveGeneration;

    /// <summary>
    /// Gets whether the active session has an owner coordinating execution and saves.
    /// </summary>
    public bool HasCoordinatedSession => _sessionOwner != null;

    /// <summary>
    /// Gets whether the active session is waiting to capture, or still writing, a scheduled autosave.
    /// </summary>
    public bool IsAutosaving =>
        _sessionOwner != null
            ? _sessionOwner.IsAutosaving
            : _pendingAutosaveSettings != null || _autosavePublications > 0;

    /// <summary>
    /// Raised when a scheduled autosave starts waiting for its capture or finishes writing.
    /// </summary>
    public event Action AutosaveStateChanged;

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
    public event Action<GameSessionManager, SaveGameOperation, Exception> PersistenceFailed;

    /// <summary>
    /// Gets whether a game session is currently active.
    /// </summary>
    public bool HasActiveGame => _activeGameSession != null;

    /// <summary>
    /// Gets whether the persistence owner can accept a request without requiring an immediate snapshot.
    /// </summary>
    public bool CanRequestSave =>
        _sessionOwner != null ? !_operationsBlocked && _sessionOwner.CanRequestSave : CanSave;

    /// <summary>
    /// Gets whether a complete settled snapshot can be captured immediately.
    /// </summary>
    public bool CanSave =>
        !_operationsBlocked && _activeGameSession?.CanCaptureSettledState == true;

    /// <summary>
    /// Creates a session manager backed by the active content pack.
    /// </summary>
    /// <param name="contentPack">The active content pack.</param>
    /// <param name="saveGameManager">The save manager, or null to use the application singleton.</param>
    /// <param name="getGameplaySettings">Returns the current gameplay settings.</param>
    /// <param name="analyticsSink">Receives consented campaign analytics.</param>
    /// <param name="getFrameCount">Returns the presented frame number, or null to use Unity's frame count.</param>
    internal GameSessionManager(
        ContentPack contentPack,
        SaveGameManager saveGameManager = null,
        Func<UserGameplaySettings> getGameplaySettings = null,
        IAnalyticsEventSink analyticsSink = null,
        Func<int> getFrameCount = null
    )
    {
        _contentPack = contentPack ?? throw new ArgumentNullException(nameof(contentPack));
        _contentBinding = new GameContentBinding(_contentPack);
        _saveGameManager = saveGameManager ?? SaveGameManager.Instance;
        _getGameplaySettings = getGameplaySettings;
        _analyticsSink = analyticsSink;
        _getFrameCount = getFrameCount ?? (() => Time.frameCount);
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
        GameSession session = ReplaceSession(game, owner);
        StartCampaignAnalytics(session, true);
        return session;
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
        StartCampaignAnalytics(session, false);
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
        _contentBinding.Prepare(game);
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
        ResetAutosaveState();
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
            owner?.BindSessionManager(this);
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
    /// Admits the next tick through this session's existing execution owner. The clock holds
    /// while a scheduled local autosave waits for its capture.
    /// </summary>
    /// <param name="elapsedSeconds">The elapsed application frame interval.</param>
    /// <returns>True when the current session should begin its next tick.</returns>
    public bool TryBeginGameTick(float elapsedSeconds)
    {
        return HasActiveGame
            && _pendingAutosaveSettings == null
            && (
                _sessionOwner != null
                    ? _sessionOwner.TryBeginGameTick(elapsedSeconds)
                    : _gameManager.TryAdvanceTickTimer(elapsedSeconds)
            );
    }

    /// <summary>
    /// Waits for the active campaign's execution owner before presentation releases its terminal session.
    /// </summary>
    /// <returns>True when the same settled session is ready to end.</returns>
    public async Task<bool> WaitForCampaignCompletionAsync()
    {
        GameSession session = _activeGameSession;
        if (session?.CanCaptureSettledState != true || !session.CanExecuteCommands)
            return false;
        IGameSessionOwner owner = _sessionOwner;
        bool completed = owner == null || await owner.WaitForCampaignCompletionAsync();
        return completed && ReferenceEquals(_activeGameSession, session);
    }

    /// <summary>
    /// End the current game session.
    /// Stops game logic and clears the active session.
    /// </summary>
    public void EndGame()
    {
        if (_activeGameSession == null)
            return;

        GameSession outgoingSession = _activeGameSession;
        GameManager outgoingClock = _gameManager;
        try
        {
            _sessionOwner?.ReleaseSessionManager(this);
        }
        finally
        {
            StopCampaignAnalytics();
            _sessionOwner = null;
            ResetAutosaveState();
            outgoingSession.Tick.TickCompleted -= HandleTickCompleted;
            _activeGameSession = null;
            _gameManager = null;
            _playerRequests = null;
            try
            {
                outgoingClock.ApplyGameSpeed(TickSpeed.Paused);
            }
            finally
            {
                outgoingSession.Dispose();
            }
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
    /// Saves the quicksave through the current persistence owner.
    /// </summary>
    /// <returns>True only after the quicksave has completed.</returns>
    public async Task<bool> QuickSaveAsync()
    {
        bool coordinated = HasCoordinatedSession;
        bool saved = await ExecutePersistenceAsync(
            SaveGameOperation.Save,
            () =>
                coordinated
                    ? _sessionOwner.SaveGameAsync(SaveGameManager.QuickSaveFileName, null)
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
            SaveGameOperation.Load,
            () =>
                HasCoordinatedSession
                    ? _sessionOwner.LoadGameAsync(SaveGameManager.QuickSaveFileName)
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
            SaveGameOperation.Save,
            () =>
                _sessionOwner != null
                    ? _sessionOwner.SaveGameAsync(fileName, displayName)
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
            SaveGameOperation.Load,
            () =>
                _sessionOwner != null
                    ? _sessionOwner.LoadGameAsync(fileName)
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
        SaveGameOperation operation,
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
    /// Forwards completed simulation ticks to the session owner's autosave policy, or schedules a
    /// due local autosave.
    /// </summary>
    private void HandleTickCompleted()
    {
        if (!CanSave)
            return;

        UserGameplaySettings settings = _getGameplaySettings?.Invoke();
        if (_sessionOwner != null)
        {
            bool wasAutosaving = _sessionOwner.IsAutosaving;
            _sessionOwner.ProcessAutosaveTick(settings);
            if (_sessionOwner.IsAutosaving != wasAutosaving)
                AutosaveStateChanged?.Invoke();
        }
        else
            ScheduleLocalAutosave(settings);
    }

    /// <summary>
    /// Holds a due local autosave until a later frame, so presentation can show its indicator
    /// before the capture blocks the main thread.
    /// </summary>
    /// <param name="settings">The automatic-save cadence and retention settings.</param>
    private void ScheduleLocalAutosave(UserGameplaySettings settings)
    {
        if (!SaveGameManager.IsAutosaveDue(GetActiveGame(), settings))
            return;

        _pendingAutosaveSettings = new UserGameplaySettings
        {
            AutosaveEnabled = settings.AutosaveEnabled,
            AutosaveIntervalTicks = settings.AutosaveIntervalTicks,
            AutosavesToKeep = settings.AutosavesToKeep,
        };
        _pendingAutosaveFrame = _getFrameCount();
        AutosaveStateChanged?.Invoke();
    }

    /// <summary>
    /// Captures a scheduled local autosave once a frame has been presented since it was scheduled,
    /// then tracks its background publication until the file is written.
    /// </summary>
    /// <returns>True after the autosave was written; false when none was ready or it failed.</returns>
    public Task<bool> ProcessPendingAutosaveAsync()
    {
        UserGameplaySettings settings = _pendingAutosaveSettings;
        if (settings == null || _getFrameCount() <= _pendingAutosaveFrame)
            return _noAutosave;

        _pendingAutosaveSettings = null;
        if (!CanSave)
        {
            LogUnsettledSaveWarning();
            AutosaveStateChanged?.Invoke();
            return _noAutosave;
        }

        _autosavePublications++;
        return CompleteAutosaveAsync(
            _saveGameManager.ProcessAutosaveTickAsync(GetActiveGame(), settings),
            _autosaveGeneration
        );
    }

    /// <summary>
    /// Clears the autosave state when a publication belonging to the current game finishes.
    /// </summary>
    /// <param name="publication">The captured autosave's durable publication.</param>
    /// <param name="generation">The game generation that captured the autosave.</param>
    /// <returns>The publication's outcome.</returns>
    private async Task<bool> CompleteAutosaveAsync(Task<bool> publication, int generation)
    {
        try
        {
            return await publication;
        }
        finally
        {
            if (generation == _autosaveGeneration)
            {
                _autosavePublications--;
                AutosaveStateChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// Forgets scheduled and in-flight autosave state that belongs to an outgoing game.
    /// </summary>
    private void ResetAutosaveState()
    {
        _pendingAutosaveSettings = null;
        _autosavePublications = 0;
        _autosaveGeneration++;
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

        _contentBinding.Prepare(game);
        bool pendingMultiplayerStart =
            _campaignAnalyticsTracker?.HasPendingMultiplayerStart == true
            && _activeGameSession.Game.Metadata.CampaignID == game.Metadata?.CampaignID;
        _activeGameSession.ReplaceGame(game);
        StopCampaignAnalytics();
        _gameManager.Reset();
        _operationsBlocked = false;
        ResetAutosaveState();
        GameReplaced?.Invoke(_activeGameSession.Game);
        ReconcileLoadedSession();
        StartCampaignAnalytics(_activeGameSession, pendingMultiplayerStart);
    }

    /// <summary>
    /// Connects campaign analytics to one active game session when the build supports collection.
    /// </summary>
    /// <param name="session">The active game session.</param>
    /// <param name="newCampaign">Whether the session created this campaign.</param>
    private void StartCampaignAnalytics(GameSession session, bool newCampaign)
    {
        if (_analyticsSink?.IsAvailable != true || session == null)
            return;

        try
        {
            _campaignAnalyticsTracker = new CampaignAnalyticsTracker(
                session.Game,
                _analyticsSink,
                newCampaign
            );
            session.Tick.TickCompleted += _campaignAnalyticsTracker.RecordTickCompleted;
            session.Pipeline.VictoryDeclared += _campaignAnalyticsTracker.RecordVictory;
        }
        catch (Exception exception)
        {
            _campaignAnalyticsTracker = null;
            Debug.LogWarning($"Campaign analytics could not start: {exception.Message}");
        }
    }

    /// <summary>
    /// Records multiplayer creation only when the coordinator has admitted synchronized gameplay.
    /// </summary>
    internal void RecordMultiplayerCampaignStarted()
    {
        _campaignAnalyticsTracker?.RecordMultiplayerStarted();
    }

    /// <summary>
    /// Disconnects the active campaign analytics tracker.
    /// </summary>
    private void StopCampaignAnalytics()
    {
        CampaignAnalyticsTracker tracker = _campaignAnalyticsTracker;
        if (tracker == null)
            return;

        if (_activeGameSession != null)
        {
            _activeGameSession.Tick.TickCompleted -= tracker.RecordTickCompleted;
            _activeGameSession.Pipeline.VictoryDeclared -= tracker.RecordVictory;
        }
        _campaignAnalyticsTracker = null;
    }
}
