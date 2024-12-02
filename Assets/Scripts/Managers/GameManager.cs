using System;
using System.Diagnostics;

/// <summary>
/// Specifies different speeds for the game tick processing.
/// </summary>
public enum TickSpeed
{
    Fast,
    Medium,
    Slow,
    Paused,
}

/// <summary>
/// Manages the overall game state, including the current game instance,
/// event management, and tick processing. This class is also responsible for
/// coordinating the game's logic and event-driven architecture.
/// </summary>
public class GameManager
{
    private Game game;
    private AIManager aiManager;
    private PlanetManager planetManager;
    private GameEventManager eventManager;
    private MissionManager missionManager;
    private UnitManager unitManager;
    private float? tickInterval;
    private float tickTimer;
    private readonly Stopwatch stopwatch;

    /// <summary>
    ///
    /// </summary>
    /// <param name="game"></param>
    public GameManager(Game game)
    {
        // Initialize private variables.
        this.game = game;

        // Initialize other managers.
        eventManager = new GameEventManager(game);
        unitManager = new UnitManager(game);
        missionManager = new MissionManager(game);
        planetManager = new PlanetManager(game);
        aiManager = new AIManager(game, missionManager, unitManager, planetManager);

        // Initialize the stopwatch for tracking time deltas.
        stopwatch = new Stopwatch();

        // Set the default tick speed to Slow (slowest).
        SetTickSpeed(TickSpeed.Medium);
    }

    /// <summary>
    /// Gets the current game instance.
    /// </summary>
    /// <returns>The current Game object.</returns>
    public Game GetGame() => game;

    /// <summary>
    /// Sets the speed of the game ticks.
    /// </summary>
    /// <param name="speed">The desired tick speed (Fast, Medium, Slow, Paused).</param>
    public void SetTickSpeed(TickSpeed speed)
    {
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
        // Start the stopwatch if the game is not paused
        if (tickInterval != null)
        {
            stopwatch.Start();
        }
    }

    /// <summary>
    /// Processes the passage of time and executes ticks when the timer reaches the interval.
    /// </summary>
    public void Update()
    {
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
    ///
    /// </summary>
    /// <param name="node"></param>
    private void UpdateNode(ISceneNode node)
    {
        // Update the movement of movable units.
        if (node is IMovable moveable)
        {
            unitManager.UpdateMovement(moveable);
        }

        // Update the state of planets.
        if (node is Planet planet)
        {
            planetManager.UpdatePlanet(planet);
        }

        // Update the state of active missions.
        if (node is Mission mission)
        {
            missionManager.UpdateMission(mission);
        }
    }

    /// <summary>
    /// Processes a single game tick, incrementing the tick count and processing events.
    /// </summary>
    private void ProcessTick()
    {
        // Increment the current game's tick counter.
        game.CurrentTick++;

        GameLogger.Log("Tick: " + game.CurrentTick);

        // Update the state of each scene node in the game.
        game.GetGalaxyMap().Traverse(UpdateNode);

        // Process any events scheduled for this tick.
        eventManager.ProcessEvents(game.GetEventPool());

        // Update the NPC AI factions in the game.
        aiManager.Update();
    }
}
