using TMPro;
using UnityEngine;

/// <summary>
/// Owns the shared tooltip used to reveal the complete value of a truncated text label.
/// </summary>
public sealed class OverflowTextTooltip : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI _textField;

    private bool _isHovered;
    private string _lastObservedText;
    private float _lastObservedWidth;
    private HoverTooltip _tooltip;

    /// <summary>
    /// Assigns the bounded text label revealed by this hover surface.
    /// </summary>
    /// <param name="textField">The bounded text label.</param>
    public void Configure(TextMeshProUGUI textField)
    {
        _textField = textField;
        ConfigureTextField();
    }

    /// <summary>
    /// Starts the delayed overflow tooltip when one of its hover surfaces is entered.
    /// </summary>
    internal void BeginHover()
    {
        _isHovered = true;
        RefreshHover(Time.unscaledTime, true);
    }

    /// <summary>
    /// Cancels the overflow tooltip when its active hover surface is exited.
    /// </summary>
    internal void EndHover()
    {
        _isHovered = false;
        _tooltip?.Cancel();
    }

    /// <summary>
    /// Configures the label for bounded, hoverable text.
    /// </summary>
    private void Awake()
    {
        if (_textField == null)
            _textField = GetComponent<TextMeshProUGUI>();
        if (_textField == null)
            throw new MissingReferenceException($"{name}/TextField is missing.");

        ConfigureTextField();
    }

    /// <summary>
    /// Configures the source label for bounded single-line presentation.
    /// </summary>
    private void ConfigureTextField()
    {
        if (_textField == null)
            return;

        _textField.textWrappingMode = TextWrappingModes.NoWrap;
        _textField.overflowMode = TextOverflowModes.Ellipsis;
        _textField.raycastTarget = false;
    }

    /// <summary>
    /// Hides the tooltip whenever the source label becomes inactive.
    /// </summary>
    private void OnDisable()
    {
        _isHovered = false;
        _tooltip?.Cancel();
    }

    /// <summary>
    /// Removes the sibling tooltip owned by this component.
    /// </summary>
    private void OnDestroy()
    {
        _tooltip?.Destroy();
    }

    /// <summary>
    /// Determines whether the complete text is wider than its rendered label.
    /// </summary>
    /// <returns>True when the label truncates its complete text.</returns>
    private bool IsOverflowing()
    {
        if (_textField == null || string.IsNullOrEmpty(_textField.text))
            return false;

        float availableWidth = _textField.rectTransform.rect.width;
        float preferredWidth = _textField
            .GetPreferredValues(
                _textField.text,
                float.PositiveInfinity,
                _textField.rectTransform.rect.height
            )
            .x;
        return preferredWidth > availableWidth;
    }

    /// <summary>
    /// Advances the shared delayed tooltip using the current unscaled time.
    /// </summary>
    private void Update()
    {
        AdvanceHover(Time.unscaledTime);
    }

    /// <summary>
    /// Advances a pending tooltip reveal using an explicit unscaled timestamp.
    /// </summary>
    /// <param name="unscaledTime">The current unscaled time.</param>
    internal void AdvanceHover(float unscaledTime)
    {
        RefreshHover(unscaledTime, false);
        _tooltip?.Advance(unscaledTime);
    }

    /// <summary>
    /// Creates the shared tooltip presentation when needed.
    /// </summary>
    private void EnsureTooltip()
    {
        if (_tooltip == null)
        {
            _tooltip = new HoverTooltip(
                _textField.transform.parent as RectTransform,
                $"{name}Tooltip",
                int.MaxValue,
                true
            );
        }
    }

    /// <summary>
    /// Revalidates overflow when the hovered label or its available width changes.
    /// </summary>
    /// <param name="unscaledTime">The current unscaled time.</param>
    /// <param name="force">Whether to refresh even when the observed source is unchanged.</param>
    private void RefreshHover(float unscaledTime, bool force)
    {
        if (!_isHovered || _textField == null)
            return;

        string text = _textField.text;
        float width = _textField.rectTransform.rect.width;
        if (!force && text == _lastObservedText && Mathf.Approximately(width, _lastObservedWidth))
            return;

        _lastObservedText = text;
        _lastObservedWidth = width;
        EnsureTooltip();
        if (IsOverflowing())
            _tooltip.Begin(GetTooltipContent, unscaledTime);
        else
            _tooltip.Cancel();
    }

    /// <summary>
    /// Resolves current text and bounds after the shared delay has elapsed.
    /// </summary>
    /// <returns>Current tooltip content, or null when the label no longer overflows.</returns>
    private HoverTooltipContent? GetTooltipContent()
    {
        return IsOverflowing()
            ? new HoverTooltipContent(
                _textField.text,
                UILayout.GetSourceRect(_textField.rectTransform)
            )
            : null;
    }
}
