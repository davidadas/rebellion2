using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MissionParticipantRowView
    : MonoBehaviour,
        IPointerDownHandler,
        IPointerClickHandler
{
    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private RawImage entityImage;

    [SerializeField]
    private TextMeshProUGUI nameTextField;

    [SerializeField]
    private Texture2D backgroundTexture;

    private RectInt entitySlotRect;
    private bool hasEntitySlotRect;

    public event Action<MissionParticipantRowView, PointerEventData> Clicked;

    public int ListId { get; private set; }
    public int Index { get; private set; }

    public void SetPosition(int listId, int index)
    {
        ListId = listId;
        Index = index;
    }

    public void Render(MissionParticipantRowRenderData data)
    {
        VerifyReferences();

        SetImage(backgroundImage, data.BackgroundTexture ?? backgroundTexture);
        backgroundImage.raycastTarget = true;
        UILayout.SetCenteredImage(entityImage, data.EntityTexture, entitySlotRect);
        SetText(nameTextField, data.Name, data.NameColor);
        gameObject.SetActive(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            Clicked?.Invoke(this, eventData);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        UIWindow window = GetComponentInParent<UIWindow>();
        if (eventData.button == PointerEventData.InputButton.Right)
            window?.RequestContext(eventData);
        else if (eventData.button == PointerEventData.InputButton.Left)
            window?.RequestFocus();
    }

    private void Awake()
    {
        VerifyReferences();
    }

    private void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (entityImage == null)
            throw new MissingReferenceException($"{name}/EntityImage is missing.");
        if (nameTextField == null)
            throw new MissingReferenceException($"{name}/NameTextField is missing.");
        if (backgroundTexture == null)
            throw new MissingReferenceException($"{name}/BackgroundTexture is missing.");

        if (!hasEntitySlotRect)
        {
            entitySlotRect = UILayout.GetSourceRect(entityImage.rectTransform);
            hasEntitySlotRect = true;
        }
    }

    private static void SetImage(RawImage image, Texture texture)
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = false;
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

public sealed class MissionParticipantRowRenderData
{
    public string Name;
    public Color32 NameColor;
    public Texture2D BackgroundTexture;
    public Texture2D EntityTexture;
}
