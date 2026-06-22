using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MissionListRowView
    : MonoBehaviour,
        IPointerDownHandler,
        IPointerClickHandler,
        IDropHandler
{
    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private RawImage iconImage;

    [SerializeField]
    private TextMeshProUGUI nameTextField;

    [SerializeField]
    private RawImage selectionImage;

    public event Action<MissionListRowView, PointerEventData> Pressed;
    public event Action<MissionListRowView, PointerEventData> Released;
    public event Action<MissionListRowView, PointerEventData> Dropped;

    public int Index { get; private set; }

    public void SetIndex(int index)
    {
        Index = index;
    }

    public void Render(MissionListRowRenderData data)
    {
        VerifyReferences();

        hitAreaImage.enabled = true;
        hitAreaImage.raycastTarget = true;
        hitAreaImage.canvasRenderer.cullTransparentMesh = false;
        UILayout.SetImageTexture(iconImage, data.IconTexture);
        UILayout.SetTextContent(nameTextField, data.Name, Color.white);
        UILayout.SetImageTexture(selectionImage, data.SelectionTexture);
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
        if (iconImage == null)
            throw new MissingReferenceException($"{name}/IconImage is missing.");
        if (nameTextField == null)
            throw new MissingReferenceException($"{name}/NameTextField is missing.");
        if (selectionImage == null)
            throw new MissingReferenceException($"{name}/SelectionImage is missing.");
    }
}

public sealed class MissionListRowRenderData
{
    public string Name;
    public Texture2D IconTexture;
    public Texture2D SelectionTexture;
}
