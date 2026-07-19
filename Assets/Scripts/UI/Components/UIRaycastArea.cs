using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Emits pointer interaction from an authored transparent hit area.
/// </summary>
public sealed class UIRaycastArea
    : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler
{
    [SerializeField]
    private RawImage raycastTargetImage;

    private bool pressed;

    /// <summary>
    /// Occurs when the control is pressed.
    /// </summary>
    public event Action<UIRaycastArea, PointerEventData> Pressed;

    /// <summary>
    /// Occurs when the control is released.
    /// </summary>
    public event Action<UIRaycastArea, PointerEventData> Released;

    /// <summary>
    /// Occurs when the control is clicked.
    /// </summary>
    public event Action<UIRaycastArea, PointerEventData> Clicked;

    /// <summary>
    /// Occurs when a context request is raised.
    /// </summary>
    public event Action<UIRaycastArea, PointerEventData> ContextRequested;

    /// <summary>
    /// Occurs when the pointer enters the control.
    /// </summary>
    public event Action<UIRaycastArea, PointerEventData> Entered;

    /// <summary>
    /// Occurs when the pointer exits the control.
    /// </summary>
    public event Action<UIRaycastArea, PointerEventData> Exited;

    /// <summary>
    /// Applies source-space bounds to the authored hit area.
    /// </summary>
    /// <param name="hitArea">The configured hit-area bounds.</param>
    public void Render(SourceRectLayout hitArea)
    {
        if (hitArea == null)
        {
            Render((RectInt?)null);
            return;
        }

        Render(new RectInt(hitArea.X, hitArea.Y, hitArea.Width, hitArea.Height));
    }

    /// <summary>
    /// Applies immutable source-space bounds to the authored hit area.
    /// </summary>
    /// <param name="hitArea">The optional source-space hit-area bounds.</param>
    public void Render(RectInt? hitArea)
    {
        VerifyReferences();
        enabled = true;
        if (hitArea == null)
        {
            gameObject.SetActive(false);
            return;
        }

        RectInt bounds = hitArea.Value;
        UILayout.SetSourceRect(
            transform as RectTransform,
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height
        );
        UILayout.SetSourceRect(raycastTargetImage.rectTransform, 0, 0, bounds.width, bounds.height);
        raycastTargetImage.enabled = true;
        raycastTargetImage.raycastTarget = true;
        raycastTargetImage.canvasRenderer.cullTransparentMesh = false;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Emits a left-button press or right-button context request.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            ContextRequested?.Invoke(this, eventData);
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        pressed = true;
        Pressed?.Invoke(this, eventData);
    }

    /// <summary>
    /// Emits release of an active left-button press.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pressed || eventData.button != PointerEventData.InputButton.Left)
            return;

        pressed = false;
        Released?.Invoke(this, eventData);
    }

    /// <summary>
    /// Emits a completed left-button click.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            Clicked?.Invoke(this, eventData);
    }

    /// <summary>
    /// Emits pointer entry into the hit area.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        Entered?.Invoke(this, eventData);
    }

    /// <summary>
    /// Emits pointer exit from the hit area.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        Exited?.Invoke(this, eventData);
    }

    /// <summary>
    /// Verifies the authored hit-area reference before interaction begins.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
    }

    /// <summary>
    /// Verifies the authored hit-area image.
    /// </summary>
    private void VerifyReferences()
    {
        if (raycastTargetImage == null)
            throw new MissingReferenceException($"{name}/RaycastTargetImage is missing.");
    }
}
