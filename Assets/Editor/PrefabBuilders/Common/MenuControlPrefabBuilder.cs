using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static MenuPrefabBuilder;
using Object = UnityEngine.Object;

/// <summary>
/// Authors the shared menu dropdown, save list, and flexible window shell from canonical menu artwork.
/// </summary>
public static class MenuControlPrefabBuilder
{
    public const string DropdownPath = "Assets/Prefabs/UI/Common/MenuDropdown.prefab";
    public const string SaveListPath = "Assets/Prefabs/UI/Common/MenuSaveList.prefab";
    public const string ChatPanelPath = "Assets/Prefabs/UI/Common/MenuChatPanel.prefab";
    public const string ServerRowPath = "Assets/Prefabs/UI/Common/MenuServerRow.prefab";
    public const string WideWindowPath = "Assets/Prefabs/UI/Common/MenuWideWindow.prefab";
    private const int _optionsContentInset = 20;
    private const int _optionsSectionHeaderHeight = 16;
    private const string _contextMenuCheckMarkAddress =
        "Pack/Shared/Strategy/UI/ContextMenus/context-menu-check-mark.png";

    /// <summary>
    /// Rebuilds reusable controls before feature windows instantiate them.
    /// </summary>
    internal static void Rebuild()
    {
        BuildDropdown(out GameObject dropdown);
        Save(dropdown, DropdownPath);
        GameObject list = new GameObject("MenuSaveList", typeof(RectTransform));
        list.SetActive(false);
        RectTransform saveLoadPage = (RectTransform)list.transform;
        SetSourceRect(saveLoadPage, 0, 0, 382, 365);
        Color accent = new Color(0.373f, 0.659f, 0.925f);
        Color textColor = new Color(0.875f, 0.910f, 0.941f);
        Color textDim = new Color(0.573f, 0.635f, 0.706f);

        OptionsSaveListView saveListView = EnableRuntimeComponent(
            saveLoadPage.gameObject.AddComponent<OptionsSaveListView>()
        );
        TextMeshProUGUI savedGamesLabel = CreateOptionsSectionHeader(
            saveLoadPage,
            "SavedGamesLabel",
            "SAVED GAMES",
            13,
            accent
        );
        AssignReference(saveListView, "_heading", savedGamesLabel);
        savedGamesLabel.rectTransform.anchoredPosition = new Vector2(18f, -13f);
        RawImage saveIcon = CreateRawImage(
            "SaveIcon",
            saveLoadPage,
            "Application/OptionsMenu/UI/ui_settingsmenu_savedgames_save_icon",
            258,
            11,
            49,
            23
        );
        Button saveButton = CreateButton(saveIcon);
        saveIcon
            .GetComponent<ContentPressVisualBinding>()
            .SetAddresses(
                "Application/OptionsMenu/UI/ui_settingsmenu_savedgames_save_icon",
                "Application/OptionsMenu/UI/ui_settingsmenu_savedgames_save_icon_pressed"
            );
        RawImage saveDisabledIcon = CreateRawImage(
            "SaveIconDisabled",
            saveLoadPage,
            "Application/OptionsMenu/UI/ui_settingsmenu_savedgames_save_icon_disabled",
            258,
            11,
            49,
            23
        );
        saveDisabledIcon.gameObject.SetActive(false);
        RawImage loadIcon = CreateRawImage(
            "LoadIcon",
            saveLoadPage,
            "Application/OptionsMenu/UI/ui_settingsmenu_savedgames_load_icon",
            313,
            11,
            49,
            23
        );
        Button loadButton = CreateButton(loadIcon);
        loadIcon
            .GetComponent<ContentPressVisualBinding>()
            .SetAddresses(
                "Application/OptionsMenu/UI/ui_settingsmenu_savedgames_load_icon",
                "Application/OptionsMenu/UI/ui_settingsmenu_savedgames_load_icon_pressed"
            );
        RawImage loadDisabledIcon = CreateRawImage(
            "LoadIconDisabled",
            saveLoadPage,
            "Application/OptionsMenu/UI/ui_settingsmenu_savedgames_load_icon_disabled",
            313,
            11,
            49,
            23
        );
        loadDisabledIcon.gameObject.SetActive(false);
        ApplyOptionsButtonFeedback(saveButton, saveIcon);
        ApplyOptionsButtonFeedback(loadButton, loadIcon);
        ScrollAreaView saveSlotScrollArea = CreateScrollAreaView(
            saveLoadPage,
            "SaveSlotScrollArea",
            18,
            40,
            349,
            290,
            0,
            0,
            337,
            290,
            337,
            0,
            12,
            290,
            out RectTransform slotContent
        );
        Image slotRowTemplate = CreateSlicedImage(
            "SlotRowTemplate",
            slotContent,
            _menuRowAddress,
            _panelBorder,
            0,
            0,
            337,
            28,
            Color.white
        );
        slotRowTemplate.raycastTarget = true;
        Button slotRowButton = slotRowTemplate.gameObject.AddComponent<Button>();
        slotRowButton.targetGraphic = slotRowTemplate;
        slotRowButton.transition = Selectable.Transition.None;
        slotRowTemplate.gameObject.SetActive(false);
        RawImage slotIconTemplate = CreateRawButton("SlotIconTemplate", slotContent, null);
        slotIconTemplate.raycastTarget = false;
        SetSourceRect(slotIconTemplate.rectTransform, 8, 4, 20, 20);
        slotIconTemplate.gameObject.SetActive(false);
        TextMeshProUGUI slotNameTemplate = CreateTextLabel("SlotNameTemplate", slotContent);
        slotNameTemplate.text = "Save";
        slotNameTemplate.color = textColor;
        slotNameTemplate.fontSize = 12;
        slotNameTemplate.alignment = TextAlignmentOptions.TopLeft;
        slotNameTemplate.textWrappingMode = TextWrappingModes.NoWrap;
        slotNameTemplate.overflowMode = TextOverflowModes.Truncate;
        slotNameTemplate.raycastTarget = false;
        SetSourceRect(slotNameTemplate.rectTransform, 34, 0, 271, 20);
        slotNameTemplate.gameObject.SetActive(false);
        TextMeshProUGUI slotMetaTemplate = CreateTextLabel("SlotDateTemplate", slotContent);
        slotMetaTemplate.text = "Date";
        slotMetaTemplate.color = textDim;
        slotMetaTemplate.fontSize = 9;
        slotMetaTemplate.alignment = TextAlignmentOptions.TopLeft;
        slotMetaTemplate.raycastTarget = false;
        SetSourceRect(slotMetaTemplate.rectTransform, 34, 16, 300, 10);
        slotMetaTemplate.gameObject.SetActive(false);

        GameObject deleteObject = new GameObject(
            "SlotDeleteTemplate",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        deleteObject.transform.SetParent(slotContent, false);
        Image deleteHit = deleteObject.GetComponent<Image>();
        deleteHit.color = Color.clear;
        deleteHit.raycastTarget = true;
        SetSourceRect(deleteObject.GetComponent<RectTransform>(), 0, 0, 16, 16);
        Button slotDeleteTemplate = deleteObject.AddComponent<Button>();
        TextMeshProUGUI deleteGlyph = CreateTextLabel("Glyph", deleteObject.transform);
        deleteGlyph.text = "×";
        deleteGlyph.color = new Color(0.78f, 0.45f, 0.47f);
        deleteGlyph.fontSize = 16;
        deleteGlyph.alignment = TextAlignmentOptions.Midline;
        deleteGlyph.raycastTarget = false;
        SetSourceRect(deleteGlyph.rectTransform, 0, 0, 16, 16);
        ApplyOptionsButtonFeedback(slotDeleteTemplate, deleteGlyph);
        slotDeleteTemplate.gameObject.SetActive(false);

        TMP_InputField slotRenameField = CreateTextInputField(
            "SlotRenameField",
            slotContent,
            "Save name",
            32,
            0,
            275,
            16
        );
        Image renameBackground = slotRenameField.GetComponent<Image>();
        renameBackground.color = new Color(0.08f, 0.10f, 0.13f, 0.98f);
        slotRenameField.textComponent.fontSize = 12;
        slotRenameField.textComponent.alignment = TextAlignmentOptions.Left;
        StretchFillInputChild((RectTransform)slotRenameField.textComponent.transform);
        slotRenameField.customCaretColor = true;
        slotRenameField.caretColor = Color.white;
        slotRenameField.caretWidth = 2;
        slotRenameField.caretBlinkRate = 0.85f;
        slotRenameField.characterLimit = SaveGameManager.MaxDisplayNameLength;
        if (slotRenameField.placeholder is TextMeshProUGUI renamePlaceholder)
        {
            renamePlaceholder.fontSize = 12;
            renamePlaceholder.alignment = TextAlignmentOptions.Left;
            renamePlaceholder.color = new Color(0.60f, 0.65f, 0.72f);
            StretchFillInputChild(renamePlaceholder.rectTransform);
        }
        slotRenameField.onFocusSelectAll = false;
        slotRenameField.gameObject.SetActive(false);

        AssignReference(saveListView, "_saveButton", saveButton);
        AssignReference(saveListView, "_loadButton", loadButton);
        AssignReference(saveListView, "_saveDisabledImage", saveDisabledIcon);
        AssignReference(saveListView, "_loadDisabledImage", loadDisabledIcon);
        AssignReference(saveListView, "_scrollArea", saveSlotScrollArea);
        AssignReference(saveListView, "_rowTemplate", slotRowTemplate);
        AssignReference(saveListView, "_iconTemplate", slotIconTemplate);
        AssignReference(saveListView, "_nameTemplate", slotNameTemplate);
        AssignReference(saveListView, "_metaTemplate", slotMetaTemplate);
        AssignReference(saveListView, "_deleteTemplate", slotDeleteTemplate);
        AssignReference(saveListView, "_renameField", slotRenameField);
        AssignReference(saveListView, "_rowIdleSprite", LoadSprite(_menuRowAddress, _panelBorder));
        AssignReference(
            saveListView,
            "_rowActiveSprite",
            LoadSprite(_menuRowActiveAddress, _panelBorder)
        );
        AssignString(saveListView, "_rowIdleSpriteAddress", _menuRowAddress);
        AssignString(saveListView, "_rowActiveSpriteAddress", _menuRowActiveAddress);

        Save(list, SaveListPath);
        BuildWideWindow();
        BuildServerRow();
        BuildChatPanel();
    }

    /// <summary>
    /// Instantiates the same selector artwork and popup used by application Options.
    /// </summary>
    /// <param name="parent">The control owner.</param>
    /// <param name="name">The hierarchy name.</param>
    /// <param name="options">The initial choices.</param>
    /// <param name="bounds">The source-coordinate bounds.</param>
    /// <returns>The shared native selector.</returns>
    internal static TMP_Dropdown CreateDropdown(
        Transform parent,
        string name,
        IReadOnlyList<string> options,
        RectInt bounds
    )
    {
        GameObject root = Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>(DropdownPath),
            parent,
            false
        );
        root.name = name;
        float scale = bounds.height / 18f;
        RectTransform rect = (RectTransform)root.transform;
        SetSourceRect(rect, bounds.x, bounds.y, Mathf.RoundToInt(bounds.width / scale), 18);
        rect.localScale = Vector3.one * scale;
        TMP_Dropdown dropdown = root.GetComponent<TMP_Dropdown>();
        dropdown.ClearOptions();
        foreach (string option in options)
            dropdown.options.Add(new TMP_Dropdown.OptionData(option));
        dropdown.RefreshShownValue();
        root.SetActive(true);
        return dropdown;
    }

    /// <summary>
    /// Instantiates the canonical save page for Options, presets, or multiplayer loading.
    /// </summary>
    /// <param name="parent">The page owner.</param>
    /// <returns>The shared save-list presenter.</returns>
    internal static OptionsSaveListView CreateSaveList(Transform parent)
    {
        GameObject root = Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>(SaveListPath),
            parent,
            false
        );
        root.name = "MenuSaveList";
        root.SetActive(true);
        return root.GetComponent<OptionsSaveListView>();
    }

    /// <summary>
    /// Instantiates the common wide shell with an interior inset derived from its sliced frame.
    /// </summary>
    /// <param name="parent">The window owner.</param>
    /// <returns>The authored logical console root.</returns>
    internal static RectTransform CreateWideWindow(Transform parent)
    {
        return (RectTransform)
            Object
                .Instantiate(
                    AssetDatabase.LoadAssetAtPath<GameObject>(WideWindowPath),
                    parent,
                    false
                )
                .transform;
    }

    /// <summary>
    /// Builds the existing Options dropdown hierarchy once as a reusable prefab.
    /// </summary>
    /// <param name="root">Receives the prefab hierarchy.</param>
    /// <returns>The authored selector.</returns>
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
        ApplyOptionsDropdownFeedback(dropdown, surface);

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

        RawImage checkmark = CreateRawImage(
            "Item Checkmark",
            itemImage.transform,
            _contextMenuCheckMarkAddress,
            5,
            3,
            10,
            10
        );
        checkmark.color = accent;
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
    /// Authors the flexible menu frame with the same underlying fill as the canonical menu window.
    /// </summary>
    private static void BuildWideWindow()
    {
        GameObject root = new GameObject("Console", typeof(RectTransform));
        SetSourceRect((RectTransform)root.transform, 0, 0, 1000, 700);
        CreateMenuBacking(root.transform, "WindowBacking", new RectInt(20, 20, 960, 660));
        RawImage body = CreateRawImage(
            "WindowBody",
            root.transform,
            _menuPanelAddress,
            20,
            20,
            960,
            660
        );
        body.uvRect = new Rect(0.5f, 0.5f, 0, 0);
        CreateWindowFrame(root.transform, new RectInt(20, 20, 960, 660));
        Save(root, WideWindowPath);
    }

    /// <summary>
    /// Fits clean sections of the existing window artwork around a single rectangular opening.
    /// The edge samples exclude the source window's embedded headers and navigation divider.
    /// </summary>
    /// <param name="parent">The window receiving the frame.</param>
    /// <param name="bounds">The frame's outer rectangle.</param>
    private static void CreateWindowFrame(Transform parent, RectInt bounds)
    {
        RectTransform frame = new GameObject(
            "OuterFrame",
            typeof(RectTransform)
        ).GetComponent<RectTransform>();
        frame.SetParent(parent, false);
        SetSourceRect(frame, bounds.x, bounds.y, bounds.width, bounds.height);
        const float left = 73f / 4;
        const float right = 56f / 4;
        const float top = 108f / 4;
        const float bottom = 80f / 4;
        float innerWidth = bounds.width - left - right;
        float innerHeight = bounds.height - top - bottom;
        CreateWindowFrameRegion(
            frame,
            "TopLeft",
            new RectInt(0, 0, 73, 108),
            new Rect(0, 0, left, top)
        );
        CreateWindowFrameRegion(
            frame,
            "Top",
            new RectInt(1024, 0, 1, 108),
            new Rect(left, 0, innerWidth, top)
        );
        CreateWindowFrameRegion(
            frame,
            "TopRight",
            new RectInt(1992, 0, 56, 108),
            new Rect(bounds.width - right, 0, right, top)
        );
        CreateWindowFrameRegion(
            frame,
            "Left",
            new RectInt(0, 768, 73, 1),
            new Rect(0, top, left, innerHeight)
        );
        CreateWindowFrameRegion(
            frame,
            "Right",
            new RectInt(1992, 768, 56, 1),
            new Rect(bounds.width - right, top, right, innerHeight)
        );
        CreateWindowFrameRegion(
            frame,
            "BottomLeft",
            new RectInt(0, 1456, 73, 80),
            new Rect(0, bounds.height - bottom, left, bottom)
        );
        CreateWindowFrameRegion(
            frame,
            "Bottom",
            new RectInt(1024, 1456, 1, 80),
            new Rect(left, bounds.height - bottom, innerWidth, bottom)
        );
        CreateWindowFrameRegion(
            frame,
            "BottomRight",
            new RectInt(1992, 1456, 56, 80),
            new Rect(bounds.width - right, bounds.height - bottom, right, bottom)
        );
    }

    /// <summary>
    /// Places one content-bound source section with adjoining edges sharing identical coordinates.
    /// </summary>
    /// <param name="parent">The shared frame root.</param>
    /// <param name="name">The edge or corner name.</param>
    /// <param name="source">The top-left pixel rectangle in the existing artwork.</param>
    /// <param name="destination">The top-left display rectangle inside the frame.</param>
    private static void CreateWindowFrameRegion(
        Transform parent,
        string name,
        RectInt source,
        Rect destination
    )
    {
        RawImage image = CreateRawImage(
            name,
            parent,
            "Application/OptionsMenu/UI/ui_settingsmenu_background",
            0,
            0,
            1,
            1
        );
        image.raycastTarget = false;
        image.uvRect = new Rect(
            (float)source.x / image.texture.width,
            1f - (float)source.yMax / image.texture.height,
            (float)source.width / image.texture.width,
            (float)source.height / image.texture.height
        );
        image.rectTransform.anchoredPosition = new Vector2(destination.x, -destination.y);
        image.rectTransform.sizeDelta = destination.size;
    }

    /// <summary>
    /// Authors the selectable server row once for live browsers and populated native previews.
    /// </summary>
    private static void BuildServerRow()
    {
        Button button = CreateSlicedButton(
            "MenuServerRow",
            null,
            _menuRowAddress,
            _panelBorder,
            0,
            0,
            880,
            36,
            Color.white
        );
        button.transition = Selectable.Transition.SpriteSwap;
        Sprite active = LoadSprite(_menuRowActiveAddress, _panelBorder);
        SpriteState states = button.spriteState;
        states.highlightedSprite = states.pressedSprite = states.selectedSprite = active;
        button.spriteState = states;
        MenuServerRowView view = button.gameObject.AddComponent<MenuServerRowView>();
        string[] names = { "GameName", "Content", "Players" };
        int[] left = { 8, 370, 700 };
        int[] width = { 350, 290, 90 };
        string[] fields = { "nameText", "contentText", "playersText" };
        for (int index = 0; index < names.Length; index++)
        {
            TMP_Text label = CreateTextLabel(names[index], button.transform);
            label.fontSize = index == 0 ? 15 : 14;
            label.color = new Color(0.875f, 0.910f, 0.941f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.richText = false;
            SetSourceRect(label.rectTransform, left[index], 0, width[index], 36);
            AssignReference(view, fields[index], label);
        }
        GameObject icon = new GameObject("Access", typeof(RectTransform), typeof(MenuLockIcon));
        icon.transform.SetParent(button.transform, false);
        SetSourceRect((RectTransform)icon.transform, 828, 8, 20, 20);
        MenuLockIcon graphic = icon.GetComponent<MenuLockIcon>();
        graphic.color = new Color(0.875f, 0.910f, 0.941f);
        graphic.raycastTarget = false;
        AssignReference(view, "accessIcon", graphic);
        AssignReference(view, "background", button.GetComponent<Image>());
        AssignReference(view, "idleSprite", LoadSprite(_menuRowAddress, _panelBorder));
        AssignReference(view, "selectedSprite", active);
        Save(button.gameObject, ServerRowPath);
    }

    /// <summary>
    /// Authors one reusable conversation panel for browser and lobby hosts.
    /// </summary>
    private static void BuildChatPanel()
    {
        Image panel = CreateSlicedImage(
            "MenuChatPanel",
            null,
            _menuPanelAddress,
            _panelBorder,
            0,
            0,
            868,
            192,
            Color.white
        );
        MenuChatPanelView view = panel.gameObject.AddComponent<MenuChatPanelView>();
        TMP_Text heading = CreateTextLabel("Heading", panel.transform);
        heading.fontSize = 12;
        heading.alignment = TextAlignmentOptions.TopLeft;
        heading.color = new Color(0.373f, 0.659f, 0.925f);
        ScrollAreaView scroll = CreateScrollAreaView(
            panel.transform,
            "Transcript",
            13,
            33,
            842,
            114,
            0,
            0,
            825,
            114,
            828,
            0,
            13,
            114,
            out RectTransform rows
        );
        TMP_Text text = CreateTextLabel("Conversation", rows);
        text.fontSize = 15;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = new Color(0.875f, 0.910f, 0.941f);
        text.richText = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        CreateSlicedImage(
            "InputFrame",
            panel.transform,
            _menuPanelAddress,
            _panelBorder,
            13,
            156,
            741,
            26,
            Color.white
        );
        TMP_InputField input = CreateTextInputField(
            "Message",
            panel.transform,
            "Message...",
            18,
            159,
            731,
            20
        );
        input.textComponent.fontSize = 15;
        input.textComponent.richText = false;
        input.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
        if (input.placeholder is TMP_Text hint)
        {
            hint.fontSize = 15;
            hint.alignment = TextAlignmentOptions.MidlineLeft;
        }
        GameObject send = Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/Common/MenuActionButton.prefab"
            ),
            panel.transform,
            false
        );
        send.name = "Send";
        SetSourceRect((RectTransform)send.transform, 767, 156, 88, 26);
        TMP_Text caption = send.GetComponentInChildren<TMP_Text>();
        caption.text = "SEND";
        caption.fontSize = 14;
        AssignReference(view, "heading", heading);
        AssignReference(view, "transcript", text);
        AssignReference(view, "scrollArea", scroll);
        AssignReference(view, "messageInput", input);
        AssignReference(view, "sendButton", send.GetComponent<Button>());
        view.SetLayout("CHAT", 868, 192);
        Save(panel.gameObject, ChatPanelPath);
    }

    /// <summary>
    /// Reuses the canonical window's neutral interior beneath translucent menu panels.
    /// </summary>
    /// <param name="parent">The backing owner.</param>
    /// <param name="name">The hierarchy name.</param>
    /// <param name="bounds">The backing bounds.</param>
    /// <returns>The content-bound backing.</returns>
    internal static RawImage CreateMenuBacking(Transform parent, string name, RectInt bounds)
    {
        RawImage image = CreateRawImage(
            name,
            parent,
            "Application/OptionsMenu/UI/ui_settingsmenu_background",
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height
        );
        image.uvRect = new Rect(0.5f, 0.5f, 0, 0);
        image.raycastTarget = false;
        return image;
    }

    /// <summary>
    /// Preserves native horizontal insets when a shared selector changes width.
    /// </summary>
    /// <param name="rect">The native child rectangle.</param>
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
    /// Preserves an arrow's right-edge inset as the shared selector changes width.
    /// </summary>
    /// <param name="rect">The arrow rectangle.</param>
    /// <param name="right">The right-edge coordinate.</param>
    private static void AnchorRight(RectTransform rect, float right)
    {
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.anchoredPosition = new Vector2(-right, rect.anchoredPosition.y);
    }

    /// <summary>
    /// Saves a reusable control and releases its authoring hierarchy.
    /// </summary>
    /// <param name="root">The authored control.</param>
    /// <param name="path">The prefab asset path.</param>
    private static void Save(GameObject root, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    /// <summary>
    /// Adds color feedback to a button.
    /// </summary>
    /// <param name="button">The button to restyle.</param>
    /// <param name="target">The graphic to tint, or null to keep the button's target graphic.</param>
    internal static void ApplyOptionsButtonFeedback(Button button, Graphic target = null)
    {
        if (button == null)
            return;

        if (target != null)
            button.targetGraphic = target;

        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.88f, 0.91f, 0.96f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.52f, 0.60f, 0.72f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.45f, 0.48f, 0.54f, 0.4f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    /// <summary>
    /// Adds visible pointer feedback to a dropdown's light-colored surface.
    /// </summary>
    /// <param name="dropdown">The dropdown to restyle.</param>
    /// <param name="surface">The visible surface receiving the tint.</param>
    internal static void ApplyOptionsDropdownFeedback(TMP_Dropdown dropdown, Graphic surface)
    {
        if (dropdown == null || surface == null)
            return;

        dropdown.targetGraphic = surface;
        dropdown.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = dropdown.colors;
        colors.normalColor = new Color(0.76f, 0.82f, 0.90f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.60f, 0.69f, 0.82f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.45f, 0.48f, 0.54f, 0.4f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.06f;
        dropdown.colors = colors;
    }

    /// <summary>
    /// Creates an Options menu section header.
    /// </summary>
    /// <param name="parent">The page container.</param>
    /// <param name="name">The header object name.</param>
    /// <param name="caption">The header caption.</param>
    /// <param name="y">The source-space header top.</param>
    /// <param name="accent">The accent color.</param>
    /// <returns>The header text field.</returns>
    internal static TextMeshProUGUI CreateOptionsSectionHeader(
        RectTransform parent,
        string name,
        string caption,
        int y,
        Color accent
    )
    {
        TextMeshProUGUI headerField = CreateTextLabel(name, parent);
        UILayout.ConfigureMenuSectionHeading(headerField, caption, accent);
        SetSourceRect(
            headerField.rectTransform,
            _optionsContentInset,
            y,
            200,
            _optionsSectionHeaderHeight
        );
        return headerField;
    }

    /// <summary>
    /// Converts a raw image into a button with content-driven pressed visuals.
    /// </summary>
    /// <param name="image">The image that receives pointer input.</param>
    /// <returns>The generated button.</returns>
    internal static Button CreateButton(RawImage image)
    {
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;

        RawImagePressVisual pressVisual = EnableRuntimeComponent(
            image.gameObject.AddComponent<RawImagePressVisual>()
        );
        AssignReference(pressVisual, "image", image);
        AssignReference(pressVisual, "button", button);
        pressVisual.SetTextures(image.texture, null);

        ContentTextureBinding textureBinding = image.GetComponent<ContentTextureBinding>();
        if (textureBinding != null)
        {
            ContentPressVisualBinding pressBinding =
                image.gameObject.AddComponent<ContentPressVisualBinding>();
            pressBinding.SetAddresses(textureBinding.Address, null);
            UnityEngine.Object.DestroyImmediate(textureBinding);
        }
        return button;
    }

    /// <summary>
    /// Assigns a string to a serialized component field.
    /// </summary>
    /// <param name="target">The component containing the serialized field.</param>
    /// <param name="propertyName">The serialized field name.</param>
    /// <param name="value">The string to assign.</param>
    internal static void AssignString(UnityEngine.Object target, string propertyName, string value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        FindRequiredProperty(target, serializedObject, propertyName).stringValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Positions text inside an input field.
    /// </summary>
    /// <param name="child">The text or placeholder rect transform.</param>
    internal static void StretchFillInputChild(RectTransform child)
    {
        child.anchorMin = new Vector2(0f, 0f);
        child.anchorMax = new Vector2(1f, 1f);
        child.offsetMin = new Vector2(6f, -2f);
        child.offsetMax = new Vector2(-6f, -2f);
    }
}
