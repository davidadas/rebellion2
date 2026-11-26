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
        ISelectHandler,
        IDeselectHandler,
        ICanvasRaycastFilter
{
    private static readonly Color _accentColor = new(1f, 0.72f, 0.15f, 1f);
    private static Component previewDragOwner;

    /// <summary>
    /// Gets whether a preview currently owns a pointer drag.
    /// </summary>
    internal static bool IsPreviewDragging => previewDragOwner != null;

    private Action<ContentPackEditorField> opened;
    private Vector2 frameAnchorMax = Vector2.one;
    private Vector2 frameAnchorMin = Vector2.zero;
    private Vector2 hoverFrameAnchorMax = Vector2.one;
    private Vector2 hoverFrameAnchorMin = Vector2.zero;
    private RectTransform hoverFrame;
    private RectMask2D hoverClip;
    private TMP_Text placementText;
    private bool hasTextInk;
    private bool textGeometryChanged;
    private readonly List<RectMask2D> enclosingMasks = new();
    private ContentPackEditorField hoverField;
    private Texture hoverTexture;
    private RectTransform interactionArea;
    private bool? originalRaycastTarget;
    private bool hovered;
    private bool requiresPress;
    private bool selectionOnly;
    private bool moving;
    private Vector2 movementOrigin;
    private Vector2 sizeOrigin;
    private RectTransform resizeHandle;
    private ContentEditorArtworkPlacement dimensions;
    private RectInt? movementBounds;
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

    private bool IsSelected => EventSystem.current?.currentSelectedGameObject == gameObject;
    private bool UsesAuthoredHitArea => placementText != null || interactionArea != null;

    internal bool IsGalaxyBackdrop =>
        releasedField?.MemberName == nameof(GalaxyBackground.ImagePath)
        && releasedField.Group == nameof(FactionTheme.GalaxyBackground);

    /// <summary>
    /// Suppresses hover effects across previews for the duration of a pointer drag.
    /// </summary>
    /// <param name="owner">The component handling the gesture.</param>
    /// <param name="dragging">Whether the owner is starting or ending its gesture.</param>
    internal static void SetPreviewDragging(Component owner, bool dragging)
    {
        if (dragging)
            previewDragOwner = owner;
        else if (previewDragOwner == owner)
            previewDragOwner = null;
    }

    /// <summary>
    /// Configures the represented image field and its navigation callback.
    /// </summary>
    /// <param name="value">The exact field supplying the rendered texture.</param>
    /// <param name="onOpened">The action invoked after an intentional double click.</param>
    /// <param name="contentBounds">The texture-space bounds containing visible pixels.</param>
    /// <exception cref="InvalidOperationException">The target has no graphic to bind.</exception>
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
    /// <param name="bounds">The optional source-space limits for placement.</param>
    /// <exception cref="InvalidOperationException">The target has no graphic to bind.</exception>
    internal void Bind(
        ContentPackEditorField releasedValue,
        ContentPackEditorField hoverValue,
        Action<ContentPackEditorField> onOpened,
        RectInt releasedContentBounds,
        RectInt hoverContentBounds,
        Texture previewHoverTexture,
        IReadOnlyList<ContentEditorArtworkPlacement> placementBindings = null,
        RectTransform pointerInteractionArea = null,
        bool requiresPress = false,
        RectInt? bounds = null
    )
    {
        if (releasedValue == null && hoverValue == null && placementBindings?.Count is not > 0)
            throw new ArgumentNullException(nameof(releasedValue));
        Graphic graphic = GetComponent<Graphic>();
        if (graphic == null)
            throw new InvalidOperationException("An artwork binding requires a Graphic component.");

        UnbindTextGeometry();
        selectionOnly = false;
        releasedField = releasedValue ?? hoverValue;
        hoverField = hoverValue ?? releasedValue;
        opened =
            releasedField == null
                ? null
                : onOpened ?? throw new ArgumentNullException(nameof(onOpened));
        RawImage image = graphic as RawImage;
        originalRaycastTarget ??= graphic.raycastTarget;
        graphic.raycastTarget = true;
        releasedTexture = image?.texture;
        hoverTexture = previewHoverTexture;
        interactionArea = pointerInteractionArea;
        this.requiresPress = requiresPress;
        placements = placementBindings ?? Array.Empty<ContentEditorArtworkPlacement>();
        movementBounds = bounds;
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
        SetHovered(false);
    }

    /// <summary>
    /// Enables placement for a graphic with no replaceable image field.
    /// </summary>
    /// <param name="placement">The two authored source coordinates moved with the graphic.</param>
    /// <param name="bounds">The allowed placement area, when the graphic must stay within its preview.</param>
    internal void BindPlacement(ContentEditorArtworkPlacement placement, RectInt? bounds = null)
    {
        Bind(null, null, null, default, default, null, new[] { placement }, bounds: bounds);
        if (GetComponent<Graphic>() is not TMP_Text text)
            return;

        placementText = text;
        placementText.OnPreRenderText += UpdateTextGeometry;
        placementText.ForceMeshUpdate(ignoreActiveState: true);
        SetHovered(false);
    }

    /// <summary>
    /// Adds a selection outline to a non-interactive rectangle that follows a preview object.
    /// </summary>
    internal void BindSelection()
    {
        Clear();
        selectionOnly = true;
        frameAnchorMin = Vector2.zero;
        frameAnchorMax = Vector2.one;
        hoverFrame ??= CreateHoverFrame(transform);
        SetHovered(false);
    }

    /// <summary>
    /// Measures the visible glyph quads after the text renderer lays them out.
    /// </summary>
    /// <param name="textInfo">The text geometry for the current preview frame.</param>
    private void UpdateTextGeometry(TMP_TextInfo textInfo)
    {
        Vector2 minimum = new(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new(float.NegativeInfinity, float.NegativeInfinity);
        for (int index = 0; index < textInfo.characterCount; index++)
        {
            TMP_CharacterInfo glyph = textInfo.characterInfo[index];
            if (!glyph.isVisible)
                continue;
            minimum = Vector2.Min(minimum, glyph.bottomLeft);
            maximum = Vector2.Max(maximum, glyph.topRight);
        }

        Rect rect = placementText.rectTransform.rect;
        hasTextInk =
            rect.width > 0f && rect.height > 0f && maximum.x > minimum.x && maximum.y > minimum.y;
        if (hasTextInk)
        {
            frameAnchorMin = (minimum - rect.min) / rect.size;
            frameAnchorMax = (maximum - rect.min) / rect.size;
        }
        textGeometryChanged = true;
    }

    /// <summary>
    /// Releases the listener and geometry belonging to a previous text binding.
    /// </summary>
    private void UnbindTextGeometry()
    {
        if (placementText != null)
            placementText.OnPreRenderText -= UpdateTextGeometry;
        placementText = null;
        hasTextInk = false;
        textGeometryChanged = false;
    }

    /// <summary>
    /// Outlines an editable rectangle and displays a grip for resizing it from the lower-right corner.
    /// </summary>
    /// <param name="position">The fields storing the rectangle's source position.</param>
    /// <param name="size">The fields storing its width and height.</param>
    /// <param name="bounds">The editable bounds in the same source coordinates.</param>
    internal void BindResizableArea(
        ContentEditorArtworkPlacement position,
        ContentEditorArtworkPlacement size,
        RectInt bounds
    )
    {
        Bind(null, null, null, default, default, null, new[] { position }, bounds: bounds);
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
        if (delta != Vector2Int.zero)
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
        if (movementBounds is not RectInt bounds || delta == Vector2Int.zero)
            return delta;

        Vector2Int origin = Vector2Int.RoundToInt(new Vector2(movementOrigin.x, -movementOrigin.y));
        Vector2Int size = Vector2Int.RoundToInt(sizeOrigin);
        if (dragPlacements[0] == dimensions)
        {
            Vector2Int maximum = Vector2Int.Max(Vector2Int.one, bounds.max - origin);
            return Vector2Int.Max(Vector2Int.one, Vector2Int.Min(size + delta, maximum)) - size;
        }
        return Vector2Int.Max(bounds.min, Vector2Int.Min(origin + delta, bounds.max - size))
            - origin;
    }

    /// <summary>
    /// Checks whether the pointer is on the rectangle's resize grip.
    /// </summary>
    /// <param name="position">The screen point to check.</param>
    /// <param name="camera">The event camera for the preview.</param>
    /// <returns>True when the pointer is over the visible grip.</returns>
    private bool IsResizeHandleAt(Vector2 position, Camera camera) =>
        resizeHandle
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
        if (
            UsesAuthoredHitArea
            && GetComponentInParent<ContentEditorArtworkWindowPreview>() is { } preview
        )
        {
            preview.OnPointerMove(eventData);
            return;
        }
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
    public void OnPointerExit(PointerEventData eventData)
    {
        if (
            !UsesAuthoredHitArea
            || GetComponentInParent<ContentEditorArtworkWindowPreview>() == null
        )
            SetHovered(false);
    }

    /// <summary>
    /// Opens the exact authored image field after an intentional primary-button double click.
    /// </summary>
    /// <param name="eventData">The completed pointer gesture.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (IsPreviewDragging)
            return;
        if (
            UsesAuthoredHitArea
            && GetComponentInParent<ContentEditorArtworkWindowPreview>() is { } preview
        )
        {
            preview.OnPointerClick(eventData);
            return;
        }
        if (
            eventData?.button == PointerEventData.InputButton.Left
            && GetComponentInParent<IContentEditorArtworkDragPreview>() != null
        )
            Select(this);
        TryOpen(eventData);
    }

    /// <summary>
    /// Gives one preview element keyboard focus or clears it when the background is clicked.
    /// </summary>
    /// <param name="target">The selected artwork, or null to clear artwork focus.</param>
    internal static void Select(ContentEditorArtworkEditTarget target)
    {
        EventSystem events = EventSystem.current;
        if (
            events != null
            && (
                target != null
                || events.currentSelectedGameObject?.GetComponent<ContentEditorArtworkEditTarget>()
                    != null
            )
        )
            events.SetSelectedGameObject(target == null ? null : target.gameObject);
    }

    /// <summary>
    /// Keeps the frame visible as focus enters this artwork.
    /// </summary>
    /// <param name="eventData">The focus event.</param>
    public void OnSelect(BaseEventData eventData) => SetHovered(hovered, true);

    /// <summary>
    /// Returns to the hover outline after focus leaves this artwork.
    /// </summary>
    /// <param name="eventData">The focus event.</param>
    public void OnDeselect(BaseEventData eventData) => SetHovered(hovered, false);

    /// <summary>
    /// Sends a source-pixel nudge to the owning preview.
    /// </summary>
    /// <param name="delta">The horizontal and downward source displacement.</param>
    /// <returns>True when the preview handles movement for this target.</returns>
    internal bool Nudge(Vector2Int delta) =>
        GetComponentInParent<IContentEditorArtworkDragPreview>()?.NudgeArtwork(this, delta) == true;

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
    /// <param name="selected">The selection state during a focus event, when provided.</param>
    internal void SetHovered(bool hovered, bool? selected = null)
    {
        if (hoverFrame == null || releasedField == null && !CanMove && !selectionOnly)
            return;
        hovered &= !IsPreviewDragging;
        this.hovered = hovered;
        textGeometryChanged = false;
        if (hoverTexture != null && GetComponent<Graphic>() is RawImage image)
            image.texture = hovered ? hoverTexture : releasedTexture;
        bool showFrame =
            (hovered || (selected ?? IsSelected)) && (placementText == null || hasTextInk);
        if (showFrame)
        {
            if (hoverClip == null)
            {
                GetComponentsInParent(false, enclosingMasks);
                if (enclosingMasks.Count > 0)
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
    internal bool ContainsScreenPoint(Vector2 position, Camera camera) =>
        TryHitScreenPoint(position, camera, out _);

    /// <summary>
    /// Distinguishes a hit on rendered artwork from the associated gameplay grab area.
    /// </summary>
    /// <param name="position">The pointer's screen position.</param>
    /// <param name="camera">The event camera, when required by the canvas.</param>
    /// <param name="onArtwork">Whether the point lies on the artwork or resize handle itself.</param>
    /// <returns>True when either the artwork or its grab area contains the point.</returns>
    internal bool TryHitScreenPoint(Vector2 position, Camera camera, out bool onArtwork)
    {
        onArtwork = ContainsArtworkPoint(position, camera);
        return onArtwork
            || (
                interactionArea != null
                && RectTransformUtility.RectangleContainsScreenPoint(
                    interactionArea,
                    position,
                    camera
                )
            );
    }

    /// <summary>
    /// Tests the pointer against the rendered bounds rather than the control's layout rectangle.
    /// </summary>
    /// <param name="position">The pointer's screen position.</param>
    /// <param name="camera">The event camera, when required by the canvas.</param>
    /// <returns>True when the pointer is inside the rendered bounds.</returns>
    private bool ContainsArtworkPoint(Vector2 position, Camera camera)
    {
        if (IsResizeHandleAt(position, camera))
            return true;
        if (
            placementText != null && !hasTextInk
            || transform is not RectTransform imageRect
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
        UnbindTextGeometry();
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
        selectionOnly = false;
        opened = null;
        placements = Array.Empty<ContentEditorArtworkPlacement>();
        dimensions = null;
        movementBounds = null;
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
    /// Detaches the text renderer if the edit target is removed.
    /// </summary>
    private void OnDestroy() => UnbindTextGeometry();

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
        if (textGeometryChanged)
            SetHovered(hovered);
        if (hoverFrame?.gameObject.activeSelf != true)
            return;
        ClipHoverFrame();
        float alpha = IsSelected ? 1f : 0.88f + Mathf.Sin(Time.unscaledTime * 5f) * 0.1f;
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
        IEndDragHandler
{
    /// <summary>
    /// Moves the selected interface element through the preview's placement history.
    /// </summary>
    /// <param name="target">The selected element.</param>
    /// <param name="delta">The source-pixel movement, with positive Y down.</param>
    /// <returns>True when the preview handles placement for this target.</returns>
    bool NudgeArtwork(ContentEditorArtworkEditTarget target, Vector2Int delta);
}
