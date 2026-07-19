using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Converts pointer presses on the authored dismissal surface into a semantic event.
/// </summary>
public sealed class ContextMenuDismissBoundary : MonoBehaviour, IPointerDownHandler
{
    /// <summary>
    /// Raised when the pointer presses the dismissal surface.
    /// </summary>
    public event Action<PointerEventData> PointerDown;

    /// <summary>
    /// Forwards one pointer press from the Unity event system.
    /// </summary>
    /// <param name="eventData">The pointer press.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        PointerDown?.Invoke(eventData);
    }
}
