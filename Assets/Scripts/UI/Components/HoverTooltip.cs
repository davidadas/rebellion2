using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Describes the text and source-space anchor for a hover tooltip.
/// </summary>
public readonly struct HoverTooltipContent
{
    /// <summary>
    /// Gets the tooltip anchor bounds.
    /// </summary>
    public RectInt SourceBounds { get; }

    /// <summary>
    /// Gets the tooltip text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Creates tooltip content.
    /// </summary>
    /// <param name="text">The text to display.</param>
    /// <param name="sourceBounds">The anchor bounds relative to the tooltip parent.</param>
    public HoverTooltipContent(string text, RectInt sourceBounds)
    {
        Text = text;
        SourceBounds = sourceBounds;
    }
}

/// <summary>
/// Owns the delay, presentation, and lifecycle of one shared hover tooltip.
/// </summary>
public sealed class HoverTooltip
{
    public const float HoverDelaySeconds = 0.5f;

    private const int _borderWidth = 1;
    private const int _gap = 1;
    private const int _height = 14;
    private const int _horizontalPadding = 3;
    private const int _minimumWidth = 20;

    private readonly int _maximumWidth;
    private readonly int _fontSize;
    private readonly string _name;
    private readonly RectTransform _parent;
    private readonly bool _wrapText;
    private Image _background;
    private Func<HoverTooltipContent?> _contentProvider;
    private RectTransform _root;
    private float _showTime;
    private TextMeshProUGUI _text;

    /// <summary>
    /// Gets whether the tooltip is currently visible.
    /// </summary>
    public bool IsVisible => _root?.gameObject.activeSelf == true;

    /// <summary>
    /// Creates a tooltip owned by the supplied source-space parent.
    /// </summary>
    /// <param name="parent">The parent that contains the tooltip and its anchor.</param>
    /// <param name="name">The tooltip object's name.</param>
    /// <param name="maximumWidth">The maximum width within the supplied parent.</param>
    /// <param name="wrapText">Whether long content may expand onto multiple lines.</param>
    /// <param name="fontSize">The text size in the parent's coordinate space.</param>
    public HoverTooltip(
        RectTransform parent,
        string name,
        int maximumWidth = 160,
        bool wrapText = false,
        int fontSize = 8
    )
    {
        _parent = parent != null ? parent : throw new ArgumentNullException(nameof(parent));
        _name = !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException("A tooltip name is required.", nameof(name));
        _maximumWidth =
            maximumWidth >= _minimumWidth
                ? maximumWidth
                : throw new ArgumentOutOfRangeException(nameof(maximumWidth));
        _wrapText = wrapText;
        _fontSize =
            fontSize > 0 ? fontSize : throw new ArgumentOutOfRangeException(nameof(fontSize));
    }

    /// <summary>
    /// Starts a delayed reveal using content resolved when the delay expires.
    /// </summary>
    /// <param name="contentProvider">Provides current text and anchor bounds.</param>
    /// <param name="unscaledTime">The current unscaled time.</param>
    public void Begin(Func<HoverTooltipContent?> contentProvider, float unscaledTime)
    {
        _contentProvider =
            contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
        _showTime = unscaledTime + HoverDelaySeconds;
        Hide();
    }

    /// <summary>
    /// Reveals pending content once the shared hover delay has elapsed.
    /// </summary>
    /// <param name="unscaledTime">The current unscaled time.</param>
    public void Advance(float unscaledTime)
    {
        if (_contentProvider == null || unscaledTime < _showTime || IsVisible)
            return;

        HoverTooltipContent? content = _contentProvider();
        if (!content.HasValue || string.IsNullOrEmpty(content.Value.Text))
        {
            Cancel();
            return;
        }

        EnsurePresentation();
        Show(content.Value);
    }

    /// <summary>
    /// Cancels a pending reveal and hides visible content.
    /// </summary>
    public void Cancel()
    {
        _contentProvider = null;
        _showTime = 0f;
        Hide();
    }

    /// <summary>
    /// Destroys the tooltip hierarchy.
    /// </summary>
    public void Destroy()
    {
        if (_root == null)
            return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(_root.gameObject);
        else
            UnityEngine.Object.DestroyImmediate(_root.gameObject);
        _root = null;
        _background = null;
        _text = null;
    }

    /// <summary>
    /// Creates the shared tooltip presentation when first needed.
    /// </summary>
    private void EnsurePresentation()
    {
        if (_root != null)
            return;

        GameObject rootObject = new GameObject(
            _name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        rootObject.transform.SetParent(_parent, false);
        Image border = rootObject.GetComponent<Image>();
        border.color = Color.black;
        border.raycastTarget = false;
        _root = border.rectTransform;

        GameObject backgroundObject = new GameObject(
            "Background",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        backgroundObject.transform.SetParent(rootObject.transform, false);
        _background = backgroundObject.GetComponent<Image>();
        _background.color = new Color32(255, 255, 225, 255);
        _background.raycastTarget = false;

        GameObject textObject = new GameObject(
            "Text",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        textObject.transform.SetParent(rootObject.transform, false);
        _text = textObject.GetComponent<TextMeshProUGUI>();
        _text.color = Color.black;
        _text.fontSize = _fontSize;
        _text.alignment = TextAlignmentOptions.Center;
        _text.textWrappingMode = _wrapText ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        _text.overflowMode = _wrapText ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
        _text.raycastTarget = false;
        rootObject.SetActive(false);
    }

    /// <summary>
    /// Hides the tooltip presentation when it exists.
    /// </summary>
    private void Hide()
    {
        if (_root != null)
            _root.gameObject.SetActive(false);
    }

    /// <summary>
    /// Positions and displays resolved tooltip content.
    /// </summary>
    /// <param name="content">The resolved tooltip content.</param>
    private void Show(HoverTooltipContent content)
    {
        _text.text = content.Text;
        RectInt parentBounds = UILayout.GetSourceRect(_parent);
        int maximumWidth = Mathf.Max(_minimumWidth, parentBounds.width - 2);
        int width = Mathf.Clamp(
            Mathf.CeilToInt(_text.preferredWidth) + 2 * (_horizontalPadding + _borderWidth),
            _minimumWidth,
            Mathf.Min(_maximumWidth, maximumWidth)
        );
        int textWidth = width - 2 * (_borderWidth + _horizontalPadding);
        int minimumHeight = Mathf.Max(_height, _fontSize + 6);
        int height = minimumHeight;
        if (_wrapText)
        {
            float preferredHeight = _text
                .GetPreferredValues(content.Text, textWidth, float.PositiveInfinity)
                .y;
            height = Mathf.Clamp(
                Mathf.CeilToInt(preferredHeight) + 2 * _borderWidth,
                minimumHeight,
                Mathf.Max(minimumHeight, parentBounds.height - 2)
            );
        }
        int x = content.SourceBounds.x + (content.SourceBounds.width - width) / 2;
        x = Mathf.Clamp(x, 1, Mathf.Max(1, parentBounds.width - width - 1));
        int y = content.SourceBounds.yMax + _gap;
        if (y + height > parentBounds.height - 1)
            y = content.SourceBounds.y - _gap - height;
        y = Mathf.Clamp(y, 1, Mathf.Max(1, parentBounds.height - height - 1));

        UILayout.SetSourceRect(_root, x, y, width, height);
        UILayout.SetSourceRect(
            _background.rectTransform,
            _borderWidth,
            _borderWidth,
            width - 2 * _borderWidth,
            height - 2 * _borderWidth
        );
        UILayout.SetSourceRect(
            _text.rectTransform,
            _borderWidth + _horizontalPadding,
            _borderWidth,
            textWidth,
            height - 2 * _borderWidth
        );
        _root.gameObject.SetActive(true);
        _root.SetAsLastSibling();
    }
}
