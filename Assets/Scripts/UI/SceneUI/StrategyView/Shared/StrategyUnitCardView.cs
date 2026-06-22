using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class StrategyUnitCardView
    : MonoBehaviour,
        IPointerDownHandler,
        IPointerClickHandler,
        IDropHandler
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

    private RectInt entityFrameRect;
    private bool layoutCaptured;
    private bool canDrag;

    public event Action<StrategyUnitCardView, PointerEventData> Pressed;
    public event Action<StrategyUnitCardView, PointerEventData> Released;
    public event Action<StrategyUnitCardView, PointerEventData> Dropped;

    public int Index { get; private set; }

    public void SetIndex(int index)
    {
        Index = index;
    }

    public void Render(StrategyUnitCardRenderData data)
    {
        data ??= new StrategyUnitCardRenderData();
        VerifyReferences();
        CaptureLayout();
        canDrag = data.CanDrag;

        hitAreaImage.enabled = true;
        hitAreaImage.raycastTarget = true;
        hitAreaImage.canvasRenderer.cullTransparentMesh = false;
        SetImage(backgroundImage, data.BackgroundTexture);
        SetCenteredImage(constructionOverlayImage, data.ConstructionOverlayTexture);
        SetCenteredImage(enrouteOverlayImage, data.EnrouteOverlayTexture);
        SetCenteredImage(damagedOverlayImage, data.DamagedOverlayTexture);
        SetCenteredImage(entityImage, data.EntityTexture);
        SetCenteredImage(capturedOverlayImage, data.CapturedOverlayTexture);
        SetImage(starfighterBadgeImage, data.StarfighterBadgeTexture);
        SetImage(troopBadgeImage, data.TroopBadgeTexture);
        SetImage(personnelBadgeImage, data.PersonnelBadgeTexture);
        SetImage(selectionImage, data.SelectionTexture);
        RenderName(data);
        gameObject.SetActive(true);
    }

    public bool TryGetDragImage(out Texture texture, out RectTransform sourceTransform)
    {
        texture = null;
        sourceTransform = null;
        VerifyReferences();
        CaptureLayout();
        if (!canDrag || entityImage == null || !entityImage.gameObject.activeInHierarchy)
            return false;

        texture = entityImage.texture;
        sourceTransform = entityImage.rectTransform;
        return texture != null
            && sourceTransform.rect.width > 0f
            && sourceTransform.rect.height > 0f;
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

    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (entityImage == null)
            throw new MissingReferenceException($"{name}/EntityImage is missing.");

        HideTemplate(alternateNameTextTemplate);
    }

    private void CaptureLayout()
    {
        if (layoutCaptured)
            return;

        entityFrameRect = UILayout.GetSourceRect(entityImage.rectTransform);
        layoutCaptured = true;
    }

    private void RenderName(StrategyUnitCardRenderData data)
    {
        if (nameTextField == null)
            return;

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

    private void SetCenteredImage(RawImage image, Texture texture)
    {
        if (image == null)
            return;

        UILayout.SetCenteredImage(image, texture, entityFrameRect);
    }

    private static void SetImage(RawImage image, Texture texture)
    {
        if (image == null)
            return;

        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = false;
    }

    private static void HideTemplate(Component component)
    {
        if (component != null)
            component.gameObject.SetActive(false);
    }
}

public sealed class StrategyUnitCardRenderData
{
    public string Name;
    public Color32 NameColor = Color.white;
    public bool ShowName = true;
    public bool UseAlternateNameLayout;
    public Texture BackgroundTexture;
    public Texture EntityTexture;
    public Texture ConstructionOverlayTexture;
    public Texture EnrouteOverlayTexture;
    public Texture DamagedOverlayTexture;
    public Texture CapturedOverlayTexture;
    public Texture SelectionTexture;
    public Texture StarfighterBadgeTexture;
    public Texture TroopBadgeTexture;
    public Texture PersonnelBadgeTexture;
    public bool CanDrag;
}
