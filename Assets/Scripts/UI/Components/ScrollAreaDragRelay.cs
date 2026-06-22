using UnityEngine;
using UnityEngine.EventSystems;

public sealed class ScrollAreaDragRelay
    : MonoBehaviour,
        IInitializePotentialDragHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IDropHandler
{
    private ScrollAreaView owner;

    public void Initialize(ScrollAreaView owner)
    {
        this.owner = owner;
    }

    public void Clear(ScrollAreaView owner)
    {
        if (this.owner == owner)
            this.owner = null;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData) { }

    public void OnBeginDrag(PointerEventData eventData)
    {
        owner?.RelayDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        owner?.RelayDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        owner?.RelayDragEnd(eventData);
    }

    public void OnDrop(PointerEventData eventData)
    {
        owner?.RelayDrop(eventData);
    }
}
