using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the Options menu prefab.
/// </summary>
public static partial class StrategyViewPrefabBuilder
{
    /// <summary>
    /// Builds the Options menu prefab.
    /// </summary>
    /// <returns>The Options menu view.</returns>
    private static OptionsMenuView BuildOptionsMenuPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_optionsMenuWindowPrefabPath));

        Color accent = new Color(0.373f, 0.659f, 0.925f);
        Color textColor = new Color(0.875f, 0.910f, 0.941f);
        Color textDim = new Color(0.573f, 0.635f, 0.706f);

        GameObject window = new GameObject(
            "OptionsMenu",
            typeof(RectTransform),
            typeof(UIWindow),
            typeof(OptionsMenuView)
        );
        OptionsMenuView view = EnableRuntimeComponent(window.GetComponent<OptionsMenuView>());
        ConfigureWindowRoot(window.GetComponent<UIWindow>());
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, 454, 341);

        // Options Menu Content.
        RectTransform contentRoot = CreateSourceRectLayer(
            "ContentRoot",
            window.transform,
            632,
            474
        );
        contentRoot.localScale = new Vector3(0.719f, 0.719f, 1f);

        RawImage background = CreateRawImage(
            "BackgroundImage",
            contentRoot,
            "Application/OptionsMenu/UI/ui_settingsmenu_background",
            0,
            0,
            632,
            474
        );
        background.raycastTarget = true;
        CreateSlicedImage(
            "FrameGlow",
            contentRoot,
            _optionsFrameGlowSpritePath,
            2,
            2,
            628,
            470,
            Color.white
        );

        CreateSlicedImage(
            "NavPanel",
            contentRoot,
            _optionsPanelSpritePath,
            23,
            69,
            192,
            365,
            Color.white
        );
        CreateSlicedImage(
            "ContentPanel",
            contentRoot,
            _optionsPanelSpritePath,
            228,
            69,
            382,
            365,
            Color.white
        );

        TextMeshProUGUI header = CreateTextLabel("HeaderTextField", contentRoot);
        header.text = "OPTIONS";
        header.color = textColor;
        header.fontSize = 22;
        header.alignment = TextAlignmentOptions.Midline;
        ApplyOptionsDisplayFont(header);
        SetSourceRect(header.rectTransform, 41, 32, 155, 26);

        TextMeshProUGUI pageTitle = CreateTextLabel("PageTitleTextField", contentRoot);
        pageTitle.text = "GRAPHICS";
        pageTitle.color = accent;
        pageTitle.fontSize = 14;
        pageTitle.alignment = TextAlignmentOptions.Midline;
        ApplyOptionsDisplayFont(pageTitle);
        SetSourceRect(pageTitle.rectTransform, 240, 36, 357, 22);

        string[] tabNames = { "GRAPHICS", "AUDIO", "SAVE / LOAD", "CONTROLS" };
        string[] tabObjectNames = { "GraphicsTab", "AudioTab", "SaveLoadTab", "ControlsTab" };
        Button[] tabButtons = new Button[4];
        TextMeshProUGUI[] tabLabels = new TextMeshProUGUI[4];
        for (int i = 0; i < tabNames.Length; i++)
        {
            Button tabButton = CreateSlicedButton(
                tabObjectNames[i],
                contentRoot,
                _optionsRowSpritePath,
                38,
                82 + i * 36,
                163,
                30,
                Color.white
            );
            tabButtons[i] = tabButton;
            ApplyOptionsButtonFeedback(tabButton);
            AddOptionsButtonBorder(tabButton.targetGraphic);
            TextMeshProUGUI tabLabel = CreateTextLabel(
                $"{tabObjectNames[i]}Label",
                tabButton.transform
            );
            tabLabel.text = tabNames[i];
            tabLabel.color = textColor;
            tabLabel.fontSize = 13;
            tabLabel.alignment = TextAlignmentOptions.MidlineLeft;
            ApplyOptionsDisplayFont(tabLabel);
            SetSourceRect(tabLabel.rectTransform, 14, 5, 140, 20);
            tabLabels[i] = tabLabel;
        }

        RectTransform graphicsPage = CreateChildLayer("GraphicsPage", contentRoot);
        SetSourceRect(graphicsPage, 228, 69, 382, 365);
        RectTransform audioPage = CreateChildLayer("AudioPage", contentRoot);
        SetSourceRect(audioPage, 228, 69, 382, 365);
        RectTransform saveLoadPage = CreateChildLayer("SaveLoadPage", contentRoot);
        SetSourceRect(saveLoadPage, 228, 69, 382, 365);
        RectTransform controlsPage = CreateChildLayer("ControlsPage", contentRoot);
        SetSourceRect(controlsPage, 228, 69, 382, 365);

        // Hidden Pages.
        audioPage.gameObject.SetActive(false);
        saveLoadPage.gameObject.SetActive(false);
        controlsPage.gameObject.SetActive(false);

        Button backToGameButton = CreateOptionsNavRow(
            contentRoot,
            "BackToGame",
            "BACK TO GAME",
            226,
            textDim
        );
        Button mainMenuButton = CreateOptionsNavRow(
            contentRoot,
            "MainMenu",
            "MAIN MENU",
            265,
            textDim
        );
        Button quitButton = CreateOptionsNavRow(contentRoot, "Quit", "QUIT", 304, textDim);

        RectTransform settingsActions = CreateChildLayer("SettingsActions", contentRoot);
        Button defaultsButton = CreateOptionsActionButton(
            settingsActions,
            "DefaultsButton",
            "DEFAULTS",
            338,
            textColor
        );
        Button applyButton = CreateOptionsActionButton(
            settingsActions,
            "ApplyButton",
            "APPLY",
            424,
            textColor
        );

        CreateOptionsSectionHeader(graphicsPage, "DisplayHeader", "DISPLAY", 16, accent);
        TextMeshProUGUI resolutionValue = CreateOptionsFieldRow(
            graphicsPage,
            "Resolution",
            "Resolution",
            41,
            out Button resolutionPrev,
            out Button resolutionNext
        );
        TextMeshProUGUI fullScreenValue = CreateOptionsFieldRow(
            graphicsPage,
            "Display",
            "Display Mode",
            68,
            out Button fullScreenPrev,
            out Button fullScreenNext
        );
        CreateOptionsSectionHeader(graphicsPage, "DetailHeader", "DETAIL", 101, accent);

        UserTacticalOption[] options =
        {
            UserTacticalOption.Starfield,
            UserTacticalOption.Planet,
            UserTacticalOption.Pyro,
            UserTacticalOption.HighDetail,
            UserTacticalOption.Holocube,
        };
        string[] optionLabels =
        {
            "Starfield",
            "Planet Backdrop",
            "Pyrotechnics",
            "High Detail Textures",
            "Holocube",
        };
        OptionsToggleRowView[] tacticalRows = new OptionsToggleRowView[5];
        for (int i = 0; i < options.Length; i++)
            tacticalRows[i] = CreateOptionsTacticalRow(
                graphicsPage,
                $"Tactical{options[i]}",
                options[i],
                optionLabels[i],
                20,
                126 + i * 26
            );

        CreateOptionsSectionHeader(audioPage, "VolumeHeader", "VOLUME", 16, accent);
        string[] volumeLabels = { "Master", "Music", "Sound Effects", "Ambience", "Video" };
        SaveMenuSliderView[] volumeSliders = new SaveMenuSliderView[5];
        TextMeshProUGUI[] volumeValues = new TextMeshProUGUI[5];
        for (int i = 0; i < volumeLabels.Length; i++)
        {
            int rowY = 44 + i * 34;
            TextMeshProUGUI volumeLabel = CreateTextLabel($"VolumeLabel{i}", audioPage);
            volumeLabel.text = volumeLabels[i];
            volumeLabel.color = textColor;
            volumeLabel.fontSize = 11;
            volumeLabel.alignment = TextAlignmentOptions.MidlineLeft;
            SetSourceRect(volumeLabel.rectTransform, 20, rowY - 1, 100, 14);
            volumeSliders[i] = CreateOptionsSlider(
                audioPage,
                $"VolumeSlider{i}",
                181,
                rowY + 2,
                146
            );
            TextMeshProUGUI volumeValue = CreateTextLabel($"VolumeValue{i}", audioPage);
            volumeValue.text = "100";
            volumeValue.color = textColor;
            volumeValue.fontSize = 10;
            volumeValue.alignment = TextAlignmentOptions.MidlineLeft;
            SetSourceRect(volumeValue.rectTransform, 336, rowY - 1, 30, 14);
            volumeValues[i] = volumeValue;
        }

        TextMeshProUGUI savedGamesLabel = CreateOptionsSectionHeader(
            saveLoadPage,
            "SavedGamesLabel",
            "SAVED GAMES",
            13,
            accent
        );
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
            _optionsRowSpritePath,
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
        slotNameTemplate.raycastTarget = false;
        ApplyOptionsDisplayFont(slotNameTemplate);
        SetSourceRect(slotNameTemplate.rectTransform, 34, 2, 300, 13);
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
        slotRenameField.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
        StretchFillInputChild((RectTransform)slotRenameField.textComponent.transform);
        slotRenameField.customCaretColor = true;
        slotRenameField.caretColor = Color.white;
        slotRenameField.caretWidth = 2;
        slotRenameField.caretBlinkRate = 0.85f;
        if (slotRenameField.placeholder is TextMeshProUGUI renamePlaceholder)
        {
            renamePlaceholder.fontSize = 12;
            renamePlaceholder.alignment = TextAlignmentOptions.MidlineLeft;
            renamePlaceholder.color = new Color(0.60f, 0.65f, 0.72f);
            StretchFillInputChild(renamePlaceholder.rectTransform);
        }
        slotRenameField.onFocusSelectAll = false;
        slotRenameField.gameObject.SetActive(false);

        ScrollAreaView controlsScrollArea = CreateScrollAreaView(
            controlsPage,
            "ControlsScrollArea",
            18,
            16,
            356,
            312,
            0,
            0,
            342,
            312,
            344,
            0,
            12,
            312,
            out RectTransform controlsContent
        );

        TextMeshProUGUI primaryColumnHeader = CreateTextLabel("PrimaryColumnHeader", controlsPage);
        primaryColumnHeader.text = "PRIMARY";
        primaryColumnHeader.color = textDim;
        primaryColumnHeader.fontSize = 9;
        primaryColumnHeader.alignment = TextAlignmentOptions.Midline;
        ApplyOptionsDisplayFont(primaryColumnHeader);
        SetSourceRect(primaryColumnHeader.rectTransform, 216, 2, 63, 12);
        TextMeshProUGUI secondaryColumnHeader = CreateTextLabel(
            "SecondaryColumnHeader",
            controlsPage
        );
        secondaryColumnHeader.text = "SECONDARY";
        secondaryColumnHeader.color = textDim;
        secondaryColumnHeader.fontSize = 9;
        secondaryColumnHeader.alignment = TextAlignmentOptions.Midline;
        ApplyOptionsDisplayFont(secondaryColumnHeader);
        SetSourceRect(secondaryColumnHeader.rectTransform, 283, 2, 63, 12);

        Image bindingRowTemplate = CreateSlicedImage(
            "BindingRowTemplate",
            controlsContent,
            _optionsRowSpritePath,
            0,
            0,
            342,
            23,
            Color.white
        );
        bindingRowTemplate.gameObject.SetActive(false);
        TextMeshProUGUI bindingHeaderTemplate = CreateTextLabel(
            "BindingHeaderTemplate",
            controlsContent
        );
        bindingHeaderTemplate.text = "GROUP";
        bindingHeaderTemplate.color = accent;
        bindingHeaderTemplate.fontSize = 12;
        bindingHeaderTemplate.alignment = TextAlignmentOptions.TopLeft;
        ApplyOptionsDisplayFont(bindingHeaderTemplate);
        SetSourceRect(bindingHeaderTemplate.rectTransform, 0, 0, 220, 16);
        bindingHeaderTemplate.gameObject.SetActive(false);
        TextMeshProUGUI bindingActionTemplate = CreateTextLabel(
            "BindingActionTemplate",
            controlsContent
        );
        bindingActionTemplate.text = "Action";
        bindingActionTemplate.color = textDim;
        bindingActionTemplate.fontSize = 11;
        bindingActionTemplate.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(bindingActionTemplate.rectTransform, 11, 4, 245, 15);
        bindingActionTemplate.gameObject.SetActive(false);
        Image bindingKeyBadgeTemplate = CreateSlicedImage(
            "BindingKeyBadgeTemplate",
            controlsContent,
            _optionsBadgeSpritePath,
            266,
            3,
            61,
            16,
            Color.white
        );
        bindingKeyBadgeTemplate.raycastTarget = true;
        Button bindingKeyBadgeButton = bindingKeyBadgeTemplate.gameObject.AddComponent<Button>();
        bindingKeyBadgeButton.targetGraphic = bindingKeyBadgeTemplate;
        bindingKeyBadgeButton.transition = Selectable.Transition.None;
        bindingKeyBadgeTemplate.gameObject.SetActive(false);
        TextMeshProUGUI bindingKeyTemplate = CreateTextLabel("BindingKeyTemplate", controlsContent);
        bindingKeyTemplate.text = "Key";
        bindingKeyTemplate.color = textColor;
        bindingKeyTemplate.fontSize = 10;
        bindingKeyTemplate.alignment = TextAlignmentOptions.Midline;
        SetSourceRect(bindingKeyTemplate.rectTransform, 266, 3, 61, 16);
        bindingKeyTemplate.gameObject.SetActive(false);

        // Confirmation Dialog.
        SaveMenuConfirmDialogView confirmDialog = SaveMenuPrefabBuilder.CreateConfirmDialog(
            contentRoot
        );
        confirmDialog.transform.SetAsLastSibling();
        confirmDialog.gameObject.SetActive(false);
        ConfigureOptionsNavigationLayout(contentRoot, backToGameButton, mainMenuButton, quitButton);

        AssignReference(view, "_backgroundImage", background);
        AssignReference(view, "_headerTextField", header);
        AssignReference(view, "_pageTitleTextField", pageTitle);
        AssignReference(
            view,
            "_rowIdleSprite",
            AssetDatabase.LoadAssetAtPath<Sprite>(_optionsRowSpritePath)
        );
        AssignReference(
            view,
            "_rowActiveSprite",
            AssetDatabase.LoadAssetAtPath<Sprite>(_optionsRowActiveSpritePath)
        );
        AssignReferenceArray(view, "_tabButtons", tabButtons);
        AssignReferenceArray(view, "_tabLabelFields", tabLabels);
        AssignReference(view, "_graphicsPage", graphicsPage.gameObject);
        AssignReference(view, "_audioPage", audioPage.gameObject);
        AssignReference(view, "_saveLoadPage", saveLoadPage.gameObject);
        AssignReference(view, "_controlsPage", controlsPage.gameObject);
        AssignReference(view, "_backToGameButton", backToGameButton);
        AssignReference(view, "_mainMenuButton", mainMenuButton);
        AssignReference(view, "_quitButton", quitButton);
        AssignReferenceArray(view, "_tacticalRows", tacticalRows);
        AssignReference(view, "_resolutionValueField", resolutionValue);
        AssignReference(view, "_resolutionPrevButton", resolutionPrev);
        AssignReference(view, "_resolutionNextButton", resolutionNext);
        AssignReference(view, "_fullScreenValueField", fullScreenValue);
        AssignReference(view, "_fullScreenPrevButton", fullScreenPrev);
        AssignReference(view, "_fullScreenNextButton", fullScreenNext);
        AssignReferenceArray(view, "_volumeSliders", volumeSliders);
        AssignReferenceArray(view, "_volumeValueFields", volumeValues);
        AssignReference(view, "_saveButton", saveButton);
        AssignReference(view, "_loadButton", loadButton);
        AssignReference(view, "_saveDisabledImage", saveDisabledIcon);
        AssignReference(view, "_loadDisabledImage", loadDisabledIcon);
        AssignReference(view, "_settingsActions", settingsActions.gameObject);
        AssignReference(view, "_applyButton", applyButton);
        AssignReference(view, "_defaultsButton", defaultsButton);
        AssignReference(view, "_confirmDialog", confirmDialog);
        AssignReference(view, "_saveSlotScrollArea", saveSlotScrollArea);
        AssignReference(view, "_slotRowTemplate", slotRowTemplate);
        AssignReference(view, "_slotIconTemplate", slotIconTemplate);
        AssignReference(view, "_slotNameTemplate", slotNameTemplate);
        AssignReference(view, "_slotMetaTemplate", slotMetaTemplate);
        AssignReference(view, "_slotDeleteTemplate", slotDeleteTemplate);
        AssignReference(view, "_slotRenameField", slotRenameField);
        AssignReference(view, "_controlsScrollArea", controlsScrollArea);
        AssignReference(view, "_bindingRowTemplate", bindingRowTemplate);
        AssignReference(view, "_bindingHeaderTemplate", bindingHeaderTemplate);
        AssignReference(view, "_bindingActionTemplate", bindingActionTemplate);
        AssignReference(view, "_bindingKeyBadgeTemplate", bindingKeyBadgeTemplate);
        AssignReference(view, "_bindingKeyTemplate", bindingKeyTemplate);

        GameObject saved = SaveGeneratedPrefabAsset(window, _optionsMenuWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<OptionsMenuView>();
    }

    /// <summary>
    /// Adds color feedback to a button.
    /// </summary>
    /// <param name="button">The button to restyle.</param>
    /// <param name="target">The graphic to tint, or null to keep the button's target graphic.</param>
    private static void ApplyOptionsButtonFeedback(Button button, Graphic target = null)
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
    /// Adds color feedback to a filled button.
    /// </summary>
    /// <param name="button">The fill button to restyle.</param>
    private static void ApplyOptionsFillButtonFeedback(Button button)
    {
        if (button == null)
            return;

        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.34f, 0.44f, 0.60f);
        colors.highlightedColor = new Color(0.49f, 0.61f, 0.80f);
        colors.pressedColor = new Color(0.66f, 0.79f, 0.98f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.22f, 0.24f, 0.28f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    /// <summary>
    /// Adds a border to a button.
    /// </summary>
    /// <param name="graphic">The button fill graphic to outline.</param>
    private static void AddOptionsButtonBorder(Graphic graphic)
    {
        if (graphic == null)
            return;

        Outline outline = graphic.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.60f, 0.69f, 0.85f, 0.9f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        outline.useGraphicAlpha = false;
    }

    /// <summary>
    /// Positions text inside an input field.
    /// </summary>
    /// <param name="child">The text or placeholder rect transform.</param>
    private static void StretchFillInputChild(RectTransform child)
    {
        child.anchorMin = new Vector2(0f, 0f);
        child.anchorMax = new Vector2(1f, 1f);
        child.offsetMin = new Vector2(6f, 0f);
        child.offsetMax = new Vector2(-6f, 0f);
    }

    /// <summary>
    /// Applies the Options menu font.
    /// </summary>
    /// <param name="text">The text field to restyle.</param>
    private static void ApplyOptionsDisplayFont(TextMeshProUGUI text)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Oswald Bold SDF.asset"
        );
        if (font != null)
            text.font = font;
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
    private static TextMeshProUGUI CreateOptionsSectionHeader(
        RectTransform parent,
        string name,
        string caption,
        int y,
        Color accent
    )
    {
        TextMeshProUGUI headerField = CreateTextLabel(name, parent);
        headerField.text = caption;
        headerField.color = accent;
        headerField.fontSize = 13;
        headerField.alignment = TextAlignmentOptions.MidlineLeft;
        ApplyOptionsDisplayFont(headerField);
        SetSourceRect(headerField.rectTransform, 20, y, 200, 16);
        return headerField;
    }

    /// <summary>
    /// Creates an Options menu navigation row.
    /// </summary>
    /// <param name="parent">The content-root transform.</param>
    /// <param name="name">The button object name.</param>
    /// <param name="label">The caps row caption.</param>
    /// <param name="y">The source-space row top.</param>
    /// <param name="color">The label color.</param>
    /// <returns>The configured button.</returns>
    private static Button CreateOptionsNavRow(
        Transform parent,
        string name,
        string label,
        int y,
        Color color
    )
    {
        Button button = CreateSlicedButton(
            name,
            parent,
            _optionsBadgeSpritePath,
            38,
            y,
            163,
            30,
            Color.white
        );
        TextMeshProUGUI text = CreateTextLabel("Label", button.transform);
        text.text = label;
        text.color = color;
        text.fontSize = 13;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        ApplyOptionsDisplayFont(text);
        SetSourceRect(text.rectTransform, 14, 5, 140, 20);
        ApplyOptionsFillButtonFeedback(button);
        AddOptionsButtonBorder(button.targetGraphic);
        return button;
    }

    /// <summary>
    /// Creates the navigation layout.
    /// </summary>
    private static void ConfigureOptionsNavigationLayout(
        RectTransform contentRoot,
        Button backToGameButton,
        Button mainMenuButton,
        Button quitButton
    )
    {
        VerticalLayoutGroup layout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(38, 0, 226, 0);
        layout.spacing = 9f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        for (int index = 0; index < contentRoot.childCount; index++)
        {
            Transform child = contentRoot.GetChild(index);
            bool navigationChild =
                child == backToGameButton.transform
                || child == mainMenuButton.transform
                || child == quitButton.transform;
            LayoutElement element = child.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = !navigationChild;
            if (navigationChild)
            {
                element.preferredWidth = 163f;
                element.preferredHeight = 30f;
            }
        }
    }

    /// <summary>
    /// Creates a settings button.
    /// </summary>
    /// <param name="parent">The settings-actions container.</param>
    /// <param name="name">The button object name.</param>
    /// <param name="label">The caps caption.</param>
    /// <param name="x">The source-space left position.</param>
    /// <param name="color">The label color.</param>
    /// <returns>The configured button.</returns>
    private static Button CreateOptionsActionButton(
        Transform parent,
        string name,
        string label,
        int x,
        Color color
    )
    {
        Button button = CreateSlicedButton(
            name,
            parent,
            _optionsBadgeSpritePath,
            x,
            408,
            76,
            22,
            Color.white
        );
        TextMeshProUGUI text = CreateTextLabel("Label", button.transform);
        text.text = label;
        text.color = color;
        text.fontSize = 11;
        text.alignment = TextAlignmentOptions.Midline;
        ApplyOptionsDisplayFont(text);
        SetSourceRect(text.rectTransform, 0, 4, 76, 14);
        ApplyOptionsFillButtonFeedback(button);
        AddOptionsButtonBorder(button.targetGraphic);
        return button;
    }

    /// <summary>
    /// Creates a display setting row.
    /// </summary>
    /// <param name="parent">The Graphics-page container.</param>
    /// <param name="name">The row object-name prefix.</param>
    /// <param name="caption">The row caption.</param>
    /// <param name="y">The source-space row top.</param>
    /// <param name="prevButton">Receives the previous-step button.</param>
    /// <param name="nextButton">Receives the next-step button.</param>
    /// <returns>The value text field.</returns>
    private static TextMeshProUGUI CreateOptionsFieldRow(
        RectTransform parent,
        string name,
        string caption,
        int y,
        out Button prevButton,
        out Button nextButton
    )
    {
        TextMeshProUGUI labelField = CreateTextLabel($"{name}Label", parent);
        labelField.text = caption;
        labelField.color = new Color(0.875f, 0.910f, 0.941f);
        labelField.fontSize = 11;
        labelField.alignment = TextAlignmentOptions.MidlineLeft;
        SetSourceRect(labelField.rectTransform, 20, y + 2, 150, 14);

        CreateSlicedImage(
            $"{name}Badge",
            parent,
            _optionsBadgeSpritePath,
            194,
            y,
            155,
            18,
            Color.white
        );
        prevButton = CreateOptionsStepperButton(parent, $"{name}Prev", "<", 194, y, 78, 0);
        TextMeshProUGUI valueField = CreateTextLabel($"{name}Value", parent);
        valueField.text = "-";
        valueField.color = new Color(0.875f, 0.910f, 0.941f);
        valueField.fontSize = 10;
        valueField.alignment = TextAlignmentOptions.Midline;
        SetSourceRect(valueField.rectTransform, 218, y, 107, 18);
        nextButton = CreateOptionsStepperButton(parent, $"{name}Next", ">", 272, y, 77, 51);
        return valueField;
    }

    /// <summary>
    /// Creates a display setting step button.
    /// </summary>
    /// <param name="parent">The row container.</param>
    /// <param name="name">The button object name.</param>
    /// <param name="glyph">The chevron glyph.</param>
    /// <param name="x">The source-space hotspot left.</param>
    /// <param name="y">The source-space hotspot top.</param>
    /// <param name="hitWidth">The full clickable width.</param>
    /// <param name="glyphX">The chevron's horizontal position inside the hotspot.</param>
    /// <returns>The configured button.</returns>
    private static Button CreateOptionsStepperButton(
        Transform parent,
        string name,
        string glyph,
        int x,
        int y,
        int hitWidth,
        int glyphX
    )
    {
        GameObject hotspot = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        hotspot.transform.SetParent(parent, false);
        Image hitArea = hotspot.GetComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;
        SetSourceRect(hotspot.GetComponent<RectTransform>(), x, y, hitWidth, 18);
        Button button = hotspot.AddComponent<Button>();
        button.targetGraphic = hitArea;
        button.transition = Selectable.Transition.None;
        TextMeshProUGUI glyphText = CreateTextLabel("Glyph", hotspot.transform);
        glyphText.text = glyph;
        glyphText.color = new Color(0.373f, 0.659f, 0.925f);
        glyphText.fontSize = 10;
        glyphText.alignment = TextAlignmentOptions.Midline;
        SetSourceRect(glyphText.rectTransform, glyphX, 0, 26, 18);
        ApplyOptionsButtonFeedback(button, glyphText);
        return button;
    }

    /// <summary>
    /// Creates a volume slider.
    /// </summary>
    /// <param name="parent">The Audio-page container.</param>
    /// <param name="name">The slider object name.</param>
    /// <param name="x">The source-space slider left.</param>
    /// <param name="y">The source-space slider top.</param>
    /// <param name="width">The source-space track width.</param>
    /// <returns>The configured slider view.</returns>
    private static SaveMenuSliderView CreateOptionsSlider(
        Transform parent,
        string name,
        int x,
        int y,
        int width
    )
    {
        Texture2D knobTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(_optionsKnobTexturePath);
        Vector2Int knobSize =
            knobTexture != null
                ? UILayout.GetTextureSourceSize(knobTexture)
                : new Vector2Int(10, 10);

        GameObject root = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Slider),
            typeof(SaveMenuSliderView)
        );
        root.transform.SetParent(parent, false);
        SetSourceRect(root.GetComponent<RectTransform>(), x, y, width, knobSize.y);

        Image hitArea = root.GetComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;

        RectTransform trackBand = CreateChildLayer("TrackBand", root.transform);
        SetSourceRect(trackBand, 0, Mathf.Max(0, (knobSize.y - 4) / 2), width, 4);
        GameObject trackBackground = new GameObject(
            "TrackBackground",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        trackBackground.transform.SetParent(trackBand, false);
        Image trackImage = trackBackground.GetComponent<Image>();
        trackImage.color = new Color(0.180f, 0.220f, 0.275f);
        trackImage.raycastTarget = false;
        FillParent(trackBackground.GetComponent<RectTransform>());
        RectTransform fillArea = CreateChildLayer("FillArea", trackBand);
        FillParent(fillArea);
        Image fillImage = fillArea.gameObject.AddComponent<Image>();
        fillImage.color = new Color(0.373f, 0.659f, 0.925f);
        fillImage.raycastTarget = false;

        GameObject thumb = new GameObject(
            "ThumbImage",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage)
        );
        thumb.transform.SetParent(root.transform, false);
        RawImage thumbImage = thumb.GetComponent<RawImage>();
        thumbImage.texture = knobTexture;
        thumbImage.raycastTarget = true;
        SetSourceRect(thumbImage.rectTransform, 0, 0, knobSize.x, knobSize.y);

        Slider slider = root.GetComponent<Slider>();
        slider.enabled = true;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fillArea;
        slider.handleRect = null;
        slider.targetGraphic = null;
        slider.transition = Selectable.Transition.None;

        SaveMenuSliderView view = EnableRuntimeComponent(root.GetComponent<SaveMenuSliderView>());
        AssignReference(view, "slider", slider);
        AssignReference(view, "thumbImage", thumbImage);
        return view;
    }

    /// <summary>
    /// Creates a tactical option row.
    /// </summary>
    /// <param name="parent">The Graphics-page container.</param>
    /// <param name="name">The row object name.</param>
    /// <param name="option">The tactical option the row toggles.</param>
    /// <param name="label">The row caption.</param>
    /// <param name="x">The source-space row left.</param>
    /// <param name="y">The source-space row top.</param>
    /// <returns>The configured toggle-row view.</returns>
    private static OptionsToggleRowView CreateOptionsTacticalRow(
        Transform parent,
        string name,
        UserTacticalOption option,
        string label,
        int x,
        int y
    )
    {
        RectTransform root = CreateChildLayer(name, parent);
        SetSourceRect(root, x, y, 330, 14);
        OptionsToggleRowView view = EnableRuntimeComponent(
            root.gameObject.AddComponent<OptionsToggleRowView>()
        );

        TextMeshProUGUI labelField = CreateTextLabel("LabelTextField", root);
        labelField.text = label;
        labelField.color = new Color(0.875f, 0.910f, 0.941f);
        labelField.fontSize = 11;
        labelField.alignment = TextAlignmentOptions.MidlineLeft;
        SetSourceRect(labelField.rectTransform, 0, 0, 170, 14);

        GameObject toggle = new GameObject(
            "ToggleImage",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        toggle.transform.SetParent(root, false);
        Image toggleImage = toggle.GetComponent<Image>();
        toggleImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(_optionsToggleOffSpritePath);
        toggleImage.type = Image.Type.Simple;
        toggleImage.raycastTarget = true;
        SetSourceRect(toggle.GetComponent<RectTransform>(), 299, 0, 30, 14);
        Button button = toggle.AddComponent<Button>();
        button.targetGraphic = toggleImage;
        button.transition = Selectable.Transition.None;

        TextMeshProUGUI stateField = CreateTextLabel("StateTextField", root);
        stateField.text = "ON";
        stateField.color = new Color(0.875f, 0.910f, 0.941f);
        stateField.fontSize = 9;
        stateField.alignment = TextAlignmentOptions.MidlineRight;
        SetSourceRect(stateField.rectTransform, 239, 0, 50, 14);

        AssignInt(view, "_option", (int)option);
        AssignReference(view, "_toggleImage", toggleImage);
        AssignReference(
            view,
            "_offSprite",
            AssetDatabase.LoadAssetAtPath<Sprite>(_optionsToggleOffSpritePath)
        );
        AssignReference(
            view,
            "_onSprite",
            AssetDatabase.LoadAssetAtPath<Sprite>(_optionsToggleOnSpritePath)
        );
        AssignReference(view, "_button", button);
        AssignReference(view, "_labelTextField", labelField);
        AssignReference(view, "_stateTextField", stateField);
        return view;
    }
}
