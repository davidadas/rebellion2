using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders one authored advisor-report row.
/// </summary>
public sealed class AdvisorReportRowView : MonoBehaviour
{
    [SerializeField]
    private RectTransform imageSlot;

    [SerializeField]
    private RawImage image;

    [SerializeField]
    private TextMeshProUGUI primaryTextField;

    [SerializeField]
    private TextMeshProUGUI secondaryTextField;

    /// <summary>
    /// Gets the authored source-space row height.
    /// </summary>
    public int Height => UILayout.GetSourceRect(transform as RectTransform).height;

    /// <summary>
    /// Applies one projected report row to the authored image and text fields.
    /// </summary>
    /// <param name="data">The immutable row presentation.</param>
    public void Render(AdvisorReportRowRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        UILayout.SetCenteredImage(image, data.Texture, UILayout.GetSourceRect(imageSlot));
        UILayout.SetTextContent(primaryTextField, data.PrimaryText);
        if (secondaryTextField != null)
            UILayout.SetTextContent(secondaryTextField, data.SecondaryText);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Verifies the authored row hierarchy required for rendering.
    /// </summary>
    private void VerifyReferences()
    {
        if (imageSlot == null)
            throw new MissingReferenceException($"{name}/ImageSlot is missing.");
        if (image == null)
            throw new MissingReferenceException($"{name}/Image is missing.");
        if (primaryTextField == null)
            throw new MissingReferenceException($"{name}/PrimaryTextField is missing.");
    }
}
