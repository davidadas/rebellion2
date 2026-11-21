using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Outlines one rendered image on hover and opens its exact authored field on double click.
/// </summary>
[RequireComponent(typeof(RawImage))]
public sealed class ContentEditorArtworkEditTarget
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        ICanvasRaycastFilter
{
    private static readonly Color _accentColor = new(1f, 0.72f, 0.15f, 1f);

    private Action<ContentPackEditorField> opened;
    private Vector2 frameAnchorMax = Vector2.one;
    private Vector2 frameAnchorMin = Vector2.zero;
    private Vector2 hoverFrameAnchorMax = Vector2.one;
    private Vector2 hoverFrameAnchorMin = Vector2.zero;
    private RectTransform hoverFrame;
    private ContentPackEditorField hoverField;
    private Texture hoverTexture;
    private RectTransform interactionArea;
    private bool? originalRaycastTarget;
    private bool hovered;
    private bool requiresPress;
    private bool moving;
    private Vector2 movementOrigin;
    private IReadOnlyList<Vector2Int> placementOrigins = Array.Empty<Vector2Int>();
    private IReadOnlyList<ContentEditorArtworkPlacement> placements =
        Array.Empty<ContentEditorArtworkPlacement>();
    private ContentPackEditorField releasedField;
    private Texture releasedTexture;

    /// <summary>
    /// Gets the exact authored field represented by the hover frame.
    /// </summary>
    internal ContentPackEditorField Field =>
        hovered && !requiresPress ? hoverField ?? releasedField : releasedField ?? hoverField;

    /// <summary>
    /// Gets every authored image state represented by this rendered target.
    /// </summary>
    internal IReadOnlyList<ContentPackEditorField> Fields =>
        new[] { releasedField, hoverField }.Where(value => value != null).Distinct().ToArray();

    /// <summary>
    /// Gets whether this image has authored source coordinates that can be moved.
    /// </summary>
    internal bool CanMove => placements.Count > 0;

    /// <summary>
    /// Configures the represented image field and its navigation callback.
    /// </summary>
    /// <param name="value">The exact field supplying the rendered texture.</param>
    /// <param name="onOpened">The action invoked after an intentional double click.</param>
    /// <param name="contentBounds">The texture-space bounds containing visible pixels.</param>
    public void Bind(
        ContentPackEditorField value,
        Action<ContentPackEditorField> onOpened,
        RectInt contentBounds
    )
    {
        Bind(value, value, onOpened, contentBounds, contentBounds, null, null, null);
    }

    /// <summary>
    /// Configures the released and hover-state fields represented by one rendered image.
    /// </summary>
    /// <param name="releasedValue">The field supplying the released texture.</param>
    /// <param name="hoverValue">The field supplying the hover texture, or the released field when unchanged.</param>
    /// <param name="onOpened">The action invoked after an intentional double click.</param>
    /// <param name="releasedContentBounds">The released texture's visible-pixel bounds.</param>
    /// <param name="hoverContentBounds">The hover texture's visible-pixel bounds.</param>
    /// <param name="previewHoverTexture">The hover texture to exercise when the gameplay control uses a separate hit area.</param>
    /// <param name="placementBindings">The authored coordinate pairs moved with this image.</param>
    /// <param name="pointerInteractionArea">The optional gameplay hit area that may begin interaction with this image.</param>
    /// <param name="requiresPress">Whether the alternate texture is shown by the gameplay press handler.</param>
    internal void Bind(
        ContentPackEditorField releasedValue,
        ContentPackEditorField hoverValue,
        Action<ContentPackEditorField> onOpened,
        RectInt releasedContentBounds,
        RectInt hoverContentBounds,
        Texture previewHoverTexture,
        IReadOnlyList<ContentEditorArtworkPlacement> placementBindings = null,
        RectTransform pointerInteractionArea = null,
        bool requiresPress = false
    )
    {
        if (releasedValue == null && hoverValue == null)
            throw new ArgumentNullException(nameof(releasedValue));
        releasedField = releasedValue ?? hoverValue;
        hoverField = hoverValue ?? releasedValue;
        opened = onOpened ?? throw new ArgumentNullException(nameof(onOpened));
        RawImage image = GetComponent<RawImage>();
        originalRaycastTarget ??= image.raycastTarget;
        image.raycastTarget = true;
        releasedTexture = image.texture;
        hoverTexture = previewHoverTexture;
        interactionArea = pointerInteractionArea;
        this.requiresPress = requiresPress;
        placements = placementBindings ?? Array.Empty<ContentEditorArtworkPlacement>();
        placementOrigins = Array.Empty<Vector2Int>();
        moving = false;
        CalculateFrameAnchors(image, releasedContentBounds, out frameAnchorMin, out frameAnchorMax);
        CalculateFrameAnchors(
            image,
            hoverContentBounds,
            out hoverFrameAnchorMin,
            out hoverFrameAnchorMax
        );
        hoverFrame ??= CreateHoverFrame(transform);
        hoverFrame.gameObject.SetActive(false);
        hovered = false;
    }

    /// <summary>
    /// Captures the rendered and authored origins for one click-drag placement gesture.
    /// </summary>
    /// <returns>True when the image has valid movable coordinate fields.</returns>
    internal bool BeginMove()
    {
        if (!CanMove)
            return false;

        List<Vector2Int> origins = new(placements.Count);
        foreach (ContentEditorArtworkPlacement placement in placements)
        {
            if (
                !int.TryParse(
                    placement.XField.Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int x
                )
                || !int.TryParse(
                    placement.YField.Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int y
                )
            )
                return false;
            origins.Add(new Vector2Int(x, y));
        }

        movementOrigin = ((RectTransform)transform).anchoredPosition;
        placementOrigins = origins;
        moving = true;
        return true;
    }

    /// <summary>
    /// Moves the rendered image by a source-space delta without committing project data.
    /// </summary>
    /// <param name="delta">The top-left source-coordinate movement from the grab point.</param>
    internal void PreviewMove(Vector2Int delta)
    {
        if (!moving)
            return;
        ((RectTransform)transform).anchoredPosition =
            movementOrigin + new Vector2(delta.x, -delta.y);
    }

    /// <summary>
    /// Completes one preview move and returns every authored coordinate that must be committed.
    /// </summary>
    /// <param name="delta">The final top-left source-coordinate movement.</param>
    /// <returns>The distinct authored coordinate replacements.</returns>
    internal IReadOnlyList<ContentEditorArtworkPlacementChange> CompleteMove(Vector2Int delta)
    {
        if (!moving)
            return Array.Empty<ContentEditorArtworkPlacementChange>();

        PreviewMove(delta);
        List<ContentEditorArtworkPlacementChange> changes = new(placements.Count * 2);
        for (int index = 0; index < placements.Count; index++)
        {
            ContentEditorArtworkPlacement placement = placements[index];
            Vector2Int origin = placementOrigins[index];
            AddPlacementChange(changes, placement.XField, checked(origin.x + delta.x));
            AddPlacementChange(changes, placement.YField, checked(origin.y + delta.y));
        }
        moving = false;
        placementOrigins = Array.Empty<Vector2Int>();
        return changes;
    }

    /// <summary>
    /// Restores the rendered origin when an in-progress placement gesture is interrupted.
    /// </summary>
    internal void CancelMove()
    {
        if (moving)
            ((RectTransform)transform).anchoredPosition = movementOrigin;
        moving = false;
        placementOrigins = Array.Empty<Vector2Int>();
    }

    /// <summary>
    /// Shows an unfilled frame while the pointer is over the represented image.
    /// </summary>
    /// <param name="eventData">The pointer-enter event.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        ContentEditorArtworkEditTarget enteredTarget =
            eventData?.pointerEnter == null
                ? this
                : eventData.pointerEnter.GetComponentInParent<ContentEditorArtworkEditTarget>();
        SetHovered(enteredTarget == this);
    }

    /// <summary>
    /// Hides the frame after the pointer leaves the represented image.
    /// </summary>
    /// <param name="eventData">The pointer-exit event.</param>
    public void OnPointerExit(PointerEventData eventData) => SetHovered(false);

    /// <summary>
    /// Opens the exact authored image field after an intentional primary-button double click.
    /// </summary>
    /// <param name="eventData">The completed pointer gesture.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        TryOpen(eventData);
    }

    /// <summary>
    /// Routes a grab through the preview even when an image has its own gameplay drag handler.
    /// </summary>
    /// <param name="eventData">The initial drag event.</param>
    public void OnBeginDrag(PointerEventData eventData) =>
        GetComponentInParent<ContentEditorArtworkWindowPreview>()?.OnBeginDrag(eventData);

    /// <summary>
    /// Routes movement of the current artwork to the preview.
    /// </summary>
    /// <param name="eventData">The current drag event.</param>
    public void OnDrag(PointerEventData eventData) =>
        GetComponentInParent<ContentEditorArtworkWindowPreview>()?.OnDrag(eventData);

    /// <summary>
    /// Routes the completed placement gesture to the preview.
    /// </summary>
    /// <param name="eventData">The final drag event.</param>
    public void OnEndDrag(PointerEventData eventData) =>
        GetComponentInParent<ContentEditorArtworkWindowPreview>()?.OnEndDrag(eventData);

    /// <summary>
    /// Applies hover state from direct pointer events or a composed-preview router.
    /// </summary>
    /// <param name="hovered">Whether this image is the current edit target.</param>
    internal void SetHovered(bool hovered)
    {
        if (hoverFrame == null || releasedField == null)
            return;
        this.hovered = hovered;
        RawImage image = GetComponent<RawImage>();
        if (hoverTexture != null)
            image.texture = hovered ? hoverTexture : releasedTexture;
        if (hovered)
        {
            ConfigureFrameGeometry(
                hoverFrame,
                transform,
                Field == releasedField ? frameAnchorMin : hoverFrameAnchorMin,
                Field == releasedField ? frameAnchorMax : hoverFrameAnchorMax
            );
        }
        hoverFrame.gameObject.SetActive(hovered);
    }

    /// <summary>
    /// Opens the state currently represented beneath the pointer after a primary-button double click.
    /// </summary>
    /// <param name="eventData">The completed pointer gesture.</param>
    /// <returns>True when an authored field was opened.</returns>
    internal bool TryOpen(PointerEventData eventData)
    {
        ContentPackEditorField current = Field;
        if (
            current == null
            || eventData?.button != PointerEventData.InputButton.Left
            || eventData.clickCount < 2
        )
            return false;
        eventData.Use();
        opened?.Invoke(current);
        return true;
    }

    /// <summary>
    /// Reports whether the pointer is over the represented image.
    /// </summary>
    /// <param name="position">The pointer's screen position.</param>
    /// <param name="camera">The event camera, when required by the canvas.</param>
    /// <returns>True while the image contains the pointer.</returns>
    internal bool ContainsScreenPoint(Vector2 position, Camera camera)
    {
        if (
            interactionArea != null
            && RectTransformUtility.RectangleContainsScreenPoint(interactionArea, position, camera)
        )
            return true;
        if (
            transform is not RectTransform imageRect
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                imageRect,
                position,
                camera,
                out Vector2 localPoint
            )
        )
            return false;
        Rect rect = imageRect.rect;
        if (rect.width <= 0f || rect.height <= 0f)
            return false;
        Vector2 normalized = new(
            (localPoint.x - rect.xMin) / rect.width,
            (localPoint.y - rect.yMin) / rect.height
        );
        Vector2 minimum =
            hovered && hoverField != releasedField ? hoverFrameAnchorMin : frameAnchorMin;
        Vector2 maximum =
            hovered && hoverField != releasedField ? hoverFrameAnchorMax : frameAnchorMax;
        return normalized.x >= minimum.x
            && normalized.x <= maximum.x
            && normalized.y >= minimum.y
            && normalized.y <= maximum.y;
    }

    /// <summary>
    /// Restricts direct Unity raycasts to the visible artwork instead of its transparent canvas.
    /// </summary>
    /// <param name="screenPoint">The pointer position in screen coordinates.</param>
    /// <param name="eventCamera">The camera used by the owning canvas.</param>
    /// <returns>True when the pointer is over visible artwork represented by this target.</returns>
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) =>
        ContainsScreenPoint(screenPoint, eventCamera);

    /// <summary>
    /// Hides the frame and releases its authored-field reference.
    /// </summary>
    public void Clear()
    {
        CancelMove();
        RawImage image = GetComponent<RawImage>();
        if (image != null && originalRaycastTarget.HasValue)
            image.raycastTarget = originalRaycastTarget.Value;
        originalRaycastTarget = null;
        releasedField = null;
        hoverField = null;
        releasedTexture = null;
        hoverTexture = null;
        interactionArea = null;
        hovered = false;
        opened = null;
        placements = Array.Empty<ContentEditorArtworkPlacement>();
        if (hoverFrame != null)
            hoverFrame.gameObject.SetActive(false);
    }

    /// <summary>
    /// Hides the transient frame when its preview or image becomes inactive.
    /// </summary>
    private void OnDisable()
    {
        CancelMove();
        SetHovered(false);
    }

    /// <summary>
    /// Adds a coordinate replacement once when paired image states share one layout field.
    /// </summary>
    /// <param name="changes">The replacements collected for the gesture.</param>
    /// <param name="field">The authored coordinate field.</param>
    /// <param name="value">The replacement source coordinate.</param>
    private static void AddPlacementChange(
        ICollection<ContentEditorArtworkPlacementChange> changes,
        ContentPackEditorField field,
        int value
    )
    {
        foreach (ContentEditorArtworkPlacementChange change in changes)
        {
            if (
                change.Field.XmlElement == field.XmlElement
                && change.Field.XmlAttributeName == field.XmlAttributeName
            )
                return;
        }
        changes.Add(new ContentEditorArtworkPlacementChange(field, value));
    }

    /// <summary>
    /// Applies a restrained alpha pulse without filling or obscuring the represented image.
    /// </summary>
    private void Update()
    {
        if (hoverFrame?.gameObject.activeSelf != true)
            return;
        float alpha = 0.88f + Mathf.Sin(Time.unscaledTime * 5f) * 0.1f;
        foreach (Image border in hoverFrame.GetComponentsInChildren<Image>())
            border.color = new Color(_accentColor.r, _accentColor.g, _accentColor.b, alpha);
    }

    /// <summary>
    /// Creates a four-stroke frame with no central graphic or raycast target.
    /// </summary>
    /// <param name="parent">The represented image transform.</param>
    /// <returns>The configured frame transform.</returns>
    private static RectTransform CreateHoverFrame(Transform parent)
    {
        GameObject root = new(
            "ContentEditorArtworkHoverFrame",
            typeof(RectTransform),
            typeof(Canvas)
        );
        RectTransform rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 30000;
        CreateBorder("Top", rect);
        CreateBorder("Right", rect);
        CreateBorder("Bottom", rect);
        CreateBorder("Left", rect);
        return rect;
    }

    /// <summary>
    /// Creates one non-interactive border stroke beneath the hover frame.
    /// </summary>
    /// <param name="name">The border object name.</param>
    /// <param name="parent">The hover frame transform.</param>
    private static void CreateBorder(string name, Transform parent)
    {
        GameObject border = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        border.transform.SetParent(parent, false);
        Image image = border.GetComponent<Image>();
        image.color = _accentColor;
        image.raycastTarget = false;
    }

    /// <summary>
    /// Calculates the image-local anchors containing visible texture pixels within the sampled UV rectangle.
    /// </summary>
    /// <param name="image">The rendered image and sampled UV rectangle.</param>
    /// <param name="contentBounds">The texture-space visible-pixel bounds.</param>
    /// <param name="minimum">Receives the lower-left normalized image anchor.</param>
    /// <param name="maximum">Receives the upper-right normalized image anchor.</param>
    private static void CalculateFrameAnchors(
        RawImage image,
        RectInt contentBounds,
        out Vector2 minimum,
        out Vector2 maximum
    )
    {
        minimum = Vector2.zero;
        maximum = Vector2.one;
        if (
            image?.texture == null
            || image.texture.width <= 0
            || image.texture.height <= 0
            || contentBounds.width <= 0
            || contentBounds.height <= 0
            || Mathf.Approximately(image.uvRect.width, 0f)
            || Mathf.Approximately(image.uvRect.height, 0f)
        )
            return;

        Rect uv = image.uvRect;
        float contentMinimumX = (float)contentBounds.xMin / image.texture.width;
        float contentMaximumX = (float)contentBounds.xMax / image.texture.width;
        float contentMinimumY = (float)contentBounds.yMin / image.texture.height;
        float contentMaximumY = (float)contentBounds.yMax / image.texture.height;
        float localX0 = (contentMinimumX - uv.x) / uv.width;
        float localX1 = (contentMaximumX - uv.x) / uv.width;
        float localY0 = (contentMinimumY - uv.y) / uv.height;
        float localY1 = (contentMaximumY - uv.y) / uv.height;
        minimum = new Vector2(
            Mathf.Clamp01(Mathf.Min(localX0, localX1)),
            Mathf.Clamp01(Mathf.Min(localY0, localY1))
        );
        maximum = new Vector2(
            Mathf.Clamp01(Mathf.Max(localX0, localX1)),
            Mathf.Clamp01(Mathf.Max(localY0, localY1))
        );
        if (maximum.x <= minimum.x || maximum.y <= minimum.y)
        {
            minimum = Vector2.zero;
            maximum = Vector2.one;
        }
    }

    /// <summary>
    /// Sizes the four strokes in screen-consistent units just outside the visible artwork bounds.
    /// </summary>
    /// <param name="frame">The unfilled hover frame.</param>
    /// <param name="image">The represented image transform.</param>
    /// <param name="minimum">The visible artwork's lower-left normalized image anchor.</param>
    /// <param name="maximum">The visible artwork's upper-right normalized image anchor.</param>
    private static void ConfigureFrameGeometry(
        RectTransform frame,
        Transform image,
        Vector2 minimum,
        Vector2 maximum
    )
    {
        Canvas rootCanvas = image.GetComponentInParent<Canvas>()?.rootCanvas;
        float rootScale =
            rootCanvas == null
                ? 1f
                : Mathf.Max(0.001f, Mathf.Abs(rootCanvas.transform.lossyScale.x));
        float imageScale = Mathf.Max(0.001f, Mathf.Abs(image.lossyScale.x));
        float inverseScale = rootScale / imageScale;
        float padding = 3f * inverseScale;
        float thickness = 3f * inverseScale;
        frame.anchorMin = minimum;
        frame.anchorMax = maximum;
        frame.offsetMin = new Vector2(-padding, -padding);
        frame.offsetMax = new Vector2(padding, padding);

        SetBorder(frame.Find("Top") as RectTransform, Vector2.up, true, thickness);
        SetBorder(frame.Find("Right") as RectTransform, Vector2.right, false, thickness);
        SetBorder(frame.Find("Bottom") as RectTransform, Vector2.zero, true, thickness);
        SetBorder(frame.Find("Left") as RectTransform, Vector2.zero, false, thickness);
    }

    /// <summary>
    /// Anchors one border stroke to a horizontal or vertical frame edge.
    /// </summary>
    /// <param name="border">The border transform.</param>
    /// <param name="edge">The edge anchor.</param>
    /// <param name="horizontal">Whether the stroke spans the frame width.</param>
    /// <param name="thickness">The stroke thickness in image-local units.</param>
    private static void SetBorder(
        RectTransform border,
        Vector2 edge,
        bool horizontal,
        float thickness
    )
    {
        if (border == null)
            return;
        if (horizontal)
        {
            border.anchorMin = new Vector2(0f, edge.y);
            border.anchorMax = new Vector2(1f, edge.y);
            border.pivot = new Vector2(0.5f, edge.y);
            border.offsetMin = new Vector2(0f, -thickness * 0.5f);
            border.offsetMax = new Vector2(0f, thickness * 0.5f);
        }
        else
        {
            border.anchorMin = new Vector2(edge.x, 0f);
            border.anchorMax = new Vector2(edge.x, 1f);
            border.pivot = new Vector2(edge.x, 0.5f);
            border.offsetMin = new Vector2(-thickness * 0.5f, 0f);
            border.offsetMax = new Vector2(thickness * 0.5f, 0f);
        }
    }
}
