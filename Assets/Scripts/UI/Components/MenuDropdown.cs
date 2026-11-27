using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fits the canonical menu selector popup to its current choices without changing native TMP interaction.
/// </summary>
public sealed class MenuDropdown : TMP_Dropdown
{
    private const int _maximumVisibleOptions = 8;
    private float textItemHeight;
    private Vector2 textItemMin;
    private Vector2 textItemMax;
    private Vector2 textCaptionMin;
    private Vector2 textCheckmarkPosition;
    private TextWrappingModes textItemWrapping;
    private TextOverflowModes textItemOverflow;
    private bool capturedTextLayout;

    /// <summary>
    /// Presents native text and optional image choices using the same selector and popup.
    /// </summary>
    /// <param name="choices">The choices and any thumbnails supplied by the owning view.</param>
    public void SetOptions(List<OptionData> choices)
    {
        if (!capturedTextLayout)
        {
            textItemHeight = ((RectTransform)itemText.transform.parent).rect.height;
            textItemMin = itemText.rectTransform.offsetMin;
            textItemMax = itemText.rectTransform.offsetMax;
            textCaptionMin = captionText.rectTransform.offsetMin;
            textCheckmarkPosition = itemText
                .GetComponentInParent<Toggle>(true)
                .graphic.rectTransform.anchoredPosition;
            textItemWrapping = itemText.textWrappingMode;
            textItemOverflow = itemText.overflowMode;
            capturedTextLayout = true;
        }
        bool hasImages = choices.Any(choice => choice.image != null);
        RectTransform item = (RectTransform)itemText.transform.parent;
        item.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            hasImages ? Mathf.Max(64, textItemHeight) : textItemHeight
        );
        itemText.rectTransform.offsetMin = hasImages
            ? new Vector2(108, -item.rect.height + 4)
            : textItemMin;
        itemText.rectTransform.offsetMax = hasImages ? new Vector2(textItemMax.x, -4) : textItemMax;
        captionText.rectTransform.offsetMin = hasImages
            ? textCaptionMin + new Vector2(32, 0)
            : textCaptionMin;
        itemText.textWrappingMode = hasImages ? TextWrappingModes.Normal : textItemWrapping;
        itemText.overflowMode = hasImages ? TextOverflowModes.Ellipsis : textItemOverflow;
        RectTransform checkmark = item.GetComponent<Toggle>().graphic.rectTransform;
        checkmark.anchoredPosition = hasImages
            ? new Vector2(textCheckmarkPosition.x, -(item.rect.height - checkmark.rect.height) / 2)
            : textCheckmarkPosition;
        if (itemImage != null)
        {
            itemImage.gameObject.SetActive(hasImages);
            itemImage.preserveAspect = true;
            UILayout.SetSourceRect(
                itemImage.rectTransform,
                28,
                4,
                72,
                Mathf.Max(1, Mathf.RoundToInt(item.rect.height - 8))
            );
        }
        if (captionImage != null)
        {
            captionImage.gameObject.SetActive(hasImages);
            captionImage.preserveAspect = true;
            RectTransform image = captionImage.rectTransform;
            image.anchorMin = image.anchorMax = image.pivot = new Vector2(0, 0.5f);
            image.anchoredPosition = new Vector2(textCaptionMin.x, 0);
            image.sizeDelta = new Vector2(26, 26);
        }
        ClearOptions();
        AddOptions(choices);
    }

    /// <summary>
    /// Sizes the shared popup from its authored item height before TMP creates the interactive list.
    /// </summary>
    /// <param name="templateObject">The canonical menu popup template.</param>
    /// <returns>The native interactive dropdown list.</returns>
    protected override GameObject CreateDropdownList(GameObject templateObject)
    {
        RectTransform popup = (RectTransform)templateObject.transform;
        RectTransform item = (RectTransform)itemText.transform.parent;
        float itemHeight = item.rect.height;
        popup.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            Mathf.Min(options.Count, _maximumVisibleOptions) * itemHeight + 4f
        );
        RectTransform content = (RectTransform)item.parent;
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, itemHeight);
        ScrollRect scroll = popup.GetComponent<ScrollRect>();
        bool requiresScrollbar = options.Count > _maximumVisibleOptions;
        scroll.vertical = requiresScrollbar;
        scroll.scrollSensitivity = itemHeight;
        scroll.verticalScrollbar.gameObject.SetActive(requiresScrollbar);
        RectTransform viewport = scroll.viewport;
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(2f, 2f);
        viewport.offsetMax = new Vector2(requiresScrollbar ? -16f : -2f, -2f);
        return base.CreateDropdownList(templateObject);
    }
}
