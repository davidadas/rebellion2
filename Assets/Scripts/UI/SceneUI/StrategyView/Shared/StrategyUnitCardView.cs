using System;
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
    private RawImage constructionOverlayImage;

    [SerializeField]
    private RawImage enrouteOverlayImage;

    [SerializeField]
    private RawImage damagedOverlayImage;

    [SerializeField]
    private RawImage entityImage;

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
        if (pointerGestures == null)
            return;

        pointerGestures.DoubleClicked -= HandleDoubleClicked;
        pointerGestures.Dropped -= HandleDropped;
        pointerGestures.Pressed -= HandlePressed;
        pointerGestures.Released -= HandleReleased;
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
            constructionOverlayImage,
            data.ConstructionOverlayTexture,
            entityRenderFrameRect
        );
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
        SetOptionalCenteredImage(
            capturedOverlayImage,
            data.CapturedOverlayTexture,
            entityRenderFrameRect
        );
        SetOptionalImageTexture(selectionImage, data.SelectionTexture);
        SetOptionalImageTexture(starfighterBadgeImage, data.StarfighterBadgeTexture);
        SetOptionalImageTexture(troopBadgeImage, data.TroopBadgeTexture);
        SetOptionalImageTexture(personnelBadgeImage, data.PersonnelBadgeTexture);
        RenderName(data);
        gameObject.SetActive(true);
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
        if (!canDrag || !entityImage.gameObject.activeInHierarchy)
            return false;

        texture = entityImage.texture;
        sourceTransform = entityImage.rectTransform;
        return texture != null
            && sourceTransform.rect.width > 0f
            && sourceTransform.rect.height > 0f;
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
    /// Verifies the complete common card hierarchy and optional variant templates.
    /// </summary>
    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (constructionOverlayImage == null)
            throw new MissingReferenceException($"{name}/ConstructionOverlayImage is missing.");
        if (enrouteOverlayImage == null)
            throw new MissingReferenceException($"{name}/EnrouteOverlayImage is missing.");
        if (damagedOverlayImage == null)
            throw new MissingReferenceException($"{name}/DamagedOverlayImage is missing.");
        if (entityImage == null)
            throw new MissingReferenceException($"{name}/EntityImage is missing.");
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
    /// <summary>
    /// Creates one complete strategy unit-card presentation snapshot.
    /// </summary>
    /// <param name="name">The displayed unit name.</param>
    /// <param name="nameColor">The displayed unit-name color.</param>
    /// <param name="showName">Whether the unit name is visible.</param>
    /// <param name="useAlternateNameLayout">Whether to use the authored alternate name layout.</param>
    /// <param name="backgroundTexture">The optional card background.</param>
    /// <param name="constructionOverlayTexture">The optional construction overlay.</param>
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
    public StrategyUnitCardRenderData(
        string name,
        Color32 nameColor,
        bool showName,
        bool useAlternateNameLayout,
        Texture backgroundTexture,
        Texture constructionOverlayTexture,
        Texture enrouteOverlayTexture,
        Texture damagedOverlayTexture,
        Texture entityTexture,
        Texture capturedOverlayTexture,
        Texture selectionTexture,
        int entityFrameYOffset,
        Texture starfighterBadgeTexture,
        Texture troopBadgeTexture,
        Texture personnelBadgeTexture,
        bool canDrag
    )
    {
        Name = name ?? string.Empty;
        NameColor = nameColor;
        ShowName = showName;
        UseAlternateNameLayout = useAlternateNameLayout;
        BackgroundTexture = backgroundTexture;
        ConstructionOverlayTexture = constructionOverlayTexture;
        EnrouteOverlayTexture = enrouteOverlayTexture;
        DamagedOverlayTexture = damagedOverlayTexture;
        EntityTexture = entityTexture;
        CapturedOverlayTexture = capturedOverlayTexture;
        SelectionTexture = selectionTexture;
        EntityFrameYOffset = entityFrameYOffset;
        StarfighterBadgeTexture = starfighterBadgeTexture;
        TroopBadgeTexture = troopBadgeTexture;
        PersonnelBadgeTexture = personnelBadgeTexture;
        CanDrag = canDrag;
    }

    public string Name { get; }

    public Color32 NameColor { get; }

    public bool ShowName { get; }

    public bool UseAlternateNameLayout { get; }

    public Texture BackgroundTexture { get; }

    public Texture ConstructionOverlayTexture { get; }

    public Texture EnrouteOverlayTexture { get; }

    public Texture DamagedOverlayTexture { get; }

    public Texture EntityTexture { get; }

    public Texture CapturedOverlayTexture { get; }

    public Texture SelectionTexture { get; }

    public int EntityFrameYOffset { get; }

    public Texture StarfighterBadgeTexture { get; }

    public Texture TroopBadgeTexture { get; }

    public Texture PersonnelBadgeTexture { get; }

    public bool CanDrag { get; }
}
