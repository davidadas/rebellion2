using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders one tactical option and emits a typed toggle request.
/// </summary>
public sealed class SaveMenuTacticalOptionRowView : MonoBehaviour
{
    [SerializeField]
    private UserTacticalOption option;

    [SerializeField]
    private Color enabledTextColor;

    [SerializeField]
    private Color disabledTextColor;

    [SerializeField]
    private RawImagePressVisual buttonPressVisual;

    [SerializeField]
    private Button button;

    [SerializeField]
    private TextMeshProUGUI labelTextField;

    [SerializeField]
    private TextMeshProUGUI stateTextField;

    [SerializeField]
    private Texture2D disabledTexture;

    [SerializeField]
    private Texture2D enabledTexture;

    private bool bound;

    /// <summary>
    /// Gets the tactical option represented by this authored row.
    /// </summary>
    public UserTacticalOption Option => option;

    /// <summary>
    /// Occurs when the player requests toggling this row's configured option.
    /// </summary>
    public event Action<UserTacticalOption> ToggleRequested;

    /// <summary>
    /// Renders the current option state without changing authored geometry or text.
    /// </summary>
    /// <param name="enabled">Whether the tactical option is enabled.</param>
    public void Render(bool enabled)
    {
        VerifyReferences();
        BindControls();
        Texture2D buttonTexture = enabled ? enabledTexture : disabledTexture;
        buttonPressVisual.SetTextures(buttonTexture, buttonTexture);
        labelTextField.color = enabled ? enabledTextColor : disabledTextColor;
        stateTextField.text = enabled ? "ON" : "OFF";
        stateTextField.color = enabled ? enabledTextColor : disabledTextColor;
    }

    /// <summary>
    /// Verifies every authored reference required by the row.
    /// </summary>
    public void VerifyReferences()
    {
        if (buttonPressVisual == null)
            throw new MissingReferenceException("ButtonPressVisual is missing.");
        if (button == null)
            throw new MissingReferenceException("Button is missing.");
        if (labelTextField == null)
            throw new MissingReferenceException("LabelTextField is missing.");
        if (stateTextField == null)
            throw new MissingReferenceException("StateTextField is missing.");
        if (disabledTexture == null)
            throw new MissingReferenceException("DisabledTexture is missing.");
        if (enabledTexture == null)
            throw new MissingReferenceException("EnabledTexture is missing.");
    }

    /// <summary>
    /// Binds the authored button while the row is active.
    /// </summary>
    private void OnEnable()
    {
        if (ReferencesAssigned())
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

    /// <summary>
    /// Checks whether the prefab has all references needed for early binding.
    /// </summary>
    /// <returns>True when every required authored reference is assigned.</returns>
    private bool ReferencesAssigned()
    {
        return buttonPressVisual != null
            && button != null
            && labelTextField != null
            && stateTextField != null
            && disabledTexture != null
            && enabledTexture != null;
    }
}
