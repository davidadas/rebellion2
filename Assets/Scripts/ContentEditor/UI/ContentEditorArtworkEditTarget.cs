using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Outlines one rendered image on hover and opens its exact authored field on double click.
/// </summary>
[RequireComponent(typeof(RectTransform))]
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
    private RectMask2D hoverClip;
    private readonly List<RectMask2D> enclosingMasks = new();
    private ContentPackEditorField hoverField;
    private Texture hoverTexture;
    private RectTransform interactionArea;
    private bool? originalRaycastTarget;
    private bool hovered;
    private bool requiresPress;
    private bool moving;
    private Vector2 movementOrigin;
    private Vector2 sizeOrigin;
    private RectTransform resizeHandle;
    private ContentEditorArtworkPlacement dimensions;
    private IReadOnlyList<ContentEditorArtworkPlacement> dragPlacements;
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

    internal bool IsGalaxyBackdrop =>
        releasedField?.MemberName == nameof(GalaxyBackground.ImagePath)
        && releasedField.Group == nameof(FactionTheme.GalaxyBackground);

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
        if (releasedValue == null && hoverValue == null && placementBindings?.Count is not > 0)
            throw new ArgumentNullException(nameof(releasedValue));
        releasedField = releasedValue ?? hoverValue;
        hoverField = hoverValue ?? releasedValue;
        opened =
            releasedField == null
                ? null
                : onOpened ?? throw new ArgumentNullException(nameof(onOpened));
        Graphic graphic = GetComponent<Graphic>();
        RawImage image = graphic as RawImage;
        originalRaycastTarget ??= graphic.raycastTarget;
        graphic.raycastTarget = true;
        releasedTexture = image?.texture;
        hoverTexture = previewHoverTexture;
        interactionArea = pointerInteractionArea;
        this.requiresPress = requiresPress;
        placements = placementBindings ?? Array.Empty<ContentEditorArtworkPlacement>();
        dimensions = null;
        if (resizeHandle != null)
            resizeHandle.gameObject.SetActive(false);
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
    /// Enables placement for a graphic with no replaceable image field.
    /// </summary>
    /// <param name="placement">The two authored source coordinates moved with the graphic.</param>
    internal void BindPlacement(ContentEditorArtworkPlacement placement)
    {
        Bind(null, null, null, default, default, null, new[] { placement });
        if (GetComponent<Graphic>() is not TMP_Text text)
            return;

        text.ForceMeshUpdate();
        Rect rect = text.rectTransform.rect;
        Bounds ink = text.textBounds;
        if (rect.width <= 0f || rect.height <= 0f || ink.size.x <= 0f || ink.size.y <= 0f)
            return;

        frameAnchorMin = new Vector2(
            Mathf.Clamp01((ink.min.x - rect.xMin) / rect.width),
            Mathf.Clamp01((ink.min.y - rect.yMin) / rect.height)
        );
        frameAnchorMax = new Vector2(
            Mathf.Clamp01((ink.max.x - rect.xMin) / rect.width),
            Mathf.Clamp01((ink.max.y - rect.yMin) / rect.height)
        );
    }

    /// <summary>
    /// Outlines an editable rectangle and displays a grip for resizing it from the lower-right corner.
    /// </summary>
    /// <param name="position">The fields storing the rectangle's source position.</param>
    /// <param name="size">The fields storing its width and height.</param>
    internal void BindResizableArea(
        ContentEditorArtworkPlacement position,
        ContentEditorArtworkPlacement size
    )
    {
        Bind(null, null, null, default, default, null, new[] { position });
        dimensions = size;
        if (resizeHandle == null)
        {
            CreateBorder("ResizeHandle", hoverFrame);
            resizeHandle = (RectTransform)hoverFrame.Find("ResizeHandle");
            resizeHandle.anchorMin = resizeHandle.anchorMax = resizeHandle.pivot = Vector2.right;
            resizeHandle.sizeDelta = new Vector2(12f, 12f);
        }
        resizeHandle.gameObject.SetActive(true);
        SetHovered(false);
    }

    /// <summary>
    /// Captures the rendered and authored origins for one click-drag placement gesture.
    /// </summary>
    /// <param name="pointer">The pointer grab, when resize is available.</param>
    /// <returns>True when the image has valid movable coordinate fields.</returns>
    internal bool BeginMove(PointerEventData pointer = null)
    {
        if (!CanMove)
            return false;

        bool resizing =
            dimensions != null
            && pointer != null
            && IsResizeHandleAt(
                pointer.pressPosition,
                pointer.pressEventCamera ?? pointer.enterEventCamera
            );
        dragPlacements = resizing ? new[] { dimensions } : placements;
        List<Vector2Int> origins = new(dragPlacements.Count);
        foreach (ContentEditorArtworkPlacement placement in dragPlacements)
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

        RectTransform rect = (RectTransform)transform;
        movementOrigin = rect.anchoredPosition;
        sizeOrigin = rect.sizeDelta;
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
        delta = ConstrainAreaDrag(delta);
        RectTransform rect = (RectTransform)transform;
        if (dragPlacements[0] == dimensions)
            rect.sizeDelta = sizeOrigin + (Vector2)delta;
        else
            rect.anchoredPosition = movementOrigin + new Vector2(delta.x, -delta.y);
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

        delta = ConstrainAreaDrag(delta);
        PreviewMove(delta);
        List<ContentEditorArtworkPlacementChange> changes = new(dragPlacements.Count * 2);
        if (dimensions == null || delta != Vector2Int.zero)
        {
            for (int index = 0; index < dragPlacements.Count; index++)
            {
                ContentEditorArtworkPlacement placement = dragPlacements[index];
                Vector2Int origin = placementOrigins[index];
                AddPlacementChange(changes, placement.XField, checked(origin.x + delta.x));
                AddPlacementChange(changes, placement.YField, checked(origin.y + delta.y));
            }
        }
        moving = false;
        placementOrigins = Array.Empty<Vector2Int>();
        return changes;
    }

    /// <summary>
    /// Keeps an edited rectangle within its source canvas and preserves a positive size.
    /// </summary>
    /// <param name="delta">The proposed drag from the captured origin.</param>
    /// <returns>The drag that the editable rectangle can apply.</returns>
    private Vector2Int ConstrainAreaDrag(Vector2Int delta)
    {
        if (dimensions == null || delta == Vector2Int.zero)
            return delta;

        Vector2Int canvas = Vector2Int.RoundToInt(((RectTransform)transform.parent).rect.size);
        Vector2Int origin = Vector2Int.RoundToInt(new Vector2(movementOrigin.x, -movementOrigin.y));
        Vector2Int size = Vector2Int.RoundToInt(sizeOrigin);
        if (dragPlacements[0] == dimensions)
        {
            Vector2Int maximum = Vector2Int.Max(Vector2Int.one, canvas - origin);
            return Vector2Int.Max(Vector2Int.one, Vector2Int.Min(size + delta, maximum)) - size;
        }
        return Vector2Int.Max(Vector2Int.zero, Vector2Int.Min(origin + delta, canvas - size))
            - origin;
    }

    /// <summary>
    /// Checks whether the pointer is on the rectangle's resize grip.
    /// </summary>
    /// <param name="position">The screen point to check.</param>
    /// <param name="camera">The event camera for the preview.</param>
    /// <returns>True when the pointer is over the visible grip.</returns>
    private bool IsResizeHandleAt(Vector2 position, Camera camera) =>
        resizeHandle != null
        && resizeHandle.gameObject.activeInHierarchy
        && RectTransformUtility.RectangleContainsScreenPoint(resizeHandle, position, camera);

    /// <summary>
    /// Restores the rendered origin when an in-progress placement gesture is interrupted.
    /// </summary>
    internal void CancelMove()
    {
        if (moving)
        {
            RectTransform rect = (RectTransform)transform;
            rect.anchoredPosition = movementOrigin;
            if (dimensions != null)
                rect.sizeDelta = sizeOrigin;
        }
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
        GetComponentInParent<IContentEditorArtworkDragPreview>()?.OnBeginDrag(eventData);

    /// <summary>
    /// Routes movement of the current artwork to the preview.
    /// </summary>
    /// <param name="eventData">The current drag event.</param>
    public void OnDrag(PointerEventData eventData) =>
        GetComponentInParent<IContentEditorArtworkDragPreview>()?.OnDrag(eventData);

    /// <summary>
    /// Routes the completed placement gesture to the preview.
    /// </summary>
    /// <param name="eventData">The final drag event.</param>
    public void OnEndDrag(PointerEventData eventData) =>
        GetComponentInParent<IContentEditorArtworkDragPreview>()?.OnEndDrag(eventData);

    /// <summary>
    /// Applies hover state from direct pointer events or a composed-preview router.
    /// </summary>
    /// <param name="hovered">Whether this image is the current edit target.</param>
    internal void SetHovered(bool hovered)
    {
        if (hoverFrame == null || releasedField == null && !CanMove)
            return;
        this.hovered = hovered;
        if (hoverTexture != null && GetComponent<Graphic>() is RawImage image)
            image.texture = hovered ? hoverTexture : releasedTexture;
        bool showFrame = hovered || dimensions != null;
        if (showFrame)
        {
            if (
                hoverClip == null
                && (
                    dimensions != null
                    || GetComponentInParent<ContentEditorArtworkWindowPreview>()?.CanZoom == true
                    || GetComponentInParent<ContentEditorSectorPreview>() != null
                )
            )
            {
                GetComponentsInParent(false, enclosingMasks);
                hoverClip = hoverFrame.gameObject.AddComponent<RectMask2D>();
            }
            ConfigureFrameGeometry(
                hoverFrame,
                transform,
                Field == releasedField ? frameAnchorMin : hoverFrameAnchorMin,
                Field == releasedField ? frameAnchorMax : hoverFrameAnchorMax
            );
        }
        hoverFrame.gameObject.SetActive(showFrame);
        if (showFrame)
            ClipHoverFrame();
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
        if (IsResizeHandleAt(position, camera))
            return true;
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
        Graphic graphic = GetComponent<Graphic>();
        if (graphic != null && originalRaycastTarget.HasValue)
            graphic.raycastTarget = originalRaycastTarget.Value;
        originalRaycastTarget = null;
        releasedField = null;
        hoverField = null;
        releasedTexture = null;
        hoverTexture = null;
        interactionArea = null;
        hovered = false;
        opened = null;
        placements = Array.Empty<ContentEditorArtworkPlacement>();
        dimensions = null;
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
        ClipHoverFrame();
        float alpha = hovered ? 0.88f + Mathf.Sin(Time.unscaledTime * 5f) * 0.1f : 0.88f;
        foreach (Image border in hoverFrame.GetComponentsInChildren<Image>())
            border.color = new Color(_accentColor.r, _accentColor.g, _accentColor.b, alpha);
    }

    /// <summary>
    /// Applies the enclosing viewport masks inside the frame's top-sorted canvas.
    /// </summary>
    private void ClipHoverFrame()
    {
        if (hoverClip == null)
            return;
        Rect visible = Clipping.FindCullAndClipWorldRect(enclosingMasks, out _);
        Rect frame = hoverClip.canvasRect;
        Vector4 padding = new(
            visible.xMin - frame.xMin,
            visible.yMin - frame.yMin,
            frame.xMax - visible.xMax,
            frame.yMax - visible.yMax
        );
        if (hoverClip.padding != padding)
            hoverClip.padding = padding;
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

/// <summary>
/// Routes drags from an artwork edit target to the preview that owns its placement.
/// </summary>
internal interface IContentEditorArtworkDragPreview
    : IBeginDragHandler,
        IDragHandler,
        IEndDragHandler { }
