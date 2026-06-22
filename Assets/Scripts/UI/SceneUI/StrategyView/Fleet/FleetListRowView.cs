using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class FleetListRowView
    : MonoBehaviour,
        IStrategyUIContextReceiver,
        IPointerDownHandler,
        IPointerClickHandler,
        IDropHandler
{
    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private RawImage iconImage;

    [SerializeField]
    private RawImage enrouteOverlayImage;

    [SerializeField]
    private RawImage damagedOverlayImage;

    [SerializeField]
    private RawImage starfighterBadgeImage;

    [SerializeField]
    private RawImage troopBadgeImage;

    [SerializeField]
    private RawImage personnelBadgeImage;

    [SerializeField]
    private TextMeshProUGUI nameTextField;

    [SerializeField]
    private RawImage selectionImage;

    private UIContext uiContext;

    public event Action<FleetListRowView, PointerEventData> Pressed;
    public event Action<FleetListRowView, PointerEventData> Released;
    public event Action<FleetListRowView, PointerEventData> Dropped;

    public int Index { get; private set; }

    public void Initialize(UIContext uiContext)
    {
        this.uiContext = uiContext;
    }

    public void SetIndex(int index)
    {
        Index = index;
    }

    public void Render(FleetListRowRenderData data)
    {
        VerifyReferences();

        hitAreaImage.enabled = true;
        hitAreaImage.raycastTarget = true;
        hitAreaImage.canvasRenderer.cullTransparentMesh = false;
        UILayout.SetImageTexture(
            selectionImage,
            data.Selected ? GetSelectionTexture(data.OwnerFactionId) : null
        );
        UILayout.SetImageTexture(
            iconImage,
            data.IconTexture ?? GetIconTexture(data.OwnerFactionId)
        );
        UILayout.SetImage(
            enrouteOverlayImage,
            data.EnrouteOverlayTexture,
            Mathf.RoundToInt(iconImage.rectTransform.anchoredPosition.x),
            Mathf.RoundToInt(-iconImage.rectTransform.anchoredPosition.y)
        );
        UILayout.SetImage(
            damagedOverlayImage,
            data.DamagedOverlayTexture,
            Mathf.RoundToInt(iconImage.rectTransform.anchoredPosition.x),
            Mathf.RoundToInt(-iconImage.rectTransform.anchoredPosition.y)
        );
        UILayout.SetImageTexture(starfighterBadgeImage, data.StarfighterBadgeTexture);
        UILayout.SetImageTexture(troopBadgeImage, data.TroopBadgeTexture);
        UILayout.SetImageTexture(personnelBadgeImage, data.PersonnelBadgeTexture);
        UILayout.SetTextContent(nameTextField, data.Name, Color.white);
        gameObject.SetActive(true);
    }

    internal bool TryGetDragImage(out Texture texture, out RectTransform sourceTransform)
    {
        VerifyReferences();
        texture = iconImage.texture;
        sourceTransform = iconImage.rectTransform;
        return texture != null;
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
    }

    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (iconImage == null)
            throw new MissingReferenceException($"{name}/IconImage is missing.");
        if (enrouteOverlayImage == null)
            throw new MissingReferenceException($"{name}/EnrouteOverlayImage is missing.");
        if (damagedOverlayImage == null)
            throw new MissingReferenceException($"{name}/DamagedOverlayImage is missing.");
        if (starfighterBadgeImage == null)
            throw new MissingReferenceException($"{name}/StarfighterBadgeImage is missing.");
        if (troopBadgeImage == null)
            throw new MissingReferenceException($"{name}/TroopBadgeImage is missing.");
        if (personnelBadgeImage == null)
            throw new MissingReferenceException($"{name}/PersonnelBadgeImage is missing.");
        if (nameTextField == null)
            throw new MissingReferenceException($"{name}/NameTextField is missing.");
        if (selectionImage == null)
            throw new MissingReferenceException($"{name}/SelectionImage is missing.");
    }

    private Texture2D GetIconTexture(string ownerFactionId)
    {
        return uiContext?.GetTexture(
            uiContext?.GetTheme(ownerFactionId)?.GetFleetListIconImagePath()
        );
    }

    private Texture2D GetSelectionTexture(string ownerFactionId)
    {
        return uiContext?.GetTexture(
            uiContext?.GetTheme(ownerFactionId)?.GetFleetListSelectionImagePath()
        );
    }
}

public sealed class FleetListRowRenderData
{
    public string Name;
    public string OwnerFactionId;
    public Texture IconTexture;
    public Texture EnrouteOverlayTexture;
    public Texture DamagedOverlayTexture;
    public Texture StarfighterBadgeTexture;
    public Texture TroopBadgeTexture;
    public Texture PersonnelBadgeTexture;
    public bool Selected;
}
