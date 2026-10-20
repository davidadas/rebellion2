using System;
using UnityEngine;

/// <summary>
/// Hosts the shared Options menu over the tactical scene and suspends battle input.
/// </summary>
public sealed class TacticalMenuController : MonoBehaviour, ICancelable
{
    [SerializeField]
    private OptionsMenuView menuPrefab;

    [SerializeField]
    private GameObject overlay;

    [SerializeField]
    private RectTransform windowLayer;

    [SerializeField]
    private UIWindowManager windowManager;

    [SerializeField]
    private Behaviour[] battleInput;

    private AppBootstrap _bootstrap;
    private AppInputController _input;
    private OptionsMenuController _menu;
    private bool[] _enabledBeforeMenu;
    private bool _inputSuspended;
    private bool _dirty;

    /// <summary>
    /// Gets whether the tactical scene is paused by its modal options menu.
    /// </summary>
    public bool IsOpen => _menu?.IsOpen == true;

    /// <summary>
    /// Connects this scene to the shared menu services after application initialization.
    /// </summary>
    /// <param name="bootstrap">The initialized application service owner.</param>
    public void Initialize(AppBootstrap bootstrap)
    {
        if (_menu != null)
            throw new InvalidOperationException("The tactical menu is already initialized.");
        _bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
        _enabledBeforeMenu = new bool[battleInput.Length];
        windowManager.SetContentSource(bootstrap.GetContentAssets());
        _menu = new OptionsMenuController(
            menuPrefab,
            windowLayer,
            windowManager,
            GetWindowPosition,
            windowManager.DestroyWindow,
            bootstrap,
            bootstrap.GetRuntime().SaveGame,
            LoadSavedGame,
            HandleMenuChanged,
            SaveGameManager.Instance
        );
        AttachInput();
    }

    /// <summary>
    /// Opens Options for an unhandled Escape, or delegates cancellation to the open menu.
    /// </summary>
    /// <returns>Whether this scene handled the cancellation request.</returns>
    public bool TryCancel()
    {
        if (_menu == null)
            return false;
        ToggleMenu();
        return true;
    }

    /// <summary>
    /// Restores scene input subscriptions when the initialized controller is re-enabled.
    /// </summary>
    private void OnEnable()
    {
        if (_menu != null)
            AttachInput();
    }

    /// <summary>
    /// Renders changed shared-menu data without rebuilding it every frame.
    /// </summary>
    private void Update()
    {
        if (!_dirty || _menu?.IsOpen != true)
            return;
        _dirty = false;
        _menu.RenderWindows();
    }

    /// <summary>
    /// Detaches scene shortcuts and closes its overlay when leaving the scene.
    /// </summary>
    private void OnDisable()
    {
        if (_input != null)
            _input.OptionsMenuRequested -= ToggleMenu;
        _input = null;
        _bootstrap?.GetCancelStack()?.Unregister(this);
        _menu?.Close();
    }

    /// <summary>
    /// Releases shared-menu subscriptions and unapplied settings owned by this scene.
    /// </summary>
    private void OnDestroy() => _menu?.Dispose();

    /// <summary>
    /// Uses the application's rebindable cancellation and dedicated menu actions.
    /// </summary>
    private void AttachInput()
    {
        _input = _bootstrap.GetInputController();
        _input.SetContext(InputContext.Tactical);
        _input.OptionsMenuRequested += ToggleMenu;
        _bootstrap.GetCancelStack().Register(this);
    }

    /// <summary>
    /// Opens the shared menu or lets it close confirmations, rebinding, and settings safely.
    /// </summary>
    private void ToggleMenu()
    {
        if (_menu.IsOpen)
            _menu.TryCancel();
        else
        {
            overlay.SetActive(true);
            Canvas.ForceUpdateCanvases();
            try
            {
                _menu.Open();
            }
            catch
            {
                overlay.SetActive(false);
                throw;
            }
        }
    }

    /// <summary>
    /// Centers the existing menu in the overlay's current source-coordinate surface.
    /// </summary>
    /// <returns>The top-left window position expected by the window manager.</returns>
    private Vector2Int GetWindowPosition()
    {
        Vector2 size = ((RectTransform)menuPrefab.transform).sizeDelta;
        return new Vector2Int(
            Mathf.RoundToInt((windowLayer.rect.width - size.x) * 0.5f),
            Mathf.RoundToInt((windowLayer.rect.height - size.y) * 0.5f)
        );
    }

    /// <summary>
    /// Synchronizes modal visibility and restores each battle input component's previous state.
    /// </summary>
    private void HandleMenuChanged()
    {
        _dirty = true;
        bool open = _menu.IsOpen;
        overlay.SetActive(open);
        if (_inputSuspended == open)
            return;
        for (int i = 0; i < battleInput.Length; i++)
        {
            if (!battleInput[i])
                continue;
            if (open)
                _enabledBeforeMenu[i] = battleInput[i].enabled;
            battleInput[i].enabled = open ? false : _enabledBeforeMenu[i];
        }
        _inputSuspended = open;
    }

    /// <summary>
    /// Loads a save through the established scene-launch flow instead of retaining stale battle models.
    /// </summary>
    /// <param name="fileName">The selected save identifier.</param>
    /// <returns>Whether the saved-game scene transition was requested.</returns>
    private bool LoadSavedGame(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;
        _bootstrap.GetRuntime()?.EndGame();
        GameLaunchContext.IsLoadGame = true;
        GameLaunchContext.SaveFileName = fileName;
        GameLaunchContext.PlayIntroCutscene = false;
        _bootstrap.LoadScene("StrategyView");
        return true;
    }
}
