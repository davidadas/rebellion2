using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a tactical option in the Options menu.
/// </summary>
public sealed class OptionsToggleRowView : MonoBehaviour
{
    private static readonly Color _onTextColor = Color.white;
    private static readonly Color _offTextColor = new Color(0.55f, 0.63f, 0.73f);

    [SerializeField]
    private UserTacticalOption _option;

    [SerializeField]
    private Image _toggleImage;

    [SerializeField]
    private Sprite _offSprite;

    [SerializeField]
    private Sprite _onSprite;

    [SerializeField]
    private Button _button;

    [SerializeField]
    private TextMeshProUGUI _labelTextField;

    [SerializeField]
    private TextMeshProUGUI _stateTextField;

    private bool _bound;

    // Toggle Info.
    public UserTacticalOption Option => _option;
    public event Action<UserTacticalOption> ToggleRequested;

    /// <summary>
    /// Renders the current option state.
    /// </summary>
    /// <param name="enabled">Whether the tactical option is enabled.</param>
    public void Render(bool enabled)
    {
        VerifyReferences();
        BindControls();
        _toggleImage.sprite = enabled ? _onSprite : _offSprite;
        _labelTextField.color = enabled ? _onTextColor : _offTextColor;
        _stateTextField.text = enabled ? "ON" : "OFF";
        _stateTextField.color = enabled ? _onTextColor : _offTextColor;
    }

    /// <summary>
    /// Checks the toggle row references.
    /// </summary>
    public void VerifyReferences()
    {
        if (_toggleImage == null)
            throw new MissingReferenceException("ToggleImage is missing.");
        if (_offSprite == null || _onSprite == null)
            throw new MissingReferenceException("Toggle sprites are missing.");
        if (_button == null)
            throw new MissingReferenceException("Button is missing.");
        if (_labelTextField == null)
            throw new MissingReferenceException("LabelTextField is missing.");
        if (_stateTextField == null)
            throw new MissingReferenceException("StateTextField is missing.");
    }

    /// <summary>
    /// Adds the toggle button listener.
    /// </summary>
    private void OnEnable()
    {
        if (_button != null)
            BindControls();
    }

    /// <summary>
    /// Removes the button listener while the row is inactive.
    /// </summary>
    private void OnDisable()
    {
        if (!_bound)
            return;

        _button.onClick.RemoveListener(RequestToggle);
        _bound = false;
    }

    /// <summary>
    /// Adds the toggle listener.
    /// </summary>
    private void BindControls()
    {
        if (_bound)
            return;

        _button.onClick.AddListener(RequestToggle);
        _bound = true;
    }

    /// <summary>
    /// Requests a change to the tactical option.
    /// </summary>
    private void RequestToggle()
    {
        ToggleRequested?.Invoke(_option);
    }
}
