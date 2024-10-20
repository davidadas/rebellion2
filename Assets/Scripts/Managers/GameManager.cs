using DependencyInjectionExtensions;

/// <summary>
/// Specifies different speeds for the game tick processing.
/// </summary>
public enum TickSpeed
{
    Fast,
    Medium,
    Slow,
    Paused
}

/// <summary>
/// Manages the overall game state, including the current game instance, 
/// event management, and tick processing. This class is also responsible for
/// coordinating the game's logic and event-driven architecture.
/// </summary>
public class GameManager
{
    private Game game;
    private GameEventManager eventManager;
    private MissionManager missionManager;
    private IServiceLocator serviceLocator;
    private float? tickInterval;
    private float tickTimer;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="serviceLocator"></param>
    /// <param name="game"></param>
    public GameManager(IServiceLocator serviceLocator, Game game)
    {
        // Initialize private variables.
        this.serviceLocator = serviceLocator;
        this.game = game;

        // Initialize other managers.
        eventManager = new GameEventManager(serviceLocator);
        missionManager = new MissionManager(serviceLocator, game);

        Initialize(game);
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
                tickInterval = 1f;  // Fast speed: 1 second per tick
                break;
            case TickSpeed.Medium:
                tickInterval = 10f;  // Medium speed: 10 seconds per tick
                break;
            case TickSpeed.Slow:
                tickInterval = 60f;  // Slow speed: 60 seconds per tick
                break;
            case TickSpeed.Paused:
                tickInterval = null;  // Paused: No ticking
                break;
        }
    }

    /// <summary>
    /// Processes the passage of time and executes ticks when the timer reaches the interval.
    /// </summary>
    /// <param name="deltaTime">The time since the last update (passed from an external source).</param>
    public void Update(float deltaTime)
    {
        if (tickInterval == null) return;  // If paused, do not process ticks

        tickTimer += deltaTime;  // Add the elapsed time to the tick timer

        // If the tick timer exceeds the interval, process a tick
        if (tickTimer >= tickInterval)
        {
            tickTimer = 0f;  // Reset the timer
            ProcessTick();  // Execute the game tick logic
        }
    }

    /// <summary>
    /// Processes a single game tick, incrementing the tick count and processing events.
    /// </summary>
    private void ProcessTick()
    {
        // Increment the current game's tick counter.
        game.CurrentTick++;

        // Process any events scheduled for this tick.
        eventManager.ProcessEvents(game.CurrentTick);  
    }

    /// <summary>
    /// Initializes the game by adding all nodes to the game's node registry.
    /// </summary>
    /// <param name="game"></param>
    public void Initialize(Game game)
    {
        GalaxyMap galaxy = game.Galaxy;

        galaxy.Traverse((SceneNode node) => {
            foreach (SceneNode child in node.GetChildren()) 
            {
                child.SetParent(node);
            }
            game.AddNodeByInstanceID(node);
        });
    }
}
