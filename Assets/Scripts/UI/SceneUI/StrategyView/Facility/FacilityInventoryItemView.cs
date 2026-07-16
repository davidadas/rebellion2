using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders one facility inventory item and reports pointer interaction.
/// </summary>
[RequireComponent(typeof(UIPointerGestureRelay))]
public sealed class FacilityInventoryItemView : MonoBehaviour, IStrategyStatusDoubleClickTarget
{
    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private RawImage itemImage;

    [SerializeField]
    private RawImage selectionImage;

    private Texture defaultItemTexture;
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
    /// Gets or sets the index.
    /// </summary>
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
