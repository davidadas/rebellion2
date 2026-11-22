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
    private TMP_FontAsset font;

    /// <summary>
    /// Rebinds a pooled field or caption without retaining a previous field's guidance.
    /// </summary>
    /// <param name="description">The explanation to show.</param>
    /// <param name="textInput">The input whose editing should suppress the tooltip.</param>
    /// <param name="captionFont">The font used by the adjacent editor caption.</param>
    public void Configure(
        string description,
        TMP_InputField textInput = null,
        TMP_FontAsset captionFont = null
    )
    {
        text = description;
        input = textInput;
        font = captionFont ?? textInput?.textComponent.font;
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
            float zoom = transform.lossyScale.y / surface.lossyScale.y;
            int fontSize = Mathf.Clamp(Mathf.RoundToInt(16 * zoom), 12, 20);
            tooltip = new HoverTooltip(surface, "Field help", 400, true, fontSize, font);
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
    /// Centers the help marker next to the first visible caption line.
    /// </summary>
    /// <param name="caption">The laid-out caption beside the marker.</param>
    /// <param name="left">The caption's horizontal offset in the marker parent.</param>
    /// <param name="top">The caption's vertical offset in the marker parent.</param>
    public void PositionBeside(TMP_Text caption, int left = 0, int top = 0)
    {
        RectTransform marker = (RectTransform)transform;
        int size = Mathf.RoundToInt(marker.rect.width);
        caption.ForceMeshUpdate(ignoreActiveState: true);
        bool hasLine = caption.textInfo.lineCount > 0;
        TMP_LineInfo line = hasLine ? caption.textInfo.lineInfo[0] : default;
        float end = hasLine ? line.maxAdvance : caption.preferredWidth;
        float center = hasLine
            ? -(line.ascender + line.descender) * 0.5f
            : caption.fontSize * 0.55f;
        UILayout.SetSourceRect(
            marker,
            left + Mathf.RoundToInt(Mathf.Min(end, caption.rectTransform.rect.width) + 4),
            top + Mathf.RoundToInt(center - size * 0.5f),
            size,
            size
        );
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
