using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static MenuPrefabBuilder;

/// <summary>
/// Builds the Options menu prefab.
/// </summary>
public static class OptionsMenuPrefabBuilder
{
    private const int _navigationRowHeight = 28;
    private const int _navigationRowSpacing = 4;
    private const int _navigationRowStride = _navigationRowHeight + _navigationRowSpacing;
    private const int _footerNavigationHeight =
        3 * _navigationRowHeight + 2 * _navigationRowSpacing;
    private const int _optionsPageTopInset = 16;
    private const int _optionsPageBottomInset = 10;
    private const int _optionsContentInset = 20;
    private const int _graphicsFieldWidth = 150;
    private const int _optionsSectionHeaderHeight = 16;
    private const int _optionsSectionHeaderSpacing = 12;
    private const int _optionsSectionSpacing = 10;
    private const int _optionsFieldRowHeight = 18;
    private const int _optionsFieldRowSpacing = 8;
    private const int _optionsSectionHeaderStride =
        _optionsSectionHeaderHeight + _optionsSectionHeaderSpacing;
    private const int _optionsFieldRowStride = _optionsFieldRowHeight + _optionsFieldRowSpacing;
    private const int _tabNavigationStartY = 82;
    private const int _footerNavigationStartY = 318;
    private const string _optionsMenuWindowPrefabPath =
        "Assets/Prefabs/UI/OptionsMenu/OptionsMenu.prefab";
    private const string _optionsToggleOnAddress =
        "Application/OptionsMenu/UI/ui_settingsmenu_toggle_selected_icon";
    private const string _optionsToggleOffAddress =
        "Application/OptionsMenu/UI/ui_settingsmenu_toggle_icon";
    private const string _optionsKnobAddress =
        "Application/OptionsMenu/UI/ui_settingsmenu_slider_knob";
    private const string _contextMenuCheckMarkAddress =
        "Pack/Shared/Strategy/UI/ContextMenus/context-menu-check-mark.png";

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

        RectTransform contentRoot = CreateMenuWindowContent(
            window.transform,
            "OPTIONS",
            out TextMeshProUGUI header,
            out TextMeshProUGUI pageTitle
        );
        pageTitle.text = "GAMEPLAY";
        RawImage background = FindRequiredChild<RawImage>(contentRoot, "BackgroundImage");

        BuildTabNavigation(view, contentRoot, textColor);

        RectTransform gameplayPage = CreateChildLayer("GameplayPage", contentRoot);
        SetSourceRect(gameplayPage, 228, 69, 382, 365);
        RectTransform userInterfacePage = CreateChildLayer("UserInterfacePage", contentRoot);
        SetSourceRect(userInterfacePage, 228, 69, 382, 365);
        RectTransform graphicsPage = CreateChildLayer("GraphicsPage", contentRoot);
        SetSourceRect(graphicsPage, 228, 69, 382, 365);
        RectTransform audioPage = CreateChildLayer("AudioPage", contentRoot);
        SetSourceRect(audioPage, 228, 69, 382, 365);
        RectTransform saveLoadPage = CreateChildLayer("SaveLoadPage", contentRoot);
        SetSourceRect(saveLoadPage, 228, 69, 382, 365);
        RectTransform controlsPage = CreateChildLayer("ControlsPage", contentRoot);
        SetSourceRect(controlsPage, 228, 69, 382, 365);
        RectTransform modsPage = CreateChildLayer("ModsPage", contentRoot);
        SetSourceRect(modsPage, 228, 69, 382, 365);

        // Hidden Pages.
        userInterfacePage.gameObject.SetActive(false);
        graphicsPage.gameObject.SetActive(false);
        audioPage.gameObject.SetActive(false);
        saveLoadPage.gameObject.SetActive(false);
        controlsPage.gameObject.SetActive(false);
        modsPage.gameObject.SetActive(false);

        BuildGameplayPage(view, gameplayPage, accent);

        BuildUserInterfacePage(view, userInterfacePage, accent);

        BuildGraphicsPage(view, graphicsPage, accent);

        BuildAudioPage(view, audioPage, accent, textColor);

        BuildSaveLoadPage(view, saveLoadPage, accent, textColor, textDim);

        BuildControlsPage(view, controlsPage, accent, textColor, textDim);
        BuildModsPage(view, modsPage, accent, textDim);
        BuildFooter(view, contentRoot, textColor, textDim);

        AssignReference(view, "_backgroundImage", background);
        AssignReference(view, "_headerTextField", header);
        AssignReference(view, "_pageTitleTextField", pageTitle);
        AssignReference(view, "_rowIdleSprite", LoadSprite(_menuRowAddress, _panelBorder));
        AssignReference(view, "_rowActiveSprite", LoadSprite(_menuRowActiveAddress, _panelBorder));
        AssignString(view, "_rowIdleSpriteAddress", _menuRowAddress);
        AssignString(view, "_rowActiveSpriteAddress", _menuRowActiveAddress);
        AssignReference(view, "_gameplayPage", gameplayPage.gameObject);
        AssignReference(view, "_userInterfacePage", userInterfacePage.gameObject);
        AssignReference(view, "_graphicsPage", graphicsPage.gameObject);
        AssignReference(view, "_audioPage", audioPage.gameObject);
        AssignReference(view, "_saveLoadPage", saveLoadPage.gameObject);
        AssignReference(view, "_controlsPage", controlsPage.gameObject);
        AssignReference(view, "_modsPage", modsPage.gameObject);

        GameObject saved = SaveGeneratedPrefabAsset(window, _optionsMenuWindowPrefabPath);
        UnityEngine.Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<OptionsMenuView>();
    }

    /// <summary>
    /// Builds and wires the Options page-selection buttons.
    /// </summary>
    /// <param name="view">The Options view receiving the authored references.</param>
    /// <param name="contentRoot">The Options content root.</param>
    /// <param name="textColor">The primary Options text color.</param>
    private static void BuildTabNavigation(
        OptionsMenuView view,
        RectTransform contentRoot,
        Color textColor
    )
    {
        string[] tabNames =
        {
            "GAMEPLAY",
            "GRAPHICS",
            "UI",
            "AUDIO",
            "CONTROLS",
            "MODS",
            "SAVE / LOAD",
        };
        string[] tabObjectNames =
        {
            "GameplayTab",
            "GraphicsTab",
            "UserInterfaceTab",
            "AudioTab",
            "ControlsTab",
            "ModsTab",
            "SaveLoadTab",
        };
        Button[] tabButtons = new Button[tabNames.Length];
        TextMeshProUGUI[] tabLabels = new TextMeshProUGUI[tabNames.Length];
        Image[] tabSurfaces = new Image[tabNames.Length];
        for (int i = 0; i < tabNames.Length; i++)
        {
            Button tabButton = CreateMenuNavigationRow(
                tabObjectNames[i],
                contentRoot,
                tabNames[i],
                38,
                _tabNavigationStartY + i * _navigationRowStride,
                textColor,
                out TextMeshProUGUI tabLabel,
                out Image tabSurface
            );
            tabButtons[i] = tabButton;
            tabLabels[i] = tabLabel;
            tabSurfaces[i] = tabSurface;
        }

        AssignReferenceArray(view, "_tabButtons", tabButtons);
        AssignReferenceArray(view, "_tabLabelFields", tabLabels);
        AssignReferenceArray(view, "_tabSurfaceImages", tabSurfaces);
    }

    /// <summary>
    /// Builds the mod enablement controls.
    /// </summary>
    /// <param name="view">The Options view receiving the authored references.</param>
    /// <param name="modsPage">The authored Mods page root.</param>
    /// <param name="accent">The Options accent color.</param>
    /// <param name="textDim">The secondary Options text color.</param>
    private static void BuildModsPage(
        OptionsMenuView view,
        RectTransform modsPage,
        Color accent,
        Color textDim
    )
    {
        CreateOptionsSectionHeader(modsPage, "ContentPackHeader", "CONTENT PACK", 16, accent);
        TextMeshProUGUI packValue = CreateOptionsFieldRow(
            modsPage,
            "ContentPack",
            string.Empty,
            41,
            out Button packPrev,
            out Button packNext,
            fieldX: 20,
            fieldWidth: 329,
            textInset: 28
        );
        packValue.overflowMode = TextOverflowModes.Ellipsis;
        CreateOptionsSectionHeader(modsPage, "LoadedModsHeader", "MODS", 74, accent);

        TextMeshProUGUI status = CreateTextLabel("ModsStatusTextField", modsPage);
        status.text = "NO MODS LOADED";
        status.color = textDim;
        status.fontSize = 11;
        status.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(status.rectTransform, 20, 101, 348, 18);

        ScrollAreaView modsScrollArea = CreateScrollAreaView(
            modsPage,
            "ModsScrollArea",
            20,
            126,
            348,
            176,
            0,
            0,
            330,
            176,
            334,
            0,
            12,
            176,
            out RectTransform modsContent
        );
        OptionsToggleRowView rowTemplate = CreateOptionsToggleRow(
            modsContent,
            "ModRowTemplate",
            0,
            "Mod",
            0,
            0
        );
        rowTemplate.gameObject.SetActive(false);

        AssignReference(view, "_modsStatusTextField", status);
        AssignReference(view, "_contentPackValueField", packValue);
        AssignReference(view, "_contentPackPrevButton", packPrev);
        AssignReference(view, "_contentPackNextButton", packNext);
        AssignReference(view, "_modsScrollArea", modsScrollArea);
        AssignReference(view, "_modRowTemplate", rowTemplate);
    }

    /// <summary>
    /// Builds and wires the Gameplay page controls.
    /// </summary>
    /// <param name="view">The view.</param>
    /// <param name="gameplayPage">The gameplay page.</param>
    /// <param name="accent">The accent.</param>
    private static void BuildGameplayPage(
        OptionsMenuView view,
        RectTransform gameplayPage,
        Color accent
    )
    {
        CreateOptionsSectionHeader(gameplayPage, "SavingHeader", "SAVING", 16, accent);
        OptionsToggleRowView autosaveRow = CreateOptionsToggleRow(
            gameplayPage,
            "GameplayAutosaveEnabled",
            (int)UserGameplayOption.AutosaveEnabled,
            "Enable Autosave",
            20,
            44
        );
        TMP_InputField autosaveIntervalInput = CreateOptionsNumericFieldRow(
            gameplayPage,
            "AutosaveInterval",
            "Autosave Interval (Ticks)",
            70,
            out Image autosaveIntervalBadge
        );
        TMP_InputField autosavesToKeepInput = CreateOptionsNumericFieldRow(
            gameplayPage,
            "AutosavesToKeep",
            "Autosaves to Keep",
            97,
            out Image autosavesToKeepBadge
        );

        CreateOptionsSectionHeader(gameplayPage, "GalaxyViewHeader", "GALAXY VIEW", 134, accent);
        TMP_Dropdown gameLaunchSpeedDropdown = CreateOptionsDropdownRow(
            gameplayPage,
            "GameLaunchSpeed",
            "Galaxy speed override",
            new[] { "None", "Paused", "Very slow", "Slow", "Medium", "Fast" },
            162,
            accent
        );
        OptionsToggleRowView disableBriefingsRow = CreateOptionsToggleRow(
            gameplayPage,
            "GameplayDisableBriefings",
            (int)UserGameplayOption.DisableBriefings,
            "Disable Briefings",
            20,
            188
        );
        UserGameplayOption[] options =
        {
            UserGameplayOption.PauseAfterEnemyBombardment,
            UserGameplayOption.PauseWhenSpaceBattleBegins,
        };
        string[] labels = { "Pause After Enemy Bombardment", "Pause on Space Battles" };
        OptionsToggleRowView[] rows = new OptionsToggleRowView[options.Length + 2];
        rows[0] = disableBriefingsRow;
        rows[1] = autosaveRow;
        for (int i = 0; i < options.Length; i++)
        {
            rows[i + 2] = CreateOptionsToggleRow(
                gameplayPage,
                $"Gameplay{options[i]}",
                (int)options[i],
                labels[i],
                20,
                214 + i * 26
            );
        }

        AssignReferenceArray(view, "_gameplayRows", rows);
        AssignReference(view, "_gameLaunchSpeedDropdown", gameLaunchSpeedDropdown);
        AssignReference(view, "_autosaveIntervalInputField", autosaveIntervalInput);
        AssignReference(view, "_autosaveIntervalBadgeImage", autosaveIntervalBadge);
        AssignReference(view, "_autosavesToKeepInputField", autosavesToKeepInput);
        AssignReference(view, "_autosavesToKeepBadgeImage", autosavesToKeepBadge);
    }

    /// <summary>
    /// Builds and wires the Graphics page controls.
    /// </summary>
    /// <param name="view">The Options view receiving the authored references.</param>
    /// <param name="graphicsPage">The authored Graphics page root.</param>
    /// <param name="accent">The Options accent color.</param>
    private static void BuildGraphicsPage(
        OptionsMenuView view,
        RectTransform graphicsPage,
        Color accent
    )
    {
        ScrollAreaView graphicsScrollArea = CreateScrollAreaView(
            graphicsPage,
            "GraphicsScrollArea",
            0,
            0,
            362,
            328,
            0,
            0,
            344,
            328,
            350,
            _optionsPageTopInset,
            12,
            328 - _optionsPageTopInset,
            out RectTransform graphicsContent
        );
        int nextRowY = _optionsPageTopInset;
        AppendOptionsSectionHeader(
            graphicsContent,
            "DisplayHeader",
            "DISPLAY",
            accent,
            ref nextRowY
        );
        TextMeshProUGUI resolutionValue = AppendOptionsFieldRow(
            graphicsContent,
            "Resolution",
            "Resolution",
            ref nextRowY,
            out Button resolutionPrev,
            out Button resolutionNext,
            _graphicsFieldWidth
        );
        TextMeshProUGUI fullScreenValue = AppendOptionsFieldRow(
            graphicsContent,
            "Display",
            "Display Mode",
            ref nextRowY,
            out Button fullScreenPrev,
            out Button fullScreenNext,
            _graphicsFieldWidth
        );

        int graphicsSettingCount = Enum.GetValues(typeof(UserGraphicsSetting)).Length;
        TextMeshProUGUI[] valueFields = new TextMeshProUGUI[graphicsSettingCount];
        Button[] previousButtons = new Button[graphicsSettingCount];
        Button[] nextButtons = new Button[graphicsSettingCount];

        AppendGraphicsFieldRow(
            graphicsContent,
            UserGraphicsSetting.VerticalSync,
            "VSync",
            ref nextRowY,
            valueFields,
            previousButtons,
            nextButtons
        );
        AppendGraphicsFieldRow(
            graphicsContent,
            UserGraphicsSetting.FrameRateLimit,
            "Frame Rate Limit",
            ref nextRowY,
            valueFields,
            previousButtons,
            nextButtons
        );
        AppendGraphicsFieldRow(
            graphicsContent,
            UserGraphicsSetting.HdrOutput,
            "HDR Output",
            ref nextRowY,
            valueFields,
            previousButtons,
            nextButtons
        );

        AppendOptionsSectionHeader(
            graphicsContent,
            "RenderingHeader",
            "RENDERING",
            accent,
            ref nextRowY
        );
        AppendGraphicsFieldRow(
            graphicsContent,
            UserGraphicsSetting.QualityPreset,
            "Quality Preset",
            ref nextRowY,
            valueFields,
            previousButtons,
            nextButtons
        );
        AppendGraphicsFieldRow(
            graphicsContent,
            UserGraphicsSetting.AntiAliasing,
            "Anti-Aliasing",
            ref nextRowY,
            valueFields,
            previousButtons,
            nextButtons
        );
        AppendOptionsSectionHeader(
            graphicsContent,
            "RayTracingHeader",
            "RAY TRACING",
            accent,
            ref nextRowY
        );
        AppendGraphicsFieldRow(
            graphicsContent,
            UserGraphicsSetting.RayTracing,
            "Ray-Traced AO (RTX)",
            ref nextRowY,
            valueFields,
            previousButtons,
            nextButtons
        );

        AppendOptionsSectionHeader(
            graphicsContent,
            "EffectsHeader",
            "EFFECTS",
            accent,
            ref nextRowY
        );
        AppendGraphicsFieldRow(
            graphicsContent,
            UserGraphicsSetting.AmbientOcclusion,
            "Ambient Occlusion",
            ref nextRowY,
            valueFields,
            previousButtons,
            nextButtons
        );
        AppendGraphicsFieldRow(
            graphicsContent,
            UserGraphicsSetting.Bloom,
            "Bloom",
            ref nextRowY,
            valueFields,
            previousButtons,
            nextButtons
        );
        AppendGraphicsFieldRow(
            graphicsContent,
            UserGraphicsSetting.MotionBlur,
            "Motion Blur",
            ref nextRowY,
            valueFields,
            previousButtons,
            nextButtons
        );

        AssignReferenceArray(view, "_tacticalRows", Array.Empty<OptionsToggleRowView>());
        AssignReference(view, "_graphicsScrollArea", graphicsScrollArea);
        AssignInt(
            view,
            "_graphicsContentHeight",
            nextRowY - _optionsFieldRowSpacing + _optionsPageBottomInset
        );
        AssignInt(view, "_graphicsScrollStep", _optionsFieldRowStride);
        AssignReference(view, "_resolutionValueField", resolutionValue);
        AssignReference(view, "_resolutionPrevButton", resolutionPrev);
        AssignReference(view, "_resolutionNextButton", resolutionNext);
        AssignReference(view, "_fullScreenValueField", fullScreenValue);
        AssignReference(view, "_fullScreenPrevButton", fullScreenPrev);
        AssignReference(view, "_fullScreenNextButton", fullScreenNext);
        AssignReferenceArray(view, "_graphicsValueFields", valueFields);
        AssignReferenceArray(view, "_graphicsPrevButtons", previousButtons);
        AssignReferenceArray(view, "_graphicsNextButtons", nextButtons);
    }

    /// <summary>
    /// Builds and wires the User Interface page's Galaxy View controls.
    /// </summary>
    /// <param name="view">The Options view receiving the authored references.</param>
    /// <param name="userInterfacePage">The authored User Interface page root.</param>
    /// <param name="accent">The Options accent color.</param>
    private static void BuildUserInterfacePage(
        OptionsMenuView view,
        RectTransform userInterfacePage,
        Color accent
    )
    {
        CreateOptionsSectionHeader(
            userInterfacePage,
            "GalaxyViewHeader",
            "GALAXY VIEW",
            16,
            accent
        );
        TMP_Dropdown idleBarLocationDropdown = CreateOptionsDropdownRow(
            userInterfacePage,
            "IdleBarLocation",
            "Idle Bar Location",
            new[] { "Top Right", "Top Left", "Bottom Right", "Bottom Left" },
            44,
            accent
        );
        UserInterfaceOption[] options =
        {
            UserInterfaceOption.ShowIdleBar,
            UserInterfaceOption.KeepIdleBarOpen,
            UserInterfaceOption.KeepIdleBarAboveWindows,
            UserInterfaceOption.ShowUnitCardStats,
            UserInterfaceOption.ShowFrameRate,
        };
        string[] labels =
        {
            "Show Idle Bar",
            "Keep Idle Bar Open",
            "Keep Idle Bar Above Windows",
            "Show Unit Card Stats",
            "Show FPS Counter",
        };
        int[] yPositions = { 70, 96, 122, 148, 206 };
        CreateOptionsSectionHeader(userInterfacePage, "OverlayHeader", "OVERLAY", 184, accent);
        OptionsToggleRowView[] rows = new OptionsToggleRowView[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            rows[i] = CreateOptionsToggleRow(
                userInterfacePage,
                $"UserInterface{options[i]}",
                (int)options[i],
                labels[i],
                20,
                yPositions[i]
            );
        }

        AssignReference(view, "_idleBarLocationDropdown", idleBarLocationDropdown);
        AssignReferenceArray(view, "_userInterfaceRows", rows);
    }

    /// <summary>
    /// Creates a themed Options dropdown row with a complete popup template.
    /// </summary>
    /// <param name="parent">The page receiving the row.</param>
    /// <param name="name">The generated control name.</param>
    /// <param name="label">The row label.</param>
    /// <param name="options">The dropdown options in presentation order.</param>
    /// <param name="y">The source-space row coordinate.</param>
    /// <param name="accent">The Options accent color.</param>
    /// <returns>The generated dropdown.</returns>
    private static TMP_Dropdown CreateOptionsDropdownRow(
        Transform parent,
        string name,
        string label,
        IReadOnlyList<string> options,
        int y,
        Color accent
    )
    {
        const int controlWidth = 120;
        const int controlRight = 349;
        const int controlHeight = 18;
        const int itemHeight = 18;

        TextMeshProUGUI labelField = CreateTextLabel($"{name}Label", parent);
        labelField.text = label;
        labelField.color = new Color(0.875f, 0.910f, 0.941f);
        labelField.fontSize = 11;
        labelField.alignment = TextAlignmentOptions.MidlineLeft;
        SetSourceRect(labelField.rectTransform, 20, y, 165, controlHeight);

        Image surface = CreateSlicedImage(
            $"{name}Dropdown",
            parent,
            _menuBadgeAddress,
            _badgeBorder,
            controlRight - controlWidth,
            y,
            controlWidth,
            controlHeight,
            Color.white
        );
        surface.raycastTarget = true;
        TMP_Dropdown dropdown = surface.gameObject.AddComponent<TMP_Dropdown>();
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
        return dropdown;
    }

    /// <summary>
    /// Builds and wires the Audio page controls.
    /// </summary>
    /// <param name="view">The Options view receiving the authored references.</param>
    /// <param name="audioPage">The authored Audio page root.</param>
    /// <param name="accent">The Options accent color.</param>
    /// <param name="textColor">The primary Options text color.</param>
    private static void BuildAudioPage(
        OptionsMenuView view,
        RectTransform audioPage,
        Color accent,
        Color textColor
    )
    {
        CreateOptionsSectionHeader(audioPage, "VolumeHeader", "VOLUME", 16, accent);
        string[] volumeLabels = { "Master", "Music", "Sound Effects", "Ambience", "Video" };
        NormalizedSliderView[] volumeSliders = new NormalizedSliderView[volumeLabels.Length];
        TextMeshProUGUI[] volumeValues = new TextMeshProUGUI[volumeLabels.Length];
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

        AssignReferenceArray(view, "_volumeSliders", volumeSliders);
        AssignReferenceArray(view, "_volumeValueFields", volumeValues);
    }

    /// <summary>
    /// Builds and wires the Save/Load page controls.
    /// </summary>
    /// <param name="view">The Options view receiving the authored references.</param>
    /// <param name="saveLoadPage">The authored Save/Load page root.</param>
    /// <param name="accent">The Options accent color.</param>
    /// <param name="textColor">The primary Options text color.</param>
    /// <param name="textDim">The secondary Options text color.</param>
    private static void BuildSaveLoadPage(
        OptionsMenuView view,
        RectTransform saveLoadPage,
        Color accent,
        Color textColor,
        Color textDim
    )
    {
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

        AssignReference(view, "_saveListView", saveListView);
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
    }

    /// <summary>
    /// Builds and wires the Controls page controls.
    /// </summary>
    /// <param name="view">The Options view receiving the authored references.</param>
    /// <param name="controlsPage">The authored Controls page root.</param>
    /// <param name="accent">The Options accent color.</param>
    /// <param name="textColor">The primary Options text color.</param>
    /// <param name="textDim">The secondary Options text color.</param>
    private static void BuildControlsPage(
        OptionsMenuView view,
        RectTransform controlsPage,
        Color accent,
        Color textColor,
        Color textDim
    )
    {
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
        SetSourceRect(primaryColumnHeader.rectTransform, 208, 2, 57, 12);
        TextMeshProUGUI secondaryColumnHeader = CreateTextLabel(
            "SecondaryColumnHeader",
            controlsPage
        );
        secondaryColumnHeader.text = "SECONDARY";
        secondaryColumnHeader.color = textDim;
        secondaryColumnHeader.fontSize = 9;
        secondaryColumnHeader.alignment = TextAlignmentOptions.Midline;
        SetSourceRect(secondaryColumnHeader.rectTransform, 269, 2, 57, 12);

        Image bindingRowTemplate = CreateSlicedImage(
            "BindingRowTemplate",
            controlsContent,
            _menuRowAddress,
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
        SetSourceRect(bindingActionTemplate.rectTransform, 11, 4, 175, 15);
        bindingActionTemplate.gameObject.SetActive(false);
        Image bindingKeyBadgeTemplate = CreateSlicedImage(
            "BindingKeyBadgeTemplate",
            controlsContent,
            _menuBadgeAddress,
            _badgeBorder,
            251,
            3,
            57,
            16,
            Color.white
        );
        bindingKeyBadgeTemplate.raycastTarget = true;
        Button bindingKeyBadgeButton = bindingKeyBadgeTemplate.gameObject.AddComponent<Button>();
        bindingKeyBadgeButton.targetGraphic = bindingKeyBadgeTemplate;
        ApplyMenuFillButtonFeedback(bindingKeyBadgeButton);
        bindingKeyBadgeTemplate.gameObject.SetActive(false);
        TextMeshProUGUI bindingKeyTemplate = CreateTextLabel("BindingKeyTemplate", controlsContent);
        bindingKeyTemplate.text = "Key";
        bindingKeyTemplate.color = textColor;
        bindingKeyTemplate.fontSize = 10;
        bindingKeyTemplate.textWrappingMode = TextWrappingModes.NoWrap;
        bindingKeyTemplate.overflowMode = TextOverflowModes.Truncate;
        bindingKeyTemplate.alignment = TextAlignmentOptions.Midline;
        SetSourceRect(bindingKeyTemplate.rectTransform, 251, 3, 57, 16);
        bindingKeyTemplate.gameObject.SetActive(false);
        Button bindingRestoreButton = CreateMenuRestoreButton(
            "BindingRestoreTemplate",
            controlsContent,
            312,
            3
        );
        Image bindingRestoreTemplate = bindingRestoreButton.GetComponent<Image>();
        bindingRestoreTemplate.gameObject.SetActive(false);

        AssignReference(view, "_controlsScrollArea", controlsScrollArea);
        AssignReference(view, "_bindingRowTemplate", bindingRowTemplate);
        AssignReference(view, "_bindingHeaderTemplate", bindingHeaderTemplate);
        AssignReference(view, "_bindingActionTemplate", bindingActionTemplate);
        AssignReference(view, "_bindingKeyBadgeTemplate", bindingKeyBadgeTemplate);
        AssignReference(view, "_bindingKeyTemplate", bindingKeyTemplate);
        AssignReference(view, "_bindingRestoreTemplate", bindingRestoreTemplate);
    }

    /// <summary>
    /// Builds and wires the persistent Options navigation and confirmation controls.
    /// </summary>
    /// <param name="view">The Options view receiving the authored references.</param>
    /// <param name="contentRoot">The Options content root.</param>
    /// <param name="textColor">The primary Options text color.</param>
    /// <param name="textDim">The secondary Options text color.</param>
    private static void BuildFooter(
        OptionsMenuView view,
        RectTransform contentRoot,
        Color textColor,
        Color textDim
    )
    {
        RectTransform footerNavigation = CreateChildLayer("FooterNavigation", contentRoot);
        SetSourceRect(footerNavigation, 38, _footerNavigationStartY, 163, _footerNavigationHeight);
        Button backToGameButton = CreateMenuNavigationRow(
            "BackToGame",
            footerNavigation,
            "RESUME GAME",
            0,
            0,
            textDim,
            out _,
            out _
        );
        Button mainMenuButton = CreateMenuNavigationRow(
            "MainMenu",
            footerNavigation,
            "MAIN MENU",
            0,
            _navigationRowStride,
            textDim,
            out _,
            out _
        );
        Button quitButton = CreateMenuNavigationRow(
            "Quit",
            footerNavigation,
            "QUIT",
            0,
            2 * _navigationRowStride,
            textDim,
            out _,
            out _
        );
        RectTransform settingsActions = CreateChildLayer("SettingsActions", contentRoot);
        Button defaultsButton = CreateMenuActionButton(
            settingsActions,
            "DefaultsButton",
            "APPLY DEFAULTS",
            300,
            textColor,
            114
        );
        Button applyButton = CreateMenuActionButton(
            settingsActions,
            "ApplyButton",
            "APPLY",
            424,
            textColor
        );
        ConfirmationDialogView confirmDialog = CommonUIPrefabBuilder.InstantiateConfirmationDialog(
            contentRoot
        );
        confirmDialog.gameObject.name = "ConfirmDialog";
        confirmDialog.transform.SetAsLastSibling();
        confirmDialog.gameObject.SetActive(false);
        ConfigureOptionsNavigationLayout(
            footerNavigation,
            backToGameButton,
            mainMenuButton,
            quitButton
        );

        AssignReference(view, "_backToGameButton", backToGameButton);
        AssignReference(view, "_mainMenuButton", mainMenuButton);
        AssignReference(view, "_quitButton", quitButton);
        AssignReference(view, "_settingsActions", settingsActions.gameObject);
        AssignReference(view, "_applyButton", applyButton);
        AssignReference(view, "_defaultsButton", defaultsButton);
        AssignReference(view, "_confirmDialog", confirmDialog);
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
    /// Adds visible pointer feedback to a dropdown's light-colored surface.
    /// </summary>
    /// <param name="dropdown">The dropdown to restyle.</param>
    /// <param name="surface">The visible surface receiving the tint.</param>
    private static void ApplyOptionsDropdownFeedback(TMP_Dropdown dropdown, Graphic surface)
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
    /// Positions text inside an input field.
    /// </summary>
    /// <param name="child">The text or placeholder rect transform.</param>
    private static void StretchFillInputChild(RectTransform child)
    {
        child.anchorMin = new Vector2(0f, 0f);
        child.anchorMax = new Vector2(1f, 1f);
        child.offsetMin = new Vector2(6f, -2f);
        child.offsetMax = new Vector2(-6f, -2f);
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
    /// Appends a section header using the shared Options-page vertical rhythm.
    /// </summary>
    /// <param name="parent">The page container.</param>
    /// <param name="name">The header object name.</param>
    /// <param name="caption">The header caption.</param>
    /// <param name="accent">The accent color.</param>
    /// <param name="nextRowY">The shared page-layout cursor.</param>
    /// <returns>The header text field.</returns>
    private static TextMeshProUGUI AppendOptionsSectionHeader(
        RectTransform parent,
        string name,
        string caption,
        Color accent,
        ref int nextRowY
    )
    {
        if (nextRowY != _optionsPageTopInset)
            nextRowY += _optionsSectionSpacing;

        TextMeshProUGUI header = CreateOptionsSectionHeader(
            parent,
            name,
            caption,
            nextRowY,
            accent
        );
        nextRowY += _optionsSectionHeaderStride;
        return header;
    }

    /// <summary>
    /// Creates the navigation layout.
    /// </summary>
    /// <param name="navigationRoot">The navigation root.</param>
    /// <param name="backToGameButton">The back to game button.</param>
    /// <param name="mainMenuButton">The main menu button.</param>
    /// <param name="quitButton">The quit button.</param>
    private static void ConfigureOptionsNavigationLayout(
        RectTransform navigationRoot,
        Button backToGameButton,
        Button mainMenuButton,
        Button quitButton
    )
    {
        VerticalLayoutGroup layout = navigationRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = _navigationRowSpacing;
        layout.childAlignment = TextAnchor.LowerLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        for (int index = 0; index < navigationRoot.childCount; index++)
        {
            Transform child = navigationRoot.GetChild(index);
            bool navigationChild =
                child == backToGameButton.transform
                || child == mainMenuButton.transform
                || child == quitButton.transform;
            LayoutElement element = child.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = !navigationChild;
            if (navigationChild)
            {
                element.preferredWidth = 163f;
                element.preferredHeight = _navigationRowHeight;
            }
        }
    }

    /// <summary>
    /// Creates a display setting row.
    /// </summary>
    /// <param name="parent">The Options-page container.</param>
    /// <param name="name">The row object-name prefix.</param>
    /// <param name="caption">The row caption.</param>
    /// <param name="y">The source-space row top.</param>
    /// <param name="prevButton">Receives the previous-step button.</param>
    /// <param name="nextButton">Receives the next-step button.</param>
    /// <param name="fieldX">The selector's source-space left edge.</param>
    /// <param name="fieldWidth">The selector's source-space width, including arrow buttons.</param>
    /// <param name="textInset">The text's inset from each selector edge.</param>
    /// <returns>The value text field.</returns>
    private static TextMeshProUGUI CreateOptionsFieldRow(
        RectTransform parent,
        string name,
        string caption,
        int y,
        out Button prevButton,
        out Button nextButton,
        int fieldX = 194,
        int fieldWidth = 155,
        int textInset = 24
    )
    {
        TextMeshProUGUI labelField = CreateTextLabel($"{name}Label", parent);
        labelField.text = caption;
        labelField.color = new Color(0.875f, 0.910f, 0.941f);
        labelField.fontSize = 11;
        labelField.alignment = TextAlignmentOptions.MidlineLeft;
        SetSourceRect(labelField.rectTransform, _optionsContentInset, y + 2, 150, 14);

        CreateSlicedImage(
            $"{name}Badge",
            parent,
            _menuBadgeAddress,
            _badgeBorder,
            fieldX,
            y,
            fieldWidth,
            _optionsFieldRowHeight,
            Color.white
        );
        int previousWidth = (fieldWidth + 1) / 2;
        int nextWidth = fieldWidth - previousWidth;
        prevButton = CreateOptionsStepperButton(
            parent,
            $"{name}Prev",
            "<",
            fieldX,
            y,
            previousWidth,
            false
        );
        TextMeshProUGUI valueField = CreateTextLabel($"{name}Value", parent);
        valueField.text = "-";
        valueField.color = new Color(0.875f, 0.910f, 0.941f);
        valueField.fontSize = 10;
        valueField.alignment = TextAlignmentOptions.Midline;
        valueField.overflowMode = TextOverflowModes.Ellipsis;
        valueField.raycastTarget = false;
        SetSourceRect(
            valueField.rectTransform,
            fieldX + textInset,
            y,
            fieldWidth - textInset * 2,
            _optionsFieldRowHeight
        );
        nextButton = CreateOptionsStepperButton(
            parent,
            $"{name}Next",
            ">",
            fieldX + previousWidth,
            y,
            nextWidth,
            true
        );
        OverflowTextTooltip tooltip = valueField.gameObject.AddComponent<OverflowTextTooltip>();
        tooltip.Configure(valueField);
        prevButton.gameObject.AddComponent<OverflowTextTooltipTrigger>().Configure(tooltip);
        nextButton.gameObject.AddComponent<OverflowTextTooltipTrigger>().Configure(tooltip);
        return valueField;
    }

    /// <summary>
    /// Appends a stepped field using the shared Options-page vertical rhythm.
    /// </summary>
    /// <param name="parent">The page container.</param>
    /// <param name="name">The row object-name prefix.</param>
    /// <param name="caption">The row caption.</param>
    /// <param name="nextRowY">The shared page-layout cursor.</param>
    /// <param name="prevButton">Receives the previous-step button.</param>
    /// <param name="nextButton">Receives the next-step button.</param>
    /// <param name="fieldWidth">The selector width within the content viewport.</param>
    /// <returns>The value text field.</returns>
    private static TextMeshProUGUI AppendOptionsFieldRow(
        RectTransform parent,
        string name,
        string caption,
        ref int nextRowY,
        out Button prevButton,
        out Button nextButton,
        int fieldWidth = 155
    )
    {
        TextMeshProUGUI value = CreateOptionsFieldRow(
            parent,
            name,
            caption,
            nextRowY,
            out prevButton,
            out nextButton,
            fieldWidth: fieldWidth
        );
        nextRowY += _optionsFieldRowStride;
        return value;
    }

    /// <summary>
    /// Appends and indexes one Graphics setting using the shared stepped-field component.
    /// </summary>
    /// <param name="parent">The Graphics-page content container.</param>
    /// <param name="setting">The setting represented by the row.</param>
    /// <param name="caption">The displayed row caption.</param>
    /// <param name="nextRowY">The shared page-layout cursor.</param>
    /// <param name="valueFields">The setting-indexed value fields.</param>
    /// <param name="previousButtons">The setting-indexed previous-step buttons.</param>
    /// <param name="nextButtons">The setting-indexed next-step buttons.</param>
    private static void AppendGraphicsFieldRow(
        RectTransform parent,
        UserGraphicsSetting setting,
        string caption,
        ref int nextRowY,
        TextMeshProUGUI[] valueFields,
        Button[] previousButtons,
        Button[] nextButtons
    )
    {
        int index = (int)setting;
        valueFields[index] = AppendOptionsFieldRow(
            parent,
            setting.ToString(),
            caption,
            ref nextRowY,
            out previousButtons[index],
            out nextButtons[index],
            _graphicsFieldWidth
        );
    }

    /// <summary>
    /// Creates a directly editable integer setting row.
    /// </summary>
    /// <param name="parent">The Options-page container.</param>
    /// <param name="name">The row object-name prefix.</param>
    /// <param name="caption">The row caption.</param>
    /// <param name="y">The source-space row top.</param>
    /// <param name="badgeImage">Receives the numeric-field background.</param>
    /// <returns>The configured integer input field.</returns>
    private static TMP_InputField CreateOptionsNumericFieldRow(
        RectTransform parent,
        string name,
        string caption,
        int y,
        out Image badgeImage
    )
    {
        const int fieldX = 293;
        const int fieldWidth = 56;
        const int inputInset = 4;
        TextMeshProUGUI labelField = CreateTextLabel($"{name}Label", parent);
        labelField.text = caption;
        labelField.color = new Color(0.875f, 0.910f, 0.941f);
        labelField.fontSize = 11;
        labelField.alignment = TextAlignmentOptions.MidlineLeft;
        SetSourceRect(labelField.rectTransform, 20, y + 2, 150, 14);

        badgeImage = CreateSlicedImage(
            $"{name}Badge",
            parent,
            _menuBadgeAddress,
            _badgeBorder,
            fieldX,
            y,
            fieldWidth,
            18,
            Color.white
        );
        TMP_InputField input = CreateTextInputField(
            $"{name}Input",
            parent,
            string.Empty,
            fieldX + inputInset,
            y,
            fieldWidth - inputInset * 2,
            18
        );
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.characterValidation = TMP_InputField.CharacterValidation.Integer;
        input.characterLimit = 10;
        UILayout.ConfigureCenteredNumericInput(input, new Color(0.875f, 0.910f, 0.941f), 10f, 0, 1);
        return input;
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
    /// <param name="alignRight">Whether the glyph follows the hotspot's right edge.</param>
    /// <returns>The configured button.</returns>
    private static Button CreateOptionsStepperButton(
        Transform parent,
        string name,
        string glyph,
        int x,
        int y,
        int hitWidth,
        bool alignRight
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
        SetSourceRect(
            hotspot.GetComponent<RectTransform>(),
            x,
            y,
            hitWidth,
            _optionsFieldRowHeight
        );
        Button button = hotspot.AddComponent<Button>();
        button.targetGraphic = hitArea;
        button.transition = Selectable.Transition.None;
        TextMeshProUGUI glyphText = CreateTextLabel("Glyph", hotspot.transform);
        glyphText.text = glyph;
        glyphText.color = new Color(0.373f, 0.659f, 0.925f);
        glyphText.fontSize = 10;
        glyphText.alignment = TextAlignmentOptions.Midline;
        SetSourceRect(glyphText.rectTransform, 0, 0, 26, _optionsFieldRowHeight);
        glyphText.rectTransform.anchorMin =
            glyphText.rectTransform.anchorMax =
            glyphText.rectTransform.pivot =
                new Vector2(alignRight ? 1 : 0, 1);
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
    /// Creates a Boolean option row.
    /// </summary>
    /// <param name="parent">The page container.</param>
    /// <param name="name">The row object name.</param>
    /// <param name="optionIndex">The page-owned option index the row toggles.</param>
    /// <param name="label">The row caption.</param>
    /// <param name="x">The source-space row left.</param>
    /// <param name="y">The source-space row top.</param>
    /// <returns>The configured toggle-row view.</returns>
    private static OptionsToggleRowView CreateOptionsToggleRow(
        Transform parent,
        string name,
        int optionIndex,
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
        SetSourceRect(labelField.rectTransform, 0, 0, 220, 14);

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

        AssignInt(view, "_optionIndex", optionIndex);
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
}
