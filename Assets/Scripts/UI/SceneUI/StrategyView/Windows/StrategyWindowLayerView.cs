using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(UIWindowManager))]
public sealed class StrategyWindowLayerView : MonoBehaviour, ICancelable
{
    private readonly Dictionary<int, MonoBehaviour> windowViews =
        new Dictionary<int, MonoBehaviour>();
    private readonly Dictionary<int, UIWindow> windowShells = new Dictionary<int, UIWindow>();
    private readonly HashSet<int> visibleWindows = new HashSet<int>();
    private StrategyUIRuntime uiRuntime;

    [SerializeField]
    private UIWindowManager windowManager;

    [SerializeField]
    private RectTransform normalWindowLayer;

    [SerializeField]
    private RectTransform modalWindowLayer;

    [SerializeField]
    private RawImage modalInputBlockerImage;

    [SerializeField]
    private PlanetSystemWindowView planetSystemWindowPrefab;

    [SerializeField]
    private FacilityWindowView facilityWindowPrefab;

    [SerializeField]
    private DefenseWindowView defenseWindowPrefab;

    [SerializeField]
    private FleetWindowView fleetWindowPrefab;

    [SerializeField]
    private MissionsWindowView missionsWindowPrefab;

    [SerializeField]
    private ConstructionWindowView constructionWindowPrefab;

    [SerializeField]
    private MissionCreateWindowView missionCreateWindowPrefab;

    [SerializeField]
    private StatusWindowView statusWindowPrefab;

    [SerializeField]
    private MessagesWindowView messagesWindowPrefab;

    [SerializeField]
    private ConfirmDialogWindowView confirmDialogWindowPrefab;

    [SerializeField]
    private FinderWindowView finderWindowPrefab;

    [SerializeField]
    private EncyclopediaWindowView encyclopediaWindowPrefab;

    [SerializeField]
    private int sectorLeftOpenThresholdOffset;

    [SerializeField]
    private int sectorRightOpenThresholdOffset;

    [SerializeField]
    private Vector2Int constructionWindowOffset;

    internal event System.Action<int, int> WindowButtonRequested;
    internal event System.Action<int> WindowFocused;
    internal event System.Action<int> WindowMoved;
    internal event System.Action<int, RectInt> WindowMovePreviewChanged;
    internal event System.Action<int> WindowMovePreviewEnded;
    internal event System.Action<int> WindowCloseRequested;
    internal event System.Action<UIWindow, PointerEventData, int, int> WindowContextRequested;
    internal event System.Action ModalWindowOpened;
    internal event System.Action WindowClosed;

    public void Initialize(StrategyUIRuntime uiRuntime)
    {
        if (uiRuntime == null)
            throw new System.ArgumentNullException(nameof(uiRuntime));

        this.uiRuntime = uiRuntime;
        UIWindowManager currentWindowManager = EnsureWindowManager();
        currentWindowManager.ModalOpened -= HandleWindowManagerModalOpened;
        currentWindowManager.WindowClosed -= HandleWindowManagerWindowClosed;
        currentWindowManager.ModalOpened += HandleWindowManagerModalOpened;
        currentWindowManager.WindowClosed += HandleWindowManagerWindowClosed;
    }

    public void BeginRender()
    {
        visibleWindows.Clear();
    }

    internal PlanetSystemWindowView OpenPlanetSystemWindow(int windowId, int x, int y)
    {
        return OpenWindow(windowId, planetSystemWindowPrefab, x, y, false, false, false, true);
    }

    internal FacilityWindowView OpenFacilityWindow(int windowId, int x, int y)
    {
        return OpenWindow(windowId, facilityWindowPrefab, x, y, false, true, true);
    }

    internal DefenseWindowView OpenDefenseWindow(int windowId, int x, int y)
    {
        return OpenWindow(windowId, defenseWindowPrefab, x, y, false, true, true);
    }

    internal FleetWindowView OpenFleetWindow(int windowId, int x, int y)
    {
        return OpenWindow(windowId, fleetWindowPrefab, x, y, false, true, true);
    }

    internal MissionsWindowView OpenMissionsWindow(int windowId, int x, int y)
    {
        return OpenWindow(windowId, missionsWindowPrefab, x, y, false, true, true);
    }

    internal ConstructionWindowView OpenConstructionWindow(int windowId, int x, int y)
    {
        return OpenWindow(windowId, constructionWindowPrefab, x, y, true, true, true);
    }

    internal MissionCreateWindowView OpenMissionCreateWindow(int windowId, int x, int y)
    {
        return OpenWindow(windowId, missionCreateWindowPrefab, x, y, true, true, false);
    }

    internal StatusWindowView OpenStatusWindow(int windowId, int x, int y)
    {
        return OpenWindow(windowId, statusWindowPrefab, x, y, true, true, false);
    }

    internal MessagesWindowView OpenMessagesWindow(int windowId, int x, int y)
    {
        return OpenWindow(windowId, messagesWindowPrefab, x, y, true, true, false);
    }

    internal ConfirmDialogWindowView OpenConfirmDialogWindow(int windowId, int x, int y)
    {
        return OpenWindow(windowId, confirmDialogWindowPrefab, x, y, true, true, false);
    }

    internal FinderWindowView OpenFinderWindow(int windowId, int x, int y)
    {
        return OpenWindow(windowId, finderWindowPrefab, x, y, true, true, false);
    }

    internal EncyclopediaWindowView OpenEncyclopediaWindow(int windowId, int x, int y)
    {
        return OpenWindow(windowId, encyclopediaWindowPrefab, x, y, true, true, false);
    }

    internal void ShowWindow(UIWindow window)
    {
        if (window == null)
            return;

        MonoBehaviour view = window.Content;
        if (view == null)
            return;

        visibleWindows.Add(window.Id);
        view.gameObject.SetActive(true);
        UpdateModalInputBlocker();
    }

    internal bool TryGetWindowView<TView>(UIWindow window, out TView typedView)
        where TView : class
    {
        typedView = null;
        return window != null
            && (window.TryGetContent(out typedView) || TryGetWindowView(window.Id, out typedView));
    }

    internal bool TryGetWindowView<TView>(int windowId, out TView typedView)
        where TView : class
    {
        typedView = null;
        if (!windowViews.TryGetValue(windowId, out MonoBehaviour view) || view == null)
            return false;

        typedView = view as TView;
        return typedView != null;
    }

    internal Vector2Int GetMissionCreateWindowSize()
    {
        return GetRequiredWindowPrefabSize(missionCreateWindowPrefab);
    }

    internal int SectorLeftOpenThresholdOffset => sectorLeftOpenThresholdOffset;
    internal int SectorRightOpenThresholdOffset => sectorRightOpenThresholdOffset;
    internal Vector2Int ConstructionWindowOffset => constructionWindowOffset;

    internal Vector2Int ClampPlanetWindowPosition(PlanetIcon icon, int sourceX, int sourceY)
    {
        return icon switch
        {
            PlanetIcon.Facility => ClampWindowPositionByPrefab(
                facilityWindowPrefab,
                sourceX,
                sourceY
            ),
            PlanetIcon.Defense => ClampWindowPositionByPrefab(
                defenseWindowPrefab,
                sourceX,
                sourceY
            ),
            PlanetIcon.Fleet => ClampWindowPositionByPrefab(fleetWindowPrefab, sourceX, sourceY),
            PlanetIcon.Mission => ClampWindowPositionByPrefab(
                missionsWindowPrefab,
                sourceX,
                sourceY
            ),
            _ => new Vector2Int(sourceX, sourceY),
        };
    }

    internal Vector2Int ClampConstructionWindowPosition(int sourceX, int sourceY)
    {
        return ClampWindowPositionByPrefab(constructionWindowPrefab, sourceX, sourceY);
    }

    private Vector2Int ClampWindowPositionByPrefab(MonoBehaviour prefab, int sourceX, int sourceY)
    {
        return ClampWindowPositionBySize(sourceX, sourceY, GetRequiredWindowPrefabSize(prefab));
    }

    private Vector2Int ClampWindowPositionBySize(int sourceX, int sourceY, Vector2Int windowSize)
    {
        Vector2Int surfaceSize = GetSurfaceSize();
        int maxX = Mathf.Max(0, surfaceSize.x - windowSize.x);
        int maxY = Mathf.Max(0, surfaceSize.y - windowSize.y);
        return new Vector2Int(Mathf.Clamp(sourceX, 0, maxX), Mathf.Clamp(sourceY, 0, maxY));
    }

    private Vector2Int GetRequiredWindowPrefabSize(MonoBehaviour prefab)
    {
        if (prefab == null)
            throw new MissingReferenceException("Window prefab is missing.");
        if (prefab.transform is not RectTransform rect)
            throw new MissingReferenceException($"{prefab.name} is missing RectTransform.");

        Vector2Int size = new Vector2Int(
            Mathf.RoundToInt(rect.sizeDelta.x),
            Mathf.RoundToInt(rect.sizeDelta.y)
        );
        if (size.x <= 0 || size.y <= 0)
            throw new MissingReferenceException($"{prefab.name} has no fixed prefab size.");

        return size;
    }

    internal Vector2Int GetSurfaceSize()
    {
        if (transform is not RectTransform rect)
            return Vector2Int.zero;

        int width = Mathf.RoundToInt(rect.sizeDelta.x);
        int height = Mathf.RoundToInt(rect.sizeDelta.y);
        if (width <= 0)
            width = Mathf.RoundToInt(rect.rect.width);
        if (height <= 0)
            height = Mathf.RoundToInt(rect.rect.height);

        return new Vector2Int(width, height);
    }

    internal UIWindow GetWindow(PointerEventData eventData)
    {
        return EnsureWindowManager().GetWindow(eventData);
    }

    internal UIWindow GetWindowById(int windowId)
    {
        return windowShells.TryGetValue(windowId, out UIWindow shell) ? shell : null;
    }

    public bool TryCancel()
    {
        return EnsureWindowManager().TryCancel();
    }

    internal bool HasModalWindow()
    {
        return HasVisibleModalWindow();
    }

    internal void CloseWindow(UIWindow window)
    {
        if (window != null)
            CloseWindow(window.Id);
    }

    private void CloseWindow(int windowId)
    {
        visibleWindows.Remove(windowId);
        if (windowShells.TryGetValue(windowId, out UIWindow shell) && shell != null)
        {
            UnbindWindowShell(shell);
            EnsureWindowManager()?.Unregister(shell);
        }

        windowShells.Remove(windowId);
        if (!windowViews.TryGetValue(windowId, out MonoBehaviour view))
        {
            UpdateModalInputBlocker();
            return;
        }

        windowViews.Remove(windowId);
        if (view != null)
            Destroy(view.gameObject);

        UpdateModalInputBlocker();
    }

    internal bool FocusWindow(UIWindow window)
    {
        return window != null && EnsureWindowManager().Focus(window);
    }

    public void EndRender()
    {
        foreach (KeyValuePair<int, MonoBehaviour> entry in windowViews)
        {
            if (entry.Value == null || visibleWindows.Contains(entry.Key))
                continue;

            entry.Value.gameObject.SetActive(false);
        }

        UpdateModalInputBlocker();
    }

    private TView OpenWindow<TView>(
        int windowId,
        TView prefab,
        int x,
        int y,
        bool modal,
        bool canFocus,
        bool canMove,
        bool registerBehind = false
    )
        where TView : MonoBehaviour
    {
        TView view = GetWindow(windowId, prefab, modal);
        InitializeWindowView(view);
        ConfigureWindowShell(windowId, x, y, modal, canFocus, canMove, registerBehind, view);
        visibleWindows.Add(windowId);
        view.gameObject.SetActive(true);
        UpdateModalInputBlocker();
        return view;
    }

    private TView GetWindow<TView>(int windowId, TView prefab, bool modal)
        where TView : MonoBehaviour
    {
        if (windowViews.TryGetValue(windowId, out MonoBehaviour view) && view != null)
        {
            TView typedView = CastWindowView<TView>(view);
            SetWindowParent(typedView, modal);
            return typedView;
        }

        if (prefab == null)
            throw new MissingReferenceException($"{typeof(TView).Name} prefab is missing.");

        TView instance = Instantiate(prefab, GetWindowParent(modal));
        instance.name = $"{prefab.name}{windowId}";
        instance.gameObject.SetActive(false);
        InitializeWindowView(instance);
        windowViews[windowId] = instance;
        return instance;
    }

    private static TView CastWindowView<TView>(MonoBehaviour view)
        where TView : MonoBehaviour
    {
        if (view is TView typedView)
            return typedView;

        throw new MissingReferenceException(
            $"{view.name} is not a {typeof(TView).Name} window view."
        );
    }

    private void InitializeWindowView(MonoBehaviour view)
    {
        if (uiRuntime != null && view is IStrategyUIRuntimeReceiver runtimeReceiver)
        {
            runtimeReceiver.Initialize(uiRuntime);
            return;
        }

        if (uiRuntime?.Context != null && view is IStrategyUIContextReceiver contextReceiver)
            contextReceiver.Initialize(uiRuntime.Context);
    }

    private void ConfigureWindowShell(
        int windowId,
        int x,
        int y,
        bool modal,
        bool canFocus,
        bool canMove,
        bool registerBehind,
        MonoBehaviour view
    )
    {
        if (view == null)
            return;

        UIWindow shell = GetWindowShell(windowId, view);
        shell.SetContent(view);
        Vector2Int size = GetRequiredWindowPrefabSize(view);
        shell.Configure(windowId, x, y, size.x, size.y, modal, canFocus, canMove);

        if (!windowShells.ContainsKey(windowId))
        {
            BindWindowShell(shell);
            windowShells[windowId] = shell;
            EnsureWindowManager()?.Register(shell, registerBehind);
        }
    }

    private UIWindow GetWindowShell(int windowId, MonoBehaviour view)
    {
        if (windowShells.TryGetValue(windowId, out UIWindow shell) && shell != null)
            return shell;

        shell = view.GetComponent<UIWindow>();
        if (shell == null)
            throw new MissingReferenceException($"{view.name} is missing UIWindow.");

        return shell;
    }

    private Transform GetWindowParent(bool modal)
    {
        if (modal && modalWindowLayer != null)
            return modalWindowLayer;

        if (!modal && normalWindowLayer != null)
            return normalWindowLayer;

        return transform;
    }

    private void SetWindowParent(MonoBehaviour view, bool modal)
    {
        Transform parent = GetWindowParent(modal);
        if (view != null && view.transform.parent != parent)
            view.transform.SetParent(parent, false);
    }

    private void UpdateModalInputBlocker()
    {
        if (modalInputBlockerImage == null)
            return;

        bool active = HasVisibleModalWindow();
        if (modalInputBlockerImage.gameObject.activeSelf != active)
            modalInputBlockerImage.gameObject.SetActive(active);

        if (active)
            modalInputBlockerImage.transform.SetAsFirstSibling();
    }

    private bool HasVisibleModalWindow()
    {
        foreach (KeyValuePair<int, UIWindow> entry in windowShells)
        {
            if (!visibleWindows.Contains(entry.Key) || entry.Value == null || !entry.Value.Modal)
                continue;

            if (
                windowViews.TryGetValue(entry.Key, out MonoBehaviour view)
                && view != null
                && view.gameObject.activeSelf
            )
                return true;
        }

        return false;
    }

    private void BindWindowShell(UIWindow shell)
    {
        shell.ButtonRequested += HandleWindowButtonRequested;
        shell.ContextRequested += HandleWindowContextRequested;
        shell.FocusRequested += HandleWindowFocusRequested;
        shell.MovePreviewChanged += HandleWindowMovePreviewChanged;
        shell.MovePreviewEnded += HandleWindowMovePreviewEnded;
        shell.Moved += HandleWindowMoved;
        shell.CloseRequested += HandleWindowCloseRequested;
    }

    private void UnbindWindowShell(UIWindow shell)
    {
        shell.ButtonRequested -= HandleWindowButtonRequested;
        shell.ContextRequested -= HandleWindowContextRequested;
        shell.FocusRequested -= HandleWindowFocusRequested;
        shell.MovePreviewChanged -= HandleWindowMovePreviewChanged;
        shell.MovePreviewEnded -= HandleWindowMovePreviewEnded;
        shell.Moved -= HandleWindowMoved;
        shell.CloseRequested -= HandleWindowCloseRequested;
    }

    private void HandleWindowButtonRequested(UIWindow window, int action)
    {
        if (window != null)
            WindowButtonRequested?.Invoke(window.Id, action);
    }

    private void HandleWindowCloseRequested(UIWindow window)
    {
        if (window != null)
            WindowCloseRequested?.Invoke(window.Id);
    }

    private void HandleWindowFocusRequested(UIWindow window)
    {
        if (window != null)
            WindowFocused?.Invoke(window.Id);
    }

    private void HandleWindowContextRequested(
        UIWindow window,
        PointerEventData eventData,
        int x,
        int y
    )
    {
        if (window != null)
            WindowContextRequested?.Invoke(window, eventData, x, y);
    }

    private void HandleWindowManagerModalOpened(UIWindow window)
    {
        ModalWindowOpened?.Invoke();
    }

    private void HandleWindowManagerWindowClosed(UIWindow window)
    {
        WindowClosed?.Invoke();
    }

    private void HandleWindowMovePreviewChanged(UIWindow window, RectInt bounds)
    {
        if (window != null)
            WindowMovePreviewChanged?.Invoke(window.Id, bounds);
    }

    private void HandleWindowMovePreviewEnded(UIWindow window)
    {
        if (window != null)
            WindowMovePreviewEnded?.Invoke(window.Id);
    }

    private void HandleWindowMoved(UIWindow window)
    {
        if (window == null)
            return;

        WindowMoved?.Invoke(window.Id);
    }

    private UIWindowManager EnsureWindowManager()
    {
        if (windowManager == null)
            windowManager = GetComponent<UIWindowManager>();

        if (windowManager == null)
            throw new MissingReferenceException("Windows is missing UIWindowManager.");

        return windowManager;
    }

    private void OnDestroy()
    {
        if (windowManager != null)
        {
            windowManager.ModalOpened -= HandleWindowManagerModalOpened;
            windowManager.WindowClosed -= HandleWindowManagerWindowClosed;
        }

        foreach (UIWindow shell in windowShells.Values)
        {
            if (shell != null)
                UnbindWindowShell(shell);
        }
    }
}
