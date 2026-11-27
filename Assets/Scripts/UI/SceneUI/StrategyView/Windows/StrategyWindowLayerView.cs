using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Exposes the authored strategy-window hierarchy, prefabs, and modal presentation.
/// </summary>
public sealed class StrategyWindowLayerView : MonoBehaviour
{
    [SerializeField]
    private RectTransform modelessWindowLayer;

    [SerializeField]
    private RectTransform idleBarLayer;

    [SerializeField]
    private RectTransform modalWindowLayer;

    [SerializeField]
    private RawImage modalInputBlockerImage;

    [SerializeField]
    private RawImage modalBackgroundDimImage;

    [SerializeField]
    private PlanetSectorWindowView planetSectorWindowPrefab;

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
    private AdvisorReportWindowView advisorReportWindowPrefab;

    [SerializeField]
    private MessagesWindowView messagesWindowPrefab;

    [SerializeField]
    private ConfirmDialogWindowView confirmDialogWindowPrefab;

    [SerializeField]
    private BattleAlertWindowView battleAlertWindowPrefab;

    [SerializeField]
    private FinderWindowView finderWindowPrefab;

    [SerializeField]
    private EncyclopediaWindowView encyclopediaWindowPrefab;

    [SerializeField]
    private OptionsMenuView _optionsMenuWindowPrefab;

    [SerializeField]
    private Vector2Int constructionWindowOffset;

    [SerializeField]
    private int itemDragStartDistance;

    private PlanetWindowHeaderLayout planetWindowHeaderLayout;
    private IContentAssetSource planetWindowHeaderAssets;

    internal PlanetSectorWindowView PlanetSectorWindowPrefab => planetSectorWindowPrefab;

    internal FacilityWindowView FacilityWindowPrefab => facilityWindowPrefab;

    internal DefenseWindowView DefenseWindowPrefab => defenseWindowPrefab;

    internal FleetWindowView FleetWindowPrefab => fleetWindowPrefab;

    internal MissionsWindowView MissionsWindowPrefab => missionsWindowPrefab;

    internal ConstructionWindowView ConstructionWindowPrefab => constructionWindowPrefab;

    internal MissionCreateWindowView MissionCreateWindowPrefab => missionCreateWindowPrefab;

    internal StatusWindowView StatusWindowPrefab => statusWindowPrefab;

    internal AdvisorReportWindowView AdvisorReportWindowPrefab => advisorReportWindowPrefab;

    internal MessagesWindowView MessagesWindowPrefab => messagesWindowPrefab;

    internal ConfirmDialogWindowView ConfirmDialogWindowPrefab => confirmDialogWindowPrefab;

    internal BattleAlertWindowView BattleAlertWindowPrefab => battleAlertWindowPrefab;

    internal FinderWindowView FinderWindowPrefab => finderWindowPrefab;

    internal EncyclopediaWindowView EncyclopediaWindowPrefab => encyclopediaWindowPrefab;

    internal OptionsMenuView OptionsMenuWindowPrefab => _optionsMenuWindowPrefab;

    internal Vector2Int ConstructionWindowOffset => constructionWindowOffset;

    internal int ItemDragStartDistance => itemDragStartDistance;

    /// <summary>
    /// Sets the active pack's presentation for newly opened planet windows.
    /// </summary>
    /// <param name="layout">The pack's shared header layout.</param>
    /// <param name="assets">The active pack's content source.</param>
    internal void SetPlanetWindowHeader(PlanetWindowHeaderLayout layout, IContentAssetSource assets)
    {
        planetWindowHeaderLayout = layout;
        planetWindowHeaderAssets = assets;
    }

    /// <summary>
    /// Opens one modeless planet window and applies the shared header to its instance.
    /// </summary>
    /// <typeparam name="TView">The authored planet window's view type.</typeparam>
    /// <param name="manager">The manager that registers the new window.</param>
    /// <param name="prefab">The authored planet window.</param>
    /// <param name="hierarchyName">The instance's semantic name.</param>
    /// <param name="position">The requested source position.</param>
    /// <param name="view">Receives the instantiated planet view.</param>
    /// <returns>The registered window shell.</returns>
    internal UIWindow CreatePlanetWindow<TView>(
        UIWindowManager manager,
        TView prefab,
        string hierarchyName,
        Vector2Int position,
        out TView view
    )
        where TView : MonoBehaviour
    {
        UIWindow window = manager.CreateWindow(
            prefab,
            GetWindowParent(false),
            hierarchyName,
            position.x,
            position.y,
            GetWindowSize(prefab),
            modal: false,
            canFocus: true,
            canMove: true,
            behind: false,
            out view
        );
        if (planetWindowHeaderLayout != null)
            window
                .GetComponent<PlanetWindowHeader>()
                .Apply(planetWindowHeaderLayout, planetWindowHeaderAssets);
        return window;
    }

    /// <summary>
    /// Gets the authored parent for a window modality.
    /// </summary>
    /// <param name="modal">Whether the window belongs to the modal layer.</param>
    /// <returns>The matching authored parent, or this layer when the reference is absent.</returns>
    internal Transform GetWindowParent(bool modal)
    {
        if (modal && modalWindowLayer != null)
            return modalWindowLayer;
        if (!modal && modelessWindowLayer != null)
            return modelessWindowLayer;

        return transform;
    }

    /// <summary>
    /// Places the idle bar immediately above or below modeless strategy windows.
    /// </summary>
    /// <param name="aboveModelessWindows">
    /// Whether the idle bar should remain visible above modeless windows.
    /// </param>
    internal void RenderIdleBarLayer(bool aboveModelessWindows)
    {
        if (idleBarLayer == null || modelessWindowLayer == null)
            return;

        bool isAboveModelessWindows =
            idleBarLayer.GetSiblingIndex() > modelessWindowLayer.GetSiblingIndex();
        if (isAboveModelessWindows != aboveModelessWindows)
            idleBarLayer.SetSiblingIndex(modelessWindowLayer.GetSiblingIndex());
    }

    /// <summary>
    /// Gets the current strategy-window surface size in source-space pixels.
    /// </summary>
    /// <returns>The current surface dimensions, or zero when no rectangle is available.</returns>
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

    /// <summary>
    /// Reads and validates an authored window view's fixed source-space size.
    /// </summary>
    /// <param name="view">The authored window prefab or instantiated view.</param>
    /// <returns>The validated source-space size.</returns>
    internal Vector2Int GetWindowSize(MonoBehaviour view)
    {
        if (view == null)
            throw new MissingReferenceException("Window view is missing.");
        if (view.transform is not RectTransform rect)
            throw new MissingReferenceException($"{view.name} is missing RectTransform.");

        Vector2Int size = new Vector2Int(
            Mathf.RoundToInt(rect.sizeDelta.x),
            Mathf.RoundToInt(rect.sizeDelta.y)
        );
        if (size.x <= 0 || size.y <= 0)
            throw new MissingReferenceException($"{view.name} has no fixed prefab size.");

        return size;
    }

    /// <summary>
    /// Renders the modal input blocker and background dimmer for the current registry state.
    /// </summary>
    /// <param name="blockInput">Whether the modal layer owns interaction.</param>
    /// <param name="dimBackground">Whether the underlying strategy screen is dimmed.</param>
    internal void RenderModalState(bool blockInput, bool dimBackground)
    {
        if (modalInputBlockerImage == null)
            return;

        SetModalImageActive(modalInputBlockerImage, blockInput);
        SetModalImageActive(modalBackgroundDimImage, dimBackground);

        if (blockInput)
            modalInputBlockerImage.transform.SetAsFirstSibling();
        if (dimBackground && modalBackgroundDimImage != null)
            modalBackgroundDimImage.transform.SetSiblingIndex(1);
    }

    /// <summary>
    /// Updates one optional modal presentation image without redundant activation changes.
    /// </summary>
    /// <param name="image">The authored modal presentation image.</param>
    /// <param name="active">Whether the image should be active.</param>
    private static void SetModalImageActive(RawImage image, bool active)
    {
        if (image != null && image.gameObject.activeSelf != active)
            image.gameObject.SetActive(active);
    }
}
