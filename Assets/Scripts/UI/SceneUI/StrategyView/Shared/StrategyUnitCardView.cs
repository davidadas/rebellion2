using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders one authored strategy unit card and emits normalized local pointer gestures.
/// </summary>
[RequireComponent(typeof(UIPointerGestureRelay))]
public sealed class StrategyUnitCardView : MonoBehaviour, IStrategyStatusDoubleClickTarget
{
    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private RawImage enrouteOverlayImage;

    [SerializeField]
    private RawImage damagedOverlayImage;

    [SerializeField]
    private RawImage entityImage;

    [SerializeField]
    private RawImage statsOverlayImage;

    [SerializeField]
    private RawImage capturedOverlayImage;

    [SerializeField]
    private RawImage selectionImage;

    [SerializeField]
    private RawImage starfighterBadgeImage;

    [SerializeField]
    private RawImage troopBadgeImage;

    [SerializeField]
    private RawImage personnelBadgeImage;

    [SerializeField]
    private TextMeshProUGUI nameTextField;

    [SerializeField]
    private TextMeshProUGUI alternateNameTextTemplate;

    private bool canDrag;
    private RectInt entityFrameRect;
    private bool layoutCaptured;
    private UIPointerGestureRelay pointerGestures;
    private Color32[] statsOverlayPixels;
    private Texture2D statsOverlayTexture;

    /// <summary>
    /// Raised when the card receives a primary-button double click.
    /// </summary>
    public event Action<StrategyUnitCardView, PointerEventData> DoubleClicked;

    /// <summary>
    /// Raised when a pointer payload is dropped on the card.
    /// </summary>
    public event Action<StrategyUnitCardView, PointerEventData> Dropped;

    /// <summary>
    /// Raised when a supported pointer button is pressed on the card.
    /// </summary>
    public event Action<StrategyUnitCardView, PointerEventData> Pressed;

    /// <summary>
    /// Raised when a primary-button click completes on the card.
    /// </summary>
    public event Action<StrategyUnitCardView, PointerEventData> Released;

    public int Index { get; private set; }

    internal TextMeshProUGUI NameTextField => nameTextField;

    /// <summary>
    /// Verifies the authored hierarchy and binds shared pointer gestures.
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
        if (pointerGestures != null)
        {
            pointerGestures.DoubleClicked -= HandleDoubleClicked;
            pointerGestures.Dropped -= HandleDropped;
            pointerGestures.Pressed -= HandlePressed;
            pointerGestures.Released -= HandleReleased;
        }

        ReleaseStatsOverlayTexture();
    }

    /// <summary>
    /// Assigns this reusable card's stable visual index.
    /// </summary>
    /// <param name="index">The zero-based card index.</param>
    public void SetIndex(int index)
    {
        Index = index;
    }

    /// <summary>
    /// Applies one complete unit-card presentation snapshot.
    /// </summary>
    /// <param name="data">The immutable card presentation.</param>
    public void Render(StrategyUnitCardRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        CaptureLayout();
        canDrag = data.CanDrag;

        RectInt entityRenderFrameRect = GetEntityRenderFrameRect(data.EntityFrameYOffset);
        SetOptionalImageTexture(backgroundImage, data.BackgroundTexture);
        SetOptionalCenteredImage(
            enrouteOverlayImage,
            data.EnrouteOverlayTexture,
            entityRenderFrameRect
        );
        SetOptionalCenteredImage(
            damagedOverlayImage,
            data.DamagedOverlayTexture,
            entityRenderFrameRect
        );
        SetOptionalCenteredImage(entityImage, data.EntityTexture, entityRenderFrameRect);
        RenderStatsOverlay(data.StatsOverlay, entityRenderFrameRect);
        SetOptionalCenteredImage(
            capturedOverlayImage,
            data.CapturedOverlayTexture,
            entityRenderFrameRect
        );
        RenderSelection(data.SelectionTexture, data.NameColor);
        SetOptionalImageTexture(starfighterBadgeImage, data.StarfighterBadgeTexture);
        SetOptionalImageTexture(troopBadgeImage, data.TroopBadgeTexture);
        SetOptionalImageTexture(personnelBadgeImage, data.PersonnelBadgeTexture);
        RenderName(data);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Updates only the card's selection presentation.
    /// </summary>
    /// <param name="texture">The selected frame, or null when the card is not selected.</param>
    /// <param name="nameColor">The name color for the new selection state.</param>
    internal void RenderSelection(Texture texture, Color32 nameColor)
    {
        VerifyReferences();
        SetOptionalImageTexture(selectionImage, texture);
        nameTextField.color = nameColor;
    }

    /// <summary>
    /// Returns the currently rendered entity image and its source bounds for drag presentation.
    /// </summary>
    /// <param name="texture">Receives the rendered entity texture.</param>
    /// <param name="sourceTransform">Receives the rendered entity bounds.</param>
    /// <returns>True when the card can initiate a drag with a drawable entity image.</returns>
    internal bool TryGetDragImage(out Texture texture, out RectTransform sourceTransform)
    {
        texture = null;
        sourceTransform = null;
        VerifyReferences();
        CaptureLayout();
        if (!canDrag || !IsDrawableDragImage(entityImage))
            return false;

        texture = entityImage.texture;
        sourceTransform = entityImage.rectTransform;
        return true;
    }

    /// <summary>
    /// Appends the card's drawable visual layers in their authored rendering order.
    /// </summary>
    /// <param name="images">The destination image collection.</param>
    /// <returns>True when the card is draggable and has a drawable entity image.</returns>
    internal bool TryAppendDragImages(List<RawImage> images)
    {
        if (images == null)
            throw new ArgumentNullException(nameof(images));

        VerifyReferences();
        CaptureLayout();
        if (!canDrag || !IsDrawableDragImage(entityImage))
            return false;

        AppendDragImage(images, backgroundImage);
        AppendDragImage(images, entityImage);
        AppendDragImage(images, statsOverlayImage);
        AppendDragImage(images, enrouteOverlayImage);
        AppendDragImage(images, damagedOverlayImage);
        AppendDragImage(images, capturedOverlayImage);
        AppendDragImage(images, starfighterBadgeImage);
        AppendDragImage(images, troopBadgeImage);
        AppendDragImage(images, personnelBadgeImage);
        AppendDragImage(images, selectionImage);
        return true;
    }

    /// <summary>
    /// Reports whether a pointer event began within the rendered entity drag image.
    /// </summary>
    /// <param name="eventData">The pointer event to inspect.</param>
    /// <returns>True when the pointer lies inside a valid drag image.</returns>
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
    /// Forwards a normalized primary-button double click.
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
    /// Forwards a normalized pointer press to the owning feature view.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void HandlePressed(PointerEventData eventData)
    {
        Pressed?.Invoke(this, eventData);
    }

    /// <summary>
    /// Forwards a normalized primary-button release.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void HandleReleased(PointerEventData eventData)
    {
        Released?.Invoke(this, eventData);
    }

    /// <summary>
    /// Captures the authored entity slot before dynamic image fitting changes its bounds.
    /// </summary>
    private void CaptureLayout()
    {
        if (layoutCaptured)
            return;

        entityFrameRect = UILayout.GetSourceRect(entityImage.rectTransform);
        layoutCaptured = true;
    }

    /// <summary>
    /// Applies the configured primary or alternate authored name layout.
    /// </summary>
    /// <param name="data">The current card presentation.</param>
    private void RenderName(StrategyUnitCardRenderData data)
    {
        if (!data.ShowName)
        {
            nameTextField.gameObject.SetActive(false);
            return;
        }

        TextMeshProUGUI template =
            data.UseAlternateNameLayout && alternateNameTextTemplate != null
                ? alternateNameTextTemplate
                : nameTextField;
        UILayout.SetTemplateText(nameTextField, template, data.Name, data.NameColor);
    }

    /// <summary>
    /// Offsets the captured entity slot for one projected card layout.
    /// </summary>
    /// <param name="yOffset">The source-space vertical offset.</param>
    /// <returns>The offset entity slot.</returns>
    private RectInt GetEntityRenderFrameRect(int yOffset)
    {
        RectInt renderRect = entityFrameRect;
        renderRect.y += yOffset;
        return renderRect;
    }

    /// <summary>
    /// Renders an optional source-resolution stat overlay into the authored entity slot.
    /// </summary>
    /// <param name="overlay">The projected stat pixels, or null when disabled.</param>
    /// <param name="frameRect">The available source-space entity slot.</param>
    private void RenderStatsOverlay(StrategyUnitCardStatsOverlayData overlay, RectInt frameRect)
    {
        if (overlay == null)
        {
            SetOptionalImageTexture(statsOverlayImage, null);
            return;
        }

        EnsureStatsOverlayTexture(overlay.Width, overlay.Height);
        overlay.CopyToTexturePixels(statsOverlayPixels);
        statsOverlayTexture.SetPixels32(statsOverlayPixels);
        statsOverlayTexture.Apply(false, false);
        SetOptionalImageTexture(statsOverlayImage, statsOverlayTexture);
        RectInt renderRect = GetStatsOverlayRenderRect(overlay, frameRect);
        UILayout.SetSourceRect(
            statsOverlayImage.rectTransform,
            renderRect.x,
            renderRect.y,
            renderRect.width,
            renderRect.height
        );
    }

    /// <summary>
    /// Fits source-resolution overlay pixels into the same source-space slot as the unit artwork.
    /// </summary>
    /// <param name="overlay">The source-resolution overlay.</param>
    /// <param name="frameRect">The available source-space entity slot.</param>
    /// <returns>The centered source-space overlay bounds.</returns>
    internal static RectInt GetStatsOverlayRenderRect(
        StrategyUnitCardStatsOverlayData overlay,
        RectInt frameRect
    )
    {
        if (overlay == null)
            throw new ArgumentNullException(nameof(overlay));

        float scale = Mathf.Min(
            1f,
            frameRect.width / (float)overlay.Width,
            frameRect.height / (float)overlay.Height
        );
        int width = Mathf.Max(1, Mathf.RoundToInt(overlay.Width * scale));
        int height = Mathf.Max(1, Mathf.RoundToInt(overlay.Height * scale));
        return new RectInt(
            frameRect.x + (frameRect.width - width) / 2,
            frameRect.y + (frameRect.height - height) / 2,
            width,
            height
        );
    }

    /// <summary>
    /// Ensures the reusable runtime texture matches the projected overlay dimensions.
    /// </summary>
    /// <param name="width">The required source width.</param>
    /// <param name="height">The required source height.</param>
    private void EnsureStatsOverlayTexture(int width, int height)
    {
        if (
            statsOverlayTexture != null
            && statsOverlayTexture.width == width
            && statsOverlayTexture.height == height
        )
        {
            return;
        }

        ReleaseStatsOverlayTexture();
        statsOverlayTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = $"{name} Stats Overlay",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        statsOverlayPixels = new Color32[width * height];
    }

    /// <summary>
    /// Releases the runtime texture owned by this card view.
    /// </summary>
    private void ReleaseStatsOverlayTexture()
    {
        if (statsOverlayTexture == null)
            return;

        if (Application.isPlaying)
            Destroy(statsOverlayTexture);
        else
            DestroyImmediate(statsOverlayTexture);

        statsOverlayTexture = null;
        statsOverlayPixels = null;
    }

    /// <summary>
    /// Applies an optional image while preserving its authored component ownership.
    /// </summary>
    /// <param name="image">The optional authored image.</param>
    /// <param name="texture">The projected texture.</param>
    private static void SetOptionalImageTexture(RawImage image, Texture texture)
    {
        if (image != null)
            UILayout.SetImageTexture(image, texture);
    }

    /// <summary>
    /// Fits an optional image within the projected entity slot.
    /// </summary>
    /// <param name="image">The optional authored image.</param>
    /// <param name="texture">The projected texture.</param>
    /// <param name="frameRect">The available source-space slot.</param>
    private static void SetOptionalCenteredImage(RawImage image, Texture texture, RectInt frameRect)
    {
        if (image != null)
            UILayout.SetCenteredImage(image, texture, frameRect);
    }

    /// <summary>
    /// Appends one drawable card image to a drag-preview collection.
    /// </summary>
    /// <param name="images">The destination image collection.</param>
    /// <param name="image">The candidate card image.</param>
    private static void AppendDragImage(List<RawImage> images, RawImage image)
    {
        if (IsDrawableDragImage(image))
            images.Add(image);
    }

    /// <summary>
    /// Reports whether one rendered card image can appear in a drag preview.
    /// </summary>
    /// <param name="image">The card image to inspect.</param>
    /// <returns>True when the image is visible, textured, and has positive dimensions.</returns>
    private static bool IsDrawableDragImage(RawImage image)
    {
        if (image == null)
            return false;

        return image.gameObject.activeInHierarchy
            && image.enabled
            && image.texture != null
            && image.rectTransform.rect.width > 0f
            && image.rectTransform.rect.height > 0f;
    }

    /// <summary>
    /// Verifies the complete common card hierarchy and optional variant templates.
    /// </summary>
    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (enrouteOverlayImage == null)
            throw new MissingReferenceException($"{name}/EnrouteOverlayImage is missing.");
        if (damagedOverlayImage == null)
            throw new MissingReferenceException($"{name}/DamagedOverlayImage is missing.");
        if (entityImage == null)
            throw new MissingReferenceException($"{name}/EntityImage is missing.");
        if (statsOverlayImage == null)
            throw new MissingReferenceException($"{name}/StatsOverlayImage is missing.");
        if (capturedOverlayImage == null)
            throw new MissingReferenceException($"{name}/CapturedOverlayImage is missing.");
        if (selectionImage == null)
            throw new MissingReferenceException($"{name}/SelectionImage is missing.");
        if (nameTextField == null)
            throw new MissingReferenceException($"{name}/NameTextField is missing.");

        hitAreaImage.enabled = true;
        hitAreaImage.raycastTarget = true;
        hitAreaImage.canvasRenderer.cullTransparentMesh = false;
        if (alternateNameTextTemplate != null)
            alternateNameTextTemplate.gameObject.SetActive(false);
    }
}

/// <summary>
/// Contains immutable presentation data for one shared strategy unit card.
/// </summary>
public sealed class StrategyUnitCardRenderData
{
    public string Name { get; }

    public Color32 NameColor { get; }

    public bool ShowName { get; }

    public bool UseAlternateNameLayout { get; }

    public Texture BackgroundTexture { get; }

    public Texture EnrouteOverlayTexture { get; }

    public Texture DamagedOverlayTexture { get; }

    public Texture EntityTexture { get; }

    public StrategyUnitCardStatsOverlayData StatsOverlay { get; }

    public Texture CapturedOverlayTexture { get; }

    public Texture SelectionTexture { get; }

    public int EntityFrameYOffset { get; }

    public Texture StarfighterBadgeTexture { get; }

    public Texture TroopBadgeTexture { get; }

    public Texture PersonnelBadgeTexture { get; }

    public bool CanDrag { get; }

    /// <summary>
    /// Creates one complete strategy unit-card presentation snapshot.
    /// </summary>
    /// <param name="name">The displayed unit name.</param>
    /// <param name="nameColor">The displayed unit-name color.</param>
    /// <param name="showName">Whether the unit name is visible.</param>
    /// <param name="useAlternateNameLayout">Whether to use the authored alternate name layout.</param>
    /// <param name="backgroundTexture">The optional card background.</param>
    /// <param name="enrouteOverlayTexture">The optional in-transit overlay.</param>
    /// <param name="damagedOverlayTexture">The optional damaged or injured overlay.</param>
    /// <param name="entityTexture">The represented entity image.</param>
    /// <param name="capturedOverlayTexture">The optional captured-personnel overlay.</param>
    /// <param name="selectionTexture">The optional selection frame.</param>
    /// <param name="entityFrameYOffset">The source-space entity-slot vertical offset.</param>
    /// <param name="starfighterBadgeTexture">The optional starfighter badge.</param>
    /// <param name="troopBadgeTexture">The optional troop badge.</param>
    /// <param name="personnelBadgeTexture">The optional personnel badge.</param>
    /// <param name="canDrag">Whether the card can initiate a move drag.</param>
    /// <param name="statsOverlay">The optional source-resolution stat overlay.</param>
    public StrategyUnitCardRenderData(
        string name,
        Color32 nameColor,
        bool showName,
        bool useAlternateNameLayout,
        Texture backgroundTexture,
        Texture enrouteOverlayTexture,
        Texture damagedOverlayTexture,
        Texture entityTexture,
        Texture capturedOverlayTexture,
        Texture selectionTexture,
        int entityFrameYOffset,
        Texture starfighterBadgeTexture,
        Texture troopBadgeTexture,
        Texture personnelBadgeTexture,
        bool canDrag,
        StrategyUnitCardStatsOverlayData statsOverlay = null
    )
    {
        Name = name ?? string.Empty;
        NameColor = nameColor;
        ShowName = showName;
        UseAlternateNameLayout = useAlternateNameLayout;
        BackgroundTexture = backgroundTexture;
        EnrouteOverlayTexture = enrouteOverlayTexture;
        DamagedOverlayTexture = damagedOverlayTexture;
        EntityTexture = entityTexture;
        StatsOverlay = statsOverlay;
        CapturedOverlayTexture = capturedOverlayTexture;
        SelectionTexture = selectionTexture;
        EntityFrameYOffset = entityFrameYOffset;
        StarfighterBadgeTexture = starfighterBadgeTexture;
        TroopBadgeTexture = troopBadgeTexture;
        PersonnelBadgeTexture = personnelBadgeTexture;
        CanDrag = canDrag;
    }
}
