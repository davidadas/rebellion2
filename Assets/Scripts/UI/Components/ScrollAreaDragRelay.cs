using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Relays pointer gestures from an authored scroll surface to its owning scroll-area view.
/// </summary>
public sealed class ScrollAreaDragRelay
    : MonoBehaviour,
        IInitializePotentialDragHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IDropHandler,
        IScrollHandler
{
    private ScrollAreaView owner;

    /// <summary>
    /// Assigns the active scroll-area owner.
    /// </summary>
    /// <param name="owner">The scroll-area view receiving pointer gestures.</param>
    public void Initialize(ScrollAreaView owner)
    {
        this.owner = owner;
    }

    /// <summary>
    /// Clears the owner when it still matches the requesting scroll area.
    /// </summary>
    /// <param name="owner">The scroll-area view being disabled.</param>
    public void Clear(ScrollAreaView owner)
    {
        if (this.owner == owner)
            this.owner = null;
    }

    /// <summary>
    /// Participates in Unity's drag lifecycle without taking ownership of the initial gesture.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnInitializePotentialDrag(PointerEventData eventData) { }

    /// <summary>
    /// Begins a relayed drag gesture.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnBeginDrag(PointerEventData eventData)
    {
        owner?.RelayDrag(eventData);
    }

    /// <summary>
    /// Relays an active drag gesture.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnDrag(PointerEventData eventData)
    {
        owner?.RelayDrag(eventData);
    }

    /// <summary>
    /// Relays the end of a drag gesture.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnEndDrag(PointerEventData eventData)
    {
        owner?.RelayDragEnd(eventData);
    }

    /// <summary>
    /// Relays a pointer drop.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnDrop(PointerEventData eventData)
    {
        owner?.RelayDrop(eventData);
    }

    /// <summary>
    /// Relays a pointer-wheel gesture.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnScroll(PointerEventData eventData)
    {
        owner?.RelayScroll(eventData);
    }
}
