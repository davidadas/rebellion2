using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders one centered icon-and-label row in a pending battle panel.
/// </summary>
public sealed class BattleAlertRowView : MonoBehaviour
{
    [SerializeField]
    private RectTransform iconSlot;

    [SerializeField]
    private RawImage iconImage;

    [SerializeField]
    private TextMeshProUGUI textField;

    [SerializeField]
    private float iconTextGap;

    private RectInt iconSlotRect;
    private RectInt textSlotRect;
    private bool hasLayout;

    /// <summary>
    /// Returns the authored source-space row height.
    /// </summary>
    internal int Height => UILayout.GetSourceRect(transform as RectTransform).height;

    /// <summary>
    /// Applies row content while preserving the authored icon and text slots.
    /// </summary>
    /// <param name="data">The immutable row presentation.</param>
    internal void Render(BattleAlertRowRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        RectInt rowRect = UILayout.GetSourceRect(transform as RectTransform);
        Vector2Int iconSize =
            data.IconTexture == null
                ? Vector2Int.zero
                : UILayout.GetFittedImageSize(data.IconTexture, iconSlotRect);
        int gap = iconSize.x == 0 ? 0 : Mathf.RoundToInt(iconTextGap);
        float preferredTextWidth = textField
            .GetPreferredValues(data.Text, rowRect.width, textSlotRect.height)
            .x;
        int maximumTextWidth = Math.Max(1, rowRect.width - iconSize.x - gap);
        int textWidth = Math.Min(Mathf.CeilToInt(preferredTextWidth), maximumTextWidth);
        int contentWidth = iconSize.x + gap + textWidth;
        int contentX = Math.Max(0, (rowRect.width - contentWidth) / 2);

        UILayout.SetImageTexture(iconImage, data.IconTexture);
        if (data.IconTexture != null)
        {
            UILayout.SetSourceRect(
                iconImage.rectTransform,
                contentX,
                Math.Max(0, (rowRect.height - iconSize.y) / 2),
                iconSize.x,
                iconSize.y
            );
        }

        UILayout.SetTextContent(textField, data.Text);
        UILayout.SetSourceRect(
            textField.rectTransform,
            contentX + iconSize.x + gap,
            Math.Max(0, (rowRect.height - textSlotRect.height) / 2),
            textWidth,
            textSlotRect.height
        );
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Verifies authored references and caches fixed source-space layout.
    /// </summary>
    private void VerifyReferences()
    {
        if (iconSlot == null)
            throw new MissingReferenceException($"{name}/IconSlot is missing.");
        if (iconImage == null)
            throw new MissingReferenceException($"{name}/IconImage is missing.");
        if (textField == null)
            throw new MissingReferenceException($"{name}/TextField is missing.");

        if (!hasLayout)
        {
            iconSlotRect = UILayout.GetSourceRect(iconSlot);
            textSlotRect = UILayout.GetSourceRect(textField.rectTransform);
            hasLayout = true;
        }
    }
}
