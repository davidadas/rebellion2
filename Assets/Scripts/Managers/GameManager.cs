using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
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
    private ResourceRebalanceSystem _resourceRebalanceManager;
    private Rebellion.Systems.ResourceIncomeSystem _resourceIncomeManager;
    private CombatSystem _combatManager;
    private FogOfWarSystem _fogOfWarManager;
    private BlockadeSystem _blockadeManager;
    private DeathStarSystem _deathStarManager;
    private ResearchSystem _researchManager;
    private JediSystem _jediSystem;
    private BetrayalSystem _betrayalManager;
    private PlanetaryControlSystem _planetaryControlSystem;
    private UprisingSystem _uprisingManager;
    private VictorySystem _victoryManager;
    private IRandomNumberProvider _randomProvider;
    private CombatDecisionContext _pendingCombatDecision;
    private float? _tickInterval;
    private float _tickTimer;
    private readonly Stopwatch _stopwatch;

    /// <summary>
    /// Creates a new GameManager for the given game instance.
    /// </summary>
    /// <param name="game">The game instance to manage.</param>
    public GameManager(GameRoot game)
    {
        _game = game;

        if (_game.Config == null)
            _game.SetConfig(ResourceManager.GetConfig<GameConfig>());

        // TODO: Make seed configurable from game settings
        _randomProvider = new SystemRandomProvider(0);
        _stopwatch = new Stopwatch();

        InitializeSystems();
        RebuildDerivedState();
        RehydrateMissions();
        SetGameSpeed(_game.GetGameSpeed());
    }

    /// <summary>
    /// Replaces the current game instance (used for hot reload) and reinitializes all systems.
    /// </summary>
    /// <param name="newGame">The replacement game instance.</param>
    public void ReplaceGame(GameRoot newGame)
    {
        if (newGame == null)
            throw new InvalidOperationException("Cannot replace game with null.");

        _game = newGame;

        if (_game.Config == null)
            _game.SetConfig(ResourceManager.GetConfig<GameConfig>());

        InitializeSystems();
        RebuildDerivedState();
        RehydrateMissions();
        _tickTimer = 0f;
        _stopwatch.Restart();
        SetGameSpeed(_game.GetGameSpeed());
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
        _game.SetGameSpeed(speed);

        switch (speed)
        {
            case TickSpeed.Fast:
                _tickInterval = 1f;
                break;
            case TickSpeed.Medium:
                _tickInterval = 10f;
                break;
            case TickSpeed.Slow:
                _tickInterval = 60f;
                break;
            case TickSpeed.Paused:
                _stopwatch.Stop();
                _tickInterval = null;
                break;
        }

        if (_tickInterval != null)
            _stopwatch.Start();
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
    /// Must be called by the UI after presenting the combat decision to the player.
    /// </summary>
    /// <param name="autoResolve">Whether to auto-resolve instead of tactical combat.</param>
    public void ResolveCombat(bool autoResolve)
    {
        if (_pendingCombatDecision == null)
            throw new InvalidOperationException("No pending combat to resolve.");

        SpaceCombatResult combatResult = _combatManager.Resolve(
            _pendingCombatDecision,
            autoResolve
        );
        if (combatResult != null)
            ProcessResults(combatResult.Events);
        _pendingCombatDecision = null;
    }

    /// <summary>
    /// Runs one game tick. Order is intentional — each system depends on state
    /// set by the systems before it.
    /// </summary>
    private void ProcessTick()
    {
        _game.CurrentTick++;
        GameLogger.Debug("Tick: " + _game.CurrentTick);

        // 0. Resource rebalance: timer-based decay, facility suspension, resource walk
        ProcessResults(_resourceRebalanceManager.ProcessTick());

        // 0b. Resource income: accumulate per-planet income into faction stockpiles
        ProcessResults(_resourceIncomeManager.ProcessTick());

        // 1. Manufacturing: produces units before movement consumes capacity
        ProcessResults(_manufacturingManager.ProcessTick());

        // 1b. Maintenance: scrap units if maintenance cost exceeds capacity
        ProcessResults(_maintenanceManager.ProcessTick());

        // 2. Movement: updates positions before combat needs them
        ProcessResults(_movementManager.ProcessTick());

        // 3. Combat: auto-resolves AI encounters; freezes tick if player is involved
        ProcessResults(_combatManager.ProcessTick());
        if (_pendingCombatDecision != null)
            return;

        // 4. Missions: executes with current fog state
        ProcessResults(_missionManager.ProcessTick());

        // 5. Events: triggers based on current world state
        _eventManager.ProcessEvents(_game.GetEventPool());

        // 6. AI: observes fog/combat/events, directly mutates manager states
        ProcessResults(_aiSystem.ProcessTick());

        // 7. Blockade: checks fleet presence after AI decisions
        ProcessResults(_blockadeManager.ProcessTick());

        // 8. Planetary control: adjusts popular support and transfers ownership on threshold
        ProcessResults(_planetaryControlSystem.ProcessTick());

        // 9. Uprising: checks garrison vs. support, rolls dice for uprising
        ProcessResults(_uprisingManager.ProcessTick());

        // 10. Betrayal: loyalty checks after uprising
        ProcessResults(_betrayalManager.ProcessTick());

        // 11. Death Star: construction countdown and planet destruction
        ProcessResults(_deathStarManager.ProcessTick());

        // 12. Research: applies tech upgrades
        ProcessResults(_researchManager.ProcessTick());

        // 13. Jedi: refreshes force discovery state
        ProcessResults(_jediSystem.ProcessTick());

        // 14. Victory: terminal check last
        ProcessResults(_victoryManager.ProcessTick());
    }

    /// <summary>
    /// Initializes all systems in dependency order. Called on construction and hot reload.
    /// </summary>
    private void InitializeSystems()
    {
        _eventManager = new GameEventSystem(_game, _randomProvider);
        _fogOfWarManager = new FogOfWarSystem(_game);
        _blockadeManager = new BlockadeSystem(_game, _randomProvider);
        _movementManager = new MovementSystem(_game, _fogOfWarManager, _blockadeManager);
        _manufacturingManager = new ManufacturingSystem(_game, _randomProvider, _movementManager);
        _maintenanceManager = new MaintenanceSystem(_game, _randomProvider);
        _resourceRebalanceManager = new ResourceRebalanceSystem(_game, _randomProvider);
        _resourceIncomeManager = new Rebellion.Systems.ResourceIncomeSystem(_game);
        _planetaryControlSystem = new PlanetaryControlSystem(
            _game,
            _movementManager,
            _manufacturingManager
        );
        _jediSystem = new JediSystem(_game, _randomProvider);
        _missionManager = new MissionSystem(
            _game,
            _randomProvider,
            _movementManager,
            _fogOfWarManager
        );
        _combatManager = new CombatSystem(
            _game,
            _randomProvider,
            _movementManager,
            _planetaryControlSystem
        );
        _deathStarManager = new DeathStarSystem(_game);
        _researchManager = new ResearchSystem(_game);
        _betrayalManager = new BetrayalSystem(_game);
        _uprisingManager = new UprisingSystem(_game, _randomProvider, _planetaryControlSystem);
        _victoryManager = new VictorySystem(_game);
        _aiSystem = new AISystem(
            _game,
            _missionManager,
            _movementManager,
            _manufacturingManager,
            _randomProvider
        );
    }

    /// <summary>
    /// Rebuilds derived state that is not persisted (tech levels, manufacturing queues).
    /// Called after system initialization on both new games and loaded saves.
    /// </summary>
    private void RebuildDerivedState()
    {
        IManufacturable[] templates = ResourceManager
            .GetGameData<Building>()
            .Cast<IManufacturable>()
            .Concat(ResourceManager.GetGameData<CapitalShip>())
            .Concat(ResourceManager.GetGameData<Starfighter>())
            .Concat(ResourceManager.GetGameData<Regiment>())
            .ToArray();

        foreach (Faction faction in _game.GetFactions())
            faction.RebuildResearchQueues(templates);

        _manufacturingManager.RebuildQueues();
    }

    /// <summary>
    /// Applies saved mission probability tables to any missions already in the scene graph.
    /// Needed after deserialization since probability tables are not persisted.
    /// </summary>
    private void RehydrateMissions()
    {
        GameConfig.MissionProbabilityTablesConfig missionTables = _game
            .Config
            ?.ProbabilityTables
            ?.Mission;
        if (missionTables == null)
            return;

        foreach (Mission mission in _game.GetSceneNodesByType<Mission>())
            mission.Configure(missionTables);
    }

    /// <summary>
    /// Handles cross-cutting side effects for a batch of game results.
    /// Per-result logging is the responsibility of the system that produced the result.
    /// </summary>
    /// <param name="results">Batch of results from a system tick.</param>
    private void ProcessResults(List<GameResult> results)
    {
        foreach (VictoryResult result in results.OfType<VictoryResult>())
        {
            // TODO: Set game over flag, trigger victory screen
        }

        foreach (PendingCombatResult result in results.OfType<PendingCombatResult>())
        {
            _pendingCombatDecision = new CombatDecisionContext
            {
                AttackerFleetInstanceID = result.AttackerFleet?.GetInstanceID(),
                DefenderFleetInstanceID = result.DefenderFleet?.GetInstanceID(),
            };
        }

        foreach (
            PlanetOwnershipChangedResult result in results.OfType<PlanetOwnershipChangedResult>()
        )
        {
            Planet changedPlanet = result.Planet;
            PlanetSystem changedSystem = changedPlanet?.GetParentOfType<PlanetSystem>();
            if (changedPlanet != null && changedSystem != null)
            {
                foreach (Faction faction in _game.Factions)
                    _fogOfWarManager.CaptureSnapshot(
                        faction,
                        changedPlanet,
                        changedSystem,
                        _game.CurrentTick
                    );
            }
        }

        foreach (MissionCompletedResult result in results.OfType<MissionCompletedResult>())
        {
            if (result.Outcome == MissionOutcome.Success)
                ProcessResults(_jediSystem.ApplyForceGrowth(result.Mission.MainParticipants));
        }
    }
}
