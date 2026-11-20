using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays an actual map marker beside its legend label.
/// </summary>
public sealed class GalacticInformationLegendRowView : MonoBehaviour
{
    [SerializeField]
    private RawImage markerImage;

    [SerializeField]
    private TextMeshProUGUI labelTextField;

    /// <summary>
    /// Applies one legend row's marker, text, and source-space bounds.
    /// </summary>
    /// <param name="data">The projected marker-key row.</param>
    public void Render(GalacticInformationLegendRowRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (markerImage == null || labelTextField == null)
            throw new MissingReferenceException($"{name}/Legend row is incomplete.");

        UILayout.SetSourceRect(
            transform as RectTransform,
            data.Bounds.x,
            data.Bounds.y,
            data.Bounds.width,
            data.Bounds.height
        );
        UILayout.SetCenteredImage(
            markerImage,
            data.Texture,
            new RectInt(0, 0, 22, data.Bounds.height)
        );
        markerImage.raycastTarget = false;
        UILayout.SetSourceRect(
            labelTextField.rectTransform,
            27,
            0,
            data.Bounds.width - 27,
            data.Bounds.height
        );
        UILayout.SetTextContent(labelTextField, data.Label);
        gameObject.SetActive(true);
    }
}
