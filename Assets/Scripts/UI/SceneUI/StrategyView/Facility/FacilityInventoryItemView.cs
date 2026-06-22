using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class FacilityInventoryItemView
    : MonoBehaviour,
        IPointerDownHandler,
        IPointerClickHandler,
        IDropHandler
{
    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private RawImage itemImage;

    [SerializeField]
    private RawImage selectionImage;

    public event Action<FacilityInventoryItemView, PointerEventData> Pressed;
    public event Action<FacilityInventoryItemView, PointerEventData> Released;
    public event Action<FacilityInventoryItemView, PointerEventData> Dropped;

    public int Index { get; private set; }
    public Texture TemplateTexture => itemImage == null ? null : itemImage.texture;

    public void Render(
        int index,
        FacilityInventoryItemRenderData data,
        Texture selectionTexture,
        RectInt frame
    )
    {
        VerifyReferences();

        Index = index;
        UILayout.SetSourceRect(
            transform as RectTransform,
            frame.x,
            frame.y,
            frame.width,
            frame.height
        );
        UILayout.SetSourceRect(hitAreaImage.rectTransform, 0, 0, frame.width, frame.height);
        hitAreaImage.enabled = true;
        hitAreaImage.raycastTarget = true;
        hitAreaImage.canvasRenderer.cullTransparentMesh = false;
        SetImage(
            itemImage,
            data.Texture,
            0,
            0,
            data.Texture?.width ?? frame.width,
            data.Texture?.height ?? frame.height
        );
        SetImage(
            selectionImage,
            data.Selected ? selectionTexture : null,
            0,
            0,
            frame.width,
            frame.height
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
    }

    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (itemImage == null)
            throw new MissingReferenceException($"{name}/ItemImage is missing.");
        if (selectionImage == null)
            throw new MissingReferenceException($"{name}/SelectionImage is missing.");
    }

    private static void SetImage(
        RawImage image,
        Texture texture,
        int x,
        int y,
        int width,
        int height
    )
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = false;
        if (texture != null)
            UILayout.SetSourceRect(image.rectTransform, x, y, width, height);
    }
}
