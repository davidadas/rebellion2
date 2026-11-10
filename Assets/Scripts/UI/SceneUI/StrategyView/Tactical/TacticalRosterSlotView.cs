using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// One roster slot in the tactical battle's top bar: a unit portrait with a label, a highlight
/// when the unit is inspected or selected, and a dimmed state once the unit is gone.
/// </summary>
public sealed class TacticalRosterSlotView : MonoBehaviour
{
    private static readonly Color _liveColor = Color.white;
    private static readonly Color _goneColor = new Color(0.4f, 0.4f, 0.45f, 0.6f);
    private const int _portraitInset = 2;
    private const int _labelHeight = 12;
    private const int _fullHeight = 30;
    private const float _minimumLabelFontSize = 6f;

    [SerializeField]
    private Button button;

    [SerializeField]
    private RawImage highlightImage;

    [SerializeField]
    private RawImage portraitImage;

    [SerializeField]
    private TextMeshProUGUI label;

    private UnityAction _listener;
    private float _labelFontSize = -1f;
    private bool _labelVisible = true;

    /// <summary>
    /// Validates the authored references.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
    }

    /// <summary>
    /// Removes the click listener with the slot.
    /// </summary>
    private void OnDestroy()
    {
        if (button != null && _listener != null)
            button.onClick.RemoveListener(_listener);
        _listener = null;
    }

    /// <summary>
    /// Shows a roster entry and routes clicks to a callback.
    /// </summary>
    /// <param name="data">The entry to show.</param>
    /// <param name="pressed">Invoked when the slot is clicked.</param>
    internal void Render(TacticalRosterSlotRenderData data, Action pressed)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        if (_listener != null)
            button.onClick.RemoveListener(_listener);
        _listener = () => pressed?.Invoke();
        button.onClick.AddListener(_listener);

        portraitImage.texture = data.Portrait;
        portraitImage.enabled = data.Portrait != null;
        portraitImage.color = data.Alive ? _liveColor : _goneColor;
        highlightImage.gameObject.SetActive(data.Highlighted);
        UILayout.SetTextContent(label, data.Label);
        label.gameObject.SetActive(_labelVisible);
        label.color = data.Alive ? _liveColor : _goneColor;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Fits the portrait and label to a slot size, hiding the label when the slot is too narrow
    /// to read it. The slot's own rectangle is laid out by the roster bar.
    /// </summary>
    /// <param name="width">The slot width in source units.</param>
    /// <param name="height">The slot height in source units.</param>
    /// <param name="showLabel">Whether the label is shown.</param>
    internal void Resize(int width, int height, bool showLabel)
    {
        VerifyReferences();
        if (_labelFontSize < 0f)
            _labelFontSize = label.fontSize;

        int inset = Math.Clamp(width / 12, 1, _portraitInset);
        UILayout.SetSourceRect(
            portraitImage.rectTransform,
            inset,
            inset,
            Math.Max(width - 2 * inset, 1),
            Math.Max(height - 2 * inset, 1)
        );
        // The label keeps its layout even while hidden, so a slot that grows back shows it in
        // place; Render re-applies the visibility because setting text activates the label.
        _labelVisible = showLabel;
        label.gameObject.SetActive(showLabel);
        UILayout.SetSourceRect(
            label.rectTransform,
            inset,
            Math.Max(height - _labelHeight - inset, 0),
            Math.Max(width - 2 * inset, 1),
            _labelHeight
        );
        label.fontSize = Math.Max(_labelFontSize * height / _fullHeight, _minimumLabelFontSize);
    }

    /// <summary>
    /// Verifies the authored references.
    /// </summary>
    private void VerifyReferences()
    {
        if (button == null)
            throw new MissingReferenceException($"{name}/Button is missing.");
        if (highlightImage == null)
            throw new MissingReferenceException($"{name}/HighlightImage is missing.");
        if (portraitImage == null)
            throw new MissingReferenceException($"{name}/PortraitImage is missing.");
        if (label == null)
            throw new MissingReferenceException($"{name}/Label is missing.");
    }
}
