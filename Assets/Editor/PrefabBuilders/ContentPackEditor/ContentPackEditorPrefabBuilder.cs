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
            typeof(StandaloneInputModule),
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
        menuStrip.rectTransform.offsetMin = new Vector2(0, -36);
        string[] headings = { "File", "Edit", "View", "Test" };
        string[] members = { "fileButton", "editButton", "viewButton", "testButton" };
        MenuBarHeadingView[] menuHeadings = new MenuBarHeadingView[headings.Length];
        for (int i = 0; i < headings.Length; i++)
        {
            Button button = PlainButton(headings[i], menuStrip.transform, headings[i]);
            Rect((RectTransform)button.transform, 12 + i * 62, 0, 60, 34);
            Assign(view, members[i], button);
            MenuBarHeadingView menuHeading = button.gameObject.AddComponent<MenuBarHeadingView>();
            menuHeadings[i] = menuHeading;
        }
        Assign(view, "menuHeadings", menuHeadings);
        RectTransform projectWorkspace = Layer("ProjectWorkspace", canvasRoot);
        Stretch(projectWorkspace);
        Assign(view, "projectWorkspace", projectWorkspace.gameObject);
        TextMeshProUGUI project = Label(
            "PackName",
            menuStrip.transform,
            "No content pack open",
            14,
            true
        );
        Stretch(project.rectTransform, 284, 0, 24, 0);
        project.alignment = TextAlignmentOptions.MidlineRight;
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
        Stretch(explorer, 20, 54, 0, 20);
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
        Button delete = PlainButton("Delete", editor, "Delete");
        Rect((RectTransform)delete.transform, 0, 36, 84, 32);
        ((RectTransform)delete.transform).anchorMin = ((RectTransform)delete.transform).anchorMax =
            Vector2.one;
        ((RectTransform)delete.transform).anchoredPosition = new Vector2(-84, -36);
        Assign(view, "duplicateButton", duplicate);
        Assign(view, "deleteButton", delete);
        TextMeshProUGUI title = Label("RecordName", editor, "Create or open a content pack", 28);
        Stretch(title.rectTransform, 0, 0, 230, 0);
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

        TextMeshProUGUI copyright = Label("Copyright", canvasRoot, "© 2026 David Adams", 12, true);
        copyright.alignment = TextAlignmentOptions.MidlineRight;
        copyright.rectTransform.anchorMin = Vector2.zero;
        copyright.rectTransform.anchorMax = Vector2.right;
        copyright.rectTransform.pivot = Vector2.zero;
        copyright.rectTransform.offsetMin = new Vector2(20, 2);
        copyright.rectTransform.offsetMax = new Vector2(-24, 26);

        BuildWelcome(canvasRoot, view);
        BuildPackDialog(canvasRoot, view);
        BuildSelectionDialog(canvasRoot, view);
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
        Stretch((RectTransform)input.transform, 244, 8, 14, 22);
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
        Stretch(welcome, 0, 36);
        RectTransform actions = Layer("Actions", welcome);
        actions.anchorMin = actions.anchorMax = actions.pivot = new Vector2(0.5f, 0.5f);
        actions.sizeDelta = new Vector2(600, 260);
        TextMeshProUGUI title = Label("Title", actions, "Content pack editor", 30);
        Rect(title.rectTransform, 0, 0, 600, 48);
        title.alignment = TextAlignmentOptions.Midline;
        TextMeshProUGUI explanation = Label(
            "Description",
            actions,
            "Create your own content pack using an installed pack as a base.\nInstalled content stays unchanged.",
            16,
            true
        );
        Rect(explanation.rectTransform, 0, 66, 600, 62);
        explanation.alignment = TextAlignmentOptions.Center;
        explanation.textWrappingMode = TextWrappingModes.Normal;
        Button create = Button("NewPack", actions, "New content pack");
        Rect((RectTransform)create.transform, 54, 158, 236, 40);
        Button open = Button("OpenPack", actions, "Open content pack");
        Rect((RectTransform)open.transform, 310, 158, 236, 40);
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
        Stretch(text.rectTransform, 20, 8, 110, 8);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.overflowMode = TextOverflowModes.Overflow;
        text.textWrappingMode = TextWrappingModes.Normal;
        Button close = Button("Dismiss", notice.transform, "Close");
        Rect((RectTransform)close.transform, 550, 10, 60, 30);
        Assign(view, "notice", notice.gameObject);
        Assign(view, "noticeText", text);
        Assign(view, "dismissNoticeButton", close);
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
        panel.rectTransform.sizeDelta = new Vector2(440, 156);
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
        Rect(heading.rectTransform, 66, 8, 350, 48);
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
        Assign(preview, "image", image);
        Assign(preview, "caption", caption);
        Assign(preview, "closeButton", close);
        Assign(view, "imagePreview", preview);
        overlay.gameObject.SetActive(false);
    }
}
