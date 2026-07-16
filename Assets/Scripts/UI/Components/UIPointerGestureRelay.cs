using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Normalizes press, release, double-click, and drop gestures for authored UI controls.
/// </summary>
public sealed class UIPointerGestureRelay
    : MonoBehaviour,
        IPointerDownHandler,
        IPointerClickHandler,
        IDropHandler
{
    [SerializeField, Min(2)]
    private int doubleClickCount = 2;

    /// <summary>
    /// Occurs when the control is double-clicked.
    /// </summary>
    public event Action<PointerEventData> DoubleClicked;

    /// <summary>
    /// Occurs when a pointer drop is received by the control.
    /// </summary>
    public event Action<PointerEventData> Dropped;

    /// <summary>
    /// Occurs when the control is pressed.
    /// </summary>
    public event Action<PointerEventData> Pressed;

    /// <summary>
    /// Occurs when the control is released.
    /// </summary>
    public event Action<PointerEventData> Released;

    /// <summary>
    /// Emits a press for a supported pointer button.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsSupportedButton(eventData))
            Pressed?.Invoke(eventData);
    }

    /// <summary>
    /// Emits release and double-click gestures for the primary pointer button.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData?.button != PointerEventData.InputButton.Left)
            return;

        Released?.Invoke(eventData);
        if (eventData.clickCount >= doubleClickCount)
            DoubleClicked?.Invoke(eventData);
    }

    /// <summary>
    /// Emits a drop gesture for a valid pointer event.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData != null)
            Dropped?.Invoke(eventData);
    }

    /// <summary>
    /// Determines whether a pointer event uses a supported press button.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    /// <returns>True for primary and secondary pointer buttons.</returns>
    private static bool IsSupportedButton(PointerEventData eventData)
    {
        return eventData?.button
            is PointerEventData.InputButton.Left
                or PointerEventData.InputButton.Right;
    }
}
