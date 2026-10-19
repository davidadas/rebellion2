using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Forwards one pointer surface to a shared overflow-text tooltip owner.
/// </summary>
public sealed class OverflowTextTooltipTrigger
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
{
    [SerializeField]
    private OverflowTextTooltip _owner;

    /// <summary>
    /// Assigns the tooltip owner shared by this surface and any adjacent surfaces.
    /// </summary>
    /// <param name="owner">The shared overflow-tooltip owner.</param>
    public void Configure(OverflowTextTooltip owner)
    {
        _owner = owner;
    }

    /// <summary>
    /// Starts the owner's delayed reveal.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        _owner?.BeginHover();
    }

    /// <summary>
    /// Cancels the owner's reveal.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        _owner?.EndHover();
    }
}
