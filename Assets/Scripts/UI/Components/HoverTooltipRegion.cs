using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Forwards an ordinary pointer region to an existing shared tooltip owner.
/// </summary>
public sealed class HoverTooltipRegion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private HoverTooltip _owner;
    private HoverTooltipContent _content;
    private bool _hovering;

    /// <summary>
    /// Binds this region to its existing tooltip renderer and reveal delay.
    /// </summary>
    /// <param name="owner">The shared tooltip owner.</param>
    /// <param name="text">The region's authored tooltip text.</param>
    /// <param name="sourceBounds">The region's bounds in the tooltip parent's coordinate space.</param>
    public void Configure(HoverTooltip owner, string text, RectInt sourceBounds)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _content = new HoverTooltipContent(text, sourceBounds);
    }

    /// <summary>
    /// Starts the shared owner's delayed reveal for this region.
    /// </summary>
    /// <param name="eventData">The entering pointer.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovering = true;
        _owner?.Begin(GetContent, Time.unscaledTime);
    }

    /// <summary>
    /// Cancels this region's reveal when its pointer leaves.
    /// </summary>
    /// <param name="eventData">The exiting pointer.</param>
    public void OnPointerExit(PointerEventData eventData) => EndHover();

    /// <summary>
    /// Ends any owned reveal when the region's panel is hidden.
    /// </summary>
    private void OnDisable() => EndHover();

    /// <summary>
    /// Resolves the retained text and anchor when the shared reveal delay expires.
    /// </summary>
    /// <returns>The configured region content.</returns>
    private HoverTooltipContent? GetContent() => _content;

    /// <summary>
    /// Releases this region's pending or visible tooltip once.
    /// </summary>
    private void EndHover()
    {
        if (!_hovering)
            return;
        _hovering = false;
        _owner?.Cancel();
    }
}
