using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders one battle-result unit with independent withdrawal and damage overlays.
/// </summary>
public sealed class BattleResultItemView : MonoBehaviour
{
    [SerializeField]
    private RectTransform imageSlot;

    [SerializeField]
    private RawImage baseImage;

    [SerializeField]
    private RawImage withdrawingOverlayImage;

    [SerializeField]
    private RawImage damagedOverlayImage;

    [SerializeField]
    private TextMeshProUGUI nameTextField;

    [SerializeField]
    private TextMeshProUGUI emptyTextField;

    private RectInt imageSlotRect;
    private bool hasImageSlot;

    /// <summary>
    /// Returns the authored source-space item height.
    /// </summary>
    internal int Height => UILayout.GetSourceRect(transform as RectTransform).height;

    /// <summary>
    /// Applies base art, ordered status overlays, and the unit or empty-state label.
    /// </summary>
    /// <param name="data">The immutable result-item presentation.</param>
    internal void Render(BattleResultItemRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        bool hasImage =
            data.BaseTexture != null
            || data.WithdrawingOverlayTexture != null
            || data.DamagedOverlayTexture != null;

        UILayout.SetCenteredImage(baseImage, data.BaseTexture, imageSlotRect);
        UILayout.SetCenteredImage(
            withdrawingOverlayImage,
            data.WithdrawingOverlayTexture,
            imageSlotRect
        );
        UILayout.SetCenteredImage(damagedOverlayImage, data.DamagedOverlayTexture, imageSlotRect);
        nameTextField.gameObject.SetActive(hasImage);
        emptyTextField.gameObject.SetActive(!hasImage);
        TextMeshProUGUI activeTextField = hasImage ? nameTextField : emptyTextField;
        UILayout.SetTextContent(activeTextField, data.Text);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Verifies authored references and caches the fixed image slot.
    /// </summary>
    private void VerifyReferences()
    {
        if (imageSlot == null)
            throw new MissingReferenceException($"{name}/ImageSlot is missing.");
        if (baseImage == null)
            throw new MissingReferenceException($"{name}/BaseImage is missing.");
        if (withdrawingOverlayImage == null)
            throw new MissingReferenceException($"{name}/WithdrawingOverlayImage is missing.");
        if (damagedOverlayImage == null)
            throw new MissingReferenceException($"{name}/DamagedOverlayImage is missing.");
        if (nameTextField == null)
            throw new MissingReferenceException($"{name}/NameTextField is missing.");
        if (emptyTextField == null)
            throw new MissingReferenceException($"{name}/EmptyTextField is missing.");

        if (!hasImageSlot)
        {
            imageSlotRect = UILayout.GetSourceRect(imageSlot);
            hasImageSlot = true;
        }
    }
}
