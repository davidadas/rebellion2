using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;
using Rebellion.Util.Common;

/// <summary>
/// Coordinates all game systems each tick and routes results through domain reactions and observers.
/// </summary>
public sealed class GameManager
{
    // Game State.
    private GameRoot _game;
    private IRandomNumberProvider _randomProvider;

    // Messaging and Events.
    private MessageSystem _messageSystem;
    private GameEventSystem _eventSystem;

    // Galaxy Systems.
    private FogOfWarSystem _fogOfWarSystem;
    private BlockadeSystem _blockadeSystem;

    // Unit Systems.
    private FleetSystem _fleetSystem;
    private PersonnelSystem _personnelSystem;
    private MovementSystem _movementSystem;

    // Economy Systems.
    private ManufacturingSystem _manufacturingSystem;
    private MaintenanceSystem _maintenanceSystem;
    private ResourceProductionSystem _resourceProductionSystem;

    // Planetary Systems.
    private PlanetaryControlSystem _planetaryControlSystem;
    private UprisingSystem _uprisingSystem;

    // Mission Systems.
    private JediSystem _jediSystem;
    private MissionSystem _missionSystem;

    // Combat Systems.
    private SpaceCombatSystem _spaceCombatSystem;
    private BombardmentSystem _bombardmentSystem;
    private PlanetaryAssaultSystem _planetaryAssaultSystem;

    // Strategic Systems.
    private ResearchSystem _researchSystem;
    private BetrayalSystem _betrayalSystem;
    private VictorySystem _victorySystem;
    private AISystem _aiSystem;

    // Result Processing.
    private GameResultProcessor _resultProcessor;
    private readonly List<GameResult> _deferredMessageResults = new List<GameResult>();

    // Tick State.
    private float? _tickInterval;
    private float _tickTimer;

    // Game Events.
    public event Action GameSpeedChanged;
    public event Action TickCompleted;
    public event Action<GameRoot> GameReplaced;

    // Exposed Game Systems.
    internal MessageSystem MessageSystem => _messageSystem;

    internal FleetSystem FleetSystem => _fleetSystem;

    internal PersonnelSystem PersonnelSystem => _personnelSystem;

    internal MovementSystem MovementSystem => _movementSystem;

    internal ManufacturingSystem ManufacturingSystem => _manufacturingSystem;

    internal MaintenanceSystem MaintenanceSystem => _maintenanceSystem;

    internal MissionSystem MissionSystem => _missionSystem;

    internal SpaceCombatSystem SpaceCombatSystem => _spaceCombatSystem;

    internal BombardmentSystem BombardmentSystem => _bombardmentSystem;

    internal PlanetaryAssaultSystem PlanetaryAssaultSystem => _planetaryAssaultSystem;

    /// <summary>
    /// Creates a new GameManager for the given game instance.
    /// </summary>
    /// <param name="game">The game instance to manage.</param>
    public GameManager(GameRoot game)
    {
        InitializeGame(game);
    }

    /// <summary>
    /// Replaces the current game instance and reinitializes all systems.
    /// </summary>
    /// <param name="game">The replacement game instance.</param>
    public void ReplaceGame(GameRoot game)
    {
        InitializeGame(game);
        GameReplaced?.Invoke(_game);
    }

    /// <summary>
    /// Returns the current game instance.
    /// </summary>
    /// <returns>The active GameRoot.</returns>
    public GameRoot GetGame() => _game;

    /// <summary>
    /// Returns the current tick count.
    /// </summary>
    /// <returns>The current tick number.</returns>
    public int GetCurrentTick() => _game.CurrentTick;

    /// <summary>
    /// Returns the player-controlled faction.
    /// </summary>
    /// <returns>The faction whose PlayerID is set.</returns>
    public Faction GetPlayerFaction() => _game.GetPlayerFaction();

    /// <summary>
    /// Returns the fog of war system for building faction-specific galaxy views.
    /// </summary>
    /// <returns>The active FogOfWarSystem instance.</returns>
    public FogOfWarSystem GetFogOfWarSystem() => _fogOfWarSystem;

    /// <summary>
    /// Returns the active game speed.
    /// </summary>
    /// <returns>The active game speed.</returns>
    public TickSpeed GetGameSpeed() => _game.GetGameSpeed();

    /// <summary>
    /// Sets the game speed and adjusts the tick interval accordingly.
    /// </summary>
    /// <param name="speed">The desired tick speed.</param>
    public void SetGameSpeed(TickSpeed speed)
    {
        TickSpeed previousSpeed = _game.GetGameSpeed();
        _game.SetGameSpeed(speed);

        switch (speed)
        {
            case TickSpeed.Fast:
                _tickInterval = _game.Config.GameSpeed.FastTickIntervalSeconds;
                break;
            case TickSpeed.Medium:
                _tickInterval = _game.Config.GameSpeed.MediumTickIntervalSeconds;
                break;
            case TickSpeed.Slow:
                _tickInterval = _game.Config.GameSpeed.SlowTickIntervalSeconds;
                break;
            case TickSpeed.VerySlow:
                _tickInterval = _game.Config.GameSpeed.VerySlowTickIntervalSeconds;
                break;
            case TickSpeed.Paused:
                _tickInterval = null;
                break;
        }

        if (previousSpeed != speed)
            GameSpeedChanged?.Invoke();
    }

    /// <summary>
    /// Advances the tick timer by elapsed game-loop time and fires a tick when the interval is reached.
    /// No-ops while combat is pending player resolution or the game is paused.
    /// </summary>
    /// <param name="elapsedSeconds">The elapsed game-loop time in seconds.</param>
    public void AdvanceTime(float elapsedSeconds)
    {
        if (elapsedSeconds <= 0f || _spaceCombatSystem.HasPendingDecision || _tickInterval == null)
            return;

        _tickTimer += elapsedSeconds;

        if (_tickTimer >= _tickInterval)
        {
            _tickTimer = 0f;
            ProcessTick();
        }
    }

    /// <summary>
    /// Runs one game tick.
    /// </summary>
    public void ProcessTick()
    {
        if (_spaceCombatSystem.HasPendingDecision || _game.GetGameSpeed() == TickSpeed.Paused)
            return;

        _game.CurrentTick++;
        _messageSystem.ProcessTick();
        GameLogger.Debug("Tick: " + _game.CurrentTick);

        ProcessResults(_resourceProductionSystem.ProcessTick());
        ProcessResults(_manufacturingSystem.ProcessTick());
        ProcessResults(_maintenanceSystem.ProcessTick());

        List<GameResult> movementResults = ProcessResults(
            _movementSystem.ProcessTick(),
            processMessages: false
        );

        List<GameResult> combatResults = ProcessResults(
            _spaceCombatSystem.ProcessTick(),
            processMessages: false
        );

        List<GameResult> messageResults = CombineResults(movementResults, combatResults);
        if (_spaceCombatSystem.HasPendingDecision)
        {
            StoreDeferredMessageResults(messageResults);
            TickCompleted?.Invoke();
            return;
        }

        _messageSystem.ProcessResults(messageResults);

        ProcessResults(_missionSystem.ProcessTick());
        ProcessResults(_eventSystem.ProcessEvents(_game.GetEventPool()));
        ProcessResults(_aiSystem.ProcessTick());

        ProcessResults(_blockadeSystem.ProcessTick());
        ProcessResults(_planetaryControlSystem.ProcessTick());
        ProcessResults(_uprisingSystem.ProcessTick());
        ProcessResults(_betrayalSystem.ProcessTick());

        ProcessResults(_researchSystem.ProcessTick());
        ProcessResults(_jediSystem.ProcessTick());
        ProcessResults(_victorySystem.ProcessTick());
        TickCompleted?.Invoke();
    }

    /// <summary>
    /// Resolves the pending combat encounter and resumes ticking.
    /// </summary>
    /// <param name="autoResolve">Whether to auto-resolve instead of tactical combat.</param>
    /// <returns>The space combat result generated by the encounter, when present.</returns>
    public SpaceCombatResult ResolveCombat(bool autoResolve)
    {
        List<GameResult> combatResults = _spaceCombatSystem.ResolvePending(autoResolve);
        return CompleteCombatResolution(combatResults);
    }

    /// <summary>
    /// Resolves a pending retreat and routes its results before resuming ticks.
    /// </summary>
    /// <param name="retreatingFactionInstanceId">The faction withdrawing from combat.</param>
    /// <returns>The resulting space-combat summary, or null when retreat is unavailable.</returns>
    public SpaceCombatResult ResolveCombatRetreat(string retreatingFactionInstanceId)
    {
        List<GameResult> combatResults = _spaceCombatSystem.ResolvePendingRetreat(
            retreatingFactionInstanceId
        );
        if (combatResults == null)
            return null;

        return CompleteCombatResolution(combatResults);
    }

    /// <summary>
    /// Initializes a game and rebuilds its runtime systems and derived state.
    /// </summary>
    /// <param name="game">The game instance to initialize.</param>
    private void InitializeGame(GameRoot game)
    {
        if (game == null)
            throw new InvalidOperationException("Cannot manage a null game.");

        _game = game;
        if (_game.Config == null)
            _game.SetConfig(ResourceManager.GetConfig<GameConfig>());

        _randomProvider = _game.Random;
        InitializeSystems();
        RebuildDerivedState();
        _tickTimer = 0f;
        SetGameSpeed(_game.GetGameSpeed());
    }

    /// <summary>
    /// Initializes all systems in dependency order.
    /// </summary>
    private void InitializeSystems()
    {
        _messageSystem = new MessageSystem(
            _game,
            ResourceManager.GetEntityData<MessageDefinition>()
        );
        _eventSystem = new GameEventSystem(_game, _randomProvider);
        _fogOfWarSystem = new FogOfWarSystem(_game);
        _blockadeSystem = new BlockadeSystem(_game, _randomProvider);
        _fleetSystem = new FleetSystem(_game);
        _personnelSystem = new PersonnelSystem(_game);
        _movementSystem = new MovementSystem(_game, _fogOfWarSystem, _fleetSystem, _blockadeSystem);
        _manufacturingSystem = new ManufacturingSystem(_game, _fleetSystem, _movementSystem);
        _maintenanceSystem = new MaintenanceSystem(_game, _randomProvider, _fleetSystem);
        _resourceProductionSystem = new ResourceProductionSystem(_game);
        _planetaryControlSystem = new PlanetaryControlSystem(
            _game,
            _movementSystem,
            _manufacturingSystem,
            _fogOfWarSystem
        );
        _uprisingSystem = new UprisingSystem(_game, _randomProvider, _planetaryControlSystem);
        _jediSystem = new JediSystem(_game, _randomProvider);
        _missionSystem = new MissionSystem(
            _game,
            _randomProvider,
            _movementSystem,
            _uprisingSystem
        );
        _spaceCombatSystem = new SpaceCombatSystem(_game, _randomProvider, _movementSystem);
        _bombardmentSystem = new BombardmentSystem(
            _game,
            _randomProvider,
            _movementSystem,
            _planetaryControlSystem
        );
        _planetaryAssaultSystem = new PlanetaryAssaultSystem(
            _game,
            _randomProvider,
            _planetaryControlSystem
        );
        _researchSystem = new ResearchSystem(_game, _randomProvider);
        _betrayalSystem = new BetrayalSystem(_game);
        _victorySystem = new VictorySystem(_game);
        _aiSystem = new AISystem(
            _game,
            _missionSystem,
            _movementSystem,
            _manufacturingSystem,
            _bombardmentSystem,
            _planetaryAssaultSystem,
            _randomProvider
        );

        InitializeResultProcessing();
    }

    /// <summary>
    /// Connects result producers, typed reactions, and observers.
    /// </summary>
    private void InitializeResultProcessing()
    {
        _resultProcessor = new GameResultProcessor();
        _resultProcessor.Subscribe<PlanetGarrisonChangedResult>(_planetaryControlSystem);
        _resultProcessor.Subscribe<PlanetGarrisonChangedResult>(_uprisingSystem);
        _resultProcessor.Subscribe<PlanetUprisingStartedResult>(_missionSystem);
        _resultProcessor.Subscribe<MissionCompletedResult>(_jediSystem);
        _resultProcessor.Observe<GameObjectSabotagedResult>(_fogOfWarSystem.ProcessResults);

        _movementSystem.ResultsProduced += HandleSystemResultsProduced;
        _maintenanceSystem.ResultsProduced += HandleSystemResultsProduced;
        _bombardmentSystem.ResultsProduced += HandleSystemResultsProduced;
        _planetaryAssaultSystem.ResultsProduced += HandleSystemResultsProduced;
    }

    /// <summary>
    /// Rebuilds derived state that is not persisted.
    /// </summary>
    private void RebuildDerivedState()
    {
        IManufacturable[] templates = ResourceManager
            .GetEntityData<Building>()
            .Cast<IManufacturable>()
            .Concat(ResourceManager.GetEntityData<CapitalShip>())
            .Concat(ResourceManager.GetEntityData<Starfighter>())
            .Concat(ResourceManager.GetEntityData<Regiment>())
            .Concat(ResourceManager.GetEntityData<SpecialForces>())
            .ToArray();

        foreach (Faction faction in _game.GetFactions())
            faction.RebuildResearchCatalog(templates);

        _manufacturingSystem.RebuildQueues();
    }

    /// <summary>
    /// Routes resolved combat results and restores the tick timer.
    /// </summary>
    /// <param name="combatResults">The results produced by combat resolution.</param>
    /// <returns>The space-combat result in the routed batch, when present.</returns>
    private SpaceCombatResult CompleteCombatResolution(List<GameResult> combatResults)
    {
        combatResults = ProcessResults(combatResults, processMessages: false);

        List<GameResult> messageResults = TakeDeferredMessageResults();
        messageResults.AddRange(combatResults);
        _messageSystem.ProcessResults(messageResults);
        _tickTimer = 0f;

        return combatResults.OfType<SpaceCombatResult>().FirstOrDefault();
    }

    /// <summary>
    /// Resolves domain reactions and then presents the completed result batch to observers.
    /// Per-result logging is the responsibility of the system that produced the result.
    /// </summary>
    /// <param name="results">Batch of results from a system tick.</param>
    /// <param name="processMessages">Whether to create faction messages for this batch.</param>
    /// <returns>The initial results followed by every result produced by their reactions.</returns>
    private List<GameResult> ProcessResults(
        IEnumerable<GameResult> results,
        bool processMessages = true
    )
    {
        List<GameResult> resolvedResults = _resultProcessor.Process(results);
        if (processMessages)
            _messageSystem.ProcessResults(resolvedResults);

        return resolvedResults;
    }

    /// <summary>
    /// Routes results emitted by an immediate system command.
    /// </summary>
    /// <param name="results">The results emitted by the system.</param>
    private void HandleSystemResultsProduced(IReadOnlyList<GameResult> results)
    {
        ProcessResults(results);
    }

    /// <summary>
    /// Stores movement and combat results until the pending combat decision is resolved.
    /// </summary>
    /// <param name="results">The results whose messages must wait for combat resolution.</param>
    private void StoreDeferredMessageResults(List<GameResult> results)
    {
        _deferredMessageResults.Clear();
        if (results != null)
            _deferredMessageResults.AddRange(results);
    }

    /// <summary>
    /// Returns and clears movement and combat results waiting on a combat decision.
    /// </summary>
    /// <returns>The pending message result batch.</returns>
    private List<GameResult> TakeDeferredMessageResults()
    {
        List<GameResult> results = new List<GameResult>(_deferredMessageResults);
        _deferredMessageResults.Clear();
        return results;
    }

    /// <summary>
    /// Combines result batches while preserving their original order.
    /// </summary>
    /// <param name="resultBatches">The result batches to combine.</param>
    /// <returns>A single ordered result list.</returns>
    private static List<GameResult> CombineResults(params List<GameResult>[] resultBatches)
    {
        List<GameResult> results = new List<GameResult>();
        foreach (List<GameResult> resultBatch in resultBatches)
        {
            if (resultBatch != null)
                results.AddRange(resultBatch);
        }

        return results;
    }
}
