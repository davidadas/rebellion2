using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents a Boolean choice with an authored label and check mark.
/// </summary>
public sealed class UICheckboxView : MonoBehaviour
{
    [SerializeField]
    private Toggle toggle;

    [SerializeField]
    private Image backgroundImage;

    [SerializeField]
    private RawImage frameImage;

    [SerializeField]
    private RawImage checkMarkImage;

    [SerializeField]
    private RectTransform checkMarkRoot;

    [SerializeField]
    private TextMeshProUGUI labelTextField;

    private bool bound;

    public bool IsChecked => toggle?.isOn == true;

    public event Action<bool> ValueChanged;

    /// <summary>
    /// Verifies the authored hierarchy and binds the toggle.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        BindControl();
        RenderCheckMark(toggle.isOn);
    }

    /// <summary>
    /// Restores the toggle listener when the checkbox is enabled.
    /// </summary>
    private void OnEnable()
    {
        if (toggle != null)
            BindControl();
    }

    /// <summary>
    /// Releases the toggle listener while the checkbox is inactive.
    /// </summary>
    private void OnDisable()
    {
        UnbindControl();
    }

    /// <summary>
    /// Applies a checked state without raising a user-change event.
    /// </summary>
    /// <param name="isChecked">Whether the checkbox should be checked.</param>
    public void SetIsCheckedWithoutNotify(bool isChecked)
    {
        VerifyReferences();
        toggle.SetIsOnWithoutNotify(isChecked);
        RenderCheckMark(isChecked);
    }

    /// <summary>
    /// Applies theme-owned checkbox artwork while retaining authored preview fallbacks.
    /// </summary>
    /// <param name="frameTexture">The checkbox frame texture, or null to retain the authored fallback.</param>
    /// <param name="checkMarkTexture">The check-mark texture, or null to retain the authored fallback.</param>
    public void SetTextures(Texture frameTexture, Texture checkMarkTexture)
    {
        VerifyReferences();
        if (frameTexture != null)
            UILayout.SetImageTexture(frameImage, frameTexture);
        if (checkMarkTexture != null)
            UILayout.SetImageTexture(checkMarkImage, checkMarkTexture);
    }

    /// <summary>
    /// Verifies all required checkbox references.
    /// </summary>
    public void VerifyReferences()
    {
        if (toggle == null)
            throw new MissingReferenceException($"{name}/Toggle is missing.");
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (frameImage == null)
            throw new MissingReferenceException($"{name}/FrameImage is missing.");
        if (checkMarkImage == null)
            throw new MissingReferenceException($"{name}/CheckMarkImage is missing.");
        if (checkMarkRoot == null)
            throw new MissingReferenceException($"{name}/CheckMarkRoot is missing.");
        if (labelTextField == null)
            throw new MissingReferenceException($"{name}/LabelTextField is missing.");
    }

    private void BindControl()
    {
        if (bound)
            return;

        toggle.onValueChanged.AddListener(HandleValueChanged);
        bound = true;
    }

    private void UnbindControl()
    {
        if (!bound)
            return;

        toggle.onValueChanged.RemoveListener(HandleValueChanged);
        bound = false;
    }

    private void HandleValueChanged(bool isChecked)
    {
        RenderCheckMark(isChecked);
        ValueChanged?.Invoke(isChecked);
    }

    private void RenderCheckMark(bool isChecked)
    {
        checkMarkRoot.gameObject.SetActive(isChecked);
    }
}
