using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders one Options detail toggle as a themed sliced pill and emits a typed toggle request.
/// </summary>
public sealed class OptionsToggleRowView : MonoBehaviour
{
    private static readonly Color OnTextColor = Color.white;
    private static readonly Color OffTextColor = new Color(0.55f, 0.63f, 0.73f);

    [SerializeField]
    private UserTacticalOption option;

    [SerializeField]
    private Image toggleImage;

    [SerializeField]
    private Sprite offSprite;

    [SerializeField]
    private Sprite onSprite;

    [SerializeField]
    private Button button;

    [SerializeField]
    private TextMeshProUGUI labelTextField;

    [SerializeField]
    private TextMeshProUGUI stateTextField;

    private bool bound;

    /// <summary>Gets the tactical option this row toggles.</summary>
    public UserTacticalOption Option => option;

    /// <summary>Occurs when the player requests toggling this row's configured option.</summary>
    public event Action<UserTacticalOption> ToggleRequested;

    /// <summary>
    /// Renders the current option state.
    /// </summary>
    /// <param name="enabled">Whether the tactical option is enabled.</param>
    public void Render(bool enabled)
    {
        VerifyReferences();
        BindControls();
        toggleImage.sprite = enabled ? onSprite : offSprite;
        labelTextField.color = enabled ? OnTextColor : OffTextColor;
        stateTextField.text = enabled ? "ON" : "OFF";
        stateTextField.color = enabled ? OnTextColor : OffTextColor;
    }

    /// <summary>
    /// Verifies every authored reference required by the row.
    /// </summary>
    public void VerifyReferences()
    {
        if (toggleImage == null)
            throw new MissingReferenceException("ToggleImage is missing.");
        if (offSprite == null || onSprite == null)
            throw new MissingReferenceException("Toggle sprites are missing.");
        if (button == null)
            throw new MissingReferenceException("Button is missing.");
        if (labelTextField == null)
            throw new MissingReferenceException("LabelTextField is missing.");
        if (stateTextField == null)
            throw new MissingReferenceException("StateTextField is missing.");
    }

    /// <summary>
    /// Binds the authored button while the row is active.
    /// </summary>
    private void OnEnable()
    {
        if (button != null)
            BindControls();
    }

    /// <summary>
    /// Removes the button listener while the row is inactive.
    /// </summary>
    private void OnDisable()
    {
        if (!bound)
            return;

        button.onClick.RemoveListener(RequestToggle);
        bound = false;
    }

    /// <summary>
    /// Attaches the semantic toggle callback exactly once.
    /// </summary>
    private void BindControls()
    {
        if (bound)
            return;

        button.onClick.AddListener(RequestToggle);
        bound = true;
    }

    /// <summary>
    /// Emits a toggle request for this row's configured option.
    /// </summary>
    private void RequestToggle()
    {
        ToggleRequested?.Invoke(option);
    }
}
