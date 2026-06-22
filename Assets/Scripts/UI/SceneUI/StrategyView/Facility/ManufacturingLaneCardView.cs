using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ManufacturingLaneCardView
    : MonoBehaviour,
        IStrategyUIContextReceiver,
        IPointerDownHandler,
        IPointerClickHandler,
        IDropHandler
{
    private bool capturedTemplateLayout;
    private float progressFillWidth;
    private RectInt baseCardRect;
    private RectInt entitySlotRect;
    private RectTransform entitySlotRoot;
    private UIContext uiContext;

    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private RawImage baseCardImage;

    [SerializeField]
    private RawImage stateCardImage;

    [SerializeField]
    private Texture2D baseTexture;

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

    public event Action<ManufacturingLaneCardView, PointerEventData> Pressed;
    public event Action<ManufacturingLaneCardView, PointerEventData> Released;
    public event Action<ManufacturingLaneCardView, PointerEventData> Dropped;

    public int Index { get; private set; }

    public void Initialize(UIContext uiContext)
    {
        this.uiContext = uiContext;
    }

    public void SetIndex(int index)
    {
        Index = index;
    }

    public void Render(ManufacturingLaneCardRenderData data)
    {
        VerifyReferences();
        CaptureTemplateLayout();

        hitAreaImage.enabled = true;
        hitAreaImage.raycastTarget = true;
        hitAreaImage.canvasRenderer.cullTransparentMesh = false;
        SetImage(baseCardImage, baseTexture);
        SetImage(stateCardImage, GetStateTexture(data.OwnerFactionId, data.Selected));
        SetText(titleTextField, data.Title, Color.black);

        if (data.EntityTexture != null)
        {
            SetManufacturingImage(entityImage, data.EntityTexture);
            SetText(currentNameTextField, data.CurrentName, Color.white);
            SetText(currentCountTextField, data.CurrentCount, Color.white);
            emptyTextField.gameObject.SetActive(false);
        }
        else
        {
            entityImage.gameObject.SetActive(false);
            currentNameTextField.gameObject.SetActive(false);
            currentCountTextField.gameObject.SetActive(false);
            SetText(emptyTextField, data.EmptyText, Color.white);
        }

        SetText(destinationTextField, data.DestinationText, Color.white);
        SetProgress(data.ManufacturingProgress, data.ManufacturingCost);

        SetText(
            facilityCountTextField,
            $"{data.ActiveFacilityCount}:{data.TotalFacilityCount}",
            Color.white
        );

        gameObject.SetActive(true);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        UIWindow window = GetComponentInParent<UIWindow>();
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            Pressed?.Invoke(this, eventData);
            window?.RequestContext(eventData);
        }
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            window?.RequestFocus();
            Pressed?.Invoke(this, eventData);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        Released?.Invoke(this, eventData);
    }

    public void OnDrop(PointerEventData eventData)
    {
        Dropped?.Invoke(this, eventData);
    }

    private void Awake()
    {
        VerifyReferences();
        CaptureTemplateLayout();
    }

    private void SetProgress(int progress, int cost)
    {
        int width =
            cost > 0
                ? Mathf.Clamp(
                    Mathf.RoundToInt(progress / (float)cost * progressFillWidth),
                    0,
                    Mathf.RoundToInt(progressFillWidth)
                )
                : 0;
        bool active = width > 0;
        progressFillImage.gameObject.SetActive(active);
        if (!active)
            return;

        progressFillImage.color = new Color32(255, 255, 84, 255);
        progressFillImage.raycastTarget = false;
        UILayout.SetSourceRect(
            progressFillImage.rectTransform,
            baseCardRect.x + 1,
            baseCardRect.y + baseCardRect.height - 9,
            width,
            4
        );
    }

    private void CaptureTemplateLayout()
    {
        if (capturedTemplateLayout)
            return;

        baseCardRect = UILayout.GetSourceRect(baseCardImage.rectTransform);
        entitySlotRoot = entityImage.transform.parent as RectTransform;
        entitySlotRect =
            entitySlotRoot != null && entitySlotRoot != transform
                ? UILayout.GetSourceRect(entitySlotRoot)
                : UILayout.GetSourceRect(entityImage.rectTransform);
        progressFillWidth = baseCardRect.width - 5;
        capturedTemplateLayout = true;
    }

    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (baseCardImage == null)
            throw new MissingReferenceException($"{name}/BaseCardImage is missing.");
        if (stateCardImage == null)
            throw new MissingReferenceException($"{name}/StateCardImage is missing.");
        if (baseTexture == null)
            throw new MissingReferenceException($"{name}/BaseTexture is missing.");
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

    private Texture2D GetStateTexture(string ownerFactionId, bool selected)
    {
        ManufacturingLaneStateTheme theme = uiContext
            ?.GetTheme(ownerFactionId)
            ?.GetBuildingsPaneTheme()
            ?.ManufacturingLaneState;
        return uiContext?.GetTexture(selected ? theme?.ActiveImagePath : theme?.InactiveImagePath);
    }

    private static void SetImage(RawImage image, Texture texture)
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = false;
    }

    private void SetManufacturingImage(RawImage image, Texture texture)
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = false;
        if (texture == null)
            return;

        if (image.transform.parent == entitySlotRoot && entitySlotRoot != null)
        {
            Vector2Int size = UILayout.GetFittedImageSize(texture, entitySlotRect);
            SetRightCenteredRect(image.rectTransform, size.x, size.y);
            return;
        }

        Vector2Int fittedSize = UILayout.GetFittedImageSize(texture, entitySlotRect);
        UILayout.SetImage(
            image,
            texture,
            entitySlotRect.x + entitySlotRect.width - fittedSize.x,
            entitySlotRect.y + (entitySlotRect.height - fittedSize.y) / 2,
            fittedSize.x,
            fittedSize.y
        );
    }

    private static void SetRightCenteredRect(RectTransform rect, int width, int height)
    {
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    private static void SetText(TextMeshProUGUI textField, string text, Color32 color)
    {
        textField.text = text ?? string.Empty;
        textField.color = color;
        textField.textWrappingMode = TextWrappingModes.NoWrap;
        textField.overflowMode = TextOverflowModes.Overflow;
        textField.raycastTarget = false;
        textField.gameObject.SetActive(true);
    }
}
