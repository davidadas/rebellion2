using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders one authored manufacturing lane and reports pointer interaction.
/// </summary>
[RequireComponent(typeof(UIPointerGestureRelay))]
public sealed class ManufacturingLaneCardView : MonoBehaviour
{
    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private RawImage stateCardImage;

    [SerializeField]
    private RawImage entityImage;

    [SerializeField]
    private Image progressFillImage;

    [SerializeField]
    private TextMeshProUGUI titleTextField;

    [SerializeField]
    private TextMeshProUGUI currentNameTextField;

    [SerializeField]
    private TextMeshProUGUI currentCountTextField;

    [SerializeField]
    private TextMeshProUGUI emptyTextField;

    [SerializeField]
    private TextMeshProUGUI destinationTextField;

    [SerializeField]
    private TextMeshProUGUI facilityCountTextField;

    private bool templateLayoutCaptured;
    private RectTransform entitySlotRoot;
    private RectInt entitySlotRect;
    private UIPointerGestureRelay pointerGestures;
    private RectInt progressFillRect;

    /// <summary>
    /// Occurs when the control is pressed.
    /// </summary>
    public event Action<ManufacturingLaneCardView, PointerEventData> Pressed;

    /// <summary>
    /// Occurs when the control is released.
    /// </summary>
    public event Action<ManufacturingLaneCardView, PointerEventData> Released;

    /// <summary>
    /// Occurs when a pointer drop is received by the control.
    /// </summary>
    public event Action<ManufacturingLaneCardView, PointerEventData> Dropped;

    public int Index { get; private set; }

    /// <summary>
    /// Assigns this card's manufacturing lane index.
    /// </summary>
    /// <param name="index">The one-based manufacturing lane index.</param>
    public void SetIndex(int index)
    {
        Index = index;
    }

    /// <summary>
    /// Applies one manufacturing lane presentation snapshot.
    /// </summary>
    /// <param name="data">The lane presentation data.</param>
    public void Render(ManufacturingLaneCardRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        CaptureTemplateLayout();
        SetImage(stateCardImage, data.StateTexture);
        SetText(titleTextField, data.Title, Color.black);

        bool hasEntity = data.EntityTexture != null;
        if (hasEntity)
        {
            SetManufacturingImage(data.EntityTexture);
            SetText(currentNameTextField, data.CurrentName, Color.white);
            SetText(currentCountTextField, data.CurrentCount, Color.white);
        }
        else
        {
            entityImage.gameObject.SetActive(false);
            currentNameTextField.gameObject.SetActive(false);
            currentCountTextField.gameObject.SetActive(false);
        }

        emptyTextField.gameObject.SetActive(!hasEntity);
        if (!hasEntity)
            SetText(emptyTextField, data.EmptyText, Color.white);

        SetText(destinationTextField, data.DestinationText, Color.white);
        SetText(facilityCountTextField, data.FacilityCount, Color.white);
        SetProgress(data.ManufacturingProgress, data.ManufacturingCost);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Verifies authored references, captures layout, and binds shared pointer gestures.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        CaptureTemplateLayout();
        pointerGestures = GetComponent<UIPointerGestureRelay>();
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

        pointerGestures.Dropped -= HandleDropped;
        pointerGestures.Pressed -= HandlePressed;
        pointerGestures.Released -= HandleReleased;
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
    /// Renders progress within the authored progress-fill bounds.
    /// </summary>
    /// <param name="progress">The current manufacturing progress.</param>
    /// <param name="cost">The total manufacturing cost.</param>
    private void SetProgress(int progress, int cost)
    {
        int width =
            cost > 0
                ? Mathf.Clamp(
                    Mathf.RoundToInt(progress / (float)cost * progressFillRect.width),
                    0,
                    progressFillRect.width
                )
                : 0;
        progressFillImage.gameObject.SetActive(width > 0);
        if (width <= 0)
            return;

        UILayout.SetSourceRect(
            progressFillImage.rectTransform,
            progressFillRect.x,
            progressFillRect.y,
            width,
            progressFillRect.height
        );
    }

    /// <summary>
    /// Captures dimensions authored by the manufacturing card prefab.
    /// </summary>
    private void CaptureTemplateLayout()
    {
        if (templateLayoutCaptured)
            return;

        entitySlotRoot = entityImage.transform.parent as RectTransform;
        entitySlotRect =
            entitySlotRoot != null && entitySlotRoot != transform
                ? UILayout.GetSourceRect(entitySlotRoot)
                : UILayout.GetSourceRect(entityImage.rectTransform);
        progressFillRect = UILayout.GetSourceRect(progressFillImage.rectTransform);
        templateLayoutCaptured = true;
    }

    /// <summary>
    /// Validates the card's authored child references.
    /// </summary>
    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (stateCardImage == null)
            throw new MissingReferenceException($"{name}/StateCardImage is missing.");
        if (entityImage == null)
            throw new MissingReferenceException($"{name}/EntityImage is missing.");
        if (progressFillImage == null)
            throw new MissingReferenceException($"{name}/ProgressFillImage is missing.");
        if (titleTextField == null)
            throw new MissingReferenceException($"{name}/TitleTextField is missing.");
        if (currentNameTextField == null)
            throw new MissingReferenceException($"{name}/CurrentNameTextField is missing.");
        if (currentCountTextField == null)
            throw new MissingReferenceException($"{name}/CurrentCountTextField is missing.");
        if (emptyTextField == null)
            throw new MissingReferenceException($"{name}/EmptyTextField is missing.");
        if (destinationTextField == null)
            throw new MissingReferenceException($"{name}/DestinationTextField is missing.");
        if (facilityCountTextField == null)
            throw new MissingReferenceException($"{name}/FacilityCountTextField is missing.");
    }

    /// <summary>
    /// Applies a texture to a presentation image.
    /// </summary>
    /// <param name="image">The destination image.</param>
    /// <param name="texture">The displayed texture.</param>
    private static void SetImage(RawImage image, Texture texture)
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = false;
    }

    /// <summary>
    /// Fits the current entity image into its authored right-aligned slot.
    /// </summary>
    /// <param name="texture">The displayed entity texture.</param>
    private void SetManufacturingImage(Texture texture)
    {
        entityImage.texture = texture;
        entityImage.enabled = true;
        entityImage.gameObject.SetActive(true);
        entityImage.raycastTarget = false;

        Vector2Int size = UILayout.GetFittedImageSize(texture, entitySlotRect);
        if (entityImage.transform.parent == entitySlotRoot && entitySlotRoot != null)
        {
            SetRightCenteredRect(entityImage.rectTransform, size.x, size.y);
            return;
        }

        UILayout.SetImage(
            entityImage,
            texture,
            entitySlotRect.x + entitySlotRect.width - size.x,
            entitySlotRect.y + (entitySlotRect.height - size.y) / 2,
            size.x,
            size.y
        );
    }

    /// <summary>
    /// Sizes an image within a right-centered authored slot.
    /// </summary>
    /// <param name="rect">The image transform.</param>
    /// <param name="width">The fitted width.</param>
    /// <param name="height">The fitted height.</param>
    private static void SetRightCenteredRect(RectTransform rect, int width, int height)
    {
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Applies presentation text without changing authored bounds.
    /// </summary>
    /// <param name="textField">The destination label.</param>
    /// <param name="text">The displayed text.</param>
    /// <param name="color">The displayed color.</param>
    private static void SetText(TextMeshProUGUI textField, string text, Color32 color)
    {
        textField.text = text ?? string.Empty;
        textField.color = color;
        textField.raycastTarget = false;
        textField.gameObject.SetActive(true);
    }
}
