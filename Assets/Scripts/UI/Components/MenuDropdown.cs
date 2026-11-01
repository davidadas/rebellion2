using TMPro;
using UnityEngine;

/// <summary>
/// Fits the canonical menu selector popup to its current choices without changing native TMP interaction.
/// </summary>
public sealed class MenuDropdown : TMP_Dropdown
{
    private const int _maximumVisibleOptions = 10;

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
            Mathf.Min(options.Count, _maximumVisibleOptions) * itemHeight
        );
        RectTransform content = (RectTransform)item.parent;
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, itemHeight);
        UnityEngine.UI.ScrollRect scroll = popup.GetComponent<UnityEngine.UI.ScrollRect>();
        scroll.vertical = options.Count > _maximumVisibleOptions;
        scroll.scrollSensitivity = itemHeight;
        RectTransform viewport = scroll.viewport;
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(2f, 0);
        viewport.offsetMax = new Vector2(-2f, 0);
        return base.CreateDropdownList(templateObject);
    }
}
