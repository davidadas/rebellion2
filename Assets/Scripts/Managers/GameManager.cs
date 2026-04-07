using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Rebellion.Core.Configuration;
using Rebellion.Core.Simulation;
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
    private CombatSystem _combatManager;
    private FogOfWarSystem _fogOfWarManager;
    private BlockadeSystem _blockadeManager;
    private DeathStarSystem _deathStarManager;
    private ResearchSystem _researchManager;
    private JediSystem _jediManager;
    private BetrayalSystem _betrayalManager;
    private SupportShiftSystem _supportShiftManager;
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
    public GameManager(GameRoot game)
    {
        _game = game;

        if (_game.Config == null)
            _game.SetConfig(ConfigLoader.LoadGameConfig());

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
    public void ReplaceGame(GameRoot newGame)
    {
        if (newGame == null)
            throw new InvalidOperationException("Cannot replace game with null.");

        _game = newGame;

        if (_game.Config == null)
            _game.SetConfig(ConfigLoader.LoadGameConfig());

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
    public GameRoot GetGame() => _game;

    /// <summary>
    /// Returns the current tick count.
    /// </summary>
    public int GetCurrentTick() => _game.CurrentTick;

    /// <summary>
    /// Returns the player-controlled faction.
    /// </summary>
    /// <returns>The faction whose PlayerID is set.</returns>
    public Faction GetPlayerFaction() => _game.GetPlayerFaction();

    /// <summary>
    /// Returns the fog of war system for building faction-specific galaxy views.
    /// </summary>
    public FogOfWarSystem GetFogOfWarSystem() => _fogOfWarManager;

    /// <summary>
    /// Sets the game speed and adjusts the tick interval accordingly.
    /// </summary>
    public void SetGameSpeed(TickSpeed speed)
    {
        _game.SetGameSpeed(speed);

        switch (speed)
        {
            case TickSpeed.Fast:
                _tickInterval = 0.1f;
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
    public void ResolveCombat(bool autoResolve)
    {
        if (_pendingCombatDecision == null)
            throw new InvalidOperationException("No pending combat to resolve.");

        _combatManager.Resolve(_game, _pendingCombatDecision, autoResolve, _randomProvider);
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
        _resourceRebalanceManager.ProcessTick(_randomProvider);

        // 1. Manufacturing: produces units before movement consumes capacity
        _manufacturingManager.ProcessTick(_movementManager, _randomProvider);

        // 1b. Maintenance: scrap units if maintenance cost exceeds capacity
        _maintenanceManager.ProcessTick(_randomProvider);

        // 2. Movement: updates positions before combat needs them
        _movementManager.ProcessTick();

        // 3. Combat: auto-resolves AI encounters; freezes tick if player is involved
        ProcessResults(_combatManager.ProcessTick(_game, _randomProvider));
        if (_pendingCombatDecision != null)
            return;

        // 4. Missions: executes with current fog state
        ProcessResults(_missionManager.ProcessTick(_game, _randomProvider));

        // 5. Events: triggers based on current world state
        _eventManager.ProcessEvents(_game.GetEventPool(), _randomProvider);

        // 6. AI: observes fog/combat/events, directly mutates manager states
        _aiSystem.ProcessTick();

        // 7. Blockade: checks fleet presence after AI decisions
        _blockadeManager.ProcessTick();

        // 8. Support shift: adjusts popular support based on hostile forces
        _supportShiftManager.ProcessTick();

        // 9. Uprising: checks garrison vs. support, rolls dice for uprising
        _uprisingManager.ProcessTick(_randomProvider);

        // 10. Betrayal: loyalty checks after uprising
        _betrayalManager.ProcessTick();

        // 11. Death Star: construction countdown and planet destruction
        _deathStarManager.ProcessTick();

        // 12. Research: applies tech upgrades
        ProcessResults(_researchManager.ProcessTick(_game));

        // 13. Jedi: refreshes force discovery state
        ProcessResults(_jediManager.ProcessTick(_randomProvider));

        // 14. Victory: terminal check last
        ProcessResults(_victoryManager.ProcessTick());
    }

    /// <summary>
    /// Initializes all systems in dependency order. Called on construction and hot reload.
    /// </summary>
    private void InitializeSystems()
    {
        _eventManager = new GameEventSystem(_game);
        _fogOfWarManager = new FogOfWarSystem(_game);
        _movementManager = new MovementSystem(_game, _fogOfWarManager);
        _manufacturingManager = new ManufacturingSystem(_game);
        _maintenanceManager = new MaintenanceSystem(_game);
        _resourceRebalanceManager = new ResourceRebalanceSystem(_game, _randomProvider);
        OwnershipSystem ownershipSystem = new OwnershipSystem(
            _game,
            _movementManager,
            _manufacturingManager
        );
        _jediManager = new JediSystem(_game);
        _missionManager = new MissionSystem(
            _game,
            _movementManager,
            ownershipSystem,
            _fogOfWarManager
        );
        _combatManager = new CombatSystem(_game, _randomProvider, _movementManager);
        _blockadeManager = new BlockadeSystem(_game);
        _deathStarManager = new DeathStarSystem(_game);
        _researchManager = new ResearchSystem();
        _betrayalManager = new BetrayalSystem(_game);
        _supportShiftManager = new SupportShiftSystem(_game);
        _uprisingManager = new UprisingSystem(_game);
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
            MissionCompletedResult result in results.OfType<MissionCompletedResult>()
        )
        {
            if (result.Outcome == MissionOutcome.Success)
            {
                foreach (string participantId in result.ParticipantInstanceIDs)
                {
                    Officer officer = _game.GetSceneNodeByInstanceID<Officer>(participantId);
                    if (officer != null)
                        _jediManager.AwardMissionForceGrowth(officer);
                }
            }
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
    }
}
