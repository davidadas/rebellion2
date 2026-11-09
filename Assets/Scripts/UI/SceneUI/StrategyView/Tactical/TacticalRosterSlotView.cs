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

    [SerializeField]
    private Button button;

    [SerializeField]
    private RawImage highlightImage;

    [SerializeField]
    private RawImage portraitImage;

    [SerializeField]
    private TextMeshProUGUI label;

    private UnityAction _listener;

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
        label.color = data.Alive ? _liveColor : _goneColor;
        gameObject.SetActive(true);
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
