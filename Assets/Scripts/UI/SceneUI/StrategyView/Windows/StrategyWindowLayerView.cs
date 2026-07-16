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
    private RectTransform modalWindowLayer;

    [SerializeField]
    private RawImage modalInputBlockerImage;

    [SerializeField]
    private RawImage modalBackgroundDimImage;

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
    private Vector2Int constructionWindowOffset;

    [SerializeField]
    private int itemDragStartDistance;

    /// <summary>
    /// Gets the authored planet-system window prefab.
    /// </summary>
    internal PlanetSystemWindowView PlanetSystemWindowPrefab => planetSystemWindowPrefab;

    /// <summary>
    /// Gets the authored facility window prefab.
    /// </summary>
    internal FacilityWindowView FacilityWindowPrefab => facilityWindowPrefab;

    /// <summary>
    /// Gets the authored defense window prefab.
    /// </summary>
    internal DefenseWindowView DefenseWindowPrefab => defenseWindowPrefab;

    /// <summary>
    /// Gets the authored fleet window prefab.
    /// </summary>
    internal FleetWindowView FleetWindowPrefab => fleetWindowPrefab;

    /// <summary>
    /// Gets the authored missions window prefab.
    /// </summary>
    internal MissionsWindowView MissionsWindowPrefab => missionsWindowPrefab;

    /// <summary>
    /// Gets the authored construction window prefab.
    /// </summary>
    internal ConstructionWindowView ConstructionWindowPrefab => constructionWindowPrefab;

    /// <summary>
    /// Gets the authored mission-creation window prefab.
    /// </summary>
    internal MissionCreateWindowView MissionCreateWindowPrefab => missionCreateWindowPrefab;

    /// <summary>
    /// Gets the authored status window prefab.
    /// </summary>
    internal StatusWindowView StatusWindowPrefab => statusWindowPrefab;

    /// <summary>
    /// Gets the authored advisor-report window prefab.
    /// </summary>
    internal AdvisorReportWindowView AdvisorReportWindowPrefab => advisorReportWindowPrefab;

    /// <summary>
    /// Gets the authored messages window prefab.
    /// </summary>
    internal MessagesWindowView MessagesWindowPrefab => messagesWindowPrefab;

    /// <summary>
    /// Gets the authored confirmation-dialog window prefab.
    /// </summary>
    internal ConfirmDialogWindowView ConfirmDialogWindowPrefab => confirmDialogWindowPrefab;

    /// <summary>
    /// Gets the authored battle-alert window prefab.
    /// </summary>
    internal BattleAlertWindowView BattleAlertWindowPrefab => battleAlertWindowPrefab;

    /// <summary>
    /// Gets the authored Finder window prefab.
    /// </summary>
    internal FinderWindowView FinderWindowPrefab => finderWindowPrefab;

    /// <summary>
    /// Gets the authored encyclopedia window prefab.
    /// </summary>
    internal EncyclopediaWindowView EncyclopediaWindowPrefab => encyclopediaWindowPrefab;

    /// <summary>
    /// Gets the authored offset from a facility window to its construction window.
    /// </summary>
    internal Vector2Int ConstructionWindowOffset => constructionWindowOffset;

    /// <summary>
    /// Gets the authored source-space distance required to start an item drag.
    /// </summary>
    internal int ItemDragStartDistance => itemDragStartDistance;

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
    /// <param name="active">Whether a modal window currently owns interaction.</param>
    internal void RenderModalState(bool active)
    {
        if (modalInputBlockerImage == null)
            return;

        SetModalImageActive(modalInputBlockerImage, active);
        SetModalImageActive(modalBackgroundDimImage, active);

        if (active)
            modalInputBlockerImage.transform.SetAsFirstSibling();
        if (active && modalBackgroundDimImage != null)
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
