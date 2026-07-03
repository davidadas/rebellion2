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
    private InputActionsManager inputActionsManager;

    [SerializeField]
    private AudioManager audioManager;

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
        DontDestroyOnLoad(gameObject);

        InitializeRuntime();
    }

    /// <summary>
    /// Creates and connects the runtime services required by application-level systems.
    /// </summary>
    private void InitializeRuntime()
    {
        _runtime = new GameRuntime();

        if (audioManager == null)
            audioManager = AudioManager.EnsureExists(transform);

        if (inputActionsManager == null)
            inputActionsManager = CreateInputActionsManager();

        _userSettingsManager = new UserSettingsManager(audioManager, inputActionsManager);
        _userSettingsManager.Load();

        if (inputController == null)
            inputController = CreateInputController();

        inputController?.Initialize(inputActionsManager, _runtime);
    }

    /// <summary>
    /// Creates the input actions manager under the bootstrap object.
    /// </summary>
    /// <returns>The created input actions manager.</returns>
    private InputActionsManager CreateInputActionsManager()
    {
        GameObject inputObj = new GameObject("InputActionsManager");
        inputObj.transform.SetParent(transform);

        return inputObj.AddComponent<InputActionsManager>();
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
    /// Returns the application input actions manager.
    /// </summary>
    /// <returns>The active input actions manager.</returns>
    public InputActionsManager GetInputActionsManager()
    {
        return inputActionsManager;
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
