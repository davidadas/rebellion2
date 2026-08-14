using System.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Initializes the GameRuntime and wires up global systems and
/// dependencies. Ensures a single source of truth for application state and
/// global command handling.
/// </summary>
public sealed class AppBootstrap : MonoBehaviour
{
    private const string _defaultCursorResourcePath = "UI/DefaultCursor";
    private const string _mainMenuPreloadID = "main-menu";
    private const string _strategyPreloadID = "strategy";

#if UNITY_EDITOR
    private const string _editorCutscenePrefabPath =
        "Assets/Prefabs/UI/Cutscenes/CutscenePlayer.prefab";
#endif

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

    private CancelStack _cancelStack;
    private ContentPreloadManifest _mainMenuApplicationPreload;
    private ContentAssets _contentAssets;
    private ContentModelCache _contentModelCache;
    private ContentPack _contentPack;
    private CutsceneManager _cutsceneManager;
    private DisplayManager _displayManager;
    private Task _mainMenuContentTask;
    private Task _strategyContentTask;
    private GameRuntime _runtime;
    private SceneLoader _sceneLoader;
    private ContentPreloadManifest _strategyApplicationPreload;
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
        _cancelStack ??= new CancelStack();
        _sceneLoader = GetComponent<SceneLoader>();
        if (_sceneLoader == null)
            _sceneLoader = gameObject.AddComponent<SceneLoader>();
        _contentPack = ContentPackLoader.OpenActive();
        _mainMenuApplicationPreload = ContentPackLoader.LoadApplicationPreloadManifest(
            _contentPack.ContentRootPath,
            _mainMenuPreloadID
        );
        _strategyApplicationPreload = ContentPackLoader.LoadApplicationPreloadManifest(
            _contentPack.ContentRootPath,
            _strategyPreloadID
        );
        _contentAssets = new ContentAssets(_contentPack.ContentRootPath, _contentPack.PackRootPath);
        Texture2D cursorTexture =
            Resources.Load<Texture2D>(_defaultCursorResourcePath)
            ?? throw new System.InvalidOperationException(
                $"Application cursor resource is missing: {_defaultCursorResourcePath}"
            );
        Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.Auto);
        _contentModelCache = new ContentModelCache(_contentAssets);
        GameLaunchContext.Reset(_contentPack);
        _runtime = new GameRuntime(_contentPack);

        if (audioManager == null)
            audioManager = AudioManager.EnsureExists(transform);
        audioManager.InitializeContent(_contentAssets);

        _cutsceneManager = GetComponent<CutsceneManager>();
        if (_cutsceneManager == null)
            _cutsceneManager = gameObject.AddComponent<CutsceneManager>();
        _cutsceneManager.InitializeContent(_contentAssets);
        _cutsceneManager.InitializeAudio(audioManager);

#if UNITY_EDITOR
        GameObject cutscenePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            _editorCutscenePrefabPath
        );
        _cutsceneManager.Initialize(cutscenePrefab);
#endif

        if (inputManager == null)
            inputManager = CreateInputManager();

        _displayManager = new DisplayManager();
        _userSettingsManager = new UserSettingsManager(audioManager, _displayManager, inputManager);
        _userSettingsManager.Load();

        if (inputController == null)
            inputController = CreateInputController();

        inputController?.Initialize(inputManager, _cancelStack, _runtime);
    }

    /// <summary>
    /// Loads content required by the main-menu scene and begins warming strategy content.
    /// </summary>
    /// <returns>The shared main-menu preload task.</returns>
    internal Task InitializeMainMenuContentAsync()
    {
        _mainMenuContentTask ??= PreloadMainMenuContentAsync();
        return _mainMenuContentTask;
    }

    /// <summary>
    /// Loads required main-menu textures and audio. Decorative models are loaded by their scene
    /// bindings so a missing model cannot prevent scene navigation.
    /// </summary>
    internal Task InitializeMainMenuSceneAsync()
    {
        return InitializeMainMenuContentAsync();
    }

    /// <summary>
    /// Loads all content required by the strategy scene.
    /// </summary>
    /// <returns>The shared strategy preload task.</returns>
    internal Task InitializeStrategyContentAsync()
    {
        StartStrategyContentPreload();
        GameStartupTrace.Log($"Strategy content task status: {_strategyContentTask.Status}.");
        return _strategyContentTask;
    }

    /// <summary>
    /// Transitions to another application scene.
    /// </summary>
    /// <param name="sceneAddress">The Unity scene name to load.</param>
    internal void LoadScene(string sceneAddress)
    {
        _sceneLoader.Load(sceneAddress);
    }

    /// <summary>
    /// Composes the persistent cutscene manager with its authored player prefab.
    /// </summary>
    /// <param name="cutscenePrefab">The cutscene player prefab.</param>
    internal void InitializeCutsceneManager(GameObject cutscenePrefab)
    {
        _cutsceneManager.Initialize(cutscenePrefab);
    }

    /// <summary>
    /// Gets the persistent cutscene manager.
    /// </summary>
    /// <returns>The active cutscene manager.</returns>
    internal CutsceneManager GetCutsceneManager()
    {
        return _cutsceneManager;
    }

    /// <summary>
    /// Returns the application-owned cache used by runtime model bindings.
    /// </summary>
    internal ContentModelCache GetContentModelCache()
    {
        return _contentModelCache;
    }

    /// <summary>
    /// Loads main-menu content, then starts the strategy preload without delaying the menu.
    /// </summary>
    /// <returns>A task that completes when main-menu content is resident.</returns>
    private async Task PreloadMainMenuContentAsync()
    {
        await Task.WhenAll(
            _contentAssets.PreloadAsync(_mainMenuApplicationPreload),
            _contentAssets.PreloadAsync(_contentPack.GetPreloadManifest(_mainMenuPreloadID))
        );
        StartStrategyContentPreload();
    }

    /// <summary>
    /// Starts the shared strategy preload task when it has not already begun.
    /// </summary>
    private void StartStrategyContentPreload()
    {
        _strategyContentTask ??= Task.WhenAll(
            TracePreloadAsync(
                "Strategy application preload",
                _contentAssets.PreloadAsync(_strategyApplicationPreload)
            ),
            TracePreloadAsync(
                "Strategy pack preload",
                _contentAssets.PreloadAsync(_contentPack.GetPreloadManifest(_strategyPreloadID))
            )
        );
    }

    /// <summary>
    /// Reports completion of one content preload when a game-startup trace is active.
    /// </summary>
    /// <param name="description">The preload phase being measured.</param>
    /// <param name="preload">The underlying content preload.</param>
    /// <returns>A task that completes with the underlying preload.</returns>
    private static async Task TracePreloadAsync(string description, Task preload)
    {
        if (GameStartupTrace.IsActive)
            GameStartupTrace.Log($"{description} pending.");
        await preload;
        GameStartupTrace.Log($"{description} complete.");
    }

    /// <summary>
    /// Releases application-owned content when the active bootstrap is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance != this)
            return;

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        _contentModelCache?.Dispose();
        _contentAssets?.Dispose();
        Instance = null;
    }

    /// <summary>
    /// Persists the active user settings before application shutdown.
    /// </summary>
    private void OnApplicationQuit()
    {
        _userSettingsManager?.Save();
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
    /// Returns the application display manager.
    /// </summary>
    /// <returns>The active display manager.</returns>
    public DisplayManager GetDisplayManager()
    {
        return _displayManager;
    }

    /// <summary>
    /// Returns the input context controller.
    /// </summary>
    /// <returns>The active application input controller.</returns>
    public AppInputController GetInputController()
    {
        return inputController;
    }

    /// <summary>
    /// Returns the application cancel stack.
    /// </summary>
    /// <returns>The active cancel stack.</returns>
    public CancelStack GetCancelStack()
    {
        return _cancelStack;
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
    /// Gets the active external content pack.
    /// </summary>
    /// <returns>The active content pack.</returns>
    internal ContentPack GetContentPack()
    {
        return _contentPack;
    }

    /// <summary>
    /// Gets the application-owned external content assets.
    /// </summary>
    /// <returns>The active content asset store.</returns>
    internal ContentAssets GetContentAssets()
    {
        return _contentAssets;
    }
}
