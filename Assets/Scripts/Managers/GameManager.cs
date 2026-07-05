using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

/// <summary>
/// Coordinates all game systems each tick and routes results to cross-cutting handlers.
/// Owned by GameRuntime — do not create directly.
/// </summary>
public class GameManager
{
    private GameRoot _game;
    private AISystem _aiSystem;
    private GameEventSystem _eventManager;
    private MissionSystem _missionManager;
    private MovementSystem _movementManager;
    private ManufacturingSystem _manufacturingManager;
    private MaintenanceSystem _maintenanceManager;
    private ResourceProductionSystem _resourceProductionManager;
    private CombatSystem _combatManager;
    private FogOfWarSystem _fogOfWarManager;
    private BlockadeSystem _blockadeManager;
    private ResearchSystem _researchManager;
    private JediSystem _jediSystem;
    private BetrayalSystem _betrayalManager;
    private PlanetaryControlSystem _planetaryControlSystem;
    private UprisingSystem _uprisingManager;
    private VictorySystem _victoryManager;
    private MessageFactory _messageFactory;
    private IRandomNumberProvider _randomProvider;
    private CombatDecisionContext _pendingCombatDecision;
    private float? _tickInterval;
    private float _tickTimer;
    private readonly Stopwatch _stopwatch;

    /// <summary>
    /// Raised when the active game speed changes.
    /// </summary>
    public event Action GameSpeedChanged;

    /// <summary>
    /// Creates a new GameManager for the given game instance.
    /// </summary>
    /// <param name="game">The game instance to manage.</param>
    public GameManager(GameRoot game)
    {
        _stopwatch = new Stopwatch();

        SetGame(game);
        InitializeSystems();
        RebuildDerivedState();
        SetGameSpeed(_game.GetGameSpeed());
    }

    /// <summary>
    /// Replaces the current game instance and reinitializes all systems.
    /// </summary>
    /// <param name="newGame">The replacement game instance.</param>
    public void ReplaceGame(GameRoot newGame)
    {
        SetGame(newGame);
        InitializeSystems();
        RebuildDerivedState();
        _tickTimer = 0f;
        _stopwatch.Restart();
        SetGameSpeed(_game.GetGameSpeed());
    }

    /// <summary>
    /// Sets the active game and ensures required runtime state exists.
    /// </summary>
    /// <param name="game">The game instance to make active.</param>
    private void SetGame(GameRoot game)
    {
        if (game == null)
            throw new InvalidOperationException("Cannot manage a null game.");

        _game = game;

        if (_game.Config == null)
            _game.SetConfig(ResourceManager.GetConfig<GameConfig>());

        _randomProvider = _game.Random;
    }

    /// <summary>
    /// Initializes all systems in dependency order.
    /// </summary>
    private void InitializeSystems()
    {
        _messageFactory = new MessageFactory(ResourceManager.GetEntityData<MessageDefinition>());
        _eventManager = new GameEventSystem(_game, _randomProvider);
        _fogOfWarManager = new FogOfWarSystem(_game);
        _blockadeManager = new BlockadeSystem(_game, _randomProvider);
        _movementManager = new MovementSystem(_game, _fogOfWarManager, _blockadeManager);
        _manufacturingManager = new ManufacturingSystem(_game, _randomProvider, _movementManager);
        _maintenanceManager = new MaintenanceSystem(_game, _randomProvider);
        _resourceProductionManager = new ResourceProductionSystem(_game);
        _planetaryControlSystem = new PlanetaryControlSystem(
            _game,
            _movementManager,
            _manufacturingManager,
            _fogOfWarManager
        );
        _jediSystem = new JediSystem(_game, _randomProvider);
        _missionManager = new MissionSystem(_game, _randomProvider, _movementManager);
        _combatManager = new CombatSystem(
            _game,
            _randomProvider,
            _movementManager,
            _planetaryControlSystem
        );
        _researchManager = new ResearchSystem(_game, _randomProvider);
        _betrayalManager = new BetrayalSystem(_game);
        _uprisingManager = new UprisingSystem(_game, _randomProvider, _planetaryControlSystem);
        _victoryManager = new VictorySystem(_game);
        _aiSystem = new AISystem(
            _game,
            _missionManager,
            _movementManager,
            _manufacturingManager,
            _combatManager,
            _randomProvider
        );
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

        _manufacturingManager.RebuildQueues();
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
    public FogOfWarSystem GetFogOfWarSystem() => _fogOfWarManager;

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
                _stopwatch.Stop();
                _tickInterval = null;
                break;
        }

        if (_tickInterval != null)
            _stopwatch.Start();

        if (previousSpeed != speed)
            GameSpeedChanged?.Invoke();
    }

    /// <summary>
    /// Returns the active game speed.
    /// </summary>
    /// <returns>The active game speed.</returns>
    public TickSpeed GetGameSpeed()
    {
        return _game.GetGameSpeed();
    }

    /// <summary>
    /// Advances the tick timer and fires a tick when the interval is reached.
    /// No-ops while combat is pending player resolution or the game is paused.
    /// </summary>
    public void Update()
    {
        if (_pendingCombatDecision != null || _tickInterval == null)
            return;

        float deltaTime = (float)_stopwatch.Elapsed.TotalSeconds;
        _stopwatch.Restart();
        _tickTimer += deltaTime;

        if (_tickTimer >= _tickInterval)
        {
            _tickTimer = 0f;
            ProcessTick();
        }
    }

    /// <summary>
    /// Resolves the pending combat encounter and resumes ticking.
    /// </summary>
    /// <param name="autoResolve">Whether to auto-resolve instead of tactical combat.</param>
    public void ResolveCombat(bool autoResolve)
    {
        if (_pendingCombatDecision == null)
            throw new InvalidOperationException("No pending combat to resolve.");

        ProcessResults(_combatManager.Resolve(_pendingCombatDecision, autoResolve));
        _pendingCombatDecision = null;
    }

    /// <summary>
    /// Runs one game tick.
    /// </summary>
    public void ProcessTick()
    {
        _game.CurrentTick++;
        GameLogger.Debug("Tick: " + _game.CurrentTick);

        ProcessResults(_resourceProductionManager.ProcessTick());
        ProcessResults(_manufacturingManager.ProcessTick());
        ProcessResults(_maintenanceManager.ProcessTick());

        ProcessResults(_movementManager.ProcessTick());
        ProcessResults(_combatManager.ProcessTick());
        if (_pendingCombatDecision != null)
            return;

        ProcessResults(_missionManager.ProcessTick());
        ProcessResults(_eventManager.ProcessEvents(_game.GetEventPool()));
        ProcessResults(_aiSystem.ProcessTick());

        ProcessResults(_blockadeManager.ProcessTick());
        ProcessResults(_planetaryControlSystem.ProcessTick());
        ProcessResults(_uprisingManager.ProcessTick());
        ProcessResults(_betrayalManager.ProcessTick());

        ProcessResults(_researchManager.ProcessTick());
        ProcessResults(_jediSystem.ProcessTick());
        ProcessResults(_victoryManager.ProcessTick());
    }

    /// <summary>
    /// Handles cross-cutting side effects for a batch of game results.
    /// Per-result logging is the responsibility of the system that produced the result.
    /// </summary>
    /// <param name="results">Batch of results from a system tick.</param>
    private void ProcessResults(List<GameResult> results)
    {
        ProcessFogOfWarResults(results);
        ProcessMessageDeliveries(results);

        foreach (VictoryResult result in results.OfType<VictoryResult>())
        {
            // TODO: Set game over flag, trigger victory screen.
        }

        foreach (PendingCombatResult result in results.OfType<PendingCombatResult>())
        {
            _pendingCombatDecision = new CombatDecisionContext
            {
                AttackerFleetInstanceID = result.AttackerFleet?.GetInstanceID(),
                DefenderFleetInstanceID = result.DefenderFleet?.GetInstanceID(),
            };
        }

        foreach (MissionCompletedResult result in results.OfType<MissionCompletedResult>())
        {
            if (result.Outcome == MissionOutcome.Success)
                ProcessResults(_jediSystem.ApplyForceGrowth(result.Mission.MainParticipants));
        }
    }

    /// <summary>
    /// Applies fog-of-war side effects for a result batch.
    /// </summary>
    /// <param name="results">The game results to process.</param>
    private void ProcessFogOfWarResults(List<GameResult> results)
    {
        foreach (GameObjectSabotagedResult result in results.OfType<GameObjectSabotagedResult>())
            RemoveSabotagedObjectFromActorSnapshot(result);
    }

    /// <summary>
    /// Removes a sabotaged object from the actor faction's fog-of-war snapshots.
    /// </summary>
    /// <param name="result">The sabotage result to process.</param>
    private void RemoveSabotagedObjectFromActorSnapshot(GameObjectSabotagedResult result)
    {
        if (result?.SabotagedObject == null || result.Saboteur is not ISceneNode saboteur)
            return;

        Faction faction = _game
            .GetFactions()
            .FirstOrDefault(f => f.InstanceID == saboteur.GetOwnerInstanceID());
        if (faction == null)
            return;

        _fogOfWarManager.RemoveEntityFromSnapshots(faction, result.SabotagedObject.GetInstanceID());
    }

    /// <summary>
    /// Creates and applies faction messages for a result batch.
    /// </summary>
    /// <param name="results">The game results to translate into messages.</param>
    private void ProcessMessageDeliveries(List<GameResult> results)
    {
        foreach (
            (Faction faction, Message message) delivery in _messageFactory.CreateMessages(
                results,
                _game
            )
        )
            AddMessage(delivery.faction, delivery.message);
    }

    /// <summary>
    /// Adds a message to a faction when both are available.
    /// </summary>
    /// <param name="faction">The faction that should receive the message.</param>
    /// <param name="message">The message to add.</param>
    private static void AddMessage(Faction faction, Message message)
    {
        if (faction == null || message == null)
            return;

        faction.AddMessage(message);
    }
}
