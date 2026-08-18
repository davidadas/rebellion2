using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a keyed Boolean option in the Options menu.
/// </summary>
public sealed class OptionsToggleRowView : MonoBehaviour, IContentInitializable
{
    private static readonly Color _onTextColor = Color.white;
    private static readonly Color _offTextColor = new Color(0.55f, 0.63f, 0.73f);

    [SerializeField]
    private int _optionIndex;

    [SerializeField]
    private Image _toggleImage;

    [SerializeField]
    private Sprite _offSprite;

    [SerializeField]
    private string _offSpriteAddress;

    [SerializeField]
    private Sprite _onSprite;

    [SerializeField]
    private string _onSpriteAddress;

    [SerializeField]
    private Button _button;

    [SerializeField]
    private TextMeshProUGUI _labelTextField;

    [SerializeField]
    private TextMeshProUGUI _stateTextField;

    private bool _bound;

    public int OptionIndex => _optionIndex;
    public event Action<int> ToggleRequested;

    /// <summary>
    /// Restores the state-swapped toggle sprites from installation content.
    /// </summary>
    /// <param name="contentAssets">The active content asset source.</param>
    public void InitializeContent(IContentAssetSource contentAssets)
    {
        _offSprite = ContentBindings.RequireSprite(contentAssets, _offSpriteAddress);
        _onSprite = ContentBindings.RequireSprite(contentAssets, _onSpriteAddress);
    }

    /// <summary>
    /// Renders the current option state.
    /// </summary>
    /// <param name="enabled">Whether the option is enabled.</param>
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
        if (
            string.IsNullOrWhiteSpace(_offSpriteAddress)
            || string.IsNullOrWhiteSpace(_onSpriteAddress)
        )
            throw new MissingReferenceException("Toggle sprite addresses are missing.");
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
    /// Requests a change to the option.
    /// </summary>
    private void RequestToggle()
    {
        ToggleRequested?.Invoke(_optionIndex);
    }
}
