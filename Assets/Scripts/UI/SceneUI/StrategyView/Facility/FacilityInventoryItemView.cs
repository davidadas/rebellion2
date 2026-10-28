using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders one facility inventory item and reports pointer interaction.
/// </summary>
[RequireComponent(typeof(UIPointerGestureRelay))]
public sealed class FacilityInventoryItemView
    : MonoBehaviour,
        IStrategyStatusDoubleClickTarget,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
{
    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private RawImage itemImage;

    [SerializeField]
    private RawImage selectionImage;

    private Texture defaultItemTexture;
    private bool canDrag;
    private UIPointerGestureRelay pointerGestures;

    /// <summary>
    /// Occurs when the control is pressed.
    /// </summary>
    public event Action<FacilityInventoryItemView, PointerEventData> Pressed;

    /// <summary>
    /// Occurs when the control is released.
    /// </summary>
    public event Action<FacilityInventoryItemView, PointerEventData> Released;

    /// <summary>
    /// Occurs when a pointer drop is received by the control.
    /// </summary>
    public event Action<FacilityInventoryItemView, PointerEventData> Dropped;

    /// <summary>
    /// Occurs when the control is double-clicked.
    /// </summary>
    public event Action<FacilityInventoryItemView, PointerEventData> DoubleClicked;

    /// <summary>
    /// Occurs while an item drag advances.
    /// </summary>
    public event Action<FacilityInventoryItemView, PointerEventData> Dragged;

    /// <summary>
    /// Occurs when an item drag ends.
    /// </summary>
    public event Action<FacilityInventoryItemView, PointerEventData> DragEnded;

    public int Index { get; private set; }

    /// <summary>
    /// Applies one inventory item presentation snapshot.
    /// </summary>
    /// <param name="index">The inventory index.</param>
    /// <param name="data">The inventory item presentation.</param>
    /// <param name="selectionTexture">The selected item frame.</param>
    /// <param name="frame">The source-space item bounds.</param>
    public void Render(
        int index,
        FacilityInventoryItemRenderData data,
        Texture selectionTexture,
        RectInt frame
    )
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        defaultItemTexture ??= itemImage.texture;
        Index = index;
        canDrag = data.CanDrag;
        UILayout.SetSourceRect(
            transform as RectTransform,
            frame.x,
            frame.y,
            frame.width,
            frame.height
        );
        UILayout.SetSourceRect(hitAreaImage.rectTransform, 0, 0, frame.width, frame.height);
        UILayout.SetCenteredImage(
            itemImage,
            data.Texture ?? defaultItemTexture,
            new RectInt(0, 0, frame.width, frame.height)
        );
        SetImage(
            selectionImage,
            data.Selected ? selectionTexture : null,
            frame.width,
            frame.height
        );
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Keeps this item as the active Unity drag source after its press created a candidate.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnBeginDrag(PointerEventData eventData) { }

    /// <summary>
    /// Reports pointer movement for an active destination-change drag.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnDrag(PointerEventData eventData)
    {
        if (canDrag && eventData?.button == PointerEventData.InputButton.Left)
            Dragged?.Invoke(this, eventData);
    }

    /// <summary>
    /// Reports the end of an active destination-change drag.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData?.button == PointerEventData.InputButton.Left)
            DragEnded?.Invoke(this, eventData);
    }

    /// <summary>
    /// Reports whether a pointer press began inside the rendered building image.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    /// <returns>True when the pointer lies inside a draggable building image.</returns>
    internal bool ContainsDragSource(PointerEventData eventData)
    {
        if (eventData == null || !TryGetDragImage(out _, out RectTransform imageTransform))
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            imageTransform,
            eventData.position,
            eventData.pressEventCamera
        );
    }

    /// <summary>
    /// Gets the rendered image used for a building drag preview.
    /// </summary>
    /// <param name="texture">Receives the rendered building texture.</param>
    /// <param name="imageTransform">Receives the rendered image bounds.</param>
    /// <returns>True when the item is draggable and has a visible image.</returns>
    internal bool TryGetDragImage(out Texture texture, out RectTransform imageTransform)
    {
        VerifyReferences();
        texture = itemImage.texture;
        imageTransform = itemImage.rectTransform;
        return canDrag
            && texture != null
            && itemImage.enabled
            && itemImage.gameObject.activeInHierarchy;
    }

    /// <summary>
    /// Verifies authored references and binds shared pointer gestures.
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
    /// Validates the item's authored child references.
    /// </summary>
    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (itemImage == null)
            throw new MissingReferenceException($"{name}/ItemImage is missing.");
        if (selectionImage == null)
            throw new MissingReferenceException($"{name}/SelectionImage is missing.");
    }

    /// <summary>
    /// Applies an optional selection frame within authored item bounds.
    /// </summary>
    /// <param name="image">The selection image.</param>
    /// <param name="texture">The selection texture.</param>
    /// <param name="width">The item width.</param>
    /// <param name="height">The item height.</param>
    private static void SetImage(RawImage image, Texture texture, int width, int height)
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = false;
        if (texture != null)
            UILayout.SetSourceRect(image.rectTransform, 0, 0, width, height);
    }
}
