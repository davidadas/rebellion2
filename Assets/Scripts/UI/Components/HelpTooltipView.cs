using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Shows field guidance using the shared delayed tooltip presentation.
/// </summary>
public sealed class HelpTooltipView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField]
    private string text;

    [SerializeField]
    private TMP_InputField input;
    private HoverTooltip tooltip;
    private RectTransform surface;
    private Vector2 pointerPosition;

    /// <summary>
    /// Rebinds a pooled field or caption without retaining a previous field's guidance.
    /// </summary>
    /// <param name="description">The explanation to show.</param>
    /// <param name="textInput">The input whose editing should suppress the tooltip.</param>
    public void Configure(string description, TMP_InputField textInput = null)
    {
        text = description;
        input = textInput;
        tooltip?.Cancel();
    }

    /// <summary>
    /// Starts a delayed reveal beside the pointer in the owning canvas.
    /// </summary>
    /// <param name="eventData">The entering pointer.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(text))
            return;
        if (tooltip == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                return;
            surface = (RectTransform)canvas.transform;
            tooltip = new HoverTooltip(surface, "Field help", 400, true, 15);
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            surface,
            eventData.position,
            eventData.enterEventCamera,
            out pointerPosition
        );
        tooltip.Begin(GetContent, Time.unscaledTime);
    }

    /// <summary>
    /// Cancels guidance when the pointer leaves its source.
    /// </summary>
    /// <param name="eventData">The leaving pointer.</param>
    public void OnPointerExit(PointerEventData eventData) => tooltip?.Cancel();

    /// <summary>
    /// Advances only the shared tooltip timer and suppresses guidance during text editing.
    /// </summary>
    private void Update()
    {
        if (input?.isFocused == true)
            tooltip?.Cancel();
        else
            tooltip?.Advance(Time.unscaledTime);
    }

    /// <summary>
    /// Hides guidance while pooled fields are unused.
    /// </summary>
    private void OnDisable() => tooltip?.Cancel();

    /// <summary>
    /// Releases the presentation owned by this hover surface.
    /// </summary>
    private void OnDestroy() => tooltip?.Destroy();

    /// <summary>
    /// Resolves the current explanation and canvas-space anchor after the hover delay.
    /// </summary>
    /// <returns>The current tooltip content.</returns>
    private HoverTooltipContent? GetContent()
    {
        Rect rect = surface.rect;
        return new HoverTooltipContent(
            text,
            new RectInt(
                Mathf.RoundToInt(pointerPosition.x - rect.xMin),
                Mathf.RoundToInt(rect.yMax - pointerPosition.y) + 12,
                1,
                1
            )
        );
    }
}
