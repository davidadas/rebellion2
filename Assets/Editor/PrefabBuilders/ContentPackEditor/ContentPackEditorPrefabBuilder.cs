using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static ContentEditorControlBuilder;

/// <summary>
/// Authors the standalone catalog editors using shared application controls.
/// </summary>
public static class ContentPackEditorPrefabBuilder
{
    private const string _locationLinkIconPath =
        "Assets/Content/Application/Strategy/UI/Windows/ui_strategyview_open_location_button.png";
    private const string _locationLinkIconAddress =
        "Application/Strategy/UI/Windows/ui_strategyview_open_location_button";
    internal const string ScenePath = "Assets/Scenes/ContentPackEditor.unity";
    internal const string PrefabPath =
        "Assets/Prefabs/UI/ContentPackEditor/ContentPackEditorRoot.prefab";

    /// <summary>
    /// Rebuilds the standalone authoring workspace and all its reusable templates.
    /// </summary>
    public static void Rebuild()
    {
        CommonUIPrefabBuilder.RebuildSharedControlPrefabs();
        MenuControlPrefabBuilder.RebuildDropdown();
        GameObject root = new GameObject("ContentPackEditorRoot");
        try
        {
            BuildWorkspace(root);
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        SceneBuilder.Build(ScenePath, PrefabPath, "ContentPackEditorRoot");
    }

    /// <summary>
    /// Builds the application menu, category navigation, record list, and inspector.
    /// </summary>
    /// <param name="root">The generated scene root.</param>
    private static void BuildWorkspace(GameObject root)
    {
        GameObject events = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(QueuedMouseInputModule),
            typeof(ShortcutNavigationInput)
        );
        events.transform.SetParent(root.transform, false);
        RectTransform canvasRoot = Layer("Canvas", root.transform);
        Canvas canvas = canvasRoot.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        CanvasScaler scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRoot.gameObject.AddComponent<GraphicRaycaster>();
        Image background = canvasRoot.gameObject.AddComponent<Image>();
        background.color = Background;
        ContentEditorAppearance appearance =
            canvasRoot.gameObject.AddComponent<ContentEditorAppearance>();
        BindColor(background, ContentEditorColorRole.Background);
        ContentPackEditorView view = canvasRoot.gameObject.AddComponent<ContentPackEditorView>();
        Assign(view, "appearance", appearance);
        ContentPackEditorController controller = root.AddComponent<ContentPackEditorController>();
        Assign(controller, "view", view);
        SerializedObject controllerSettings = new(controller);
        controllerSettings.FindProperty("gameProductName").stringValue = PlayerSettings.productName;
        controllerSettings.ApplyModifiedPropertiesWithoutUndo();

        AudioSource previewAudio = canvasRoot.gameObject.AddComponent<AudioSource>();
        previewAudio.playOnAwake = false;
        Assign(view, "previewAudio", previewAudio);
        canvasRoot.gameObject.AddComponent<AudioListener>();
        RectTransform workbench = Layer("Workbench", canvasRoot);
        Assign(view, "workbench", workbench.gameObject.AddComponent<CanvasGroup>());
        Stretch(workbench);
        canvasRoot = workbench;
        Image menuStrip = Surface("MenuBar", canvasRoot, ContentEditorColorRole.Background);
        Stretch(menuStrip.rectTransform);
        menuStrip.rectTransform.anchorMin = Vector2.up;
        menuStrip.rectTransform.offsetMin = new Vector2(0, -28);
        string[] headings = { "File", "Edit", "View", "Test" };
        string[] members = { "fileButton", "editButton", "viewButton", "testButton" };
        MenuBarHeadingView[] menuHeadings = new MenuBarHeadingView[headings.Length];
        for (int i = 0; i < headings.Length; i++)
        {
            Button button = PlainButton(headings[i], menuStrip.transform, headings[i]);
            Rect((RectTransform)button.transform, 12 + i * 52, 0, 50, 28);
            button.GetComponentInChildren<TMP_Text>().fontSize = 13;
            Assign(view, members[i], button);
            MenuBarHeadingView menuHeading = button.gameObject.AddComponent<MenuBarHeadingView>();
            menuHeadings[i] = menuHeading;
        }
        Assign(view, "menuHeadings", menuHeadings);
        Button connection = PlainButton(
            "ThunderstoreStatus",
            menuStrip.transform,
            "Thunderstore (click to connect)"
        );
        RectTransform connectionRect = (RectTransform)connection.transform;
        connectionRect.anchorMin = connectionRect.anchorMax = connectionRect.pivot = Vector2.one;
        connectionRect.anchoredPosition = new Vector2(-12, 0);
        connectionRect.sizeDelta = new Vector2(440, 28);
        TMP_Text connectionLabel = connection.GetComponentInChildren<TMP_Text>();
        connectionLabel.fontSize = 13;
        connectionLabel.alignment = TextAlignmentOptions.MidlineRight;
        connectionLabel.overflowMode = TextOverflowModes.Ellipsis;
        connectionLabel.textWrappingMode = TextWrappingModes.NoWrap;
        connectionLabel.richText = false;
        Assign(view, "thunderstoreStatus", connectionLabel);
        Assign(view, "thunderstoreConnectionButton", connection);
        Image navigation = Surface("Navigation", canvasRoot, ContentEditorColorRole.Panel);
        Stretch(navigation.rectTransform);
        navigation.rectTransform.anchorMin = Vector2.up;
        navigation.rectTransform.offsetMin = new Vector2(0, -76);
        navigation.rectTransform.offsetMax = new Vector2(0, -28);
        Image navigationRule = Surface(
            "Divider",
            navigation.transform,
            ContentEditorColorRole.Divider
        );
        Stretch(navigationRule.rectTransform);
        navigationRule.rectTransform.anchorMax = Vector2.right;
        navigationRule.rectTransform.offsetMax = new Vector2(0, 1);
        Button library = PlainButton("Library", navigation.transform, "Library");
        Rect((RectTransform)library.transform, 16, 0, 104, 48);
        Assign(view, "libraryButton", library);
        Button browse = PlainButton("Browse", navigation.transform, "Browse");
        Rect((RectTransform)browse.transform, 120, 0, 104, 48);
        Assign(view, "browseButton", browse);
        Button projects = PlainButton("Projects", navigation.transform, "Projects");
        Rect((RectTransform)projects.transform, 224, 0, 112, 48);
        Assign(view, "projectsButton", projects);
        Image navigationSelection = Surface(
            "Selection",
            projects.transform,
            ContentEditorColorRole.Accent
        );
        navigationSelection.raycastTarget = false;
        navigationSelection.rectTransform.anchorMin = Vector2.zero;
        navigationSelection.rectTransform.anchorMax = Vector2.right;
        navigationSelection.rectTransform.offsetMin = new Vector2(16, 0);
        navigationSelection.rectTransform.offsetMax = new Vector2(-16, 2);
        Assign(view, "navigationIndicator", navigationSelection.rectTransform);
        Button publish = Button("Publish", navigation.transform, "Publish…");
        RectTransform publishRect = (RectTransform)publish.transform;
        publishRect.anchorMin = publishRect.anchorMax = publishRect.pivot = Vector2.one;
        publishRect.anchoredPosition = new Vector2(-20, -8);
        publishRect.sizeDelta = new Vector2(112, 32);
        Assign(view, "publishButton", publish);
        RectTransform projectWorkspace = Layer("ProjectWorkspace", canvasRoot);
        Stretch(projectWorkspace, 0, 40, 0, 0);
        Assign(view, "projectWorkspace", projectWorkspace.gameObject);
        Assign(view, "projectInteraction", projectWorkspace.gameObject.AddComponent<CanvasGroup>());
        TextMeshProUGUI project = Label(
            "PackName",
            navigation.transform,
            "No content pack open",
            14,
            true
        );
        Stretch(project.rectTransform, 368, 0, 152, 0);
        project.alignment = TextAlignmentOptions.MidlineRight;
        project.overflowMode = TextOverflowModes.Ellipsis;
        project.textWrappingMode = TextWrappingModes.NoWrap;
        Assign(view, "projectLabel", project);
        Image rule = Surface("MenuDivider", projectWorkspace, ContentEditorColorRole.Divider);
        Stretch(rule.rectTransform, 12, 36, 12);
        rule.rectTransform.anchorMin = Vector2.up;
        rule.rectTransform.offsetMin = new Vector2(12, -37);

        RectTransform explorer = Layer("ContentExplorer", projectWorkspace);
        Image navigationBackground = Surface(
            "NavigationBackground",
            projectWorkspace,
            ContentEditorColorRole.Panel
        );
        navigationBackground.transform.SetAsFirstSibling();
        Stretch(explorer, 20, 54, 0, 60);
        explorer.anchorMax = Vector2.up;
        explorer.offsetMax = new Vector2(316, -54);
        TextMeshProUGUI heading = Label("ContentHeading", explorer, "Content", 18);
        Rect(heading.rectTransform, 0, 0, 140, 30);
        const float explorerRightInset = 18;
        Button addUnit = Button("AddUnit", explorer, "Add...");
        RectTransform addRect = (RectTransform)addUnit.transform;
        Rect(addRect, 0, 0, 132, 32);
        addRect.anchorMin = addRect.anchorMax = addRect.pivot = Vector2.one;
        addRect.anchoredPosition = new Vector2(-explorerRightInset, 0);
        Assign(view, "addUnitButton", addUnit);
        TMP_InputField search = Input("Search", explorer, "Search content...");
        Rect((RectTransform)search.transform, 0, 42, 278, 34);
        ((RectTransform)search.transform).anchorMax = Vector2.one;
        ((RectTransform)search.transform).offsetMax = new Vector2(-explorerRightInset, -42);
        ((RectTransform)search.transform).offsetMin = new Vector2(0, -76);
        ScrollAreaLayout list = Scroll("ContentTree", explorer);
        Stretch((RectTransform)list.transform, 0, 96, 0, 0);
        ContentEditorRecordRowView row = BuildRecordRow(list.ScrollArea.ContentRoot);
        row.gameObject.SetActive(false);
        Assign(view, "searchInput", search);
        Button clearSearch = PlainButton("ClearSearch", search.transform, "Clear");
        RectTransform clearRect = (RectTransform)clearSearch.transform;
        clearRect.anchorMin = clearRect.anchorMax = clearRect.pivot = Vector2.one;
        clearRect.anchoredPosition = new Vector2(-4, -4);
        clearRect.sizeDelta = new Vector2(54, 26);
        clearSearch.gameObject.SetActive(false);
        Assign(view, "clearSearchButton", clearSearch);
        Assign(view, "recordScroll", list);
        Assign(view, "recordTemplate", row);
        Button expand = PlainButton("ExpandAll", explorer, "Expand all");
        Rect((RectTransform)expand.transform, 0, 84, 116, 28);
        Button collapse = PlainButton("CollapseAll", explorer, "Collapse all");
        Rect((RectTransform)collapse.transform, 120, 84, 116, 28);
        RectTransform expandRect = (RectTransform)expand.transform;
        expandRect.anchorMin = Vector2.up;
        expandRect.anchorMax = new Vector2(0.5f, 1);
        expandRect.offsetMin = new Vector2(0, -112);
        expandRect.offsetMax = new Vector2(-4, -84);
        RectTransform collapseRect = (RectTransform)collapse.transform;
        collapseRect.anchorMin = new Vector2(0.5f, 1);
        collapseRect.anchorMax = Vector2.one;
        collapseRect.offsetMin = new Vector2(4, -112);
        collapseRect.offsetMax = new Vector2(0, -84);
        Assign(view, "expandAllButton", expand);
        Assign(view, "collapseAllButton", collapse);
        ((RectTransform)list.transform).offsetMax = new Vector2(0, -118);
        Image divider = Surface(
            "ExplorerDivider",
            projectWorkspace,
            ContentEditorColorRole.Transparent
        );
        divider.raycastTarget = true;
        divider.canvasRenderer.cullTransparentMesh = false;
        Image dividerLine = Surface("Line", divider.transform, ContentEditorColorRole.Divider);
        Stretch(dividerLine.rectTransform, 4, 0, 4, 0);
        Stretch(divider.rectTransform, 328, 54, 0, 20);
        divider.rectTransform.anchorMax = Vector2.up;
        divider.rectTransform.offsetMax = new Vector2(329, -54);
        RectTransform editor = Layer("Inspector", projectWorkspace);
        Stretch(editor, 356, 56, 28, 20);
        ContentEditorWorkbenchLayout workbenchLayout =
            projectWorkspace.gameObject.AddComponent<ContentEditorWorkbenchLayout>();
        Assign(workbenchLayout, "explorer", explorer);
        Assign(workbenchLayout, "inspector", editor);
        Assign(workbenchLayout, "divider", divider.rectTransform);
        Assign(workbenchLayout, "navigationBackground", navigationBackground.rectTransform);
        Assign(view, "workbenchLayout", workbenchLayout);
        SplitterView splitter = divider.gameObject.AddComponent<SplitterView>();
        splitter.Configure(projectWorkspace);
        Assign(workbenchLayout, "splitter", splitter);
        Button duplicate = Button("Duplicate", editor, "Duplicate");
        Rect((RectTransform)duplicate.transform, 0, 36, 112, 32);
        ((RectTransform)duplicate.transform).anchorMin = (
            (RectTransform)duplicate.transform
        ).anchorMax = Vector2.one;
        ((RectTransform)duplicate.transform).anchoredPosition = new Vector2(-208, -36);
        Button resetSection = PlainButton("ResetSection", editor, "Reset to defaults");
        UILayout.SetTopRightRect((RectTransform)resetSection.transform, 220, 36, 124, 32);
        resetSection
            .gameObject.AddComponent<HelpTooltipView>()
            .Configure(
                "Clear the overrides shown here and use the inherited or built-in defaults."
            );
        resetSection.gameObject.SetActive(false);
        Button delete = PlainButton("Delete", editor, "Delete");
        Rect((RectTransform)delete.transform, 0, 36, 84, 32);
        ((RectTransform)delete.transform).anchorMin = ((RectTransform)delete.transform).anchorMax =
            Vector2.one;
        ((RectTransform)delete.transform).anchoredPosition = new Vector2(-84, -36);
        Assign(view, "duplicateButton", duplicate);
        Assign(view, "resetSectionButton", resetSection);
        Assign(view, "deleteButton", delete);
        TextMeshProUGUI title = Label("RecordName", editor, "Create or open a content pack", 28);
        Stretch(title.rectTransform, 0, 0, 366, 0);
        title.rectTransform.anchorMin = Vector2.up;
        title.rectTransform.offsetMin = new Vector2(0, -38);
        TextMeshProUGUI subtitle = Label(
            "RecordInfo",
            editor,
            "Create a new pack or open your working folder.",
            14,
            true
        );
        Stretch(subtitle.rectTransform, 0, 42, 0, 0);
        subtitle.rectTransform.anchorMin = Vector2.up;
        subtitle.rectTransform.offsetMin = new Vector2(0, -72);
        Assign(view, "recordTitle", title);
        Assign(view, "recordSubtitle", subtitle);

        RectTransform preview = Layer("PreviewPane", editor);
        Stretch(preview, 0, 88, 0, 0);
        preview.anchorMin = Vector2.up;
        preview.offsetMin = new Vector2(0, -246);
        RawImage image = Layer("Portrait", preview).gameObject.AddComponent<RawImage>();
        Rect(image.rectTransform, 0, 0, 242, 148);
        AspectRatioFitter fitter = image.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        RectTransform portraitSlot = Layer("PortraitSlot", preview);
        Rect(portraitSlot, 0, 0, 242, 148);
        image.transform.SetParent(portraitSlot, false);
        image.raycastTarget = false;

        RectTransform tabs = Layer("InspectorTabs", editor);
        Stretch(tabs, 0, 88, 0, 0);
        tabs.anchorMin = Vector2.up;
        tabs.offsetMin = new Vector2(0, -126);
        string[] sections =
        {
            "General",
            "Artwork",
            "Audio",
            "Advanced",
            "Encyclopedia",
            "Rules",
            "Ship names",
            "Messages",
            "Encyclopedia",
            "Default backgrounds",
            "Colors",
            "Mission presentation",
            "Cutscenes",
        };
        Button[] tabButtons = new Button[sections.Length];
        for (int i = 0; i < sections.Length; i++)
        {
            tabButtons[i] = PlainButton("Tab" + i, tabs, sections[i]);
            Rect((RectTransform)tabButtons[i].transform, i * 126, 0, 118, 34);
            Image selection = Surface(
                "Selection",
                tabButtons[i].transform,
                ContentEditorColorRole.Accent
            );
            Stretch(selection.rectTransform);
            selection.rectTransform.anchorMax = Vector2.right;
            selection.rectTransform.offsetMax = new Vector2(0, 2);
            selection.gameObject.SetActive(i == 0);
        }
        Assign(view, "propertiesTab", tabButtons[0]);
        Assign(view, "artworkTab", tabButtons[1]);
        Assign(view, "audioTab", tabButtons[2]);
        Assign(view, "advancedTab", tabButtons[3]);
        tabButtons[4].name = "EncyclopediaTab";
        Assign(view, "encyclopediaTab", tabButtons[4]);
        Assign(view, "rulesTab", tabButtons[5]);
        Assign(view, "shipNamesTab", tabButtons[6]);
        Assign(view, "factionMessagesTab", tabButtons[7]);
        Assign(view, "referenceTab", tabButtons[8]);
        Assign(view, "defaultBackgroundsTab", tabButtons[9]);
        Assign(view, "colorsTab", tabButtons[10]);
        Assign(view, "missionPresentationTab", tabButtons[11]);
        tabButtons[12].name = "CutscenesTab";
        Assign(view, "cutscenesTab", tabButtons[12]);
        Button[] generationTabs = ContentPackEditorView
            .GenerationSections.Select(
                (caption, index) =>
                {
                    Button tab = PlainButton("GenerationTab" + index, tabs, caption);
                    Rect((RectTransform)tab.transform, 0, 0, 100, 36);
                    Image selection = Surface(
                        "Selection",
                        tab.transform,
                        ContentEditorColorRole.Accent
                    );
                    selection.raycastTarget = false;
                    selection.rectTransform.anchorMin = Vector2.zero;
                    selection.rectTransform.anchorMax = Vector2.right;
                    selection.rectTransform.offsetMin = Vector2.zero;
                    selection.rectTransform.offsetMax = new Vector2(0, 1);
                    tab.gameObject.SetActive(false);
                    return tab;
                }
            )
            .ToArray();
        Assign(view, "generationTabs", generationTabs);
        Assign(view, "tabRoot", tabs);
        ScrollAreaLayout fields = Scroll("PropertyScroll", editor);
        ContentEditorFormView form = fields.gameObject.AddComponent<ContentEditorFormView>();
        Assign(view, "form", form);
        Assign(form, "previewPane", preview.gameObject);
        Assign(form, "previewImage", image);
        Stretch((RectTransform)fields.transform, 0, 136, 0, 0);
        preview.SetParent(fields.ScrollArea.ContentRoot, false);
        Rect(preview, 0, 0, 280, 160);
        ContentEditorFieldView fieldTemplate = BuildField(fields.ScrollArea.ContentRoot);
        fieldTemplate.gameObject.SetActive(false);
        Assign(form, "fieldScroll", fields);
        ContentEditorColorPickerView colorPickerTemplate = BuildColorPicker(
            fields.ScrollArea.ContentRoot
        );
        colorPickerTemplate.gameObject.SetActive(false);
        Assign(form, "colorPickerTemplate", colorPickerTemplate);
        RawImage artworkPanelTemplate = Layer("ArtworkPanelTemplate", fields.ScrollArea.ContentRoot)
            .gameObject.AddComponent<RawImage>();
        artworkPanelTemplate.raycastTarget = false;
        BindColor(artworkPanelTemplate, ContentEditorColorRole.Panel);
        Outline artworkPanelOutline = artworkPanelTemplate.gameObject.AddComponent<Outline>();
        artworkPanelOutline.effectColor = new Color32(88, 88, 90, 255);
        artworkPanelOutline.effectDistance = new Vector2(1, -1);
        artworkPanelTemplate.gameObject.AddComponent<ContentEditorCardView>();
        artworkPanelTemplate.gameObject.SetActive(false);
        Assign(form, "artworkPanelTemplate", artworkPanelTemplate);
        UICheckboxView outputBackgroundToggle = Checkbox(fields.ScrollArea.ContentRoot);
        outputBackgroundToggle.name = "ImagesBackgroundToggle";
        outputBackgroundToggle.GetComponentInChildren<TextMeshProUGUI>().text = "Show background";
        Rect(
            outputBackgroundToggle.GetComponentInChildren<TextMeshProUGUI>().rectTransform,
            34,
            0,
            140,
            24
        );
        outputBackgroundToggle.gameObject.SetActive(false);
        Assign(form, "outputBackgroundToggle", outputBackgroundToggle);
        UICheckboxView stateBackgroundToggle = Checkbox(fields.ScrollArea.ContentRoot);
        stateBackgroundToggle.name = "GameplayStatesBackgroundToggle";
        stateBackgroundToggle.GetComponentInChildren<TextMeshProUGUI>().text = "Show background";
        Rect(
            stateBackgroundToggle.GetComponentInChildren<TextMeshProUGUI>().rectTransform,
            34,
            0,
            140,
            24
        );
        stateBackgroundToggle.gameObject.SetActive(false);
        Assign(form, "stateBackgroundToggle", stateBackgroundToggle);
        TMP_Dropdown generationTask = Dropdown("SetupTask", fields.ScrollArea.ContentRoot);
        generationTask.gameObject.SetActive(false);
        Assign(form, "generationTask", generationTask);
        CollectionEditorView setupCollection = BuildSetupCollection(fields.ScrollArea.ContentRoot);
        setupCollection.gameObject.SetActive(false);
        Assign(form, "setupCollectionTemplate", setupCollection);
        Button units = Button(
            "SetupUnitsTemplate",
            fields.ScrollArea.ContentRoot,
            "Choose units and quantities..."
        );
        units.gameObject.SetActive(false);
        Assign(form, "setupUnitsTemplate", units);
        TMP_Dropdown artworkViewSelector = Dropdown(
            "ArtworkViewSelector",
            fields.ScrollArea.ContentRoot
        );
        artworkViewSelector.gameObject.SetActive(false);
        Assign(form, "artworkViewSelector", artworkViewSelector);
        RawImage artworkSectorSurface = Layer("ArtworkSectorPreview", fields.ScrollArea.ContentRoot)
            .gameObject.AddComponent<RawImage>();
        BindColor(artworkSectorSurface, ContentEditorColorRole.Panel);
        artworkSectorSurface.raycastTarget = true;
        ContentEditorSectorPreview artworkSectorPreview =
            artworkSectorSurface.gameObject.AddComponent<ContentEditorSectorPreview>();
        PlanetSectorWindowView artworkSectorWindow = StrategyViewPrefabBuilder.CreateSectorPreview(
            artworkSectorSurface.transform,
            includeWindowControls: true
        );
        Assign(artworkSectorPreview, "window", artworkSectorWindow);
        artworkSectorSurface.gameObject.SetActive(false);
        Assign(form, "artworkSectorPreview", artworkSectorPreview);
        RawImage artworkWindowSurface = Layer("ArtworkWindowPreview", fields.ScrollArea.ContentRoot)
            .gameObject.AddComponent<RawImage>();
        artworkWindowSurface.gameObject.AddComponent<RectMask2D>();
        artworkWindowSurface.color = Color.clear;
        artworkWindowSurface.raycastTarget = true;
        ContentEditorArtworkWindowPreview artworkWindowPreview =
            artworkWindowSurface.gameObject.AddComponent<ContentEditorArtworkWindowPreview>();
        StrategyViewPrefabBuilder.CreateArtworkWindowPreviews(artworkWindowSurface.transform);
        StrategyViewPrefabBuilder.CreatePlanetWindowHeaderPreview(artworkWindowSurface.transform);
        CommonUIPrefabBuilder.CreateConfirmationArtworkPreview(artworkWindowSurface.transform);
        BuildStrategyPreviewZoom(fields.ScrollArea.ContentRoot, artworkWindowPreview);
        artworkWindowSurface.gameObject.SetActive(false);
        Assign(form, "artworkWindowPreview", artworkWindowPreview);
        RawImage artworkDisabledWindowSurface = Layer(
            "ArtworkDisabledWindowPreview",
            fields.ScrollArea.ContentRoot
        )
            .gameObject.AddComponent<RawImage>();
        artworkDisabledWindowSurface.gameObject.AddComponent<RectMask2D>();
        artworkDisabledWindowSurface.color = Color.clear;
        artworkDisabledWindowSurface.raycastTarget = true;
        ContentEditorArtworkWindowPreview artworkDisabledWindowPreview =
            artworkDisabledWindowSurface.gameObject.AddComponent<ContentEditorArtworkWindowPreview>();
        StrategyViewPrefabBuilder.CreateArtworkWindowPreviews(
            artworkDisabledWindowSurface.transform
        );
        artworkDisabledWindowSurface.gameObject.SetActive(false);
        Assign(form, "artworkDisabledWindowPreview", artworkDisabledWindowPreview);
        ContentEditorMainMenuPreview mainMenuPreview =
            MainMenuPrefabBuilder.CreateMainMenuPreview();
        try
        {
            const string path = "Assets/Prefabs/UI/ContentPackEditor/MainMenuPreview.prefab";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            GameObject previewPrefab = PrefabUtility.SaveAsPrefabAsset(
                mainMenuPreview.gameObject,
                path
            );
            Assign(
                form,
                "mainMenuPreviewTemplate",
                previewPrefab.GetComponent<ContentEditorMainMenuPreview>()
            );
        }
        finally
        {
            Object.DestroyImmediate(mainMenuPreview.gameObject);
        }
        Button addMessage = Button(
            "AddFactionMessage",
            fields.ScrollArea.ContentRoot,
            "Add starting message"
        );
        Rect((RectTransform)addMessage.transform, 0, 0, 190, 32);
        addMessage.gameObject.SetActive(false);
        Assign(form, "addMessageButton", addMessage);
        TMP_Dropdown collection = Dropdown("CollectionSelection", fields.ScrollArea.ContentRoot);
        collection.gameObject.SetActive(false);
        Assign(form, "collectionDropdown", collection);
        TMP_Dropdown shipDesign = Dropdown("ShipDesignDropdown", fields.ScrollArea.ContentRoot);
        shipDesign.gameObject.SetActive(false);
        Assign(form, "shipDesignDropdown", shipDesign);
        string[] stateCaptions = { "Small card", "Large card", "Battle result" };
        Button[] artworkStateSizes = new Button[stateCaptions.Length];
        for (int i = 0; i < stateCaptions.Length; i++)
        {
            Button size = Button(
                "UnitArtworkSize" + i,
                fields.ScrollArea.ContentRoot,
                stateCaptions[i]
            );
            BindColor(size.GetComponentInChildren<TMP_Text>(), ContentEditorColorRole.Text);
            size.gameObject.SetActive(false);
            artworkStateSizes[i] = size;
        }
        Assign(form, "artworkStateSizeButtons", artworkStateSizes);
        TMP_Dropdown missionPresentation = Dropdown(
            "MissionPresentationDropdown",
            fields.ScrollArea.ContentRoot
        );
        missionPresentation.gameObject.SetActive(false);
        Assign(form, "missionPresentationDropdown", missionPresentation);
        Button missionReports = Button(
            "MissionReportsToggle",
            fields.ScrollArea.ContentRoot,
            "Show reports"
        );
        missionReports.gameObject.SetActive(false);
        Assign(form, "missionReportsToggle", missionReports);
        Button removeMessage = PlainButton(
            "RemoveStartingMessage",
            fields.ScrollArea.ContentRoot,
            "Delete message"
        );
        removeMessage.gameObject.SetActive(false);
        Assign(form, "removeMessageButton", removeMessage);
        LineChartGraphic curve = Layer("ProbabilityCurve", fields.ScrollArea.ContentRoot)
            .gameObject.AddComponent<LineChartGraphic>();
        BindColor(curve, ContentEditorColorRole.Accent);
        curve.raycastTarget = false;
        curve.gameObject.SetActive(false);
        Assign(form, "curveGraphic", curve);
        TMP_Dropdown curves = Dropdown("CurveSelection", fields.ScrollArea.ContentRoot);
        curves.gameObject.SetActive(false);
        Assign(form, "curveDropdown", curves);
        TMP_InputField score = Input("NewScore", fields.ScrollArea.ContentRoot, "New score");
        score.contentType = TMP_InputField.ContentType.IntegerNumber;
        score.gameObject.SetActive(false);
        Assign(form, "scoreInput", score);
        Button addScore = Button("AddScore", fields.ScrollArea.ContentRoot, "Add score");
        addScore
            .gameObject.AddComponent<HelpTooltipView>()
            .Configure("Add a threshold, then fill a cell to save its value.");
        addScore.gameObject.SetActive(false);
        Assign(form, "addScoreButton", addScore);
        Button removeRow = PlainButton("RemoveScoreTemplate", fields.ScrollArea.ContentRoot, "-");
        removeRow
            .gameObject.AddComponent<HelpTooltipView>()
            .Configure("Remove this threshold from every column. Undo restores the row.");
        removeRow.gameObject.SetActive(false);
        Assign(form, "removeRowTemplate", removeRow);
        Assign(form, "fieldTemplate", fieldTemplate);
        TextMeshProUGUI group = Label(
            "GroupTemplate",
            fields.ScrollArea.ContentRoot,
            "Identity",
            17
        );
        group.color = new Color32(218, 178, 111, 255);
        HelpIcon(group.transform);
        BindColor(group, ContentEditorColorRole.Accent);
        group.gameObject.SetActive(false);
        Assign(form, "groupTemplate", group);
        RawImage tableRow = Layer("TableRow", fields.ScrollArea.ContentRoot)
            .gameObject.AddComponent<RawImage>();
        tableRow.raycastTarget = false;
        BindColor(tableRow, ContentEditorColorRole.Panel);
        tableRow.gameObject.SetActive(false);
        Assign(form, "tableRowTemplate", tableRow);
        RawImage articleImage = Layer("ArticleImageTemplate", fields.ScrollArea.ContentRoot)
            .gameObject.AddComponent<RawImage>();
        articleImage.raycastTarget = false;
        articleImage.gameObject.SetActive(false);
        Assign(form, "articleImageTemplate", articleImage);
        RectTransform xmlRoot = Layer("XmlEditor", editor);
        Stretch(xmlRoot);
        ContentEditorXmlView xmlView = xmlRoot.gameObject.AddComponent<ContentEditorXmlView>();
        Assign(view, "xml", xmlView);
        TMP_InputField xml = Input("AdvancedXml", xmlRoot, "Document XML");
        Stretch((RectTransform)xml.transform, 0, 136, 0, 50);
        xml.lineType = TMP_InputField.LineType.MultiLineNewline;
        xml.textComponent.alignment = TextAlignmentOptions.TopLeft;
        xml.textComponent.textWrappingMode = TextWrappingModes.Normal;
        xml.richText = false;
        xml.textComponent.richText = false;
        AddInputScrollbar(xml);
        xml.gameObject.SetActive(false);
        Button applyXml = Button("ApplyXml", xmlRoot, "Apply XML");
        Rect((RectTransform)applyXml.transform, 0, 0, 120, 34);
        ((RectTransform)applyXml.transform).anchorMin = (
            (RectTransform)applyXml.transform
        ).anchorMax = Vector2.zero;
        ((RectTransform)applyXml.transform).anchoredPosition = new Vector2(0, 38);
        applyXml.gameObject.SetActive(false);
        Assign(xmlView, "input", xml);
        TMP_Dropdown xmlScope = Dropdown("XmlScope", xmlRoot);
        RectTransform scopeRect = (RectTransform)xmlScope.transform;
        scopeRect.anchorMin = Vector2.up;
        scopeRect.anchorMax = Vector2.one;
        scopeRect.pivot = Vector2.up;
        scopeRect.offsetMin = new Vector2(0, -168);
        scopeRect.offsetMax = new Vector2(0, -136);
        xmlScope.gameObject.SetActive(false);
        Assign(xmlView, "scopeDropdown", xmlScope);
        Assign(xmlView, "applyButton", applyXml);
        Button discardXml = PlainButton("DiscardXml", xmlRoot, "Discard XML edits");
        Rect((RectTransform)discardXml.transform, 132, 0, 172, 34);
        ((RectTransform)discardXml.transform).anchorMin = (
            (RectTransform)discardXml.transform
        ).anchorMax = Vector2.zero;
        ((RectTransform)discardXml.transform).anchoredPosition = new Vector2(132, 38);
        discardXml.gameObject.SetActive(false);
        Assign(xmlView, "discardButton", discardXml);
        xmlRoot.gameObject.SetActive(false);
        BuildMap(editor, view);

        Image transfers = Surface("Downloads", canvasRoot, ContentEditorColorRole.Background);
        RectTransform transferRect = transfers.rectTransform;
        transferRect.anchorMin = Vector2.zero;
        transferRect.anchorMax = Vector2.right;
        transferRect.pivot = Vector2.zero;
        transferRect.offsetMin = Vector2.zero;
        transferRect.offsetMax = new Vector2(0, 40);
        Image transferDivider = Surface("Divider", transferRect, ContentEditorColorRole.Divider);
        Stretch(transferDivider.rectTransform);
        transferDivider.rectTransform.anchorMin = Vector2.up;
        transferDivider.rectTransform.offsetMin = new Vector2(0, -1);
        TextMeshProUGUI transferStatus = Label(
            "Status",
            transferRect,
            "No active downloads",
            13,
            true
        );
        Rect(transferStatus.rectTransform, 20, 3, 430, 34);
        Assign(view, "packageStatus", transferStatus);
        Image track = Surface("ProgressTrack", transferRect, ContentEditorColorRole.Divider);
        Stretch(track.rectTransform, 470, 18, 144, 18);
        Assign(view, "packageProgressTrack", track.gameObject);
        Image fill = Surface("Progress", track.transform, ContentEditorColorRole.Accent);
        Stretch(fill.rectTransform);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        Assign(view, "packageProgress", fill);
        track.gameObject.SetActive(false);
        Button cancelPackage = PlainButton("CancelDownload", transferRect, "Cancel");
        RectTransform cancelRect = (RectTransform)cancelPackage.transform;
        cancelRect.anchorMin = cancelRect.anchorMax = cancelRect.pivot = Vector2.one;
        cancelRect.anchoredPosition = new Vector2(-20, -3);
        cancelRect.sizeDelta = new Vector2(104, 34);
        Assign(view, "cancelPackageButton", cancelPackage);
        cancelPackage.gameObject.SetActive(false);

        BuildWelcome(canvasRoot, view);
        BuildPackDialog(canvasRoot, view);
        Assign(view, "publishing", BuildPublishing(canvasRoot));
        Assign(view, "connection", BuildThunderstoreConnection(canvasRoot));
        Assign(view, "modManager", BuildModManager(canvasRoot));
        BuildSelectionDialog(canvasRoot, view);
        Assign(view, "artworkWorkspace", ContentEditorArtworkWorkspaceBuilder.Build(canvasRoot));
        BuildConfirmation(canvasRoot, view);
        BuildImagePreview(canvasRoot, view);
        BuildNotification(canvasRoot, view);
        ContextMenuView menu = Menu(canvasRoot);
        SerializedObject menuSettings = new(menu);
        menuSettings.FindProperty("preferRightSubmenus").boolValue = true;
        menuSettings.ApplyModifiedPropertiesWithoutUndo();
        Assign(view, "menu", menu);
        Assign(view, "checkmark", LoadIcon("Checkmark").texture);
        workbenchLayout.SetZoom(1);
    }

    /// <summary>
    /// Authors a reusable record row with shared list-selection behavior.
    /// </summary>
    /// <param name="parent">The list content root.</param>
    /// <returns>The row template.</returns>
    private static ContentEditorRecordRowView BuildRecordRow(Transform parent)
    {
        RectTransform root = Layer("RecordTemplate", parent);
        Rect(root, 0, 0, 278, 36);
        RawImage hit = root.gameObject.AddComponent<RawImage>();
        hit.color = Color.clear;
        BindColor(hit, ContentEditorColorRole.Transparent);
        hit.canvasRenderer.cullTransparentMesh = false;
        ContentEditorRecordRowView row = root.gameObject.AddComponent<ContentEditorRecordRowView>();
        Button disclosure = PlainButton("Disclosure", root, ">");
        Rect((RectTransform)disclosure.transform, 8, 3, 20, 30);
        TextMeshProUGUI arrow = disclosure.GetComponentInChildren<TextMeshProUGUI>();
        Stretch(arrow.rectTransform);
        RectTransform thumbnailSlot = Layer("ThumbnailSlot", root);
        Rect(thumbnailSlot, 28, 6, 32, 24);
        RawImage thumbnail = Layer("Thumbnail", thumbnailSlot).gameObject.AddComponent<RawImage>();
        AspectRatioFitter thumbnailFit = thumbnail.gameObject.AddComponent<AspectRatioFitter>();
        thumbnailFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        thumbnail.raycastTarget = false;
        Stretch(thumbnail.rectTransform);
        TextMeshProUGUI title = Label("Name", root, "Officer", 14);
        Stretch(title.rectTransform, 64, 0, 8, 0);
        OverflowTextTooltip overflow = title.gameObject.AddComponent<OverflowTextTooltip>();
        overflow.Configure(title);
        root.gameObject.AddComponent<OverflowTextTooltipTrigger>().Configure(overflow);
        Assign(row, "hitArea", hit);
        Assign(row, "caption", title);
        Assign(row, "disclosure", arrow);
        Assign(row, "disclosureButton", disclosure);
        Assign(row, "thumbnailSlot", thumbnailSlot);
        Assign(row, "thumbnail", thumbnail);
        return row;
    }

    /// <summary>
    /// Authors a form row around the canonical text input and checkbox views.
    /// </summary>
    /// <param name="parent">The inspector content root.</param>
    /// <returns>The field template.</returns>
    private static ContentEditorFieldView BuildField(Transform parent)
    {
        RectTransform root = Layer("FieldTemplate", parent);
        Rect(root, 0, 0, 1000, 64);
        ContentEditorFieldView field = root.gameObject.AddComponent<ContentEditorFieldView>();
        TextMeshProUGUI label = Label("Label", root, "Property");
        Rect(label.rectTransform, 8, 8, 222, 34);
        TMP_InputField input = Input("Value", root, string.Empty);
        AddInputScrollbar(input);
        input.gameObject.AddComponent<ContentEditorInputScrollRelay>();
        Stretch((RectTransform)input.transform, 244, 8, 14, 22);
        TextMeshProUGUI selectedMedia = Label("SelectedMedia", root, "No file selected", 14);
        selectedMedia.overflowMode = TextOverflowModes.Ellipsis;
        selectedMedia.textWrappingMode = TextWrappingModes.NoWrap;
        selectedMedia.gameObject.SetActive(false);
        OverflowTextTooltip selectedMediaOverflow =
            selectedMedia.gameObject.AddComponent<OverflowTextTooltip>();
        selectedMediaOverflow.Configure(selectedMedia);
        selectedMedia
            .gameObject.AddComponent<OverflowTextTooltipTrigger>()
            .Configure(selectedMediaOverflow);
        Button import = Button("Import", root, "Import...");
        Rect((RectTransform)import.transform, 0, 8, 86, 34);
        ((RectTransform)import.transform).anchorMin = ((RectTransform)import.transform).anchorMax =
            Vector2.one;
        ((RectTransform)import.transform).anchoredPosition = new Vector2(-100, -8);
        Button preview = PlainButton("Preview", root, "Preview");
        Rect((RectTransform)preview.transform, 0, 8, 80, 34);
        ((RectTransform)preview.transform).anchorMin = (
            (RectTransform)preview.transform
        ).anchorMax = Vector2.one;
        ((RectTransform)preview.transform).anchoredPosition = new Vector2(-184, -8);
        TMP_Dropdown choice = Dropdown("Choice", root);
        Stretch((RectTransform)choice.transform, 244, 8, 14, 22);
        UICheckboxView checkbox = Checkbox(root);
        Rect((RectTransform)checkbox.transform, 244, 12, 160, 28);
        TextMeshProUGUI error = Label("Error", root, string.Empty, 12);
        Button revert = PlainButton("Revert", root, "Revert");
        revert
            .gameObject.AddComponent<HelpTooltipView>()
            .Configure("Restore the last accepted value.");
        revert.gameObject.SetActive(false);
        error.overflowMode = TextOverflowModes.Overflow;
        error.textWrappingMode = TextWrappingModes.Normal;
        error.color = new Color32(255, 127, 127, 255);
        BindColor(error, ContentEditorColorRole.Error);
        Stretch(error.rectTransform, 244, 0, 14, 0);
        error.rectTransform.anchorMax = Vector2.right;
        error.rectTransform.offsetMax = new Vector2(-14, 20);
        Button selectItems = Button("SelectItems", root, "Choose...");
        Assign(field, "selectItemsButton", selectItems);
        Assign(field, "label", label);
        Assign(field, "input", input);
        Assign(field, "selectedMedia", selectedMedia);
        Assign(field, "checkbox", checkbox);
        Assign(field, "error", error);
        Assign(field, "importButton", import);
        Assign(field, "revertButton", revert);
        Assign(field, "previewButton", preview);
        Assign(field, "choiceDropdown", choice);
        Assign(field, "help", HelpIcon(root));
        return field;
    }

    /// <summary>
    /// Authors the dedicated faction-color card with a live sample and color wheel.
    /// </summary>
    /// <param name="parent">The scrolling form root.</param>
    /// <returns>The reusable color-picker card.</returns>
    private static ContentEditorColorPickerView BuildColorPicker(Transform parent)
    {
        RectTransform root = Layer("ColorPickerTemplate", parent);
        Rect(root, 0, 0, 560, 296);
        Image background = root.gameObject.AddComponent<Image>();
        background.raycastTarget = false;
        BindColor(background, ContentEditorColorRole.Panel);
        Outline outline = root.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(88, 88, 90, 255);
        outline.effectDistance = new Vector2(1, -1);
        ContentEditorColorPickerView picker =
            root.gameObject.AddComponent<ContentEditorColorPickerView>();

        TextMeshProUGUI title = Label("Title", root, "Selection frames", 17);
        title.fontStyle = FontStyles.Bold;
        SetTopStretch(title.rectTransform, 16, 10, 16, 26);
        TextMeshProUGUI description = Label("Description", root, "Color description", 13, true);
        description.textWrappingMode = TextWrappingModes.Normal;
        description.overflowMode = TextOverflowModes.Overflow;
        SetTopStretch(description.rectTransform, 16, 38, 16, 40);

        Image sampleBackground = Surface("SampleBackground", root, ContentEditorColorRole.Input);
        Rect(sampleBackground.rectTransform, 16, 88, 142, 160);
        RawImage sample = Layer("Sample", sampleBackground.transform)
            .gameObject.AddComponent<RawImage>();
        sample.raycastTarget = false;
        Stretch(sample.rectTransform, 12, 12, 12, 12);
        AspectRatioFitter sampleFitter = sample.gameObject.AddComponent<AspectRatioFitter>();
        sampleFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        Image swatch = Surface("Swatch", root, Color.white);
        swatch.raycastTarget = false;
        RectTransform wheelRect = Layer("ColorWheel", root);
        Rect(wheelRect, 174, 88, 160, 160);
        RawImage wheelImage = wheelRect.gameObject.AddComponent<RawImage>();
        wheelImage.raycastTarget = true;
        ContentEditorColorWheel wheel =
            wheelRect.gameObject.AddComponent<ContentEditorColorWheel>();
        RectTransform selector = BuildColorWheelSelector(wheelRect);
        Assign(wheel, "selector", selector);

        Rect(swatch.rectTransform, 350, 88, 40, 34);
        TMP_InputField hex = Input("Hex", root, "RRGGBB");
        RectTransform hexRect = (RectTransform)hex.transform;
        hexRect.anchorMin = new Vector2(0, 1);
        hexRect.anchorMax = Vector2.one;
        hexRect.offsetMin = new Vector2(400, -122);
        hexRect.offsetMax = new Vector2(-16, -88);

        Slider brightness = BuildColorSlider(
            root,
            "Brightness",
            "Brightness",
            350,
            132,
            out TextMeshProUGUI brightnessValue
        );
        TextMeshProUGUI hueLabel = Label("HueLabel", root, "Hue", 13);
        Rect(hueLabel.rectTransform, 350, 174, 90, 24);
        TextMeshProUGUI hueValue = Label("HueValue", root, "0\u00B0", 13, true);
        hueValue.alignment = TextAlignmentOptions.MidlineRight;
        SetTopStretch(hueValue.rectTransform, 446, 174, 16, 24);
        TextMeshProUGUI saturationLabel = Label("SaturationLabel", root, "Saturation", 13);
        Rect(saturationLabel.rectTransform, 350, 202, 90, 24);
        TextMeshProUGUI saturationValue = Label("SaturationValue", root, "0%", 13, true);
        saturationValue.alignment = TextAlignmentOptions.MidlineRight;
        SetTopStretch(saturationValue.rectTransform, 446, 202, 16, 24);
        TextMeshProUGUI error = Label("Error", root, string.Empty, 12);
        error.color = new Color32(255, 127, 127, 255);
        BindColor(error, ContentEditorColorRole.Error);
        SetTopStretch(error.rectTransform, 350, 232, 16, 24);
        UICheckboxView primaryColorLink = Checkbox(root);
        primaryColorLink.name = "UsePrimaryColor";
        Rect((RectTransform)primaryColorLink.transform, 16, 256, 180, 26);
        TextMeshProUGUI linkLabel = primaryColorLink.GetComponentInChildren<TextMeshProUGUI>();
        linkLabel.text = "Use primary color";
        Rect(linkLabel.rectTransform, 34, 0, 146, 26);

        Assign(picker, "title", title);
        Assign(picker, "description", description);
        Assign(picker, "sample", sample);
        Assign(picker, "swatch", swatch);
        Assign(picker, "hexInput", hex);
        Assign(picker, "colorWheel", wheel);
        Assign(picker, "brightnessSlider", brightness);
        Assign(picker, "hueValue", hueValue);
        Assign(picker, "saturationValue", saturationValue);
        Assign(picker, "brightnessValue", brightnessValue);
        Assign(picker, "error", error);
        Assign(picker, "primaryColorLink", primaryColorLink);
        return picker;
    }

    /// <summary>
    /// Authors the Strategy preview's viewport controls beside the composed artwork.
    /// </summary>
    /// <param name="parent">The scrolling form root.</param>
    /// <param name="preview">The preview controlled by this toolbar.</param>
    private static void BuildStrategyPreviewZoom(
        Transform parent,
        ContentEditorArtworkWindowPreview preview
    )
    {
        RectTransform toolbar = Layer("StrategyPreviewZoom", parent);
        Button zoomOut = Button("ZoomOut", toolbar, "\u2212");
        Rect((RectTransform)zoomOut.transform, 0, 0, 38, 34);
        TextMeshProUGUI percentage = Label("ZoomPercentage", toolbar, "100%", 16);
        percentage.alignment = TextAlignmentOptions.Center;
        Rect(percentage.rectTransform, 46, 0, 58, 34);
        Button zoomIn = Button("ZoomIn", toolbar, "+");
        Rect((RectTransform)zoomIn.transform, 112, 0, 38, 34);
        Button fit = Button("FitPreview", toolbar, "Fit to view");
        Rect((RectTransform)fit.transform, 160, 0, 94, 34);
        Assign(preview, "zoomToolbar", toolbar);
        Assign(preview, "zoomOut", zoomOut);
        Assign(preview, "zoomIn", zoomIn);
        Assign(preview, "zoomLabel", percentage);
        Assign(preview, "fitZoom", fit);
        toolbar.gameObject.SetActive(false);
    }

    /// <summary>
    /// Authors a hollow high-contrast selector over the color wheel.
    /// </summary>
    /// <param name="parent">The color wheel transform.</param>
    /// <returns>The movable selector root.</returns>
    private static RectTransform BuildColorWheelSelector(Transform parent)
    {
        RectTransform selector = Layer("Selector", parent);
        selector.anchorMin = selector.anchorMax = selector.pivot = new Vector2(0.5f, 0.5f);
        selector.sizeDelta = new Vector2(18, 18);
        selector.anchoredPosition = Vector2.zero;
        BuildColorWheelSelectorEdge(selector, "Top", new Vector2(0, 8), new Vector2(14, 2));
        BuildColorWheelSelectorEdge(selector, "Bottom", new Vector2(0, -8), new Vector2(14, 2));
        BuildColorWheelSelectorEdge(selector, "Left", new Vector2(-8, 0), new Vector2(2, 14));
        BuildColorWheelSelectorEdge(selector, "Right", new Vector2(8, 0), new Vector2(2, 14));
        return selector;
    }

    /// <summary>
    /// Authors one outlined edge of the hollow color-wheel selector.
    /// </summary>
    /// <param name="parent">The selector root.</param>
    /// <param name="name">The edge name.</param>
    /// <param name="position">The local edge position.</param>
    /// <param name="size">The edge size.</param>
    private static void BuildColorWheelSelectorEdge(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size
    )
    {
        Image edge = Surface(name, parent, Color.white);
        edge.raycastTarget = false;
        RectTransform rect = edge.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Outline outline = edge.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1, -1);
    }

    /// <summary>
    /// Authors one labeled normalized slider for the faction-color editor.
    /// </summary>
    /// <param name="parent">The color-card root.</param>
    /// <param name="name">The control name.</param>
    /// <param name="caption">The visible caption.</param>
    /// <param name="left">The card-relative left position.</param>
    /// <param name="top">The card-relative top position.</param>
    /// <param name="valueLabel">Receives the numeric value label.</param>
    /// <returns>The configured normalized slider.</returns>
    private static Slider BuildColorSlider(
        Transform parent,
        string name,
        string caption,
        int left,
        int top,
        out TextMeshProUGUI valueLabel
    )
    {
        RectTransform root = Layer(name + "Slider", parent);
        root.anchorMin = new Vector2(0, 1);
        root.anchorMax = Vector2.one;
        root.pivot = Vector2.up;
        root.offsetMin = new Vector2(left, -top - 28);
        root.offsetMax = new Vector2(-16, -top);
        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.minValue = 0;
        slider.maxValue = 1;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        root.gameObject.AddComponent<ContentEditorSliderCommit>();

        TextMeshProUGUI label = Label("Label", root, caption, 13);
        Rect(label.rectTransform, 0, 0, 78, 28);
        TextMeshProUGUI value = Label("Value", root, "0", 13, true);
        value.alignment = TextAlignmentOptions.MidlineRight;
        value.rectTransform.anchorMin = new Vector2(1, 0);
        value.rectTransform.anchorMax = Vector2.one;
        value.rectTransform.pivot = Vector2.right;
        value.rectTransform.anchoredPosition = Vector2.zero;
        value.rectTransform.sizeDelta = new Vector2(48, 28);

        RectTransform trackRoot = Layer("Track", root);
        trackRoot.anchorMin = new Vector2(0, 0.5f);
        trackRoot.anchorMax = new Vector2(1, 0.5f);
        trackRoot.offsetMin = new Vector2(84, -4);
        trackRoot.offsetMax = new Vector2(-56, 4);
        Image track = trackRoot.gameObject.AddComponent<Image>();
        BindColor(track, ContentEditorColorRole.Input);

        RectTransform fillArea = Layer("FillArea", trackRoot);
        Stretch(fillArea, 0, 0, 0, 0);
        Image fill = Surface("Fill", fillArea, ContentEditorColorRole.Accent);
        Stretch(fill.rectTransform);

        RectTransform handleArea = Layer("HandleArea", trackRoot);
        Stretch(handleArea, 0, 0, 0, 0);
        Image handle = Surface("Handle", handleArea, ContentEditorColorRole.Button);
        RectTransform handleRect = handle.rectTransform;
        handleRect.anchorMin = handleRect.anchorMax = new Vector2(0, 0.5f);
        handleRect.sizeDelta = new Vector2(14, 22);
        handleRect.anchoredPosition = Vector2.zero;

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        valueLabel = value;
        return slider;
    }

    /// <summary>
    /// Anchors a label to the top edge of a responsive card.
    /// </summary>
    /// <param name="rect">The label transform.</param>
    /// <param name="left">The left inset.</param>
    /// <param name="top">The top inset.</param>
    /// <param name="right">The right inset.</param>
    /// <param name="height">The label height.</param>
    private static void SetTopStretch(RectTransform rect, int left, int top, int right, int height)
    {
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.up;
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>
    /// Authors shared collection controls for starting placements and nested fleet entries.
    /// </summary>
    /// <param name="parent">The form content root.</param>
    /// <returns>The reusable collection template.</returns>
    private static CollectionEditorView BuildSetupCollection(Transform parent)
    {
        RectTransform root = Layer("SetupCollectionTemplate", parent);
        Rect(root, 0, 0, 900, 120);
        CollectionEditorView view = root.gameObject.AddComponent<CollectionEditorView>();
        Assign(view, "heading", Label("Heading", root, "Starting units", 17));
        Assign(view, "entries", Dropdown("Entries", root));
        Assign(view, "add", Button("Add", root, "Add entry"));
        Assign(view, "remove", PlainButton("Delete", root, "Delete entry"));
        return view;
    }

    /// <summary>
    /// Authors a searchable multi-select using the application's checkbox, input, and scrolling controls.
    /// </summary>
    /// <param name="parent">The canvas root.</param>
    /// <param name="view">The workspace owning the modal.</param>
    private static void BuildSelectionDialog(Transform parent, ContentPackEditorView view)
    {
        Image overlay = Surface("ItemSelection", parent, new Color(0, 0, 0, 0.75f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;
        SelectionDialogView dialog = overlay.gameObject.AddComponent<SelectionDialogView>();
        Image panel = Surface("Panel", overlay.transform, ContentEditorColorRole.Panel);
        panel.rectTransform.anchorMin =
            panel.rectTransform.anchorMax =
            panel.rectTransform.pivot =
                new Vector2(0.5f, 0.5f);
        panel.rectTransform.sizeDelta = new Vector2(760, 640);
        TMP_Text title = Label("Heading", panel.transform, "Choose units", 20);
        Rect(title.rectTransform, 20, 14, 720, 36);
        TMP_InputField search = Input(
            "SelectionSearch",
            panel.transform,
            "Search by name or ID..."
        );
        Rect((RectTransform)search.transform, 20, 60, 720, 34);
        TMP_Text summary = Label("Summary", panel.transform, string.Empty, 14, true);
        Rect(summary.rectTransform, 20, 104, 560, 28);
        TMP_Text quantityTitle = Label("QuantityHeading", panel.transform, "Quantity", 14, true);
        Rect(quantityTitle.rectTransform, 620, 104, 100, 28);
        quantityTitle.alignment = TextAlignmentOptions.MidlineRight;
        ScrollAreaLayout scroll = Scroll("Options", panel.transform);
        Rect((RectTransform)scroll.transform, 20, 140, 720, 404);
        RectTransform row = Layer("OptionTemplate", scroll.ScrollArea.ContentRoot);
        Rect(row, 0, 0, 704, 64);
        SelectionOptionView option = row.gameObject.AddComponent<SelectionOptionView>();
        UICheckboxView check = Checkbox(row);
        TextMeshProUGUI optionLabel = check.GetComponentInChildren<TextMeshProUGUI>(true);
        OverflowTextTooltip overflow = optionLabel.gameObject.AddComponent<OverflowTextTooltip>();
        overflow.Configure(optionLabel);
        check.gameObject.AddComponent<OverflowTextTooltipTrigger>().Configure(overflow);
        TMP_InputField amount = Input("Quantity", row, "0");
        amount.contentType = TMP_InputField.ContentType.IntegerNumber;
        TMP_Text error = Label("Error", row, string.Empty, 12);
        error.overflowMode = TextOverflowModes.Overflow;
        BindColor(error, ContentEditorColorRole.Error);
        Assign(option, "checkbox", check);
        Assign(option, "quantity", amount);
        Assign(option, "error", error);
        row.gameObject.SetActive(false);
        Button apply = Button("Apply", panel.transform, "Apply");
        Rect((RectTransform)apply.transform, 620, 586, 120, 34);
        Button cancel = PlainButton("Cancel", panel.transform, "Cancel");
        Rect((RectTransform)cancel.transform, 488, 586, 120, 34);
        TMP_Text errorText = Label("SelectionError", panel.transform, string.Empty, 13);
        Rect(errorText.rectTransform, 20, 552, 720, 26);
        BindColor(errorText, ContentEditorColorRole.Error);
        Assign(dialog, "error", errorText);
        Assign(dialog, "heading", title);
        Assign(dialog, "summary", summary);
        Assign(dialog, "quantityHeading", quantityTitle);
        Assign(dialog, "search", search);
        Assign(dialog, "scroll", scroll);
        Assign(dialog, "rowTemplate", option);
        Assign(dialog, "apply", apply);
        Assign(dialog, "cancel", cancel);
        Assign(view, "selectionDialog", dialog);
        overlay.gameObject.SetActive(false);
    }

    /// <summary>
    /// Authors the map canvas and reusable draggable marker.
    /// </summary>
    /// <param name="parent">The inspector pane.</param>
    /// <param name="view">The owning workspace view.</param>
    private static void BuildMap(RectTransform parent, ContentPackEditorView view)
    {
        RectTransform pane = Layer("MapPane", parent);
        ContentEditorMapView mapView = pane.gameObject.AddComponent<ContentEditorMapView>();
        Assign(view, "map", mapView);
        Stretch(pane, 0, 136, 390, 0);
        VerticalLayoutGroup layout = pane.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12;
        layout.padding = new RectOffset(0, 0, 0, 40);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        RectTransform toolbar = Layer("MapToolbar", pane);
        LayoutElement toolbarSize = toolbar.gameObject.AddComponent<LayoutElement>();
        toolbarSize.minHeight = toolbarSize.preferredHeight = 34;
        HorizontalLayoutGroup toolbarLayout =
            toolbar.gameObject.AddComponent<HorizontalLayoutGroup>();
        toolbarLayout.spacing = 12;
        toolbarLayout.childAlignment = TextAnchor.UpperLeft;
        toolbarLayout.childControlWidth = toolbarLayout.childControlHeight = true;
        toolbarLayout.childForceExpandWidth = toolbarLayout.childForceExpandHeight = false;
        Button fit = Button("FitMap", toolbar, "Fit to view");
        Rect((RectTransform)fit.transform, 0, 0, 108, 34);
        LayoutElement fitSize = fit.gameObject.AddComponent<LayoutElement>();
        fitSize.minWidth = 96;
        fitSize.preferredWidth = 108;
        fitSize.preferredHeight = 34;
        Assign(mapView, "mapFitButton", fit);
        fit.gameObject.AddComponent<HelpTooltipView>()
            .Configure("Fit the whole map in the view without changing saved positions.");
        Button arrange = Button("ArrangeMap", toolbar, "Auto arrange");
        Rect((RectTransform)arrange.transform, 120, 0, 116, 34);
        LayoutElement arrangeSize = arrange.gameObject.AddComponent<LayoutElement>();
        arrangeSize.minWidth = 108;
        arrangeSize.preferredWidth = 116;
        arrangeSize.preferredHeight = 34;
        Assign(mapView, "mapArrangeButton", arrange);
        arrange
            .gameObject.AddComponent<HelpTooltipView>()
            .Configure(
                "Move planets, or whole sectors on the galaxy map, to clear overlaps and the frame. One undo restores all positions."
            );
        RectTransform previewRow = Layer("PreviewOptions", pane);
        HorizontalLayoutGroup previewLayout =
            previewRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        previewLayout.childControlWidth = previewLayout.childControlHeight = true;
        previewLayout.childForceExpandWidth = previewLayout.childForceExpandHeight = false;
        previewLayout.spacing = 12;
        LayoutElement previewRowSize = previewRow.gameObject.AddComponent<LayoutElement>();
        previewRowSize.minHeight = previewRowSize.preferredHeight = 34;
        Button back = Button("Galaxy", previewRow, "Galaxy");
        LayoutElement backSize = back.gameObject.AddComponent<LayoutElement>();
        backSize.minWidth = backSize.preferredWidth = 70;
        backSize.preferredHeight = 34;
        TMP_Dropdown previewFaction = Dropdown("PreviewFaction", previewRow);
        Rect((RectTransform)previewFaction.transform, 296, 0, 220, 34);
        LayoutElement previewSize = previewFaction.gameObject.AddComponent<LayoutElement>();
        previewSize.minWidth = 80;
        previewSize.preferredWidth = 220;
        previewSize.preferredHeight = 34;
        Assign(mapView, "mapFactionDropdown", previewFaction);
        TextMeshProUGUI hint = Label("Hint", pane, string.Empty, 12, true);
        hint.alignment = TextAlignmentOptions.TopLeft;
        hint.overflowMode = TextOverflowModes.Overflow;
        hint.textWrappingMode = TextWrappingModes.Normal;
        Assign(mapView, "mapHint", hint);
        RectTransform viewportSlot = Layer("MapViewport", pane);
        LayoutElement viewportSize = viewportSlot.gameObject.AddComponent<LayoutElement>();
        viewportSize.flexibleHeight = 1;
        RectTransform slot = Layer("MapSlot", viewportSlot);
        Stretch(slot, 24, 0, 60, 0);
        slot.gameObject.AddComponent<RectMask2D>();
        Image mapHit = slot.gameObject.AddComponent<Image>();
        mapHit.color = new Color32(10, 17, 28, 255);
        ContentEditorMapViewport viewport =
            slot.gameObject.AddComponent<ContentEditorMapViewport>();
        RawImage map = Layer("Map", slot).gameObject.AddComponent<RawImage>();
        Stretch(map.rectTransform);
        map.raycastTarget = true;
        AspectRatioFitter aspect = map.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = 1.2f;
        PlanetSectorWindowView sectorWindow = StrategyViewPrefabBuilder.CreateSectorPreview(
            map.transform
        );
        ContentEditorSectorPreview sectorPreview =
            map.gameObject.AddComponent<ContentEditorSectorPreview>();
        Assign(sectorPreview, "window", sectorWindow);
        Assign(mapView, "sectorPreview", sectorPreview);
        sectorWindow.gameObject.SetActive(false);
        Assign(viewport, "mapRoot", map.rectTransform);
        Assign(mapView, "mapViewport", viewport);
        RectTransform galaxy = Layer("GalaxyPreview", map.transform);
        Stretch(galaxy);
        ContentEditorGalaxyPreview galaxyPreview =
            galaxy.gameObject.AddComponent<ContentEditorGalaxyPreview>();
        PlanetSectorClusterView cluster = StrategyViewPrefabBuilder.CreateGalaxyClusterPreview(
            galaxy
        );
        RectTransform marker = (RectTransform)cluster.transform;
        marker.name = "MarkerTemplate";
        ContentEditorMapMarker markerView =
            marker.gameObject.AddComponent<ContentEditorMapMarker>();
        Assign(markerView, "cluster", cluster);
        marker.gameObject.SetActive(false);
        pane.gameObject.SetActive(false);
        Assign(mapView, "mapAspect", aspect);
        Assign(mapView, "mapImage", map);
        Assign(galaxyPreview, "markerTemplate", markerView);
        Assign(mapView, "galaxyPreview", galaxyPreview);
        Assign(mapView, "mapBackButton", back);
    }

    /// <summary>
    /// Presents explicit project creation and opening without selecting installed content.
    /// </summary>
    /// <param name="parent">The application canvas.</param>
    /// <param name="view">The owning workspace.</param>
    private static void BuildWelcome(Transform parent, ContentPackEditorView view)
    {
        RectTransform welcome = Layer("Welcome", parent);
        Stretch(welcome, 0, 76, 0, 40);
        ScrollAreaLayout pageScroll = Scroll("ProjectsPage", welcome, 8, 8);
        RectTransform page = (RectTransform)pageScroll.transform;
        page.anchorMin = page.anchorMax = page.pivot = new Vector2(0.5f, 0.5f);
        page.sizeDelta = new Vector2(1136, 720);
        page.anchoredPosition = new Vector2(8, 0);
        Assign(view, "projectsPage", pageScroll);
        RectTransform actions = Layer("Projects", pageScroll.ScrollArea.ContentRoot);
        Rect(actions, 0, 0, 1120, 720);
        Assign(view, "projectHub", actions);
        TextMeshProUGUI title = Label("Title", actions, "Projects", 36);
        Rect(title.rectTransform, 0, 32, 640, 48);
        title.fontStyle = FontStyles.Bold;
        TextMeshProUGUI explanation = Label(
            "Description",
            actions,
            "Create, edit, test and publish your own content.",
            16,
            true
        );
        Rect(explanation.rectTransform, 0, 96, 1120, 24);
        explanation.rectTransform.anchorMax = Vector2.one;
        explanation.rectTransform.offsetMax = new Vector2(0, -96);
        RectTransform pageActions = ActionRow(actions, 32, 48);
        Button create = Button("NewPack", pageActions, "New project");
        Rect((RectTransform)create.transform, 0, 0, 160, 44);
        Button open = Button("OpenPack", pageActions, "Open project…");
        Rect((RectTransform)open.transform, 0, 0, 176, 44);
        Assign(view, "projects", BuildProjects(actions));
        Assign(view, "welcomePane", welcome.gameObject);
        Assign(view, "newPackButton", create);
        Assign(view, "openPackButton", open);
    }

    /// <summary>
    /// Authors the New and Open workflow in a focused modal dialog.
    /// </summary>
    /// <param name="parent">The canvas.</param>
    /// <param name="view">The workspace view.</param>
    private static void BuildPackDialog(Transform parent, ContentPackEditorView view)
    {
        Image overlay = Surface("PackDialog", parent, new Color(0, 0, 0, 0.7f));
        Stretch(overlay.rectTransform);
        ContentEditorPackDialogView dialog =
            overlay.gameObject.AddComponent<ContentEditorPackDialogView>();
        overlay.raycastTarget = true;
        Image panel = Surface("Panel", overlay.transform, ContentEditorColorRole.Panel);
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(640, 600);
        TextMeshProUGUI title = Label("Heading", rect, "New content pack", 24);
        Rect(title.rectTransform, 28, 24, 584, 40);
        TMP_InputField id = Input("PackID", rect, "Pack ID (for example: my-content-pack)");
        Rect((RectTransform)id.transform, 28, 176, 380, 36);
        TMP_InputField name = Input("DisplayName", rect, "Display name");
        Rect((RectTransform)name.transform, 28, 102, 584, 36);
        TMP_InputField version = Input("Version", rect, "1.0.0");
        Rect((RectTransform)version.transform, 428, 176, 184, 36);
        TMP_Dropdown pack = Dropdown("PackChoice", rect);
        Rect((RectTransform)pack.transform, 28, 250, 584, 36);
        TMP_InputField folder = Input("Location", rect, "Parent folder");
        Rect((RectTransform)folder.transform, 28, 326, 482, 36);
        Button browse = Button("BrowseFolder", rect, "Browse...");
        Rect((RectTransform)browse.transform, 522, 326, 90, 36);
        TextMeshProUGUI help = Label(
            "Help",
            rect,
            "* Required. Your pack shows the base pack's content. Only edited files are saved locally.",
            14,
            true
        );
        TextMeshProUGUI destination = Label("PackFolder", rect, string.Empty, 14, true);
        Rect(destination.rectTransform, 28, 370, 584, 48);
        destination.textWrappingMode = TextWrappingModes.Normal;
        Rect(help.rectTransform, 28, 426, 584, 48);
        help.textWrappingMode = TextWrappingModes.Normal;
        TextMeshProUGUI error = Label("Error", rect, string.Empty, 14);
        Rect(error.rectTransform, 28, 480, 584, 48);
        error.color = new Color32(255, 130, 130, 255);
        BindColor(error, ContentEditorColorRole.Error);
        error.textWrappingMode = TextWrappingModes.Normal;
        Button confirm = Button("Confirm", rect, "Create pack");
        Rect((RectTransform)confirm.transform, 444, 536, 168, 36);
        Button cancel = Button("Cancel", rect, "Cancel");
        Rect((RectTransform)cancel.transform, 322, 536, 110, 36);
        Assign(view, "packDialog", dialog);
        Rect(Label("NameLabel", rect, "Pack name *", 14).rectTransform, 28, 78, 584, 20);
        Rect(Label("IDLabel", rect, "Pack ID *", 14).rectTransform, 28, 152, 380, 20);
        Rect(Label("VersionLabel", rect, "Version *", 14).rectTransform, 428, 152, 184, 20);
        Rect(Label("BaseLabel", rect, "Base pack *", 14).rectTransform, 28, 226, 584, 20);
        Rect(Label("FolderLabel", rect, "Location *", 14).rectTransform, 28, 302, 584, 20);
        Assign(dialog, "newIDInput", id);
        Assign(dialog, "newNameInput", name);
        Assign(dialog, "newVersionInput", version);
        Assign(dialog, "newFolderInput", folder);
        Assign(dialog, "newFolderPathText", destination);
        Assign(dialog, "helpText", help);
        Assign(dialog, "folderBrowseButton", browse);
        Assign(dialog, "packChoiceDropdown", pack);
        Assign(dialog, "dialogError", error);
        Assign(dialog, "dialogConfirmButton", confirm);
        Assign(dialog, "dialogCancelButton", cancel);
        overlay.gameObject.SetActive(false);
    }

    /// <summary>
    /// Authors a dismissible operation notice near the toolbar.
    /// </summary>
    /// <param name="parent">The canvas.</param>
    /// <param name="view">The workspace view.</param>
    private static void BuildNotification(Transform parent, ContentPackEditorView view)
    {
        Image notice = Surface("Notification", parent, ContentEditorColorRole.Panel);
        Rect(notice.rectTransform, 0, 58, 620, 96);
        notice.rectTransform.anchorMin = notice.rectTransform.anchorMax = new Vector2(0.5f, 1);
        notice.rectTransform.pivot = new Vector2(0.5f, 1);
        notice.rectTransform.anchoredPosition = new Vector2(0, -58);
        TextMeshProUGUI text = Label("Text", notice.transform, string.Empty, 15);
        Stretch(text.rectTransform, 20, 8, 152, 8);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.overflowMode = TextOverflowModes.Overflow;
        text.textWrappingMode = TextWrappingModes.Normal;
        Image linkHitArea = Surface("OpenInvalidField", notice.transform, Color.clear);
        linkHitArea.raycastTarget = true;
        Rect(linkHitArea.rectTransform, 512, 10, 30, 30);
        RawImage linkImage = Layer("Icon", linkHitArea.transform)
            .gameObject.AddComponent<RawImage>();
        linkImage.texture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(_locationLinkIconPath)
            ?? throw new FileNotFoundException("The location-link icon is missing.");
        linkImage.color = Color.white;
        linkImage.raycastTarget = false;
        linkImage
            .gameObject.AddComponent<ContentTextureBinding>()
            .SetAddress(_locationLinkIconAddress);
        Rect(linkImage.rectTransform, 7, 7, 16, 16);
        Button link = linkHitArea.gameObject.AddComponent<Button>();
        link.targetGraphic = linkImage;
        ColorBlock linkColors = link.colors;
        linkColors.normalColor = Color.white;
        linkColors.highlightedColor = new Color(1f, 1f, 1f, 0.72f);
        linkColors.selectedColor = linkColors.highlightedColor;
        linkColors.pressedColor = new Color(1f, 1f, 1f, 0.55f);
        linkColors.fadeDuration = 0.05f;
        link.colors = linkColors;
        link.gameObject.AddComponent<HelpTooltipView>()
            .Configure("Go to the field that needs attention.");
        RectTransform validationRoot = Layer("ValidationErrors", notice.transform);
        Rect(validationRoot, 16, 46, 588, 216);
        ContentEditorValidationIssueListView validationList =
            validationRoot.gameObject.AddComponent<ContentEditorValidationIssueListView>();
        ScrollAreaLayout validationScroll = Scroll("Issues", validationRoot);
        Stretch((RectTransform)validationScroll.transform);
        RectTransform row = Layer("IssueTemplate", validationScroll.ScrollArea.ContentRoot);
        Rect(row, 0, 0, 572, 72);
        ContentEditorValidationIssueView issue =
            row.gameObject.AddComponent<ContentEditorValidationIssueView>();
        Image rowSurface = Surface("ValidationIssueLink", row, ContentEditorColorRole.Button);
        Stretch(rowSurface.rectTransform, 0, 2, 0, 2);
        rowSurface.raycastTarget = false;
        TextMeshProUGUI message = Label("Message", rowSurface.transform, string.Empty, 13);
        Stretch(message.rectTransform, 12, 8, 52, 8);
        message.alignment = TextAlignmentOptions.MidlineLeft;
        message.textWrappingMode = TextWrappingModes.Normal;
        message.raycastTarget = false;
        Image issueLinkHitArea = Surface("OpenValidationIssue", rowSurface.transform, Color.clear);
        issueLinkHitArea.raycastTarget = true;
        Rect(issueLinkHitArea.rectTransform, 0, 0, 36, 36);
        issueLinkHitArea.rectTransform.anchorMin = issueLinkHitArea.rectTransform.anchorMax =
            Vector2.right;
        issueLinkHitArea.rectTransform.pivot = Vector2.right;
        issueLinkHitArea.rectTransform.anchoredPosition = new Vector2(-8, 0);
        RawImage issueLinkImage = Layer("Icon", issueLinkHitArea.transform)
            .gameObject.AddComponent<RawImage>();
        issueLinkImage.texture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(_locationLinkIconPath)
            ?? throw new FileNotFoundException("The location-link icon is missing.");
        issueLinkImage.color = Color.white;
        issueLinkImage.raycastTarget = false;
        issueLinkImage
            .gameObject.AddComponent<ContentTextureBinding>()
            .SetAddress(_locationLinkIconAddress);
        Rect(issueLinkImage.rectTransform, 10, 10, 16, 16);
        Button open = issueLinkHitArea.gameObject.AddComponent<Button>();
        open.targetGraphic = issueLinkImage;
        ColorBlock openColors = open.colors;
        openColors.normalColor = new Color(0.78f, 0.78f, 0.78f, 1);
        openColors.highlightedColor = Color.white;
        openColors.selectedColor = Color.white;
        openColors.pressedColor = new Color(0.58f, 0.58f, 0.58f, 1);
        openColors.fadeDuration = 0.05f;
        open.colors = openColors;
        open.gameObject.AddComponent<HelpTooltipView>()
            .Configure("Go to the field that needs attention.");
        Assign(issue, "openButton", open);
        Assign(issue, "message", message);
        Assign(validationList, "scroll", validationScroll);
        Assign(validationList, "rowTemplate", issue);
        row.gameObject.SetActive(false);
        validationRoot.gameObject.SetActive(false);
        Button validationToggle = Button("ToggleValidationErrors", notice.transform, "Hide");
        Rect((RectTransform)validationToggle.transform, 464, 10, 72, 30);
        TextMeshProUGUI validationToggleText =
            validationToggle.GetComponentInChildren<TextMeshProUGUI>();
        validationToggle.gameObject.SetActive(false);
        Button close = Button("Dismiss", notice.transform, "Close");
        Rect((RectTransform)close.transform, 544, 10, 60, 30);
        Assign(view, "notice", notice.gameObject);
        Assign(view, "noticeText", text);
        Assign(view, "dismissNoticeButton", close);
        Assign(view, "noticeLinkButton", link);
        Assign(view, "validationToggleButton", validationToggle);
        Assign(view, "validationToggleText", validationToggleText);
        Assign(view, "validationList", validationList);
        notice.gameObject.SetActive(false);
    }

    /// <summary>
    /// Authors the save/discard/cancel prompt used for destructive workspace transitions.
    /// </summary>
    /// <param name="parent">The canvas.</param>
    /// <param name="view">The owning workspace.</param>
    private static void BuildConfirmation(Transform parent, ContentPackEditorView view)
    {
        Image overlay = Surface("Confirmation", parent, new Color(0, 0, 0, 0.75f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;
        ActionDialogView prompt = overlay.gameObject.AddComponent<ActionDialogView>();
        Image panel = Surface("Panel", overlay.transform, ContentEditorColorRole.Panel);
        panel.rectTransform.anchorMin =
            panel.rectTransform.anchorMax =
            panel.rectTransform.pivot =
                new Vector2(0.5f, 0.5f);
        panel.rectTransform.sizeDelta = new Vector2(520, 156);
        Image banner = Surface("WarningBanner", panel.transform, ContentEditorColorRole.Warning);
        Stretch(banner.rectTransform);
        banner.rectTransform.anchorMin = Vector2.up;
        banner.rectTransform.offsetMin = new Vector2(0, -64);
        TextMeshProUGUI symbol = Label("Symbol", banner.transform, "!", 38, true);
        symbol.fontStyle = FontStyles.Bold;
        Rect(symbol.rectTransform, 18, 8, 34, 48);
        symbol.alignment = TextAlignmentOptions.Center;
        symbol
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(ContentEditorColorRole.WarningText);
        TextMeshProUGUI heading = Label("Heading", banner.transform, "WARNING", 27, true);
        heading.fontStyle = FontStyles.Bold;
        Rect(heading.rectTransform, 66, 8, 430, 48);
        heading.alignment = TextAlignmentOptions.MidlineLeft;
        heading
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(ContentEditorColorRole.WarningText);
        Assign(prompt, "heading", heading);
        banner.gameObject.SetActive(false);
        TextMeshProUGUI message = Label("Message", panel.transform, string.Empty, 18);
        Rect(message.rectTransform, 20, 18, 400, 64);
        message.alignment = TextAlignmentOptions.TopLeft;
        message.textWrappingMode = TextWrappingModes.Normal;
        Button save = Button("Primary", panel.transform, "Save");
        Rect((RectTransform)save.transform, 316, 102, 104, 34);
        Button discard = Button("Secondary", panel.transform, "Discard");
        Rect((RectTransform)discard.transform, 204, 102, 104, 34);
        Button cancel = PlainButton("Cancel", panel.transform, "Cancel");
        Rect((RectTransform)cancel.transform, 92, 102, 104, 34);
        Assign(prompt, "message", message);
        Assign(prompt, "primaryButton", save);
        Assign(prompt, "secondaryButton", discard);
        Assign(prompt, "cancelButton", cancel);
        Assign(view, "confirmation", prompt);
        overlay.gameObject.SetActive(false);
    }

    /// <summary>
    /// Authors the artwork inspector using the same modal surface and action controls.
    /// </summary>
    /// <param name="parent">The canvas.</param>
    /// <param name="view">The owning workspace.</param>
    private static void BuildImagePreview(Transform parent, ContentPackEditorView view)
    {
        Image overlay = Surface("ImagePreview", parent, new Color(0, 0, 0, 0.8f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;
        ImagePreviewView preview = overlay.gameObject.AddComponent<ImagePreviewView>();
        Image panel = Surface("Panel", overlay.transform, ContentEditorColorRole.Panel);
        Stretch(panel.rectTransform, 160, 100, 160, 100);
        TextMeshProUGUI caption = Label("Caption", panel.transform, string.Empty, 15);
        Stretch(caption.rectTransform, 24, 18, 150, 0);
        caption.rectTransform.anchorMin = Vector2.up;
        caption.rectTransform.offsetMin = new Vector2(24, -58);
        RectTransform slot = Layer("ArtworkSlot", panel.transform);
        Stretch(slot, 24, 84, 24, 28);
        RawImage image = Layer("Artwork", slot).gameObject.AddComponent<RawImage>();
        Stretch(image.rectTransform);
        image.raycastTarget = false;
        image.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter
            .AspectMode
            .FitInParent;
        Button close = Button("Close", panel.transform, "Close");
        Rect((RectTransform)close.transform, 0, 18, 110, 36);
        ((RectTransform)close.transform).anchorMin = ((RectTransform)close.transform).anchorMax =
            Vector2.one;
        ((RectTransform)close.transform).anchoredPosition = new Vector2(-134, -18);
        Button apply = BuildImagePreviewAction(
            panel.transform,
            "UseArtwork",
            "Save",
            Vector2.right,
            -134,
            110
        );
        UICheckboxView cutSelection = Checkbox(panel.transform);
        cutSelection.name = "IncludeCut";
        TextMeshProUGUI cutCaption = cutSelection.GetComponentInChildren<TextMeshProUGUI>();
        cutCaption.fontSize = 15;
        Stretch(cutCaption.rectTransform, 32, 0, 0, 0);
        cutSelection.gameObject.SetActive(false);
        TextMeshProUGUI layerInstructions = Label("LayerInstructions", panel.transform, "", 15);
        layerInstructions.rectTransform.anchorMin = Vector2.zero;
        layerInstructions.rectTransform.anchorMax = Vector2.right;
        layerInstructions.rectTransform.offsetMin = new Vector2(24, 67);
        layerInstructions.rectTransform.offsetMax = new Vector2(-24, 102);
        layerInstructions.gameObject.SetActive(false);
        Assign(preview, "layerInstructions", layerInstructions);
        Assign(preview, "image", image);
        Assign(preview, "caption", caption);
        Assign(preview, "closeButton", close);
        Assign(preview, "artworkSlot", slot);
        Assign(preview, "previewPanel", panel.rectTransform);
        Assign(preview, "applyButton", apply);
        Assign(preview, "cutSelectionTemplate", cutSelection);
        Assign(view, "imagePreview", preview);
        overlay.gameObject.SetActive(false);
    }

    /// <summary>
    /// Places one optional control in the space beneath the generated image.
    /// </summary>
    /// <param name="parent">The image viewer panel.</param>
    /// <param name="name">The button hierarchy name.</param>
    /// <param name="text">The visible button label.</param>
    /// <param name="anchor">The bottom corner supplying the horizontal origin.</param>
    /// <param name="x">The horizontal offset from that corner.</param>
    /// <param name="width">The width reserved for the button label.</param>
    /// <returns>The initially hidden optional button.</returns>
    private static Button BuildImagePreviewAction(
        Transform parent,
        string name,
        string text,
        Vector2 anchor,
        float x,
        float width
    )
    {
        Button button = Button(name, parent, text);
        RectTransform rect = (RectTransform)button.transform;
        Rect(rect, 0, 0, width, 36);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = new Vector2(x, 60);
        button.gameObject.SetActive(false);
        return button;
    }

    /// <summary>
    /// Creates the release page's dependency list without a raw manifest editor.
    /// </summary>
    /// <param name="parent">The release stage.</param>
    /// <returns>The authored dependency presentation.</returns>
    private static ContentEditorDependenciesView BuildDependencies(Transform parent)
    {
        RectTransform root = Layer("Dependencies", parent);
        Rect(root, 0, 100, 650, 164);
        ContentEditorDependenciesView view =
            root.gameObject.AddComponent<ContentEditorDependenciesView>();
        Rect(Label("Heading", root, "Dependencies (optional)", 15).rectTransform, 0, 6, 440, 28);
        Button choose = Button("ChooseDependencies", root, "Choose packages…");
        Rect((RectTransform)choose.transform, 470, 0, 180, 34);
        Assign(view, "choose", choose);
        ScrollAreaLayout scroll = Scroll("SelectedDependencies", root);
        Rect((RectTransform)scroll.transform, 0, 42, 650, 116);
        Assign(view, "scroll", scroll);
        TMP_Text empty = Label(
            "Empty",
            root,
            "No dependencies. Add packages if your pack needs them.",
            14,
            true
        );
        Rect(empty.rectTransform, 12, 52, 600, 28);
        Assign(view, "empty", empty);
        Image row = Surface(
            "DependencyTemplate",
            scroll.ScrollArea.ContentRoot,
            ContentEditorColorRole.Input
        );
        Rect(row.rectTransform, 0, 0, 626, 52);
        ContentEditorDependencyRowView item =
            row.gameObject.AddComponent<ContentEditorDependencyRowView>();
        TMP_Text name = Label("PackageName", row.transform, string.Empty, 15);
        name.overflowMode = TextOverflowModes.Ellipsis;
        Assign(item, "packageName", name);
        TMP_Text author = Label("Author", row.transform, string.Empty, 13, true);
        author.overflowMode = TextOverflowModes.Ellipsis;
        Assign(item, "author", author);
        TMP_Dropdown version = Dropdown("DependencyVersion", row.transform);
        version.template.anchoredPosition = Vector2.zero;
        Assign(item, "version", version);
        Button remove = PlainButton("RemoveDependency", row.transform, "Remove");
        Assign(item, "remove", remove);
        Assign(view, "rowTemplate", item);
        row.gameObject.SetActive(false);
        return view;
    }

    /// <summary>
    /// Creates a reusable crop dialog above its owning workflow.
    /// </summary>
    /// <param name="parent">The owning overlay.</param>
    /// <returns>The authored crop presentation.</returns>
    private static ContentEditorImageCropView BuildImageCrop(Transform parent)
    {
        Image overlay = Surface("ImageCrop", parent, new Color(0, 0, 0, 0.75f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;
        ContentEditorImageCropView view =
            overlay.gameObject.AddComponent<ContentEditorImageCropView>();
        Image panel = Surface("Panel", overlay.transform, ContentEditorColorRole.Panel);
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(920, 712);
        Assign(view, "panel", rect);
        Rect(Label("Heading", rect, "Crop image", 24).rectTransform, 28, 20, 864, 36);
        TextMeshProUGUI description = Label("Description", rect, string.Empty, 14, true);
        Rect(description.rectTransform, 28, 64, 864, 28);
        description.overflowMode = TextOverflowModes.Ellipsis;
        Assign(view, "description", description);
        RawImage artwork = Artwork(rect, "CropFrame", 28, 112, 512);
        artwork.raycastTarget = true;
        Assign(view, "artwork", artwork);
        RectTransform[] shades = new RectTransform[4];
        for (int i = 0; i < shades.Length; i++)
            shades[i] = Surface(
                "OutsideSelection" + i,
                artwork.transform,
                new Color(0, 0, 0, 0.55f)
            ).rectTransform;
        Assign(view, "shades", shades);
        Image selection = Surface("Selection", artwork.transform, Color.clear);
        selection.raycastTarget = true;
        selection.canvasRenderer.cullTransparentMesh = false;
        Assign(view, "selection", selection.rectTransform);
        BuildSelectionBorder(selection.rectTransform);
        TextMeshProUGUI dimensions = Label("OutputDimensions", rect, string.Empty, 16);
        Rect(dimensions.rectTransform, 572, 112, 320, 28);
        Assign(view, "dimensions", dimensions);
        Assign(view, "preview", Artwork(rect, "OutputPreview", 584, 156, 256));
        TextMeshProUGUI quality = Label("Quality", rect, string.Empty, 14, true);
        Rect(quality.rectTransform, 572, 428, 320, 48);
        quality.textWrappingMode = TextWrappingModes.Normal;
        Assign(view, "quality", quality);
        Rect(Label("CropSizeLabel", rect, "Crop size", 15).rectTransform, 572, 488, 160, 28);
        TextMeshProUGUI value = Label("CropSizeValue", rect, "100%", 15, true);
        Rect(value.rectTransform, 812, 488, 80, 28);
        value.alignment = TextAlignmentOptions.MidlineRight;
        Assign(view, "cropSizeValue", value);
        Assign(view, "cropSize", SizeSlider(rect));
        Button reset = PlainButton("ResetCrop", rect, "Reset crop");
        Rect((RectTransform)reset.transform, 572, 570, 140, 34);
        Assign(view, "reset", reset);
        Rect(
            Label(
                "Instructions",
                rect,
                "Move the box or use arrow keys. Drag a corner to resize.",
                14,
                true
            ).rectTransform,
            28,
            632,
            560,
            24
        );
        TextMeshProUGUI error = Label("CropError", rect, string.Empty, 14);
        Rect(error.rectTransform, 28, 666, 560, 32);
        error.textWrappingMode = TextWrappingModes.Normal;
        error.richText = false;
        error.GetComponent<ContentEditorColorBinding>().SetRole(ContentEditorColorRole.Error);
        Assign(view, "error", error);
        Button cancel = Button("CancelCrop", rect, "Cancel");
        Rect((RectTransform)cancel.transform, 620, 652, 120, 36);
        Assign(view, "cancel", cancel);
        Button apply = Button("ApplyCrop", rect, "Use image");
        Rect((RectTransform)apply.transform, 756, 652, 136, 36);
        Assign(view, "apply", apply);
        overlay.gameObject.SetActive(false);
        return view;
    }

    /// <summary>
    /// Marks the selection edges and its draggable corners without hiding the chosen image.
    /// </summary>
    /// <param name="parent">The normalized selection rectangle.</param>
    private static void BuildSelectionBorder(RectTransform parent)
    {
        for (int i = 0; i < 4; i++)
        {
            bool vertical = i < 2;
            float edge = i % 2;
            Image border = Surface("SelectionEdge" + i, parent, Color.white);
            border.rectTransform.anchorMin = vertical ? new Vector2(edge, 0) : new Vector2(0, edge);
            border.rectTransform.anchorMax = vertical ? new Vector2(edge, 1) : new Vector2(1, edge);
            border.rectTransform.offsetMin = vertical ? new Vector2(-1, 0) : new Vector2(0, -1);
            border.rectTransform.offsetMax = vertical ? new Vector2(1, 0) : new Vector2(0, 1);
            Image corner = Surface("ResizeCorner" + i, parent, Color.white);
            corner.rectTransform.anchorMin = corner.rectTransform.anchorMax = new Vector2(
                i % 2,
                i / 2
            );
            corner.rectTransform.anchoredPosition = Vector2.zero;
            corner.rectTransform.sizeDelta = new Vector2(8, 8);
        }
    }

    /// <summary>
    /// Fits artwork into a neutral square without changing the required output aspect ratio.
    /// </summary>
    /// <param name="parent">The dialog panel.</param>
    /// <param name="name">The frame name.</param>
    /// <param name="left">The left coordinate.</param>
    /// <param name="top">The top coordinate.</param>
    /// <param name="size">The frame dimensions.</param>
    /// <returns>The aspect-constrained image.</returns>
    private static RawImage Artwork(Transform parent, string name, int left, int top, int size)
    {
        Image frame = Surface(name, parent, new Color(0.16f, 0.17f, 0.19f, 1));
        Rect(frame.rectTransform, left, top, size, size);
        RawImage image = Layer("Artwork", frame.transform).gameObject.AddComponent<RawImage>();
        Stretch(image.rectTransform);
        image.raycastTarget = false;
        image.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter
            .AspectMode
            .FitInParent;
        return image;
    }

    /// <summary>
    /// Creates a keyboard-accessible selection-size control using the shared palette.
    /// </summary>
    /// <param name="parent">The dialog panel.</param>
    /// <returns>The bounded continuous size slider.</returns>
    private static Slider SizeSlider(Transform parent)
    {
        RectTransform root = Layer("CropSize", parent);
        Rect(root, 572, 526, 320, 28);
        Image hit = root.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        hit.canvasRenderer.cullTransparentMesh = false;
        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.minValue = 0.1f;
        slider.maxValue = 1;
        slider.value = 1;
        Image track = Surface("Track", root, ContentEditorColorRole.Input);
        Stretch(track.rectTransform, 7, 10, 7, 10);
        RectTransform area = Layer("HandleArea", root);
        Stretch(area, 7, 2, 7, 2);
        Image handle = Surface("Handle", area, ContentEditorColorRole.Accent);
        handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(0, 0.5f);
        handle.rectTransform.sizeDelta = new Vector2(14, 0);
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    /// <summary>
    /// Creates navigation, discovery controls, package cards, and the selected-package panel.
    /// </summary>
    /// <param name="parent">The toolkit canvas.</param>
    /// <returns>The authored manager screen.</returns>
    private static ContentEditorModManagerView BuildModManager(Transform parent)
    {
        Image background = Surface("ModManager", parent, ContentEditorColorRole.Background);
        Stretch(background.rectTransform, 0, 76, 0, 40);
        background.raycastTarget = true;
        ContentEditorModManagerView view =
            background.gameObject.AddComponent<ContentEditorModManagerView>();
        RectTransform rect = Layer("Workspace", background.transform);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1120, 816);
        Assign(view, "panel", rect);
        TextMeshProUGUI title = Caption(rect, "Title", "Library", 0, 32, 640, 48, 36);
        title.fontStyle = FontStyles.Bold;
        Assign(view, "pageTitle", title);
        RectTransform actions = ActionRow(rect, 32, 48);
        ActionButton(view, actions, "refreshButton", "Refresh", 0, 0, 128, true);

        ActionButton(view, actions, "importButton", "Install from file…", 0, 0, 208);
        Button cancel = ActionButton(
            view,
            actions,
            "closeButton",
            "Cancel operation",
            0,
            0,
            208,
            true
        );
        cancel.gameObject.SetActive(false);
        RectTransform defaults = Layer("DefaultPack", rect);
        Rect(defaults, 0, 112, 1120, 72);
        RectTransform description = Layer("Description", defaults);
        Rect(description, 0, 0, 600, 72);
        Assign(view, "defaultPackDescription", description);
        Caption(description, "DefaultLabel", "Default content pack", 0, 0, 600, 28, 18);
        Caption(
            description,
            "DefaultHelp",
            "Used when starting the game.",
            0,
            32,
            600,
            24,
            14,
            ContentEditorColorRole.Muted
        );
        foreach (TMP_Text label in description.GetComponentsInChildren<TMP_Text>())
        {
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMax = new Vector2(0, label.rectTransform.offsetMax.y);
        }
        Selector(view, defaults, "activePack", 440, new[] { "No content packs installed" });
        Assign(view, "defaultPackControls", defaults.gameObject);
        TMP_InputField search = Input(
            "SearchPackages",
            rect,
            "Search mods by name, creator, or description"
        );
        Vector2 inset = search.textViewport.offsetMax;
        inset.x = -44;
        search.textViewport.offsetMax = inset;
        search.textComponent.fontSize = 16;
        ((TMP_Text)search.placeholder).fontSize = 16;
        Assign(view, "search", search);
        ActionButton(view, rect, "clearSearchButton", "×", 0, 0, 44, true);
        ActionButton(view, rect, "filterButton", "Filters", 0, 0, 112);
        Assign(view, "sorting", Dropdown("sorting", rect));
        Assign(
            view,
            "resultCount",
            Caption(
                rect,
                "ResultCount",
                string.Empty,
                0,
                280,
                1120,
                24,
                13,
                ContentEditorColorRole.Muted
            )
        );
        ScrollAreaLayout packages = Scroll("Packages", rect, 8, 8);
        Stretch((RectTransform)packages.transform, 0, 248, -16, 0);
        Assign(view, "packages", packages);
        Assign(view, "rowTemplate", BuildCard(packages.ScrollArea.ContentRoot));
        TextMeshProUGUI empty = Caption(
            rect,
            "EmptyResults",
            string.Empty,
            0,
            384,
            1120,
            160,
            20,
            ContentEditorColorRole.Muted
        );
        empty.alignment = TextAlignmentOptions.Center;
        empty.textWrappingMode = TextWrappingModes.Normal;
        empty.gameObject.SetActive(false);
        Assign(view, "emptyResults", empty);
        TextMeshProUGUI status = Caption(
            rect,
            "ManagerStatus",
            string.Empty,
            0,
            752,
            1120,
            48,
            14,
            ContentEditorColorRole.Muted
        );
        status.textWrappingMode = TextWrappingModes.Normal;
        status.rectTransform.anchorMin = Vector2.zero;
        status.rectTransform.anchorMax = Vector2.right;
        status.rectTransform.pivot = Vector2.zero;
        status.rectTransform.offsetMin = Vector2.zero;
        status.rectTransform.offsetMax = new Vector2(0, 48);
        status.gameObject.SetActive(false);
        Assign(view, "status", status);
        BuildFilters(rect, view);
        BuildDetails(background.transform, view);
        background.gameObject.SetActive(false);
        return view;
    }

    /// <summary>
    /// Expands filter choices in the page without covering the result list.
    /// </summary>
    /// <param name="parent">The browse workspace.</param>
    /// <param name="view">The owning manager.</param>
    private static void BuildFilters(Transform parent, ContentEditorModManagerView view)
    {
        Image strip = Surface("FilterPanel", parent, ContentEditorColorRole.Panel);
        Assign(view, "filterPanel", strip.gameObject);
        ContentEditorModFiltersView filters =
            strip.gameObject.AddComponent<ContentEditorModFiltersView>();
        Assign(view, "filters", filters);
        foreach (
            (string Field, string Label) heading in new[]
            {
                (Field: "sectionHeading", Label: "Section"),
                (Field: "installationHeading", Label: "Installation"),
                (Field: "includedHeading", Label: "Include categories"),
                (Field: "excludedHeading", Label: "Exclude categories"),
            }
        )
            Assign(
                filters,
                heading.Field,
                Label(heading.Field, strip.transform, heading.Label, 13, true)
            );
        Selector(filters, strip.transform, "section", 240, new[] { "All sections" });
        Assign(filters, "installation", Dropdown("installation", strip.transform));
        Assign(
            filters,
            "includeCategories",
            MultiSelect(strip.transform, "IncludeCategories", "Any category")
        );
        Assign(
            filters,
            "excludeCategories",
            MultiSelect(strip.transform, "ExcludeCategories", "No exclusions")
        );
        TMP_Text help = Label(
            "CategoryHelp",
            strip.transform,
            "Match any included category.",
            12,
            true
        );
        help.textWrappingMode = TextWrappingModes.NoWrap;
        Assign(filters, "categoryHelp", help);
        foreach (
            (string field, string text) in new[]
            {
                ("showDeprecated", "Show deprecated"),
                ("showNsfw", "Show NSFW"),
            }
        )
        {
            UICheckboxView checkbox = Checkbox(strip.transform);
            checkbox.name = field;
            TMP_Text label = checkbox.GetComponentInChildren<TMP_Text>(true);
            label.text = text;
            label.fontSize = 14;
            Rect(label.rectTransform, 32, 0, 192, 24);
            checkbox.SetIsCheckedWithoutNotify(false);
            Assign(filters, field, checkbox);
        }
        Button clear = ActionButton(
            view,
            strip.transform,
            "resetButton",
            "Clear filters",
            0,
            0,
            176,
            true
        );
        clear.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.MidlineRight;
        Assign(filters, "clearButton", clear);
        filters.SetWidth(1120);
        strip.gameObject.SetActive(false);
    }

    /// <summary>
    /// Authors an artwork-led card whose selection and content can be rebound without reconstruction.
    /// </summary>
    /// <param name="parent">The shared scroll content root.</param>
    /// <returns>The inactive card template.</returns>
    private static ContentEditorModCardView BuildCard(Transform parent)
    {
        Image surface = Surface("PackageCard", parent, ContentEditorColorRole.Panel);
        ContentEditorModCardView card = surface.gameObject.AddComponent<ContentEditorModCardView>();
        RawImage hit = Layer("HitArea", surface.transform).gameObject.AddComponent<RawImage>();
        hit.color = Color.clear;
        Stretch(hit.rectTransform);
        hit.transform.SetAsFirstSibling();
        Assign(card, "hitArea", hit);
        Image cover = Surface("Cover", surface.transform, ContentEditorColorRole.Background);
        Rect(cover.rectTransform, 24, 24, 96, 96);
        TextMeshProUGUI initials = Caption(
            cover.transform,
            "Initials",
            string.Empty,
            0,
            0,
            96,
            96,
            28,
            ContentEditorColorRole.Accent
        );
        initials.alignment = TextAlignmentOptions.Center;
        Assign(card, "initials", initials);
        RawImage artwork = Layer("Artwork", cover.transform).gameObject.AddComponent<RawImage>();
        Stretch(artwork.rectTransform);
        artwork.raycastTarget = false;
        artwork.enabled = false;
        Assign(card, "artwork", artwork);
        TextMeshProUGUI name = Caption(
            surface.transform,
            "Name",
            string.Empty,
            144,
            24,
            680,
            28,
            22
        );
        name.fontStyle = FontStyles.Bold;
        Assign(card, "caption", name);
        TextMeshProUGUI author = Caption(
            surface.transform,
            "Author",
            string.Empty,
            144,
            56,
            680,
            20,
            13,
            ContentEditorColorRole.Muted
        );
        Assign(card, "author", author);
        TextMeshProUGUI description = Caption(
            surface.transform,
            "Summary",
            string.Empty,
            144,
            80,
            680,
            40,
            15
        );
        description.textWrappingMode = TextWrappingModes.Normal;
        description.alignment = TextAlignmentOptions.TopLeft;
        description.maxVisibleLines = 2;
        Assign(card, "description", description);
        TextMeshProUGUI popularity = Caption(
            surface.transform,
            "Popularity",
            string.Empty,
            0,
            80,
            152,
            40,
            13,
            ContentEditorColorRole.Muted
        );
        popularity.rectTransform.anchorMin =
            popularity.rectTransform.anchorMax =
            popularity.rectTransform.pivot =
                Vector2.one;
        popularity.rectTransform.anchoredPosition = new Vector2(-24, -80);
        popularity.alignment = TextAlignmentOptions.TopRight;
        Assign(card, "popularity", popularity);
        foreach (TMP_Text label in new TMP_Text[] { name, author, description })
        {
            RectTransform textRect = label.rectTransform;
            Vector2 minimum = textRect.offsetMin;
            Vector2 maximum = textRect.offsetMax;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = minimum;
            textRect.offsetMax = new Vector2(-216, maximum.y);
        }
        Button action = Button("PrimaryAction", surface.transform, "Install");
        RectTransform actionRect = (RectTransform)action.transform;
        actionRect.anchorMin = actionRect.anchorMax = actionRect.pivot = Vector2.one;
        actionRect.anchoredPosition = new Vector2(-24, -24);
        actionRect.sizeDelta = new Vector2(152, 44);
        BindColor(action.GetComponent<Image>(), ContentEditorColorRole.Accent, action);
        BindColor(action.GetComponentInChildren<TMP_Text>(), ContentEditorColorRole.Background);
        Assign(card, "primaryAction", action);
        surface.gameObject.SetActive(false);
        return card;
    }

    /// <summary>
    /// Creates a palette-aware label at an explicit source position.
    /// </summary>
    /// <param name="parent">The owning panel.</param>
    /// <param name="name">The authored control name.</param>
    /// <param name="text">The visible caption.</param>
    /// <param name="left">The horizontal position.</param>
    /// <param name="top">The vertical position.</param>
    /// <param name="width">The available width.</param>
    /// <param name="height">The available height.</param>
    /// <param name="size">The font size.</param>
    /// <param name="role">The caption's palette role.</param>
    /// <returns>The authored caption.</returns>
    private static TextMeshProUGUI Caption(
        Transform parent,
        string name,
        string text,
        int left,
        int top,
        int width,
        int height,
        int size,
        ContentEditorColorRole role = ContentEditorColorRole.Text
    )
    {
        TextMeshProUGUI label = Label(name, parent, text, size);
        label.GetComponent<ContentEditorColorBinding>().SetRole(role);
        Rect(label.rectTransform, left, top, width, height);
        return label;
    }

    /// <summary>
    /// Authors a shared dropdown with explicit options and a visible current value.
    /// </summary>
    /// <param name="view">The owning manager.</param>
    /// <param name="parent">The owning panel.</param>
    /// <param name="field">The serialized control field.</param>
    /// <param name="width">The control width.</param>
    /// <param name="options">The visible choices in enum order.</param>
    /// <returns>The authored selector.</returns>
    private static TMP_Dropdown Selector(
        Object view,
        Transform parent,
        string field,
        int width,
        string[] options
    )
    {
        TMP_Dropdown dropdown = Dropdown(field, parent);
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(options));
        Rect((RectTransform)dropdown.transform, 0, 0, width, 44);
        dropdown.captionText.alignment = TextAlignmentOptions.MidlineLeft;
        dropdown.template.anchoredPosition = Vector2.zero;
        BindColor(dropdown.template.GetComponent<Image>(), ContentEditorColorRole.Border);
        Assign(view, field, dropdown);
        return dropdown;
    }

    /// <summary>
    /// Authors and binds one manager action using the shared button style.
    /// </summary>
    /// <param name="view">The owning screen.</param>
    /// <param name="parent">The panel transform.</param>
    /// <param name="field">The serialized control field.</param>
    /// <param name="label">The visible action.</param>
    /// <param name="left">The horizontal position.</param>
    /// <param name="top">The vertical position.</param>
    /// <param name="width">The control width.</param>
    /// <param name="plain">Whether this is a text action.</param>
    /// <returns>The authored action button.</returns>
    private static Button ActionButton(
        ContentEditorModManagerView view,
        Transform parent,
        string field,
        string label,
        int left,
        int top,
        int width,
        bool plain = false
    )
    {
        Button button = plain ? PlainButton(field, parent, label) : Button(field, parent, label);
        Rect((RectTransform)button.transform, left, top, width, 44);
        Assign(view, field, button);
        return button;
    }

    /// <summary>
    /// Creates paired project lists using the existing scroll and button controls.
    /// </summary>
    /// <param name="parent">The projects overview's scaled workspace.</param>
    /// <returns>The projects list presentation.</returns>
    private static ContentEditorProjectsView BuildProjects(Transform parent)
    {
        RectTransform root = Layer("ProjectLists", parent);
        ContentEditorProjectsView view = root.gameObject.AddComponent<ContentEditorProjectsView>();
        BuildList(view, root, "Local", "Local projects");
        BuildList(view, root, "Published", "Published packages");
        RectTransform publishedHeader = Layer("PublishedHeader", root);
        Assign(view, "publishedHeader", publishedHeader);
        RectTransform actions = ActionRow(publishedHeader, 0, 36);
        UICheckboxView showDeprecated = Checkbox(actions);
        showDeprecated.name = "ShowDeprecated";
        Rect((RectTransform)showDeprecated.transform, 0, 0, 192, 24);
        TMP_Text caption = showDeprecated.GetComponentInChildren<TMP_Text>(true);
        caption.text = "Show deprecated";
        caption.fontSize = 14;
        Stretch(caption.rectTransform, 32, 0, 0, 0);
        showDeprecated.SetIsCheckedWithoutNotify(false);
        showDeprecated.gameObject.SetActive(false);
        Assign(view, "showDeprecated", showDeprecated);
        Button refresh = PlainButton("RefreshProjects", actions, "Refresh");
        Rect((RectTransform)refresh.transform, 0, 0, 104, 36);
        Assign(view, "refresh", refresh);
        refresh.gameObject.SetActive(false);
        Button connect = PlainButton("ConnectProjects", actions, "Connect Thunderstore…");
        Rect((RectTransform)connect.transform, 0, 0, 192, 36);
        Assign(view, "connect", connect);
        view.SetViewport(new Vector2(1120, 720));
        return view;
    }

    /// <summary>
    /// Authors one column and its reusable metadata row.
    /// </summary>
    /// <param name="view">The paired-list owner.</param>
    /// <param name="parent">The list workspace.</param>
    /// <param name="name">The authored column identity.</param>
    /// <param name="heading">The readable column title.</param>
    private static void BuildList(
        ContentEditorProjectsView view,
        Transform parent,
        string name,
        string heading
    )
    {
        TMP_Text title = Label(name + "Heading", parent, heading, 20);
        Assign(view, name == "Local" ? "localHeading" : "publishedHeading", title);
        TMP_Text status = Label(
            name + "Status",
            parent,
            name == "Local"
                ? "Projects you create or open appear here."
                : "Connect Thunderstore to see your team's published packages.",
            14,
            true
        );
        status.textWrappingMode = TextWrappingModes.Normal;
        status.richText = false;
        Assign(view, name == "Local" ? "localStatus" : "publishedStatus", status);
        ScrollAreaLayout scroll = Scroll(name + "Projects", parent, 8, 8);
        Assign(view, name == "Local" ? "localScroll" : "publishedScroll", scroll);
        Image card = Surface(
            name + "ProjectTemplate",
            scroll.ScrollArea.ContentRoot,
            ContentEditorColorRole.Panel
        );
        Rect(card.rectTransform, 0, 0, 544, ContentEditorProjectRowView.Height);
        ContentEditorProjectRowView row =
            card.gameObject.AddComponent<ContentEditorProjectRowView>();
        foreach ((string, int) entry in new[] { ("title", 17), ("summary", 13), ("detail", 12) })
        {
            TMP_Text label = Label(
                entry.Item1,
                card.transform,
                string.Empty,
                entry.Item2,
                entry.Item1 != "title"
            );
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.richText = false;
            Assign(row, entry.Item1, label);
        }
        Assign(row, "primary", Button("OpenProject", card.transform, "Open"));
        Assign(row, "secondary", PlainButton("ProjectAction", card.transform, "Publish…"));
        Assign(view, name == "Local" ? "localTemplate" : "publishedTemplate", row);
        card.gameObject.SetActive(false);
    }

    /// <summary>
    /// Creates the listing, release, and export-review dialog beneath the editor workbench.
    /// </summary>
    /// <param name="parent">The owning authoring canvas.</param>
    /// <returns>The dialog whose operations are coordinated by the editor controller.</returns>
    private static ContentEditorPublishingView BuildPublishing(Transform parent)
    {
        Image overlay = Surface("Publishing", parent, new Color(0, 0, 0, 0.7f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;
        ContentEditorPublishingView view =
            overlay.gameObject.AddComponent<ContentEditorPublishingView>();
        Image panel = Surface("Panel", overlay.transform, ContentEditorColorRole.Panel);
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1040, 730);
        Assign(view, "_panel", rect);
        Rect(
            Label("Heading", rect, "Prepare a Thunderstore release", 24).rectTransform,
            28,
            20,
            900,
            38
        );
        TextMeshProUGUI identity = Label("Identity", rect, string.Empty, 15, true);
        Rect(identity.rectTransform, 28, 60, 950, 24);
        Assign(view, "_identity", identity);
        string[] names = { "Listing", "Release", "Review" };
        Button[] steps = new Button[names.Length];
        GameObject[] stages = new GameObject[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            steps[i] = PlainButton(names[i] + "Step", rect, i + 1 + ". " + names[i]);
            Rect((RectTransform)steps[i].transform, 28 + i * 150, 96, 140, 36);
            RectTransform stage = Layer(names[i], rect);
            Rect(stage, 28, 150, 650, 440);
            stages[i] = stage.gameObject;
        }
        Assign(view, "_stepButtons", steps);
        Assign(view, "_stages", stages);
        TMP_InputField[] fields = new TMP_InputField[6];
        Transform listing = stages[0].transform;
        fields[0] = Field(listing, "PackageName", "Package name", 0, 36);
        fields[1] = Field(listing, "Description", "Short description", 78, 76);
        fields[1].characterLimit = 250;
        fields[2] = Field(listing, "Website", "Website (optional)", 196, 36);
        fields[3] = Field(listing, "Readme", "Readme (Markdown)", 274, 134);
        Transform release = stages[1].transform;
        fields[5] = Field(release, "Version", "Release version", 0, 36);
        fields[5].readOnly = true;
        TextMeshProUGUI versionHelp = Label(
            "VersionHelp",
            release,
            "Uses the version from your content pack. Edit it in the pack properties.",
            14,
            true
        );
        Rect(versionHelp.rectTransform, 0, 64, 650, 28);
        Assign(view, "_dependencies", BuildDependencies(release));
        fields[4] = Field(release, "Changelog", "Release notes (Markdown)", 282, 126);
        string[] fieldNames =
        {
            "_packageName",
            "_description",
            "_website",
            "_readme",
            "_changelog",
            "_version",
        };
        for (int i = 0; i < fields.Length; i++)
            Assign(view, fieldNames[i], fields[i]);
        Transform reviewStage = stages[2].transform;
        Rect(Label("TeamLabel", reviewStage, "Publishing team", 14).rectTransform, 0, 0, 650, 24);
        TMP_Dropdown team = Dropdown("PublishingTeam", reviewStage);
        Rect((RectTransform)team.transform, 0, 30, 650, 36);
        team.template.anchoredPosition = Vector2.zero;
        Assign(view, "_team", team);
        TextMeshProUGUI review = Label("ReviewText", reviewStage, string.Empty, 16);
        Rect(review.rectTransform, 0, 84, 650, 312);
        review.alignment = TextAlignmentOptions.TopLeft;
        review.textWrappingMode = TextWrappingModes.Normal;
        Assign(view, "_review", review);
        Button export = Button("ExportPackage", reviewStage, "Export ZIP…");
        Rect((RectTransform)export.transform, 0, 410, 200, 36);
        Assign(view, "_exportButton", export);
        Button community = PlainButton("OpenThunderstore", reviewStage, "Open Thunderstore");
        Rect((RectTransform)community.transform, 216, 410, 220, 36);
        Assign(view, "_communityButton", community);
        Button connection = PlainButton("ThunderstoreConnection", rect, "Connect Thunderstore…");
        Rect((RectTransform)connection.transform, 726, 96, 286, 36);
        Assign(view, "_connectionButton", connection);
        Rect(
            Label("PreviewHeading", rect, "Listing preview", 15, true).rectTransform,
            726,
            150,
            286,
            24
        );
        RawImage icon = Layer("Icon", rect).gameObject.AddComponent<RawImage>();
        Rect(icon.rectTransform, 726, 188, 128, 128);
        icon.raycastTarget = false;
        Assign(view, "_icon", icon);
        Button browse = Button("ChooseIcon", rect, "Choose icon...");
        Rect((RectTransform)browse.transform, 726, 330, 180, 36);
        Assign(view, "_browseButton", browse);
        TextMeshProUGUI iconLabel = Label("IconSize", rect, string.Empty, 14, true);
        Rect(iconLabel.rectTransform, 726, 374, 286, 28);
        Assign(view, "_iconLabel", iconLabel);
        TextMeshProUGUI preview = Label("ListingPreview", rect, string.Empty, 16);
        Rect(preview.rectTransform, 726, 418, 286, 184);
        preview.alignment = TextAlignmentOptions.TopLeft;
        preview.textWrappingMode = TextWrappingModes.Normal;
        Assign(view, "_preview", preview);
        TextMeshProUGUI status = Label("PublishingStatus", rect, string.Empty, 15);
        Rect(status.rectTransform, 28, 608, 984, 54);
        status.alignment = TextAlignmentOptions.TopLeft;
        status.richText = false;
        status.textWrappingMode = TextWrappingModes.Normal;
        Assign(view, "_status", status);
        Image progressTrack = Surface("UploadProgress", rect, ContentEditorColorRole.Divider);
        Rect(progressTrack.rectTransform, 28, 664, 984, 3);
        Image progress = Surface("Fill", progressTrack.transform, ContentEditorColorRole.Accent);
        Stretch(progress.rectTransform);
        progress.rectTransform.anchorMax = Vector2.up;
        Assign(view, "_progressTrack", progressTrack.gameObject);
        Assign(view, "_progress", progress);
        progressTrack.gameObject.SetActive(false);
        Button save = Button("SaveDraft", rect, "Save draft");
        Rect((RectTransform)save.transform, 28, 674, 144, 36);
        Assign(view, "_saveButton", save);
        Button close = Button("ClosePublishing", rect, "Close");
        Rect((RectTransform)close.transform, 648, 674, 110, 36);
        Assign(view, "_closeButton", close);
        Button next = Button("ContinuePublishing", rect, "Continue to release");
        Rect((RectTransform)next.transform, 774, 674, 238, 36);
        Assign(view, "_nextButton", next);
        Assign(view, "_imageCrop", BuildImageCrop(overlay.transform));
        overlay.gameObject.SetActive(false);
        return view;
    }

    /// <summary>
    /// Creates a labelled single-line or multiline publishing field with shared text-edit behavior.
    /// </summary>
    /// <param name="parent">The publishing stage.</param>
    /// <param name="name">The authored control name.</param>
    /// <param name="caption">The user-facing field label.</param>
    /// <param name="top">The label's vertical source coordinate.</param>
    /// <param name="height">The input's source height.</param>
    /// <returns>The configured text input.</returns>
    private static TMP_InputField Field(
        Transform parent,
        string name,
        string caption,
        int top,
        int height
    )
    {
        Rect(Label(name + "Label", parent, caption, 14).rectTransform, 0, top, 650, 24);
        TMP_InputField input = Input("Publishing" + name, parent, string.Empty);
        Rect((RectTransform)input.transform, 0, top + 30, 650, height);
        if (height > 36)
        {
            input.lineType = TMP_InputField.LineType.MultiLineNewline;
            input.textComponent.alignment = TextAlignmentOptions.TopLeft;
            input.textComponent.textWrappingMode = TextWrappingModes.Normal;
        }
        return input;
    }

    /// <summary>
    /// Creates the shared account controls independently of a publishing draft.
    /// </summary>
    /// <param name="parent">The application workbench.</param>
    /// <returns>The shared Thunderstore connection dialog.</returns>
    private static ContentEditorThunderstoreConnectionView BuildThunderstoreConnection(
        Transform parent
    )
    {
        Image overlay = Surface("Connection", parent, new Color(0, 0, 0, 0.7f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;
        ContentEditorThunderstoreConnectionView view =
            overlay.gameObject.AddComponent<ContentEditorThunderstoreConnectionView>();
        Image panel = Surface("ConnectionPanel", overlay.transform, ContentEditorColorRole.Panel);
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1040, 740);
        Assign(view, "_panel", rect);
        TMP_Text heading = Label("Heading", rect, "Connect Thunderstore", 24);
        Rect(heading.rectTransform, 28, 20, 984, 36);
        Assign(view, "_heading", heading);
        RectTransform setup = Layer("Setup", rect);
        Rect(setup, 0, 0, 1040, 656);
        Assign(view, "_setup", setup.gameObject);
        Rect(
            Label(
                "Help",
                setup,
                "Create a team token on Thunderstore, then paste it here.",
                15,
                true
            ).rectTransform,
            28,
            64,
            720,
            28
        );
        Button tokenHelp = Button("GetToken", setup, "Open team settings");
        Rect((RectTransform)tokenHelp.transform, 766, 60, 246, 36);
        Assign(view, "_tokenHelpButton", tokenHelp);
        Rect(
            Label(
                "ChooseTeamStep",
                setup,
                "1. Sign in, then choose or create your team.",
                16
            ).rectTransform,
            28,
            116,
            600,
            28
        );
        Screenshot(setup, "Teams", new UnityEngine.Rect(0, 126, 720, 204), 28, 156, 600, 170);
        TextMeshProUGUI accountStep = Label(
            "CreateAccountStep",
            setup,
            "2. Open Service Accounts, then choose Add service account.",
            16
        );
        Rect(accountStep.rectTransform, 28, 350, 600, 40);
        accountStep.textWrappingMode = TextWrappingModes.Normal;
        Screenshot(
            setup,
            "ServiceAccounts",
            new UnityEngine.Rect(0, 135, 720, 240),
            28,
            404,
            600,
            200
        );
        Rect(
            Label(
                "CopyTokenStep",
                setup,
                "3. Create the account and copy its token.",
                16
            ).rectTransform,
            656,
            116,
            356,
            28
        );
        Screenshot(setup, "Token", new UnityEngine.Rect(0, 0, 660, 370), 656, 156, 356, 200);
        TextMeshProUGUI reminder = Label(
            "TokenReminder",
            setup,
            "Give the account a nickname when asked. The token is shown only once; use the copy button beside it.",
            14,
            true
        );
        Rect(reminder.rectTransform, 656, 372, 356, 64);
        reminder.textWrappingMode = TextWrappingModes.Normal;
        Rect(
            Label("PasteTokenStep", setup, "4. Paste the token and connect.", 16).rectTransform,
            656,
            456,
            356,
            28
        );
        TMP_InputField token = Input(
            "ThunderstoreToken",
            setup,
            "Paste your service-account token"
        );
        Rect((RectTransform)token.transform, 656, 492, 356, 40);
        token.contentType = TMP_InputField.ContentType.Password;
        token.characterLimit = 4096;
        Assign(view, "_token", token);
        TextMeshProUGUI session = Label(
            "ConnectionDuration",
            setup,
            "Connect saves your token securely on this device. Disconnect removes it.",
            14,
            true
        );
        Rect(session.rectTransform, 656, 546, 356, 48);
        session.textWrappingMode = TextWrappingModes.Normal;
        Button disconnect = PlainButton("DisconnectThunderstore", rect, "Disconnect");
        Rect((RectTransform)disconnect.transform, 876, 604, 136, 32);
        Assign(view, "_disconnectButton", disconnect);
        Rect(
            Label(
                "ScreenshotCredit",
                setup,
                "Example screenshots: mbround18 / valheim-docker (BSD-3-Clause).",
                11,
                true
            ).rectTransform,
            28,
            626,
            600,
            20
        );
        RectTransform connected = Layer("Connected", rect);
        Rect(connected, 24, 64, 432, 28);
        TMP_Text teams = Label("ConnectedTeams", connected, string.Empty, 17);
        Rect(teams.rectTransform, 0, 0, 432, 28);
        teams.GetComponent<ContentEditorColorBinding>().SetRole(ContentEditorColorRole.Success);
        teams.textWrappingMode = TextWrappingModes.Normal;
        teams.richText = false;
        Assign(view, "_connected", connected.gameObject);
        Assign(view, "_connectedTeams", teams);
        connected.gameObject.SetActive(false);
        TextMeshProUGUI status = Label("ConnectionStatus", rect, string.Empty, 14);
        Rect(status.rectTransform, 28, 656, 680, 60);
        status.textWrappingMode = TextWrappingModes.Normal;
        status.richText = false;
        Assign(view, "_connectionStatus", status);
        Button close = Button("CloseConnection", rect, "Close");
        Rect((RectTransform)close.transform, 744, 680, 120, 36);
        Assign(view, "_connectionCloseButton", close);
        Button connect = Button("ConnectThunderstore", rect, "Connect");
        Rect((RectTransform)connect.transform, 880, 680, 132, 36);
        Assign(view, "_connectButton", connect);
        overlay.gameObject.SetActive(false);
        return view;
    }

    /// <summary>
    /// Shows a focused region of an instructional screenshot without changing its source image.
    /// </summary>
    /// <param name="parent">The connection dialog.</param>
    /// <param name="name">The screenshot asset name.</param>
    /// <param name="crop">The visible source pixels, measured from the top-left corner.</param>
    /// <param name="left">The display's left coordinate.</param>
    /// <param name="top">The display's top coordinate.</param>
    /// <param name="width">The display width.</param>
    /// <param name="height">The display height.</param>
    private static void Screenshot(
        Transform parent,
        string name,
        UnityEngine.Rect crop,
        float left,
        float top,
        float width,
        float height
    )
    {
        Texture2D texture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"Assets/Art/ContentEditor/Thunderstore/{name}.png"
            )
            ?? throw new FileNotFoundException(
                $"Thunderstore guide screenshot '{name}' is missing."
            );
        RawImage image = Layer(name + "Screenshot", parent).gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        image.uvRect = new UnityEngine.Rect(
            crop.x / texture.width,
            1 - crop.yMax / texture.height,
            crop.width / texture.width,
            crop.height / texture.height
        );
        Rect(image.rectTransform, left, top, width, height);
    }

    /// <summary>
    /// Creates focused package details without repeating them beside every browse result.
    /// </summary>
    /// <param name="parent">The owning overlay.</param>
    /// <param name="view">The manager whose actions operate on the selected package.</param>
    private static void BuildDetails(Transform parent, ContentEditorModManagerView view)
    {
        Image overlay = Surface("PackageDetailsOverlay", parent, new Color(0, 0, 0, 0.72f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;
        Assign(view, "detailOverlay", overlay.gameObject);
        RectTransform rect = Surface(
            "PackageDetailsPanel",
            overlay.transform,
            ContentEditorColorRole.Panel
        ).rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(760, 640);
        Assign(view, "detailPanel", rect);
        Button close = ActionButton(view, rect, "detailCloseButton", "Close", 640, 28, 88, true);
        TMP_Text closeLabel = close.GetComponentInChildren<TMP_Text>();
        closeLabel.alignment = TextAlignmentOptions.MidlineRight;
        Stretch(closeLabel.rectTransform);
        TextMeshProUGUI empty = Caption(
            rect,
            "EmptyDetails",
            "This mod is no longer available.",
            32,
            200,
            696,
            100,
            18,
            ContentEditorColorRole.Muted
        );
        empty.alignment = TextAlignmentOptions.Center;
        Assign(view, "emptyDetails", empty);
        RectTransform content = Layer("DetailContent", rect);
        Stretch(content);
        Assign(view, "detailContent", content.gameObject);
        Image cover = Surface("Cover", content, ContentEditorColorRole.Background);
        Rect(cover.rectTransform, 32, 32, 96, 96);
        TextMeshProUGUI initials = Caption(
            cover.transform,
            "IconInitials",
            string.Empty,
            0,
            0,
            96,
            96,
            28,
            ContentEditorColorRole.Accent
        );
        initials.alignment = TextAlignmentOptions.Center;
        Assign(view, "iconInitials", initials);
        RawImage icon = Layer("PackageIcon", cover.transform).gameObject.AddComponent<RawImage>();
        Stretch(icon.rectTransform);
        icon.raycastTarget = false;
        icon.enabled = false;
        Assign(view, "icon", icon);
        TextMeshProUGUI heading = Caption(
            content,
            "PackageName",
            string.Empty,
            152,
            32,
            464,
            36,
            28
        );
        heading.fontStyle = FontStyles.Bold;
        Assign(view, "heading", heading);
        Assign(
            view,
            "creator",
            Caption(
                content,
                "Creator",
                string.Empty,
                152,
                72,
                464,
                24,
                14,
                ContentEditorColorRole.Muted
            )
        );
        Assign(
            view,
            "packageState",
            Caption(
                content,
                "PackageState",
                string.Empty,
                152,
                104,
                464,
                24,
                13,
                ContentEditorColorRole.Accent
            )
        );
        Assign(
            view,
            "popularity",
            Caption(
                content,
                "Popularity",
                string.Empty,
                32,
                144,
                440,
                24,
                13,
                ContentEditorColorRole.Muted
            )
        );
        TextMeshProUGUI updated = Caption(
            content,
            "Updated",
            string.Empty,
            488,
            144,
            240,
            24,
            13,
            ContentEditorColorRole.Muted
        );
        updated.alignment = TextAlignmentOptions.MidlineRight;
        Assign(view, "updated", updated);
        Assign(
            view,
            "categories",
            Caption(
                content,
                "Categories",
                string.Empty,
                32,
                176,
                696,
                24,
                13,
                ContentEditorColorRole.Muted
            )
        );
        ScrollAreaLayout details = Scroll("PackageDetails", content, 8, 8);
        Rect((RectTransform)details.transform, 32, 208, 712, 112);
        TextMeshProUGUI description = Caption(
            details.ScrollArea.ContentRoot,
            "Description",
            string.Empty,
            0,
            0,
            696,
            112,
            16
        );
        description.textWrappingMode = TextWrappingModes.Normal;
        description.alignment = TextAlignmentOptions.TopLeft;
        description.overflowMode = TextOverflowModes.Overflow;
        Assign(view, "details", details);
        Assign(view, "detailText", description);
        TextMeshProUGUI metadata = Caption(
            content,
            "ReleaseMetadata",
            string.Empty,
            32,
            336,
            696,
            24,
            13,
            ContentEditorColorRole.Muted
        );
        metadata.textWrappingMode = TextWrappingModes.Normal;
        Assign(view, "releaseMetadata", metadata);
        TextMeshProUGUI requirement = Caption(
            content,
            "Requirement",
            string.Empty,
            32,
            352,
            696,
            48,
            14,
            ContentEditorColorRole.Error
        );
        requirement.textWrappingMode = TextWrappingModes.Normal;
        requirement.gameObject.SetActive(false);
        Assign(view, "requirement", requirement);
        TMP_Dropdown versions = Selector(
            view,
            content,
            "versions",
            184,
            new[] { "Select a release" }
        );
        Caption(
            versions.transform,
            "VersionLabel",
            "Version",
            0,
            -28,
            184,
            20,
            13,
            ContentEditorColorRole.Muted
        );
        Button install = ActionButton(view, content, "installButton", "Install", 232, 376, 496);
        ContentEditorAppearance.SetActionStyle(install, true);
        Button activate = ActionButton(
            view,
            content,
            "activateButton",
            "Set as default",
            32,
            376,
            280
        );
        ContentEditorAppearance.SetActionStyle(activate, true);
        Button uninstall = ActionButton(
            view,
            content,
            "uninstallButton",
            "Uninstall",
            32,
            600,
            100,
            true
        );
        Button listing = ActionButton(
            view,
            content,
            "listingButton",
            "Thunderstore page",
            32,
            512,
            184,
            true
        );
        Button folder = ActionButton(
            view,
            content,
            "folderButton",
            "Open folder",
            232,
            512,
            200,
            true
        );
        foreach (Button secondary in new[] { uninstall, listing, folder })
        {
            TMP_Text label = secondary.GetComponentInChildren<TMP_Text>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            Stretch(label.rectTransform);
        }
        uninstall.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.MidlineRight;
        BindColor(
            uninstall.GetComponentInChildren<TMP_Text>(),
            ContentEditorColorRole.Error,
            uninstall
        );
        TextMeshProUGUI status = Caption(
            rect,
            "DetailStatus",
            string.Empty,
            32,
            576,
            696,
            48,
            14,
            ContentEditorColorRole.Muted
        );
        status.textWrappingMode = TextWrappingModes.Normal;
        Assign(view, "detailStatus", status);
        overlay.gameObject.SetActive(false);
    }
}
