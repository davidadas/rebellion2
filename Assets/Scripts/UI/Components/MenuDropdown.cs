using TMPro;
using UnityEngine;

/// <summary>
/// Fits the canonical menu selector popup to its current choices without changing native TMP interaction.
/// </summary>
public sealed class MenuDropdown : TMP_Dropdown
{
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
            options.Count * itemHeight + 4f
        );
        RectTransform viewport = (RectTransform)popup.Find("Viewport");
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(2f, 2f);
        viewport.offsetMax = new Vector2(-2f, -2f);
        return base.CreateDropdownList(templateObject);
    }
}
