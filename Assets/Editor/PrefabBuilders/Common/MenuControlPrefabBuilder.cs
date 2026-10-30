using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static MenuPrefabBuilder;
using Object = UnityEngine.Object;

/// <summary>
/// Authors and instantiates the shared menu selector prefab.
/// </summary>
public static class MenuControlPrefabBuilder
{
    public const string DropdownPath = "Assets/Prefabs/UI/Common/MenuDropdown.prefab";

    /// <summary>
    /// Returns the native UI checkmark shared by selectors and checked controls.
    /// </summary>
    /// <returns>The built-in checkmark sprite.</returns>
    internal static Sprite GetCheckmarkSprite() =>
        AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");

    /// <summary>
    /// Rebuilds the canonical selector used by authoring forms.
    /// </summary>
    internal static void RebuildDropdown()
    {
        BuildDropdown(out GameObject root);
        Directory.CreateDirectory(Path.GetDirectoryName(DropdownPath));
        PrefabUtility.SaveAsPrefabAsset(root, DropdownPath);
        Object.DestroyImmediate(root);
    }

    /// <summary>
    /// Instantiates the canonical selector beneath a view.
    /// </summary>
    /// <param name="parent">The owning view.</param>
    /// <param name="name">The instance name.</param>
    /// <returns>The shared selector.</returns>
    internal static TMP_Dropdown CreateDropdown(Transform parent, string name)
    {
        GameObject root = Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>(DropdownPath),
            parent,
            false
        );
        root.name = name;
        return root.GetComponent<TMP_Dropdown>();
    }

    /// <summary>
    /// Authors the shared TMP selector, popup, and selectable option rows.
    /// </summary>
    /// <param name="root">The authored prefab root.</param>
    /// <returns>The shared selector.</returns>
    private static TMP_Dropdown BuildDropdown(out GameObject root)
    {
        const int controlWidth = 120;
        const int controlHeight = 18;
        const int itemHeight = 18;
        Color accent = new Color(0.373f, 0.659f, 0.925f);
        IReadOnlyList<string> options = new[] { "Value" };
        Image surface = CreateSlicedImage(
            "MenuDropdown",
            null,
            _menuBadgeAddress,
            _badgeBorder,
            0,
            0,
            controlWidth,
            controlHeight,
            Color.white
        );
        surface.raycastTarget = true;
        TMP_Dropdown dropdown = surface.gameObject.AddComponent<MenuDropdown>();
        ApplyMenuSurfaceFeedback(dropdown, surface);

        TextMeshProUGUI caption = CreateTextLabel("Label", surface.transform);
        caption.color = new Color(0.875f, 0.910f, 0.941f);
        caption.fontSize = 10;
        caption.alignment = TextAlignmentOptions.MidlineLeft;
        caption.overflowMode = TextOverflowModes.Ellipsis;
        SetSourceRect(caption.rectTransform, 7, 1, controlWidth - 25, controlHeight - 2);
        dropdown.captionText = caption;

        TextMeshProUGUI arrow = CreateTextLabel("Arrow", surface.transform);
        arrow.text = "V";
        arrow.color = accent;
        arrow.fontSize = 9;
        arrow.alignment = TextAlignmentOptions.Center;
        SetSourceRect(arrow.rectTransform, controlWidth - 19, 1, 14, controlHeight - 2);

        int popupHeight = options.Count * itemHeight + 4;
        Image templateImage = CreateSlicedImage(
            "Template",
            surface.transform,
            _menuPanelAddress,
            _panelBorder,
            0,
            controlHeight,
            controlWidth,
            popupHeight,
            Color.white
        );
        templateImage.raycastTarget = true;
        RectTransform template = templateImage.rectTransform;
        Image popupBacking = new GameObject(
            "OpaqueBacking",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        ).GetComponent<Image>();
        popupBacking.transform.SetParent(template, false);
        popupBacking.color = new Color(10f / 255, 16f / 255, 26f / 255, 1);
        popupBacking.raycastTarget = false;
        popupBacking.rectTransform.anchorMin = Vector2.zero;
        popupBacking.rectTransform.anchorMax = Vector2.one;
        popupBacking.rectTransform.offsetMin = Vector2.one;
        popupBacking.rectTransform.offsetMax = -Vector2.one;
        ScrollRect scrollRect = templateImage.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = false;
        scrollRect.inertia = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewportObject = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Mask)
        );
        viewportObject.transform.SetParent(template, false);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        SetSourceRect(viewport, 2, 2, controlWidth - 4, popupHeight - 4);
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = Color.white;
        viewportImage.raycastTarget = true;
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewport, false);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        SetSourceRect(content, 0, 0, controlWidth - 4, options.Count * itemHeight);

        Image itemImage = CreateSlicedImage(
            "Item",
            content,
            _menuRowAddress,
            _panelBorder,
            0,
            0,
            controlWidth - 4,
            itemHeight,
            _buttonSurfaceColor
        );
        itemImage.raycastTarget = true;
        Toggle item = itemImage.gameObject.AddComponent<Toggle>();
        item.targetGraphic = itemImage;
        item.transition = Selectable.Transition.SpriteSwap;
        Sprite activeRowSprite = LoadSprite(_menuRowActiveAddress, _panelBorder);
        SpriteState itemSprites = item.spriteState;
        itemSprites.highlightedSprite = activeRowSprite;
        itemSprites.selectedSprite = activeRowSprite;
        itemSprites.pressedSprite = activeRowSprite;
        item.spriteState = itemSprites;

        Sprite check = GetCheckmarkSprite();
        Image checkmark = new GameObject(
            "Item Checkmark",
            typeof(RectTransform),
            typeof(Image)
        ).GetComponent<Image>();
        checkmark.transform.SetParent(itemImage.transform, false);
        checkmark.sprite = check;
        checkmark.color = accent;
        checkmark.raycastTarget = false;
        SetSourceRect(checkmark.rectTransform, 5, 3, 10, 10);
        item.graphic = checkmark;

        TextMeshProUGUI itemLabel = CreateTextLabel("Item Label", itemImage.transform);
        itemLabel.color = new Color(0.875f, 0.910f, 0.941f);
        itemLabel.fontSize = 10;
        itemLabel.alignment = TextAlignmentOptions.MidlineLeft;
        SetSourceRect(itemLabel.rectTransform, 18, 1, controlWidth - 25, itemHeight - 2);

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        dropdown.template = template;
        dropdown.itemText = itemLabel;
        dropdown.options.Clear();
        foreach (string option in options)
            dropdown.options.Add(new TMP_Dropdown.OptionData(option));
        dropdown.value = 0;
        dropdown.RefreshShownValue();
        template.gameObject.SetActive(false);
        StretchHorizontal(caption.rectTransform, 7, 25);
        AnchorRight(arrow.rectTransform, 19);
        StretchHorizontal(template, 0, 0);
        StretchHorizontal(viewport, 2, 2);
        StretchHorizontal(content, 0, 0);
        StretchHorizontal(itemImage.rectTransform, 0, 0);
        StretchHorizontal(itemLabel.rectTransform, 18, 7);
        root = surface.gameObject;
        return dropdown;
    }

    /// <summary>
    /// Keeps an authored rectangle's horizontal edge insets when its parent changes width.
    /// </summary>
    /// <param name="rect">The authored rectangle.</param>
    /// <param name="left">The left inset.</param>
    /// <param name="right">The right inset.</param>
    private static void StretchHorizontal(RectTransform rect, float left, float right)
    {
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = new Vector2(left, rect.anchoredPosition.y);
        rect.sizeDelta = new Vector2(-left - right, rect.sizeDelta.y);
    }

    /// <summary>
    /// Anchors a selector affordance to its right edge.
    /// </summary>
    /// <param name="rect">The authored rectangle.</param>
    /// <param name="right">The right inset.</param>
    private static void AnchorRight(RectTransform rect, float right)
    {
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.anchoredPosition = new Vector2(-right, rect.anchoredPosition.y);
    }
}
