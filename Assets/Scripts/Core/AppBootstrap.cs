using UnityEngine;

/// <summary>
/// Initializes the GameRuntime and wires up global systems and
/// dependencies. Ensures a single source of truth for application state and
/// global command handling.
/// </summary>
public sealed class AppBootstrap : MonoBehaviour
{
    public static AppBootstrap Instance { get; private set; }

    [SerializeField]
    private GlobalInputHandler inputHandler;

    private GameRuntime _runtime;

    /// <summary>
    /// Ensures AppBootstrap exists. Creates minimal bootstrap if missing (for scene testing).
    /// Only creates the root GameObject - normal Awake() handles initialization.
    /// IMPORTANT: Only call this at scene entry points (GameFlowController, etc), not from random systems.
    /// </summary>
    /// <returns>The existing or newly created AppBootstrap instance.</returns>
    public static AppBootstrap EnsureExists()
    {
        if (Instance != null)
            return Instance;

        GameObject obj = new GameObject("AppBootstrap (Auto)");
        return obj.AddComponent<AppBootstrap>();
    }

    private void Awake()
    {
        // Ensure only one bootstrap exists.
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeRuntime();
    }

    private void InitializeRuntime()
    {
        _runtime = new GameRuntime();

        if (inputHandler == null)
            inputHandler = CreateInputHandler();

        if (inputHandler != null)
            inputHandler.Initialize(_runtime);
    }

    private GlobalInputHandler CreateInputHandler()
    {
        // For scene testing, create minimal input handler as child.
        GameObject inputObj = new GameObject("GlobalInputHandler");
        inputObj.transform.SetParent(transform);

        GlobalInputHandler handler = inputObj.AddComponent<GlobalInputHandler>();

        // Future-proof: allow handler to configure defaults.
        handler.ConfigureDefaults();

        return handler;
    }

    /// <summary>
    /// Returns the active <see cref="GameRuntime"/> held by this bootstrap.
    /// </summary>
    /// <returns>The runtime, or null if the bootstrap has not yet been initialized.</returns>
    public GameRuntime GetRuntime()
    {
        return _runtime;
    }
}
