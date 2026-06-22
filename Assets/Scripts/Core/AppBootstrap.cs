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
    private InputActionsManager inputActionsManager;

    [SerializeField]
    private AppInputController appInputController;

    private CancelStack cancelStack;
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
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        InitializeRuntime();
    }

    private void InitializeRuntime()
    {
        _runtime = new GameRuntime();

        if (inputActionsManager == null)
            inputActionsManager = CreateInputActionsManager();

        cancelStack ??= new CancelStack();

        if (appInputController == null)
            appInputController = CreateAppInputController();

        appInputController.Initialize(inputActionsManager, cancelStack, _runtime);
    }

    private InputActionsManager CreateInputActionsManager()
    {
        GameObject inputObj = new GameObject("InputActionsManager");
        inputObj.transform.SetParent(transform);

        return inputObj.AddComponent<InputActionsManager>();
    }

    private AppInputController CreateAppInputController()
    {
        GameObject inputObj = new GameObject("AppInputController");
        inputObj.transform.SetParent(transform);

        return inputObj.AddComponent<AppInputController>();
    }

    /// <summary>
    /// Returns the active <see cref="GameRuntime"/> held by this bootstrap.
    /// </summary>
    /// <returns>The runtime, or null if the bootstrap has not yet been initialized.</returns>
    public GameRuntime GetRuntime()
    {
        return _runtime;
    }

    public InputActionsManager GetInputActionsManager()
    {
        return inputActionsManager;
    }

    public CancelStack GetCancelStack()
    {
        return cancelStack;
    }
}
