using UnityEngine;

/// <summary>
/// Initializes the GameRuntime and wires up global systems and
/// dependencies. Ensures a single source of truth for application state and
/// global command handling.
/// </summary>
public sealed class AppBootstrap : MonoBehaviour
{
    /// <summary>
    /// Gets the active application bootstrap instance.
    /// </summary>
    public static AppBootstrap Instance { get; private set; }

    [SerializeField]
    private AppInputController inputController;

    [SerializeField]
    private InputManager inputManager;

    [SerializeField]
    private AudioManager audioManager;

    private CancelStack cancelStack;
    private GameRuntime _runtime;
    private UserSettingsManager _userSettingsManager;

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

    /// <summary>
    /// Initializes the bootstrap once for the application lifetime.
    /// </summary>
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent != null)
            transform.SetParent(null);

        DontDestroyOnLoad(gameObject);

        InitializeRuntime();
    }

    /// <summary>
    /// Creates and connects the runtime services required by application-level systems.
    /// </summary>
    private void InitializeRuntime()
    {
        _runtime = new GameRuntime();
        cancelStack ??= new CancelStack();

        if (audioManager == null)
            audioManager = AudioManager.EnsureExists(transform);

        if (inputManager == null)
            inputManager = CreateInputManager();

        _userSettingsManager = new UserSettingsManager(audioManager, inputManager);
        _userSettingsManager.Load();

        if (inputController == null)
            inputController = CreateInputController();

        inputController?.Initialize(inputManager, cancelStack, _runtime);
    }

    /// <summary>
    /// Creates the input manager under the bootstrap object.
    /// </summary>
    /// <returns>The created input manager.</returns>
    private InputManager CreateInputManager()
    {
        GameObject inputObj = new GameObject("InputManager");
        inputObj.transform.SetParent(transform);

        return inputObj.AddComponent<InputManager>();
    }

    /// <summary>
    /// Creates the application input controller under the bootstrap object.
    /// </summary>
    /// <returns>The created application input controller.</returns>
    private AppInputController CreateInputController()
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

    /// <summary>
    /// Returns the application audio manager, creating one when needed.
    /// </summary>
    /// <returns>The active application audio manager.</returns>
    public AudioManager GetAudioManager()
    {
        if (audioManager == null)
            audioManager = AudioManager.EnsureExists(transform);

        return audioManager;
    }

    /// <summary>
    /// Returns the application input manager.
    /// </summary>
    /// <returns>The active input manager.</returns>
    public InputManager GetInputManager()
    {
        return inputManager;
    }

    /// <summary>
    /// Returns the application cancel stack.
    /// </summary>
    /// <returns>The active cancel stack.</returns>
    public CancelStack GetCancelStack()
    {
        return cancelStack;
    }

    /// <summary>
    /// Returns the user settings manager.
    /// </summary>
    /// <returns>The active user settings manager.</returns>
    public UserSettingsManager GetUserSettingsManager()
    {
        return _userSettingsManager;
    }

    /// <summary>
    /// Returns the loaded user settings.
    /// </summary>
    /// <returns>The active user settings, or null when settings have not been loaded.</returns>
    public UserSettings GetUserSettings()
    {
        return _userSettingsManager?.Settings;
    }

    /// <summary>
    /// Applies the active user settings to runtime systems.
    /// </summary>
    public void ApplyUserSettings()
    {
        _userSettingsManager?.Apply();
    }

    /// <summary>
    /// Captures and saves the active user settings.
    /// </summary>
    public void SaveUserSettings()
    {
        _userSettingsManager?.Save();
    }
}
