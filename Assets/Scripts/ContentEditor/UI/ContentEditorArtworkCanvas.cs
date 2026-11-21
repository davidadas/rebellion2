using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Displays one output at a time and owns the workspace's fit, zoom, pan and layer gestures.
/// </summary>
public sealed class ContentEditorArtworkCanvas
    : MonoBehaviour,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IScrollHandler
{
    [SerializeField]
    private RectTransform artwork;

    [SerializeField]
    private RawImage backing;

    [SerializeField]
    private RawImage subject;

    [SerializeField]
    private RectTransform selection;

    private readonly List<RawImage> images = new();
    private ArtworkEffectPreviewFrame frame;
    private Canvas canvas;
    private int selected = -1;
    private int moving = -1;
    private Vector2 dragOrigin;
    private Vector2 panOrigin;
    private Rect dragBounds;
    private Vector2 movement;
    private Vector2 pan;
    private float zoom;
    private bool panning;
    private bool editable;

    internal event Action<int> LayerSelected;
    internal event Action<int, Vector2> LayerMoved;
    internal event Action ViewChanged;
    internal int ZoomPercent => Mathf.RoundToInt(Scale * ScreenScale * 100);
    internal bool IsFitted => zoom == 0f;

    private RectTransform Viewport => (RectTransform)transform;
    private float ScreenScale => canvas?.scaleFactor ?? 1f;
    private float Scale =>
        IsFitted
            ? Mathf.Max(
                0.01f,
                Mathf.Min(
                    (Viewport.rect.width - 48f) / Mathf.Max(1, frame.Size.x),
                    (Viewport.rect.height - 48f) / Mathf.Max(1, frame.Size.y)
                )
            )
            : zoom / ScreenScale;

    /// <summary>
    /// Reuses the canvas stack to show the selected output and its current composition.
    /// </summary>
    /// <param name="value">The layered image to show.</param>
    /// <param name="layerIndex">The selected layer in the draft.</param>
    /// <param name="allowEdit">Whether this is the editable draft.</param>
    /// <param name="resetView">Whether to refit the work area for a new output.</param>
    internal void Show(
        ArtworkEffectPreviewFrame value,
        int layerIndex,
        bool allowEdit,
        bool resetView
    )
    {
        canvas ??= GetComponentInParent<Canvas>();
        frame = value;
        editable = allowEdit;
        selected = layerIndex;
        moving = -1;
        panning = false;
        if (resetView)
        {
            zoom = 0f;
            pan = Vector2.zero;
        }
        SetTexture(backing, frame.BaseImage);
        SetTexture(subject, frame.SubjectOverlay);
        subject.gameObject.SetActive(frame.SubjectOverlay != null);
        for (int i = 0; i < frame.Layers.Count; i++)
        {
            if (i == images.Count)
            {
                RawImage image = new GameObject(
                    "Layer image",
                    typeof(RectTransform)
                ).AddComponent<RawImage>();
                image.transform.SetParent(artwork, false);
                image.raycastTarget = false;
                images.Add(image);
            }
            ArtworkEffectLayerPreview layer = frame.Layers[i];
            RawImage view = images[i];
            view.gameObject.SetActive(layer.Texture != null && !layer.Hidden && !layer.Locked);
            SetTexture(view, layer.Texture);
            SetBounds(view.rectTransform, layer.Bounds);
        }
        for (int i = frame.Layers.Count; i < images.Count; i++)
        {
            images[i].texture = null;
            images[i].gameObject.SetActive(false);
        }
        subject.transform.SetAsLastSibling();
        selection.SetAsLastSibling();
        UpdateSelection();
        PlaceArtwork();
    }

    /// <summary>
    /// Releases the canvas references before the controller disposes its preview textures.
    /// </summary>
    internal void Clear()
    {
        backing.texture = subject.texture = null;
        foreach (RawImage image in images)
            image.texture = null;
        frame = default;
    }

    /// <summary>
    /// Fits the complete card inside the current viewport.
    /// </summary>
    internal void Fit()
    {
        zoom = 0f;
        pan = Vector2.zero;
        PlaceArtwork();
    }

    /// <summary>
    /// Displays the artwork at one source pixel per screen pixel.
    /// </summary>
    internal void ActualPixels()
    {
        zoom = 1f;
        pan = Vector2.zero;
        PlaceArtwork();
    }

    /// <summary>
    /// Refits or constrains the current view when the desktop window changes size.
    /// </summary>
    [SuppressMessage("Roslynator", "RCS1213", Justification = "Invoked by Unity.")]
    private void OnRectTransformDimensionsChange()
    {
        if (artwork != null && frame.BaseImage != null)
            PlaceArtwork();
    }

    /// <summary>
    /// Applies a centered size and constrains panning so the card stays reachable.
    /// </summary>
    private void PlaceArtwork()
    {
        artwork.sizeDelta = (Vector2)frame.Size * Scale;
        Vector2 limit = Vector2.Max(
            Vector2.zero,
            (artwork.sizeDelta - Viewport.rect.size) * 0.5f + Vector2.one * 24f
        );
        pan = new Vector2(
            Mathf.Clamp(pan.x, -limit.x, limit.x),
            Mathf.Clamp(pan.y, -limit.y, limit.y)
        );
        artwork.anchoredPosition = pan;
        ViewChanged?.Invoke();
    }

    /// <summary>
    /// Zooms around the pointer so artwork remains under the same point while inspecting pixels.
    /// </summary>
    /// <param name="eventData">The desktop scroll gesture.</param>
    public void OnScroll(PointerEventData eventData)
    {
        if (frame.BaseImage == null || Mathf.Approximately(eventData.scrollDelta.y, 0f))
            return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            Viewport,
            eventData.position,
            eventData.enterEventCamera,
            out Vector2 pointer
        );
        float previous = Scale;
        zoom = Mathf.Clamp(
            previous * ScreenScale * Mathf.Pow(1.18f, eventData.scrollDelta.y),
            0.15f,
            12f
        );
        pan = pointer + (pan - pointer) * (Scale / previous);
        PlaceArtwork();
    }

    /// <summary>
    /// Selects the foremost visible, editable pixel under a direct click.
    /// </summary>
    /// <param name="eventData">The completed pointer click.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (
            !editable
            || eventData.dragging
            || eventData.button != PointerEventData.InputButton.Left
        )
            return;
        int index = Pick(eventData.position, eventData.pressEventCamera);
        if (index >= 0)
            LayerSelected?.Invoke(frame.Layers[index].Index);
    }

    /// <summary>
    /// Starts moving the explicitly selected layer or panning the output.
    /// </summary>
    /// <param name="eventData">The start of a pointer drag.</param>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (frame.BaseImage == null)
            return;
        panning =
            eventData.button != PointerEventData.InputButton.Left || Input.GetKey(KeyCode.Space);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            panning ? Viewport : artwork,
            eventData.pressPosition,
            eventData.pressEventCamera,
            out dragOrigin
        );
        if (panning)
        {
            panOrigin = pan;
            return;
        }
        if (!editable)
            return;
        moving = FindLayer(selected);
        if (moving < 0 || frame.Layers[moving].Hidden || frame.Layers[moving].Locked)
            moving = -1;
        if (moving < 0)
            return;
        dragBounds = frame.Layers[moving].Bounds;
        movement = Vector2.zero;
    }

    /// <summary>
    /// Tracks the pointer without rebuilding the layer list or creating undo entries mid-gesture.
    /// </summary>
    /// <param name="eventData">The pointer's live position.</param>
    public void OnDrag(PointerEventData eventData)
    {
        if (!panning && moving < 0)
            return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            panning ? Viewport : artwork,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 position
        );
        Vector2 delta = position - dragOrigin;
        if (panning)
        {
            pan = panOrigin + delta;
            PlaceArtwork();
            return;
        }
        Vector2 pixels = delta / artwork.rect.size * (Vector2)frame.Size;
        movement = (Vector2)Vector2Int.RoundToInt(pixels) / (Vector2)frame.Size;
        Rect placement = new(dragBounds.position + movement, dragBounds.size);
        SetBounds(images[moving].rectTransform, placement);
        SetBounds(selection, placement);
    }

    /// <summary>
    /// Commits one layer movement when the drag finishes.
    /// </summary>
    /// <param name="eventData">The completed pointer gesture.</param>
    public void OnEndDrag(PointerEventData eventData)
    {
        panning = false;
        if (moving < 0)
            return;
        int index = frame.Layers[moving].Index;
        moving = -1;
        if (movement != Vector2.zero)
            LayerMoved?.Invoke(index, movement);
    }

    /// <summary>
    /// Finds a selectable visible effect at the pointer's position.
    /// </summary>
    /// <param name="position">The screen point.</param>
    /// <param name="camera">The event camera, when present.</param>
    /// <returns>The rendered layer index or minus one.</returns>
    private int Pick(Vector2 position, Camera camera)
    {
        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                artwork,
                position,
                camera,
                out Vector2 local
            ) || !artwork.rect.Contains(local)
        )
            return -1;
        Vector2 point = (local - artwork.rect.min) / artwork.rect.size;
        for (int i = frame.Layers.Count - 1; i >= 0; i--)
        {
            var layer = frame.Layers[i];
            if (
                layer.Hidden
                || layer.Locked
                || layer.Texture == null
                || !layer.Bounds.Contains(point)
            )
                continue;
            Vector2 uv = (point - layer.Bounds.min) / layer.Bounds.size;
            if (layer.Texture.GetPixelBilinear(uv.x, uv.y).a > 1f / 255f)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Maps a draft layer index to the texture currently used by this output.
    /// </summary>
    /// <param name="index">The selected draft layer.</param>
    /// <returns>The rendered layer index or minus one.</returns>
    private int FindLayer(int index)
    {
        for (int i = 0; i < frame.Layers.Count; i++)
            if (frame.Layers[i].Index == index && frame.Layers[i].Texture != null)
                return i;
        return -1;
    }

    /// <summary>
    /// Positions the outline on the selected layer's actual rendered bounds.
    /// </summary>
    private void UpdateSelection()
    {
        int index = editable ? FindLayer(selected) : -1;
        bool visible = index >= 0 && !frame.Layers[index].Locked && !frame.Layers[index].Hidden;
        selection.gameObject.SetActive(visible);
        if (visible)
            SetBounds(selection, frame.Layers[index].Bounds);
    }

    /// <summary>
    /// Displays frame-owned art at its actual pixel edges while inspecting the output.
    /// </summary>
    /// <param name="view">The canvas layer.</param>
    /// <param name="texture">The texture owned by the editable frame.</param>
    private static void SetTexture(RawImage view, Texture2D texture)
    {
        if (texture != null)
            texture.filterMode = FilterMode.Point;
        view.texture = texture;
    }

    /// <summary>
    /// Places a rect in output-relative coordinates.
    /// </summary>
    /// <param name="rect">The canvas graphic or selection outline.</param>
    /// <param name="bounds">The normalized image bounds.</param>
    private static void SetBounds(RectTransform rect, Rect bounds)
    {
        rect.anchorMin = bounds.min;
        rect.anchorMax = bounds.max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
