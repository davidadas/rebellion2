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
/// Manages the overall game state, including the current game instance,
/// event management, and tick processing. This class is also responsible for
/// coordinating the game's logic and event-driven architecture.
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
    private UprisingSystem uprisingManager;
    private VictorySystem victoryManager;
    private IRandomNumberProvider randomProvider;
    private CombatDecisionContext pendingCombatDecision;
    private float? tickInterval;
    private float tickTimer;
    private readonly Stopwatch stopwatch;

    /// <summary>
    /// Creates a new GameManager for the given game instance.
    /// GameManager is owned by GameRuntime - do not create directly.
    /// </summary>
    /// <param name="game">The game instance to manage.</param>
    public GameManager(GameRoot game)
    {
        // Initialize private variables.
        this.game = game;

        // Inject config if game doesn't have one
        if (game.Config == null)
        {
            game.SetConfig(ConfigLoader.LoadGameConfig());
        }

        // Initialize random provider with seed 0 for deterministic testing
        // TODO: Make seed configurable from game settings
        randomProvider = new SystemRandomProvider(0);

        // Initialize all managers in dependency order.
        eventManager = new GameEventSystem(game);
        fogOfWarManager = new FogOfWarSystem(game);
        movementManager = new MovementSystem(game, fogOfWarManager);
        manufacturingManager = new ManufacturingSystem(game);
        OwnershipSystem ownershipSystem = new OwnershipSystem(
            game,
            movementManager,
            manufacturingManager
        );
        missionManager = new MissionSystem(game, movementManager, ownershipSystem);
        combatManager = new CombatSystem(game, randomProvider);
        blockadeManager = new BlockadeSystem(game);
        deathStarManager = new DeathStarSystem(game);
        researchManager = new ResearchSystem(game);
        jediManager = new JediSystem(game);
        betrayalManager = new BetrayalSystem(game);
        uprisingManager = new UprisingSystem(game);
        victoryManager = new VictorySystem(game);
        aiManager = new AIManager(game, missionManager, movementManager, manufacturingManager);

        // Initialize the stopwatch for tracking time deltas.
        stopwatch = new Stopwatch();

        // Set the initial speed of the game.
        SetGameSpeed(game.GetGameSpeed());
    }

    /// <summary>
    /// Gets the current game instance.
    /// </summary>
    /// <returns>The current Game object.</returns>
    public GameRoot GetGame() => game;

    /// <summary>
    /// Replace the current game instance (used for hot reload).
    /// Reinitializes all managers with the new game state.
    /// </summary>
    /// <param name="newGame">The loaded game instance.</param>
    public void ReplaceGame(GameRoot newGame)
    {
        if (newGame == null)
            throw new InvalidOperationException("Cannot replace game with null.");

        // Replace game instance
        game = newGame;

        // Reinitialize all managers with new game state
        eventManager = new GameEventSystem(game);
        fogOfWarManager = new FogOfWarSystem(game);
        movementManager = new MovementSystem(game, fogOfWarManager);
        manufacturingManager = new ManufacturingSystem(game);
        OwnershipSystem ownershipSystem = new OwnershipSystem(
            game,
            movementManager,
            manufacturingManager
        );
        missionManager = new MissionSystem(game, movementManager, ownershipSystem);
        combatManager = new CombatSystem(game, randomProvider);
        blockadeManager = new BlockadeSystem(game);
        deathStarManager = new DeathStarSystem(game);
        researchManager = new ResearchSystem(game);
        jediManager = new JediSystem(game);
        betrayalManager = new BetrayalSystem(game);
        uprisingManager = new UprisingSystem(game);
        victoryManager = new VictorySystem(game);
        aiManager = new AIManager(game, missionManager, movementManager, manufacturingManager);

        // Reset timing
        tickTimer = 0f;
        stopwatch.Restart();
        SetGameSpeed(game.GetGameSpeed());
    }

    /// <summary>
    /// Sets the speed of the game.
    /// </summary>
    /// <param name="speed">The desired speed (Fast, Medium, Slow, Paused).</param>
    public void SetGameSpeed(TickSpeed speed)
    {
        game.SetGameSpeed(speed);

        // Adjust the tick interval based on the selected speed
        switch (speed)
        {
            case TickSpeed.Fast:
                tickInterval = 1f; // Fast speed: 1 second per tick
                break;
            case TickSpeed.Medium:
                tickInterval = 10f; // Medium speed: 10 seconds per tick
                break;
            case TickSpeed.Slow:
                tickInterval = 60f; // Slow speed: 60 seconds per tick
                break;
            case TickSpeed.Paused:
                stopwatch.Stop();
                tickInterval = null; // Paused: No incrementing of ticks
                break;
        }

        // Start the stopwatch if the game is not paused.
        if (tickInterval != null)
        {
            stopwatch.Start();
        }
    }

    /// <summary>
    /// Gets the current tick count of the game.
    /// </summary>
    /// <returns></returns>
    public int GetCurrentTick() => game.CurrentTick;

    /// <summary>
    /// Gets the player-controlled faction.
    /// </summary>
    /// <returns></returns>
    public Faction GetPlayerFaction()
    {
        return game.GetPlayerFaction();
    }

    /// <summary>
    /// Resolves the pending combat encounter and resumes ticking.
    /// Must be called by the UI after presenting the combat decision to the player.
    /// </summary>
    /// <param name="autoResolve">True to auto-resolve; false for manual resolution.</param>
    public void ResolveCombat(bool autoResolve)
    {
        if (pendingCombatDecision == null)
        {
            throw new InvalidOperationException("No pending combat to resolve.");
        }

        combatManager.Resolve(game, pendingCombatDecision, autoResolve, randomProvider);
        pendingCombatDecision = null;
    }

    /// <summary>
    /// Gets the fog of war system for building faction-specific galaxy views.
    /// </summary>
    /// <returns></returns>
    public FogOfWarSystem GetFogOfWarSystem()
    {
        return fogOfWarManager;
    }

    /// <summary>
    /// Processes the passage of time and executes ticks when the timer reaches the interval.
    /// </summary>
    public void Update()
    {
        if (pendingCombatDecision != null)
        {
            return;
        }

        if (tickInterval == null)
        {
            return;
        }
        // Calculate deltaTime using the elapsed time from the stopwatch.
        float deltaTime = (float)stopwatch.Elapsed.TotalSeconds;
        stopwatch.Restart();

        tickTimer += deltaTime;

        // If the tick timer exceeds the interval, process a tick.
        if (tickTimer >= tickInterval)
        {
            // reset the timer.
            tickTimer = 0f;
            ProcessTick();
        }
    }

    /// <summary>
    /// Processes a single game tick using an intentionally sequential execution model.
    /// Order is critical - each system depends on state from previous systems.
    /// </summary>
    private void ProcessTick()
    {
        // Increment the current game's tick counter.
        game.CurrentTick++;

        GameLogger.Debug("Tick: " + game.CurrentTick);

        // 1.Manufacturing: Produces units, must happen before movement consumes capacity
        manufacturingManager.ProcessTick(game);

        // 2. Movement: Updates positions before combat needs them
        movementManager.ProcessTick();

        // 3. Combat: Detect encounter — auto-resolve AI vs AI, freeze for player involvement
        if (combatManager.TryStartCombat(game, out pendingCombatDecision))
        {
            Fleet attackerFleet = game.GetSceneNodeByInstanceID<Fleet>(
                pendingCombatDecision.AttackerFleetInstanceID
            );
            Fleet defenderFleet = game.GetSceneNodeByInstanceID<Fleet>(
                pendingCombatDecision.DefenderFleetInstanceID
            );
            Faction attacker = game.GetFactionByOwnerInstanceID(
                attackerFleet?.GetOwnerInstanceID()
            );
            Faction defender = game.GetFactionByOwnerInstanceID(
                defenderFleet?.GetOwnerInstanceID()
            );

            if (
                attacker != null
                && defender != null
                && attacker.IsAIControlled()
                && defender.IsAIControlled()
            )
            {
                combatManager.Resolve(
                    game,
                    pendingCombatDecision,
                    autoResolve: true,
                    randomProvider
                );
                pendingCombatDecision = null;
            }
            else
            {
                return;
            }
        }

        // 4. Missions: Executes with current fog state
        List<GameResult> missionResults = missionManager.ProcessTick(game, randomProvider);
        foreach (MissionCompletedResult result in missionResults.OfType<MissionCompletedResult>())
        {
            string agents = string.Join(", ", result.ParticipantNames);
            string target = string.IsNullOrEmpty(result.TargetName)
                ? ""
                : $" at {result.TargetName}";
            GameLogger.Log($"{result.MissionName} mission by {agents}{target}: {result.Outcome}");
        }
        foreach (
            PlanetOwnershipChangedResult result in missionResults.OfType<PlanetOwnershipChangedResult>()
        )
        {
            Planet changedPlanet = game.GetSceneNodeByInstanceID<Planet>(result.PlanetInstanceID);
            PlanetSystem changedSystem = changedPlanet?.GetParentOfType<PlanetSystem>();
            if (changedPlanet != null && changedSystem != null)
            {
                foreach (Faction faction in game.Factions)
                {
                    fogOfWarManager.CaptureSnapshot(
                        faction,
                        changedPlanet,
                        changedSystem,
                        game.CurrentTick
                    );
                }
            }
            GameLogger.Log(
                $"Planet {result.PlanetInstanceID} ownership changed to {result.NewOwnerInstanceID}."
            );
        }

        // 6. Events: Triggers based on current world state
        eventManager.ProcessEvents(game.GetEventPool(), randomProvider);

        // 7. AI: Observes fog/combat/events, directly mutates manager states
        aiManager.Update(randomProvider);

        // 8. Blockade: Checks fleet presence after AI decisions
        blockadeManager.ProcessTick(game);

        // 9. Uprising: Flips control based on popular support
        uprisingManager.ProcessTick(randomProvider);

        // 10. Betrayal: Loyalty checks after uprising (control changes affect loyalty)
        betrayalManager.ProcessTick(game);

        // 11. Death Star: Construction countdown and planet destruction checks
        deathStarManager.ProcessTick(game);

        // 12. Research: Applies tech upgrades
        researchManager.ProcessTick(game);

        // 13. Jedi: Advances Force tiers
        List<JediResult> jediResults = jediManager.ProcessTick(game, randomProvider);
        foreach (JediResult result in jediResults)
        {
            GameLogger.Log(
                $"{result.Officer.GetDisplayName()} {result.EventType}: {result.NewTier}"
            );
        }

        // 14. Victory: Terminal check last
        VictoryResult? outcome = victoryManager.CheckVictory();
        if (outcome != null)
        {
            // TODO: Handle victory outcome (set game over flag, show victory screen, etc.)
            GameLogger.Log($"Victory condition met: {outcome}");
        }
    }
}
