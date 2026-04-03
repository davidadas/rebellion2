using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Rebellion.Core.Configuration;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

/// <summary>
/// Coordinates all game systems each tick and routes results to cross-cutting handlers.
/// Owned by GameRuntime — do not create directly.
/// </summary>
public class GameManager
{
    private GameRoot game;
    private AIManager aiManager;
    private GameEventSystem eventManager;
    private MissionSystem missionManager;
    private MovementSystem movementManager;
    private ManufacturingSystem manufacturingManager;
    private CombatSystem combatManager;
    private FogOfWarSystem fogOfWarManager;
    private BlockadeSystem blockadeManager;
    private DeathStarSystem deathStarManager;
    private ResearchSystem researchManager;
    private JediSystem jediManager;
    private BetrayalSystem betrayalManager;
    private SupportShiftSystem supportShiftManager;
    private UprisingSystem uprisingManager;
    private VictorySystem victoryManager;
    private IRandomNumberProvider randomProvider;
    private CombatDecisionContext pendingCombatDecision;
    private float? tickInterval;
    private float tickTimer;
    private readonly Stopwatch stopwatch;

    /// <summary>
    /// Creates a new GameManager for the given game instance.
    /// </summary>
    public GameManager(GameRoot game)
    {
        this.game = game;

        if (game.Config == null)
            game.SetConfig(ConfigLoader.LoadGameConfig());

        // TODO: Make seed configurable from game settings
        randomProvider = new SystemRandomProvider(0);
        stopwatch = new Stopwatch();

        InitializeSystems();
        RebuildDerivedState();
        RehydrateMissions();
        SetGameSpeed(game.GetGameSpeed());
    }

    /// <summary>
    /// Replaces the current game instance (used for hot reload) and reinitializes all systems.
    /// </summary>
    public void ReplaceGame(GameRoot newGame)
    {
        if (newGame == null)
            throw new InvalidOperationException("Cannot replace game with null.");

        game = newGame;
        InitializeSystems();
        RebuildDerivedState();
        RehydrateMissions();
        tickTimer = 0f;
        stopwatch.Restart();
        SetGameSpeed(game.GetGameSpeed());
    }

    /// <summary>
    /// Returns the current game instance.
    /// </summary>
    public GameRoot GetGame() => game;

    /// <summary>
    /// Returns the current tick count.
    /// </summary>
    public int GetCurrentTick() => game.CurrentTick;

    /// <summary>
    /// Returns the player-controlled faction.
    /// </summary>
    public Faction GetPlayerFaction() => game.GetPlayerFaction();

    /// <summary>
    /// Returns the fog of war system for building faction-specific galaxy views.
    /// </summary>
    public FogOfWarSystem GetFogOfWarSystem() => fogOfWarManager;

    /// <summary>
    /// Sets the game speed and adjusts the tick interval accordingly.
    /// </summary>
    public void SetGameSpeed(TickSpeed speed)
    {
        game.SetGameSpeed(speed);

        switch (speed)
        {
            case TickSpeed.Fast:
                tickInterval = 1f;
                break;
            case TickSpeed.Medium:
                tickInterval = 10f;
                break;
            case TickSpeed.Slow:
                tickInterval = 60f;
                break;
            case TickSpeed.Paused:
                stopwatch.Stop();
                tickInterval = null;
                break;
        }

        if (tickInterval != null)
            stopwatch.Start();
    }

    /// <summary>
    /// Advances the tick timer and fires a tick when the interval is reached.
    /// No-ops while combat is pending player resolution or the game is paused.
    /// </summary>
    public void Update()
    {
        if (pendingCombatDecision != null || tickInterval == null)
            return;

        float deltaTime = (float)stopwatch.Elapsed.TotalSeconds;
        stopwatch.Restart();
        tickTimer += deltaTime;

        if (tickTimer >= tickInterval)
        {
            tickTimer = 0f;
            ProcessTick();
        }
    }

    /// <summary>
    /// Resolves the pending combat encounter and resumes ticking.
    /// Must be called by the UI after presenting the combat decision to the player.
    /// </summary>
    public void ResolveCombat(bool autoResolve)
    {
        if (pendingCombatDecision == null)
            throw new InvalidOperationException("No pending combat to resolve.");

        combatManager.Resolve(game, pendingCombatDecision, autoResolve, randomProvider);
        pendingCombatDecision = null;
    }

    /// <summary>
    /// Runs one game tick. Order is intentional — each system depends on state
    /// set by the systems before it.
    /// </summary>
    private void ProcessTick()
    {
        game.CurrentTick++;
        GameLogger.Debug("Tick: " + game.CurrentTick);

        // 1. Manufacturing: produces units before movement consumes capacity
        manufacturingManager.ProcessTick(movementManager);

        // 2. Movement: updates positions before combat needs them
        movementManager.ProcessTick();

        // 3. Combat: auto-resolves AI encounters; freezes tick if player is involved
        ProcessResults(combatManager.ProcessTick(game, randomProvider));
        if (pendingCombatDecision != null)
            return;

        // 4. Missions: executes with current fog state
        ProcessResults(missionManager.ProcessTick(game, randomProvider));

        // 5. Events: triggers based on current world state
        eventManager.ProcessEvents(game.GetEventPool(), randomProvider);

        // 6. AI: observes fog/combat/events, directly mutates manager states
        aiManager.Update(randomProvider);

        // 7. Blockade: checks fleet presence after AI decisions
        blockadeManager.ProcessTick(game);

        // 8. Support shift: adjusts popular support based on hostile forces
        supportShiftManager.ProcessTick();

        // 9. Uprising: checks garrison vs. support, rolls dice for uprising
        uprisingManager.ProcessTick(randomProvider);

        // 10. Betrayal: loyalty checks after uprising
        betrayalManager.ProcessTick(game);

        // 11. Death Star: construction countdown and planet destruction
        deathStarManager.ProcessTick(game);

        // 12. Research: applies tech upgrades
        researchManager.ProcessTick(game);

        // 13. Jedi: advances Force tiers
        ProcessResults(jediManager.ProcessTick(game, randomProvider));

        // 14. Victory: terminal check last
        ProcessResults(victoryManager.ProcessTick());
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
            pendingCombatDecision = new CombatDecisionContext
            {
                AttackerFleetInstanceID = result.AttackerFleetInstanceID,
                DefenderFleetInstanceID = result.DefenderFleetInstanceID,
            };
        }

        foreach (
            PlanetOwnershipChangedResult result in results.OfType<PlanetOwnershipChangedResult>()
        )
        {
            Planet changedPlanet = game.GetSceneNodeByInstanceID<Planet>(result.PlanetInstanceID);
            PlanetSystem changedSystem = changedPlanet?.GetParentOfType<PlanetSystem>();
            if (changedPlanet != null && changedSystem != null)
            {
                foreach (Faction faction in game.Factions)
                    fogOfWarManager.CaptureSnapshot(
                        faction,
                        changedPlanet,
                        changedSystem,
                        game.CurrentTick
                    );
            }
        }
    }

    /// <summary>
    /// Initializes all systems in dependency order. Called on construction and hot reload.
    /// </summary>
    private void InitializeSystems()
    {
        eventManager = new GameEventSystem(game);
        fogOfWarManager = new FogOfWarSystem(game);
        movementManager = new MovementSystem(game, fogOfWarManager);
        manufacturingManager = new ManufacturingSystem(game);
        OwnershipSystem ownershipSystem = new OwnershipSystem(
            game,
            movementManager,
            manufacturingManager
        );
        missionManager = new MissionSystem(game, movementManager, ownershipSystem, fogOfWarManager);
        combatManager = new CombatSystem(game, randomProvider);
        blockadeManager = new BlockadeSystem(game);
        deathStarManager = new DeathStarSystem(game);
        researchManager = new ResearchSystem(game);
        jediManager = new JediSystem(game);
        betrayalManager = new BetrayalSystem(game);
        supportShiftManager = new SupportShiftSystem(game);
        uprisingManager = new UprisingSystem(game);
        victoryManager = new VictorySystem(game);
        aiManager = new AIManager(game, missionManager, movementManager, manufacturingManager);
    }

    /// <summary>
    /// Rebuilds derived state that is not persisted (tech levels, manufacturing queues).
    /// Called after system initialization on both new games and loaded saves.
    /// </summary>
    private void RebuildDerivedState()
    {
        foreach (Faction faction in game.GetFactions())
            faction.RebuildTechnologyLevels(game);

        manufacturingManager.RebuildQueues();
    }

    /// <summary>
    /// Applies saved mission probability tables to any missions already in the scene graph.
    /// Needed after deserialization since probability tables are not persisted.
    /// </summary>
    private void RehydrateMissions()
    {
        GameConfig.MissionProbabilityTablesConfig missionTables = game.Config
            ?.ProbabilityTables
            ?.Mission;
        if (missionTables == null)
            return;

        foreach (Mission mission in game.GetSceneNodesByType<Mission>())
            mission.Configure(missionTables);
    }
}
