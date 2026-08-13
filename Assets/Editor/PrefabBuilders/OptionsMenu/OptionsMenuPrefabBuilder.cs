using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the Options menu prefab.
/// </summary>
public static class OptionsMenuPrefabBuilder
{
    private const string _optionsMenuWindowPrefabPath =
        "Assets/Prefabs/UI/OptionsMenu/OptionsMenu.prefab";
    private const string _optionsPanelAddress =
        "Application/OptionsMenu/UI/ui_settingsmenu_panel_background";
    private const string _optionsRowAddress =
        "Application/OptionsMenu/UI/ui_settingsmenu_row_background";
    private const string _optionsRowActiveAddress =
        "Application/OptionsMenu/UI/ui_settingsmenu_row_selected_background";
    private const string _optionsBadgeAddress =
        "Application/OptionsMenu/UI/ui_settingsmenu_badge_background";
    private const string _optionsFrameGlowAddress =
        "Application/OptionsMenu/UI/ui_settingsmenu_frame_overlay";
    private const string _optionsToggleOnAddress =
        "Application/OptionsMenu/UI/ui_settingsmenu_toggle_selected_icon";
    private const string _optionsToggleOffAddress =
        "Application/OptionsMenu/UI/ui_settingsmenu_toggle_icon";
    private const string _optionsKnobAddress =
        "Application/OptionsMenu/UI/ui_settingsmenu_slider_knob";
    private const string _scrollAreaPrefabPath = "Assets/Prefabs/UI/Common/ScrollArea.prefab";
    private const string _textInputPrefabPath = "Assets/Prefabs/UI/Common/TextInput.prefab";
    private const string _scrollUpAddress =
        "Application/Strategy/UI/Controls/ui_strategyview_scrollbar_arrow_up.png";
    private const string _scrollDownAddress =
        "Application/Strategy/UI/Controls/ui_strategyview_scrollbar_arrow_pressed_2.png";
    private const string _scrollHandleAddress =
        "Application/Strategy/UI/Controls/ui_strategyview_scrollbar_middle.png";

    private static readonly Vector4 _panelBorder = new Vector4(7f, 7f, 7f, 7f);
    private static readonly Vector4 _badgeBorder = new Vector4(6f, 6f, 6f, 6f);

    /// <summary>
    /// Rebuilds the generated Options menu prefab.
    /// </summary>
    public static void Rebuild()
    {
        BuildOptionsMenuPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

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
            _optionsFrameGlowAddress,
            _panelBorder,
            2,
            2,
            628,
            470,
            Color.white
        );

        CreateSlicedImage(
            "NavPanel",
            contentRoot,
            _optionsPanelAddress,
            _panelBorder,
            23,
            69,
            192,
            365,
            Color.white
        );
        CreateSlicedImage(
            "ContentPanel",
            contentRoot,
            _optionsPanelAddress,
            _panelBorder,
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
                _optionsRowAddress,
                _panelBorder,
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
        OptionsSaveListView saveListView = EnableRuntimeComponent(
            saveLoadPage.gameObject.AddComponent<OptionsSaveListView>()
        );
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
        NormalizedSliderView[] volumeSliders = new NormalizedSliderView[5];
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
            _optionsRowAddress,
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
        ApplyOptionsDisplayFont(slotNameTemplate);
        SetSourceRect(slotNameTemplate.rectTransform, 34, 0, 300, 20);
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
            _optionsRowAddress,
            _panelBorder,
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
            _optionsBadgeAddress,
            _badgeBorder,
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
        ConfirmationDialogView confirmDialog = CommonUIPrefabBuilder.InstantiateConfirmationDialog(
            contentRoot
        );
        confirmDialog.gameObject.name = "ConfirmDialog";
        confirmDialog.transform.SetAsLastSibling();
        confirmDialog.gameObject.SetActive(false);
        ConfigureOptionsNavigationLayout(contentRoot, backToGameButton, mainMenuButton, quitButton);

        AssignReference(view, "_backgroundImage", background);
        AssignReference(view, "_headerTextField", header);
        AssignReference(view, "_pageTitleTextField", pageTitle);
        AssignReference(view, "_rowIdleSprite", LoadSprite(_optionsRowAddress, _panelBorder));
        AssignReference(
            view,
            "_rowActiveSprite",
            LoadSprite(_optionsRowActiveAddress, _panelBorder)
        );
        AssignString(view, "_rowIdleSpriteAddress", _optionsRowAddress);
        AssignString(view, "_rowActiveSpriteAddress", _optionsRowActiveAddress);
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
        AssignReference(view, "_saveListView", saveListView);
        AssignReference(view, "_settingsActions", settingsActions.gameObject);
        AssignReference(view, "_applyButton", applyButton);
        AssignReference(view, "_defaultsButton", defaultsButton);
        AssignReference(view, "_confirmDialog", confirmDialog);
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
        AssignReference(
            saveListView,
            "_rowIdleSprite",
            LoadSprite(_optionsRowAddress, _panelBorder)
        );
        AssignReference(
            saveListView,
            "_rowActiveSprite",
            LoadSprite(_optionsRowActiveAddress, _panelBorder)
        );
        AssignString(saveListView, "_rowIdleSpriteAddress", _optionsRowAddress);
        AssignString(saveListView, "_rowActiveSpriteAddress", _optionsRowActiveAddress);
        AssignReference(view, "_controlsScrollArea", controlsScrollArea);
        AssignReference(view, "_bindingRowTemplate", bindingRowTemplate);
        AssignReference(view, "_bindingHeaderTemplate", bindingHeaderTemplate);
        AssignReference(view, "_bindingActionTemplate", bindingActionTemplate);
        AssignReference(view, "_bindingKeyBadgeTemplate", bindingKeyBadgeTemplate);
        AssignReference(view, "_bindingKeyTemplate", bindingKeyTemplate);

        GameObject saved = SaveGeneratedPrefabAsset(window, _optionsMenuWindowPrefabPath);
        UnityEngine.Object.DestroyImmediate(window);
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
            _optionsBadgeAddress,
            _badgeBorder,
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
            _optionsBadgeAddress,
            _badgeBorder,
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
            _optionsBadgeAddress,
            _badgeBorder,
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
    private static NormalizedSliderView CreateOptionsSlider(
        Transform parent,
        string name,
        int x,
        int y,
        int width
    )
    {
        Texture2D knobTexture = LoadTexture(_optionsKnobAddress);
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
            typeof(NormalizedSliderView)
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
        AttachTextureBinding(thumbImage, _optionsKnobAddress);
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

        NormalizedSliderView view = EnableRuntimeComponent(
            root.GetComponent<NormalizedSliderView>()
        );
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
        toggleImage.sprite = LoadSprite(_optionsToggleOffAddress, Vector4.zero);
        toggleImage.type = Image.Type.Simple;
        toggleImage.raycastTarget = true;
        ContentSpriteBinding toggleBinding = toggle.AddComponent<ContentSpriteBinding>();
        toggleBinding.SetAddress(_optionsToggleOffAddress);
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
        AssignReference(view, "_offSprite", LoadSprite(_optionsToggleOffAddress, Vector4.zero));
        AssignReference(view, "_onSprite", LoadSprite(_optionsToggleOnAddress, Vector4.zero));
        AssignString(view, "_offSpriteAddress", _optionsToggleOffAddress);
        AssignString(view, "_onSpriteAddress", _optionsToggleOnAddress);
        AssignReference(view, "_button", button);
        AssignReference(view, "_labelTextField", labelField);
        AssignReference(view, "_stateTextField", stateField);
        return view;
    }

    /// <summary>
    /// Enables a generated runtime component and rejects a missing reference.
    /// </summary>
    /// <typeparam name="T">The runtime component type.</typeparam>
    /// <param name="component">The component to enable.</param>
    /// <returns>The enabled component.</returns>
    private static T EnableRuntimeComponent<T>(T component)
        where T : MonoBehaviour
    {
        if (component == null)
            throw new ArgumentNullException(nameof(component));

        component.enabled = true;
        return component;
    }

    /// <summary>
    /// Enables a generated window and gives it an interactive canvas group.
    /// </summary>
    /// <param name="window">The window root being authored.</param>
    private static void ConfigureWindowRoot(UIWindow window)
    {
        EnableRuntimeComponent(window);
        CanvasGroup inputGroup = window.GetComponent<CanvasGroup>();
        if (inputGroup == null)
            inputGroup = window.gameObject.AddComponent<CanvasGroup>();

        inputGroup.alpha = 1f;
        inputGroup.interactable = true;
        inputGroup.blocksRaycasts = true;
        inputGroup.ignoreParentGroups = false;
        AssignReference(window, "inputGroup", inputGroup);
    }

    /// <summary>
    /// Creates a rectangular child layer stretched across its parent.
    /// </summary>
    /// <param name="name">The generated object name.</param>
    /// <param name="parent">The layer's parent.</param>
    /// <returns>The generated rectangle.</returns>
    private static RectTransform CreateChildLayer(string name, Transform parent)
    {
        RectTransform rect = CreateLayer(name, parent).GetComponent<RectTransform>();
        FillParent(rect);
        return rect;
    }

    /// <summary>
    /// Creates a top-left anchored layer with a source-pixel size.
    /// </summary>
    /// <param name="name">The generated object name.</param>
    /// <param name="parent">The layer's parent.</param>
    /// <param name="width">The layer width in source pixels.</param>
    /// <param name="height">The layer height in source pixels.</param>
    /// <returns>The generated rectangle.</returns>
    private static RectTransform CreateSourceRectLayer(
        string name,
        Transform parent,
        int width,
        int height
    )
    {
        RectTransform rect = CreateLayer(name, parent).GetComponent<RectTransform>();
        SetSourceRect(rect, 0, 0, width, height);
        return rect;
    }

    /// <summary>
    /// Creates and positions a sliced sprite image.
    /// </summary>
    /// <param name="name">The generated object name.</param>
    /// <param name="parent">The image's parent.</param>
    /// <param name="spriteAddress">The stable content address of the sliced sprite.</param>
    /// <param name="border">The sprite's explicit nine-slice border.</param>
    /// <param name="x">The source-space left coordinate.</param>
    /// <param name="y">The source-space top coordinate.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <param name="color">The image tint.</param>
    /// <returns>The generated image.</returns>
    private static Image CreateSlicedImage(
        string name,
        Transform parent,
        string spriteAddress,
        Vector4 border,
        int x,
        int y,
        int width,
        int height,
        Color color
    )
    {
        GameObject imageObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = LoadSprite(spriteAddress, border);
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        ContentSpriteBinding binding = imageObject.AddComponent<ContentSpriteBinding>();
        binding.SetAddress(spriteAddress, border);
        SetSourceRect(image.rectTransform, x, y, width, height);
        return image;
    }

    /// <summary>
    /// Creates a sliced image configured as a non-transitioning button.
    /// </summary>
    /// <param name="name">The generated object name.</param>
    /// <param name="parent">The button's parent.</param>
    /// <param name="spriteAddress">The stable content address of the sliced sprite.</param>
    /// <param name="border">The sprite's explicit nine-slice border.</param>
    /// <param name="x">The source-space left coordinate.</param>
    /// <param name="y">The source-space top coordinate.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <param name="color">The image tint.</param>
    /// <returns>The generated button.</returns>
    private static Button CreateSlicedButton(
        string name,
        Transform parent,
        string spriteAddress,
        Vector4 border,
        int x,
        int y,
        int width,
        int height,
        Color color
    )
    {
        Image image = CreateSlicedImage(
            name,
            parent,
            spriteAddress,
            border,
            x,
            y,
            width,
            height,
            color
        );
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        return button;
    }

    /// <summary>
    /// Creates a text label with the Options menu's baseline presentation.
    /// </summary>
    /// <param name="name">The generated object name.</param>
    /// <param name="parent">The label's parent.</param>
    /// <returns>The generated text label.</returns>
    private static TextMeshProUGUI CreateTextLabel(string name, Transform parent)
    {
        GameObject labelObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI),
            typeof(Shadow)
        );
        labelObject.transform.SetParent(parent, false);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = "Corellian";
        label.color = Color.yellow;
        label.fontSize = 13;
        label.alignment = TextAlignmentOptions.Top;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;

        Shadow shadow = labelObject.GetComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(1f, -1f);
        return label;
    }

    /// <summary>
    /// Creates an optionally textured raw-image surface used by menu controls.
    /// </summary>
    /// <param name="name">The generated object name.</param>
    /// <param name="parent">The image's parent.</param>
    /// <param name="texturePath">The optional content texture address.</param>
    /// <returns>The generated raw image.</returns>
    private static RawImage CreateRawButton(
        string name,
        Transform parent,
        string texturePath = null
    )
    {
        GameObject imageObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage)
        );
        imageObject.transform.SetParent(parent, false);
        RawImage image = imageObject.GetComponent<RawImage>();
        image.texture = string.IsNullOrEmpty(texturePath) ? null : LoadTexture(texturePath);
        image.raycastTarget = false;
        if (!string.IsNullOrEmpty(texturePath))
            AttachTextureBinding(image, texturePath);
        if (image.texture != null)
        {
            Vector2Int size = UILayout.GetTextureSourceSize(image.texture);
            SetSourceRect(image.rectTransform, 0, 0, size.x, size.y);
        }
        return image;
    }

    /// <summary>
    /// Creates a textured raw image at a source-space rectangle.
    /// </summary>
    /// <param name="name">The generated object name.</param>
    /// <param name="parent">The image's parent.</param>
    /// <param name="texturePath">The content texture address.</param>
    /// <param name="x">The source-space left coordinate.</param>
    /// <param name="y">The source-space top coordinate.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <returns>The generated raw image.</returns>
    private static RawImage CreateRawImage(
        string name,
        Transform parent,
        string texturePath,
        int x,
        int y,
        int width,
        int height
    )
    {
        RawImage image = CreateRawButton(name, parent, texturePath);
        SetSourceRect(image.rectTransform, x, y, width, height);
        return image;
    }

    /// <summary>
    /// Converts a raw image into a button with content-driven pressed visuals.
    /// </summary>
    /// <param name="image">The image that receives pointer input.</param>
    /// <returns>The generated button.</returns>
    private static Button CreateButton(RawImage image)
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
    /// Instantiates and configures the shared text-input prefab for the Options layout.
    /// </summary>
    /// <param name="name">The generated object name.</param>
    /// <param name="parent">The field's parent.</param>
    /// <param name="placeholderText">The empty-field prompt.</param>
    /// <param name="x">The source-space left coordinate.</param>
    /// <param name="y">The source-space top coordinate.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <returns>The configured input field.</returns>
    private static TMP_InputField CreateTextInputField(
        string name,
        Transform parent,
        string placeholderText,
        int x,
        int y,
        int width,
        int height
    )
    {
        TMP_InputField input = InstantiatePrefabComponent<TMP_InputField>(
            _textInputPrefabPath,
            parent
        );
        input.gameObject.name = name;
        RectTransform rect = input.transform as RectTransform;
        SetSourceRect(rect, x, y, width, height);

        Image image = input.GetComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;

        TextMeshProUGUI text = input.textComponent as TextMeshProUGUI;
        if (text == null)
            throw new MissingReferenceException($"{name}/Text is missing.");
        text.text = string.Empty;
        text.color = Color.white;
        text.fontSize = 12;
        text.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(text.rectTransform, 2, 0, width - 2, height);

        TextMeshProUGUI placeholder = input.placeholder as TextMeshProUGUI;
        if (placeholder == null)
            throw new MissingReferenceException($"{name}/Placeholder is missing.");
        placeholder.text = placeholderText;
        placeholder.color = Color.white;
        placeholder.fontSize = 12;
        placeholder.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(placeholder.rectTransform, 2, 0, width - 2, height);

        input.enabled = true;
        input.targetGraphic = image;
        input.transition = Selectable.Transition.None;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.textViewport = rect;
        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    /// <summary>
    /// Instantiates and lays out a shared scroll area for an Options list.
    /// </summary>
    /// <param name="parent">The scroll area's parent.</param>
    /// <param name="name">The generated object name.</param>
    /// <param name="x">The source-space left coordinate.</param>
    /// <param name="y">The source-space top coordinate.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <param name="viewportX">The viewport left coordinate within the scroll area.</param>
    /// <param name="viewportY">The viewport top coordinate within the scroll area.</param>
    /// <param name="viewportWidth">The viewport width.</param>
    /// <param name="viewportHeight">The viewport height.</param>
    /// <param name="scrollbarX">The scrollbar left coordinate within the scroll area.</param>
    /// <param name="scrollbarY">The scrollbar top coordinate within the scroll area.</param>
    /// <param name="scrollbarWidth">The scrollbar width.</param>
    /// <param name="scrollbarHeight">The scrollbar height.</param>
    /// <param name="contentRoot">The generated content container.</param>
    /// <returns>The configured scroll-area view.</returns>
    private static ScrollAreaView CreateScrollAreaView(
        Transform parent,
        string name,
        int x,
        int y,
        int width,
        int height,
        int viewportX,
        int viewportY,
        int viewportWidth,
        int viewportHeight,
        int scrollbarX,
        int scrollbarY,
        int scrollbarWidth,
        int scrollbarHeight,
        out RectTransform contentRoot
    )
    {
        ScrollAreaView view = InstantiatePrefabComponent<ScrollAreaView>(
            _scrollAreaPrefabPath,
            parent
        );
        GameObject root = view.gameObject;
        root.name = name;
        root.SetActive(false);
        SetSourceRect(root.GetComponent<RectTransform>(), x, y, width, height);
        view.enabled = true;

        RectTransform scrollRoot = view.ScrollRoot;
        SetSourceRect(scrollRoot, viewportX, viewportY, viewportWidth, viewportHeight);
        ScrollRect scrollRect = scrollRoot.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = false;

        RectTransform viewportRect = view.ViewportRoot;
        SetSourceRect(viewportRect, 0, 0, viewportWidth, viewportHeight);
        Image viewportImage = viewportRect.GetComponent<Image>();
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;

        contentRoot = view.ContentRoot;
        SetSourceRect(contentRoot, 0, 0, viewportWidth, viewportHeight);
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRoot;

        Scrollbar scrollbar = view.GetComponentInChildren<Scrollbar>(true);
        if (scrollbar == null)
            throw new MissingReferenceException($"{name}/Scrollbar is missing.");
        scrollbar.handleRect = null;
        SetSourceRect(
            scrollbar.transform as RectTransform,
            scrollbarX,
            scrollbarY,
            scrollbarWidth,
            scrollbarHeight
        );

        Texture2D scrollUpTexture = LoadTexture(_scrollUpAddress);
        Texture2D scrollDownTexture = LoadTexture(_scrollDownAddress);
        int upArrowHeight = GetTextureHeight(scrollUpTexture, 9);
        int downArrowHeight = GetTextureHeight(scrollDownTexture, 9);
        int trackHeight = Mathf.Max(0, scrollbarHeight - upArrowHeight - downArrowHeight);

        Image scrollbarBackground = scrollbar.GetComponent<Image>();
        scrollbarBackground.color = Color.clear;
        scrollbarBackground.raycastTarget = true;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        Image trackBackground = FindRequiredChild<Image>(
            scrollbar.transform,
            "TrackBackgroundImage"
        );
        trackBackground.color = Color.black;
        SetSourceRect(trackBackground.rectTransform, 0, upArrowHeight, scrollbarWidth, trackHeight);

        RawImage scrollUpImage = FindRequiredChild<RawImage>(
            scrollbar.transform,
            "ScrollUpButtonImage"
        );
        scrollUpImage.texture = scrollUpTexture;
        AttachTextureBinding(scrollUpImage, _scrollUpAddress);
        SetSourceRect(scrollUpImage.rectTransform, 0, 0, scrollbarWidth, upArrowHeight);
        ConfigureScrollButton(scrollUpImage);

        RawImage scrollDownImage = FindRequiredChild<RawImage>(
            scrollbar.transform,
            "ScrollDownButtonImage"
        );
        scrollDownImage.texture = scrollDownTexture;
        AttachTextureBinding(scrollDownImage, _scrollDownAddress);
        SetSourceRect(
            scrollDownImage.rectTransform,
            0,
            scrollbarHeight - downArrowHeight,
            scrollbarWidth,
            downArrowHeight
        );
        ConfigureScrollButton(scrollDownImage);

        RectTransform slidingArea = FindRequiredChild<RectTransform>(
            scrollbar.transform,
            "SlidingArea"
        );
        SetSourceRect(slidingArea, 0, upArrowHeight, scrollbarWidth, trackHeight);
        RawImage handleImage = FindRequiredChild<RawImage>(slidingArea, "Handle");
        handleImage.texture = LoadTexture(_scrollHandleAddress);
        AttachTextureBinding(handleImage, _scrollHandleAddress);
        FillParent(handleImage.rectTransform);
        handleImage.raycastTarget = true;
        scrollbar.handleRect = handleImage.rectTransform;
        scrollbar.targetGraphic = handleImage;
        scrollbar.transition = Selectable.Transition.None;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        root.SetActive(true);
        return view;
    }

    /// <summary>
    /// Applies a top-left anchored source-pixel rectangle.
    /// </summary>
    /// <param name="rect">The rectangle to place.</param>
    /// <param name="x">The source-space left coordinate.</param>
    /// <param name="y">The source-space top coordinate.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    private static void SetSourceRect(RectTransform rect, int x, int y, int width, int height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Stretches a rectangle across its parent without offsets.
    /// </summary>
    /// <param name="rect">The rectangle to stretch.</param>
    private static void FillParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Assigns an object reference to a serialized component field.
    /// </summary>
    /// <param name="target">The component containing the serialized field.</param>
    /// <param name="propertyName">The serialized field name.</param>
    /// <param name="value">The object reference to assign.</param>
    private static void AssignReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedObject serializedObject = new SerializedObject(target);
        FindRequiredProperty(target, serializedObject, propertyName).objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Assigns object references to a serialized array field.
    /// </summary>
    /// <typeparam name="T">The referenced Unity object type.</typeparam>
    /// <param name="target">The component containing the serialized field.</param>
    /// <param name="propertyName">The serialized array field name.</param>
    /// <param name="values">The references to assign.</param>
    private static void AssignReferenceArray<T>(
        UnityEngine.Object target,
        string propertyName,
        IReadOnlyList<T> values
    )
        where T : UnityEngine.Object
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = FindRequiredProperty(target, serializedObject, propertyName);
        property.arraySize = values.Count;
        for (int index = 0; index < values.Count; index++)
            property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Assigns an integer to a serialized component field.
    /// </summary>
    /// <param name="target">The component containing the serialized field.</param>
    /// <param name="propertyName">The serialized field name.</param>
    /// <param name="value">The integer to assign.</param>
    private static void AssignInt(UnityEngine.Object target, string propertyName, int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        FindRequiredProperty(target, serializedObject, propertyName).intValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Assigns a string to a serialized component field.
    /// </summary>
    /// <param name="target">The component containing the serialized field.</param>
    /// <param name="propertyName">The serialized field name.</param>
    /// <param name="value">The string to assign.</param>
    private static void AssignString(UnityEngine.Object target, string propertyName, string value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        FindRequiredProperty(target, serializedObject, propertyName).stringValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Saves a generated hierarchy as a prefab and rejects an unsuccessful write.
    /// </summary>
    /// <param name="root">The generated hierarchy root.</param>
    /// <param name="path">The destination prefab path.</param>
    /// <returns>The saved prefab asset.</returns>
    private static GameObject SaveGeneratedPrefabAsset(GameObject root, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
        if (!success || saved == null)
            throw new InvalidOperationException($"Failed to save generated prefab at {path}.");
        return saved;
    }

    /// <summary>
    /// Creates an empty rectangular layer under a parent.
    /// </summary>
    /// <param name="name">The generated object name.</param>
    /// <param name="parent">The layer's parent.</param>
    /// <returns>The generated layer.</returns>
    private static GameObject CreateLayer(string name, Transform parent)
    {
        GameObject layer = new GameObject(name, typeof(RectTransform));
        layer.transform.SetParent(parent, false);
        return layer;
    }

    /// <summary>
    /// Instantiates a shared nested prefab and returns its required component.
    /// </summary>
    /// <typeparam name="T">The required root component type.</typeparam>
    /// <param name="path">The shared prefab path.</param>
    /// <param name="parent">The nested instance's parent.</param>
    /// <returns>The enabled component on the nested instance.</returns>
    private static T InstantiatePrefabComponent<T>(string path, Transform parent)
        where T : MonoBehaviour
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            throw new MissingReferenceException($"Prefab asset is missing at {path}.");
        T prefabComponent = prefab.GetComponent<T>();
        if (prefabComponent == null)
            throw new MissingReferenceException(
                $"Prefab asset at {path} is missing {typeof(T).Name}."
            );

        GameObject instance = (GameObject)
            PrefabUtility.InstantiatePrefab(prefabComponent.gameObject, parent);
        T component = instance.GetComponent<T>();
        if (component == null)
            throw new MissingReferenceException(
                $"Nested prefab instance from {path} is missing {typeof(T).Name}."
            );
        component.enabled = true;
        return component;
    }

    /// <summary>
    /// Finds a required component on a named direct child.
    /// </summary>
    /// <typeparam name="T">The required component type.</typeparam>
    /// <param name="parent">The hierarchy parent to search.</param>
    /// <param name="childName">The direct child name.</param>
    /// <returns>The required child component.</returns>
    private static T FindRequiredChild<T>(Transform parent, string childName)
        where T : Component
    {
        Transform child = parent.Find(childName);
        T component = child == null ? null : child.GetComponent<T>();
        if (component == null)
            throw new MissingReferenceException(
                $"{parent.name}/{childName} is missing {typeof(T).Name}."
            );
        return component;
    }

    /// <summary>
    /// Loads a texture from the active development content pack.
    /// </summary>
    /// <param name="path">The content texture address.</param>
    /// <returns>The loaded texture.</returns>
    private static Texture2D LoadTexture(string path)
    {
        return ContentPackEditor.Assets.GetTexture(path);
    }

    /// <summary>
    /// Loads a sprite with an explicit border from the active development content pack.
    /// </summary>
    /// <param name="address">The content sprite address.</param>
    /// <param name="border">The sprite's nine-slice border in pixels.</param>
    /// <returns>The loaded sprite.</returns>
    private static Sprite LoadSprite(string address, Vector4 border)
    {
        return ContentPackEditor.Assets.GetSprite(address, border);
    }

    /// <summary>
    /// Replaces a raw image's runtime content binding with the requested address.
    /// </summary>
    /// <param name="image">The image receiving the runtime binding.</param>
    /// <param name="texturePath">The content texture address.</param>
    private static void AttachTextureBinding(RawImage image, string texturePath)
    {
        ContentTextureBinding existing = image.GetComponent<ContentTextureBinding>();
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing);
        ContentTextureBinding binding = image.gameObject.AddComponent<ContentTextureBinding>();
        binding.SetAddress(ToContentAddress(texturePath));
    }

    /// <summary>
    /// Removes a texture extension to produce a runtime content address.
    /// </summary>
    /// <param name="texturePath">The authored texture path.</param>
    /// <returns>The extensionless runtime address.</returns>
    private static string ToContentAddress(string texturePath)
    {
        int separatorIndex = texturePath.LastIndexOf('/');
        int extensionIndex = texturePath.LastIndexOf('.');
        return extensionIndex > separatorIndex ? texturePath[..extensionIndex] : texturePath;
    }

    /// <summary>
    /// Reads a texture's source height or uses a fallback when unavailable.
    /// </summary>
    /// <param name="texture">The texture whose source height is needed.</param>
    /// <param name="fallback">The height used when metadata is unavailable.</param>
    /// <returns>The usable source height.</returns>
    private static int GetTextureHeight(Texture texture, int fallback)
    {
        int height = UILayout.GetTextureSourceHeight(texture);
        return height > 0 ? height : fallback;
    }

    /// <summary>
    /// Configures an authored scrollbar arrow as a raw-image button.
    /// </summary>
    /// <param name="image">The arrow image to configure.</param>
    private static void ConfigureScrollButton(RawImage image)
    {
        image.raycastTarget = true;
        Button button = image.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
    }

    /// <summary>
    /// Finds a serialized field or fails the UI build when the field is missing.
    /// </summary>
    /// <param name="target">The component expected to contain the field.</param>
    /// <param name="serializedObject">The serialized representation of the component.</param>
    /// <param name="propertyName">The serialized field name.</param>
    /// <returns>The required serialized field.</returns>
    private static SerializedProperty FindRequiredProperty(
        UnityEngine.Object target,
        SerializedObject serializedObject,
        string propertyName
    )
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new MissingMemberException(target.GetType().Name, propertyName);
        return property;
    }
}
