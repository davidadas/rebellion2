using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Initializes the GameRuntime and wires up global systems and
/// dependencies. Ensures a single source of truth for application state and
/// global command handling.
/// </summary>
public sealed class AppBootstrap : MonoBehaviour
{
    private const string _defaultCursorAddress =
        "Application/Common/UI/ui_common_cursor_default_outlined";
    private const string _mainMenuPreloadID = "main-menu";
    private const string _strategyPreloadID = "strategy";
    private const string _cutscenePlayerResourcePath = "UI/CutscenePlayer";

    /// <summary>
    /// Gets the active application bootstrap instance.
    /// </summary>
    public static AppBootstrap Instance { get; private set; }

    /// <summary>
    /// Gets the storage failure awaiting acknowledgment in the active scene.
    /// </summary>
    public Rebellion.App.PersistenceFailureNotice PersistenceNotice { get; } = new();

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
    private GraphicsManager _graphicsManager;
    private Task _mainMenuContentTask;
    private Task _strategyContentTask;
    private GameRuntime _runtime;
    private SceneLoader _sceneLoader;
    private ContentPreloadManifest _strategyApplicationPreload;
    private UserSettingsManager _userSettingsManager;

    /// <summary>
    /// Raised before loading replaces the current scene or application content.
    /// Scene presentations use this boundary to stop rendering the outgoing scene.
    /// </summary>
    internal event Action LoadStarting;

    /// <summary>
    /// Gets whether the application scene loader is still completing a transition.
    /// </summary>
    internal bool IsSceneTransitionInProgress => _sceneLoader?.IsLoading == true;

    /// <summary>
    /// Ensures AppBootstrap exists. Creates a minimal bootstrap when a runtime scene has none.
    /// Only creates the root GameObject - normal Awake() handles initialization.
    /// IMPORTANT: Only call this at runtime scene entry points, not from random systems.
    /// </summary>
    /// <returns>The existing or newly created AppBootstrap instance.</returns>
    public static AppBootstrap EnsureExists()
    {
        return EnsureExists(null);
    }

    /// <summary>
    /// Reconstructs application services using explicitly prepared content.
    /// </summary>
    /// <param name="contentPack">The verified pack, or null for normal startup selection.</param>
    /// <returns>The initialized application bootstrap.</returns>
    internal static AppBootstrap EnsureExists(ContentPack contentPack)
    {
        if (Instance != null)
        {
            if (contentPack != null && !ReferenceEquals(Instance._contentPack, contentPack))
                throw new InvalidOperationException(
                    "Different application content is already active."
                );
            return Instance;
        }

        if (!Application.isPlaying)
            throw new InvalidOperationException(
                "AppBootstrap can only be created while the application is playing."
            );

        GameObject obj = new GameObject("AppBootstrap (Auto)");
        obj.SetActive(false);
        AppBootstrap bootstrap = obj.AddComponent<AppBootstrap>();
        bootstrap._contentPack = contentPack;
        obj.SetActive(true);
        if (Instance == bootstrap)
            return bootstrap;

        Destroy(obj);
        throw new System.InvalidOperationException(
            "AppBootstrap initialization failed. See the preceding exception for the root cause."
        );
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

        try
        {
            InitializeRuntimeCore();
        }
        catch
        {
            ReleaseRuntimeServices();
            Instance = null;
            throw;
        }
    }

    /// <summary>
    /// Creates the runtime services after the lifecycle guard has admitted initialization.
    /// </summary>
    private void InitializeRuntimeCore()
    {
        _cancelStack ??= new CancelStack();
        _sceneLoader = GetComponent<SceneLoader>();
        if (_sceneLoader == null)
            _sceneLoader = gameObject.AddComponent<SceneLoader>();
        if (_contentPack == null)
        {
            // Read the saved selection directly; the settings system loads after content.
            UserSettingsManager.TryReadContentSelection(
                out string selectedPackID,
                out string selectedScenarioID,
                out string[] disabledModIDs
            );
            _contentPack = ContentPackLoader.OpenActive(
                selectedPackID,
                selectedScenarioID,
                disabledModIDs
            );
        }
        _mainMenuApplicationPreload = ContentPackLoader.LoadApplicationPreloadManifest(
            _contentPack.FileResolver,
            _mainMenuPreloadID
        );
        _strategyApplicationPreload = ContentPackLoader.LoadApplicationPreloadManifest(
            _contentPack.FileResolver,
            _strategyPreloadID
        );
        _contentAssets = new ContentAssets(_contentPack.FileResolver);
        Texture2D cursorTexture =
            _contentAssets.GetCursor(_defaultCursorAddress)
            ?? throw new System.InvalidOperationException(
                $"Application cursor is missing: {_defaultCursorAddress}"
            );
        Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.Auto);
        _contentModelCache = new ContentModelCache(_contentAssets);
        GameLaunchContext.Reset(_contentPack);
        audioManager = AudioManager.EnsureExists(transform);
        audioManager.InitializeContent(_contentAssets);

        _cutsceneManager = GetComponent<CutsceneManager>();
        if (_cutsceneManager == null)
            _cutsceneManager = gameObject.AddComponent<CutsceneManager>();
        _cutsceneManager.InitializeContent(_contentAssets);
        _cutsceneManager.InitializeAudio(audioManager);

        GameObject cutscenePrefab = Resources.Load<GameObject>(_cutscenePlayerResourcePath);
        if (cutscenePrefab == null)
            throw new MissingReferenceException(
                $"Cutscene-player prefab is missing: {_cutscenePlayerResourcePath}"
            );
        _cutsceneManager.Initialize(cutscenePrefab);

        if (inputManager == null)
            inputManager = CreateInputManager();

        _displayManager = new DisplayManager();
        _graphicsManager = new GraphicsManager(transform);
        _userSettingsManager = new UserSettingsManager(
            audioManager,
            _displayManager,
            _graphicsManager,
            inputManager
        );
        _userSettingsManager.Load();
        FrameRateMonitor.EnsureExists(
            transform,
            () => _userSettingsManager?.Settings?.UserInterface?.ShowFrameRate == true
        );
        _runtime = new GameRuntime(
            _contentPack,
            getGameplaySettings: () => _userSettingsManager.Settings.Gameplay
        );
        _runtime.PersistenceFailed += HandlePersistenceFailure;
        if (inputController == null)
            inputController = CreateInputController();

        inputController?.Initialize(inputManager, _cancelStack, _runtime, LoadSaveAsync);
        LoadingScreenController.Prepare(this);
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
    /// <returns>A task representing the asynchronous operation.</returns>
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
        return _strategyContentTask;
    }

    /// <summary>
    /// Transitions to another application scene.
    /// </summary>
    /// <param name="sceneAddress">The Unity scene name to load.</param>
    /// <param name="loadFailed">The optional action invoked when scene loading fails.</param>
    /// <returns>True when the transition was accepted.</returns>
    internal bool LoadScene(string sceneAddress, Action<Exception> loadFailed = null)
    {
        return _sceneLoader.Load(sceneAddress, loadFailed);
    }

    /// <summary>
    /// Ends an interrupted shared load before or after its local Continue presentation has completed.
    /// Committed content stays active while the application returns to the main menu.
    /// </summary>
    /// <param name="interruption">The optional connection or synchronization failure to present.</param>
    /// <returns>True when an available application or active loading owner accepted recovery.</returns>
    internal static bool RecoverInterruptedMultiplayerLoad(Exception interruption = null)
    {
        return LoadingScreenController.RecoverInterruptedMultiplayerLoad(Instance, interruption);
    }

    /// <summary>
    /// Loads a save under the exact content identity recorded in its metadata.
    /// </summary>
    /// <param name="fileName">The save identifier to load.</param>
    /// <returns>True when loading or content reconstruction was started.</returns>
    internal bool LoadSave(string fileName)
    {
        if (_runtime?.HasCoordinatedPersistence == true)
            return false;
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        SaveGameManager saveGameManager = SaveGameManager.Instance;
        if (!System.IO.File.Exists(saveGameManager.GetSaveFilePath(fileName)))
            return false;

        if (LoadingScreenController.IsLoadInProgress)
            return false;

        LoadingScreenController.LoadSave(this, fileName);
        return true;
    }

    /// <summary>
    /// Awaits shared loading when coordinated, otherwise starts the saved content's scene transition.
    /// </summary>
    /// <param name="fileName">The save identifier to load.</param>
    /// <returns>True after coordinated loading completes or the local scene transition starts.</returns>
    internal Task<bool> LoadSaveAsync(string fileName)
    {
        return _runtime?.HasCoordinatedPersistence == true
            ? _runtime.LoadGameAsync(fileName)
            : Task.FromResult(LoadSave(fileName));
    }

    /// <summary>
    /// Reloads selected content and returns to the main menu without exiting the application.
    /// </summary>
    /// <param name="packID">The installed pack to select.</param>
    /// <param name="disabledModIDs">The mods excluded from the new selection.</param>
    /// <returns>True when a main-menu content transition was started.</returns>
    internal bool ReloadContent(string packID, string[] disabledModIDs)
    {
        if (
            string.IsNullOrWhiteSpace(packID)
            || _runtime.HasActiveGame
            || LoadingScreenController.IsLoadInProgress
            || IsSceneTransitionInProgress
        )
            return false;

        LoadingScreenController.ReloadContent(this, packID, disabledModIDs);
        return true;
    }

    /// <summary>
    /// Creates one content-loading owner whose lifetime can span bootstrap reconstruction.
    /// </summary>
    /// <returns>The operation holding the active content and committed preference snapshot.</returns>
    internal ContentLoadOperation CreateContentLoadOperation()
    {
        return new ContentLoadOperation(
            _contentPack,
            _userSettingsManager.Settings.Content,
            new[] { _mainMenuPreloadID, _strategyPreloadID }
        );
    }

    /// <summary>
    /// Stops the outgoing scene and runtime after the persistent loading presentation is visible.
    /// </summary>
    internal void SuspendForLoad()
    {
        LoadStarting?.Invoke();
        _runtime.EndGame();
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
    /// <returns>The requested content model cache.</returns>
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
        await PreloadContentAsync(
            _mainMenuApplicationPreload,
            _contentPack.GetPreloadManifest(_mainMenuPreloadID)
        );

        if (this == null || Instance != this)
            return;

        StartStrategyContentPreload();
    }

    /// <summary>
    /// Starts the shared strategy preload task when it has not already begun.
    /// </summary>
    private void StartStrategyContentPreload()
    {
        _strategyContentTask ??= PreloadContentAsync(
            _strategyApplicationPreload,
            _contentPack.GetPreloadManifest(_strategyPreloadID)
        );
    }

    /// <summary>
    /// Loads required content while allowing shutdown to release an abandoned asset store.
    /// </summary>
    /// <param name="applicationManifest">The application-owned assets.</param>
    /// <param name="packManifest">The selected pack's assets.</param>
    /// <returns>A task that completes after loading or after the owning bootstrap shuts down.</returns>
    private async Task PreloadContentAsync(
        ContentPreloadManifest applicationManifest,
        ContentPreloadManifest packManifest
    )
    {
        try
        {
            await Task.WhenAll(
                _contentAssets.PreloadAsync(applicationManifest),
                _contentAssets.PreloadAsync(packManifest)
            );
        }
        catch (ObjectDisposedException) when (this == null || Instance != this)
        {
            // Shutdown owns disposal; a superseded preload must not restart application recovery.
        }
    }

    /// <summary>
    /// Releases application-owned content when the active bootstrap is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance != this)
            return;

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        ReleaseRuntimeServices();
        Instance = null;
    }

    /// <summary>
    /// Releases managed runtime services after shutdown or incomplete initialization.
    /// </summary>
    private void ReleaseRuntimeServices()
    {
        _graphicsManager?.Dispose();
        _contentModelCache?.Dispose();
        _contentAssets?.Dispose();
        _contentModelCache = null;
        _contentAssets = null;
        _contentPack = null;
        _graphicsManager = null;
        _mainMenuApplicationPreload = null;
        _strategyApplicationPreload = null;
        if (_runtime != null)
            _runtime.PersistenceFailed -= HandlePersistenceFailure;
        _runtime = null;
        _mainMenuContentTask = null;
        _strategyContentTask = null;
    }

    /// <summary>
    /// Persists the active user settings before application shutdown.
    /// </summary>
    private void OnApplicationQuit()
    {
        _userSettingsManager?.Save();
    }

    /// <summary>
    /// Publishes a storage notice only for the runtime still owned by this application.
    /// </summary>
    /// <param name="runtime">The runtime that requested persistence.</param>
    /// <param name="operation">The operation that could not complete.</param>
    /// <param name="exception">The exception already recorded in the application log.</param>
    private void HandlePersistenceFailure(
        GameRuntime runtime,
        Rebellion.App.PersistenceOperation operation,
        Exception exception
    )
    {
        if (ReferenceEquals(_runtime, runtime))
            PersistenceNotice.Show(operation);
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
        audioManager = AudioManager.EnsureExists(transform);
        audioManager.InitializeContent(_contentAssets);
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
    /// Returns the application graphics manager.
    /// </summary>
    /// <returns>The active graphics manager.</returns>
    public GraphicsManager GetGraphicsManager()
    {
        return _graphicsManager;
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
