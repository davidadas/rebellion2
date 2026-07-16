using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders one authored fleet-list row and emits local pointer gestures.
/// </summary>
[RequireComponent(typeof(UIPointerGestureRelay))]
public sealed class FleetListRowView : MonoBehaviour, IStrategyStatusDoubleClickTarget
{
    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private RawImage iconImage;

    [SerializeField]
    private RawImage enrouteOverlayImage;

    [SerializeField]
    private RawImage damagedOverlayImage;

    [SerializeField]
    private RawImage starfighterBadgeImage;

    [SerializeField]
    private RawImage troopBadgeImage;

    [SerializeField]
    private RawImage personnelBadgeImage;

    [SerializeField]
    private TextMeshProUGUI nameTextField;

    [SerializeField]
    private RawImage selectionImage;

    private UIPointerGestureRelay pointerGestures;

    /// <summary>
    /// Occurs when the control is double-clicked.
    /// </summary>
    public event Action<FleetListRowView, PointerEventData> DoubleClicked;

    /// <summary>
    /// Occurs when a pointer drop is received by the control.
    /// </summary>
    public event Action<FleetListRowView, PointerEventData> Dropped;

    /// <summary>
    /// Occurs when the control is pressed.
    /// </summary>
    public event Action<FleetListRowView, PointerEventData> Pressed;

    /// <summary>
    /// Occurs when the control is released.
    /// </summary>
    public event Action<FleetListRowView, PointerEventData> Released;

    /// <summary>
    /// Gets or sets the index.
    /// </summary>
    public int Index { get; private set; }

    /// <summary>
    /// Gets the name text field.
    /// </summary>
    internal TextMeshProUGUI NameTextField => nameTextField;

    /// <summary>
    /// Verifies the complete authored row hierarchy.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        pointerGestures = GetComponent<UIPointerGestureRelay>();
        pointerGestures.DoubleClicked += HandleDoubleClicked;
        pointerGestures.Dropped += HandleDropped;
        pointerGestures.Pressed += HandlePressed;
        pointerGestures.Released += HandleReleased;
    }

    /// <summary>
    /// Releases subscriptions from shared pointer gestures.
    /// </summary>
    private void OnDestroy()
    {
        if (pointerGestures == null)
            return;

        pointerGestures.DoubleClicked -= HandleDoubleClicked;
        pointerGestures.Dropped -= HandleDropped;
        pointerGestures.Pressed -= HandlePressed;
        pointerGestures.Released -= HandleReleased;
    }

    /// <summary>
    /// Applies one complete row presentation.
    /// </summary>
    /// <param name="data">The immutable row snapshot.</param>
    public void Render(FleetListRowRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        UILayout.SetImageTexture(selectionImage, data.SelectionTexture);
        UILayout.SetImageTexture(iconImage, data.IconTexture);
        UILayout.SetImageTexture(enrouteOverlayImage, data.EnrouteOverlayTexture);
        UILayout.SetImageTexture(damagedOverlayImage, data.DamagedOverlayTexture);
        UILayout.SetImageTexture(starfighterBadgeImage, data.StarfighterBadgeTexture);
        UILayout.SetImageTexture(troopBadgeImage, data.TroopBadgeTexture);
        UILayout.SetImageTexture(personnelBadgeImage, data.PersonnelBadgeTexture);
        UILayout.SetTextContent(nameTextField, data.Name);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Assigns this reusable row's visual index.
    /// </summary>
    /// <param name="index">The row index.</param>
    public void SetIndex(int index)
    {
        Index = index;
    }

    /// <summary>
    /// Forwards a normalized double-click gesture.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void HandleDoubleClicked(PointerEventData eventData)
    {
        DoubleClicked?.Invoke(this, eventData);
    }

    /// <summary>
    /// Forwards a normalized drop gesture.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void HandleDropped(PointerEventData eventData)
    {
        Dropped?.Invoke(this, eventData);
    }

    /// <summary>
    /// Forwards a normalized press gesture.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void HandlePressed(PointerEventData eventData)
    {
        Pressed?.Invoke(this, eventData);
    }

    /// <summary>
    /// Forwards a normalized release gesture.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void HandleReleased(PointerEventData eventData)
    {
        Released?.Invoke(this, eventData);
    }

    /// <summary>
    /// Reports whether a pointer event originated on this row's authored drag image.
    /// </summary>
    /// <param name="eventData">The pointer event to inspect.</param>
    /// <returns>True when the pointer is inside the fleet icon.</returns>
    internal bool ContainsDragSource(PointerEventData eventData)
    {
        if (eventData == null || !TryGetDragImage(out _, out RectTransform sourceTransform))
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            sourceTransform,
            eventData.position,
            eventData.pressEventCamera
        );
    }

    /// <summary>
    /// Reports whether one raycast target belongs to this row hierarchy.
    /// </summary>
    /// <param name="target">The raycast target.</param>
    /// <returns>True when the target belongs to this row.</returns>
    internal bool ContainsRaycastTarget(GameObject target)
    {
        return target != null && target.transform.IsChildOf(transform);
    }

    /// <summary>
    /// Returns the current fleet icon used for a drag preview.
    /// </summary>
    /// <param name="texture">Receives the rendered fleet icon.</param>
    /// <param name="sourceTransform">Receives the icon bounds.</param>
    /// <returns>True when the row currently has a drawable fleet icon.</returns>
    internal bool TryGetDragImage(out Texture texture, out RectTransform sourceTransform)
    {
        VerifyReferences();
        texture = iconImage.texture;
        sourceTransform = iconImage.rectTransform;
        return texture != null && iconImage.gameObject.activeInHierarchy;
    }

    /// <summary>
    /// Verifies every serialized row reference required at runtime.
    /// </summary>
    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (iconImage == null)
            throw new MissingReferenceException($"{name}/IconImage is missing.");
        if (enrouteOverlayImage == null)
            throw new MissingReferenceException($"{name}/EnrouteOverlayImage is missing.");
        if (damagedOverlayImage == null)
            throw new MissingReferenceException($"{name}/DamagedOverlayImage is missing.");
        if (starfighterBadgeImage == null)
            throw new MissingReferenceException($"{name}/StarfighterBadgeImage is missing.");
        if (troopBadgeImage == null)
            throw new MissingReferenceException($"{name}/TroopBadgeImage is missing.");
        if (personnelBadgeImage == null)
            throw new MissingReferenceException($"{name}/PersonnelBadgeImage is missing.");
        if (nameTextField == null)
            throw new MissingReferenceException($"{name}/NameTextField is missing.");
        if (selectionImage == null)
            throw new MissingReferenceException($"{name}/SelectionImage is missing.");

        hitAreaImage.enabled = true;
        hitAreaImage.raycastTarget = true;
        hitAreaImage.canvasRenderer.cullTransparentMesh = false;
    }
}
