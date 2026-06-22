using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class StrategyHudButtonView
    : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler
{
    [SerializeField]
    private RawImage hitAreaImage;

    private bool pressed;

    public event Action<StrategyHudButtonView, PointerEventData> Pressed;
    public event Action<StrategyHudButtonView, PointerEventData> Released;
    public event Action<StrategyHudButtonView, PointerEventData> Clicked;
    public event Action<StrategyHudButtonView, PointerEventData> ContextRequested;

    public int Action { get; private set; }

    public void Render(int action, SourceRectLayout hitArea)
    {
        VerifyReferences();
        enabled = true;
        Action = action;
        if (hitArea == null)
        {
            gameObject.SetActive(false);
            return;
        }

        UILayout.SetSourceRect(
            transform as RectTransform,
            hitArea.X,
            hitArea.Y,
            hitArea.Width,
            hitArea.Height
        );
        UILayout.SetSourceRect(hitAreaImage.rectTransform, 0, 0, hitArea.Width, hitArea.Height);
        hitAreaImage.enabled = true;
        hitAreaImage.raycastTarget = true;
        hitAreaImage.canvasRenderer.cullTransparentMesh = false;
        gameObject.SetActive(true);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            ContextRequested?.Invoke(this, eventData);
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left || Action == 0)
            return;

        pressed = true;
        Pressed?.Invoke(this, eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pressed || eventData.button != PointerEventData.InputButton.Left)
            return;

        pressed = false;
        Released?.Invoke(this, eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && Action != 0)
            Clicked?.Invoke(this, eventData);
    }

    private void Awake()
    {
        VerifyReferences();
    }

    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
    }
}
