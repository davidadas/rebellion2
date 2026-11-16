using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ContentEditorControlBuilder;

/// <summary>
/// Authors the library from the editor's shared buttons, inputs, lists, dialogs and pane layout.
/// </summary>
internal static class ContentLibraryPrefabBuilder
{
    /// <summary>
    /// Replaces the empty-project welcome with a full installed-content library.
    /// </summary>
    /// <param name="parent">The editor workbench.</param>
    /// <param name="owner">The application view retaining its authoring entry points.</param>
    internal static void Build(Transform parent, ContentPackEditorView owner)
    {
        RectTransform root = Layer("ContentLibrary", parent);
        Stretch(root, 0, 36);
        ContentLibraryView view = root.gameObject.AddComponent<ContentLibraryView>();
        Assign(owner, "welcomePane", root.gameObject);
        Assign(owner, "libraryView", view);
        TextMeshProUGUI title = Label("LibraryTitle", root, "Mods", 28);
        Rect(title.rectTransform, 20, 12, 260, 44);
        Button create = Button("NewPack", root, "New content pack");
        Right((RectTransform)create.transform, 404, 16, 180, 36);
        Button open = Button("OpenPack", root, "Open content pack");
        Right((RectTransform)open.transform, 208, 16, 180, 36);
        Assign(owner, "newPackButton", create);
        Assign(owner, "openPackButton", open);

        RectTransform body = Layer("LibraryBody", root);
        Stretch(body, 0, 70, 0, 32);
        Button installed = PlainButton("Installed", body, "Installed");
        Rect((RectTransform)installed.transform, 12, 0, 100, 34);
        Button browse = PlainButton("Browse", body, "Browse");
        Rect((RectTransform)browse.transform, 116, 0, 100, 34);
        Button updates = PlainButton("Updates", body, "Updates");
        Rect((RectTransform)updates.transform, 220, 0, 100, 34);
        Button refresh = Button("RefreshLibrary", body, "Check updates");
        Right((RectTransform)refresh.transform, 172, 0, 144, 32);
        Button resume = PlainButton("ResumeEditor", body, "Back to editor");
        Right((RectTransform)resume.transform, 344, 0, 160, 32);
        resume.gameObject.SetActive(false);
        Button cancel = Button("CancelLibraryOperation", body, "Cancel");
        Right((RectTransform)cancel.transform, 172, 0, 144, 32);
        cancel.gameObject.SetActive(false);

        RectTransform explorer = Layer("LibraryExplorer", body);
        Image background = Surface(
            "LibraryNavigationBackground",
            body,
            ContentEditorColorRole.Panel
        );
        background.transform.SetAsFirstSibling();
        TMP_InputField search = Input("LibrarySearch", explorer, "Search mods…");
        Stretch((RectTransform)search.transform, 0, 0, 80, 0);
        search.transform.GetComponent<RectTransform>().anchorMin = Vector2.up;
        search.transform.GetComponent<RectTransform>().offsetMin = new Vector2(0, -34);
        Button searchButton = Button("SearchLibrary", explorer, "Search");
        Right((RectTransform)searchButton.transform, 72, 0, 72, 34);
        ScrollAreaLayout scroll = Scroll("LibraryItems", explorer);
        Stretch((RectTransform)scroll.transform, 0, 48, 0, 38);
        ContentLibraryRowView row = BuildRow(scroll.ScrollArea.ContentRoot);
        row.gameObject.SetActive(false);
        TextMeshProUGUI empty = Label("EmptyLibrary", explorer, string.Empty, 15, true);
        Stretch(empty.rectTransform, 10, 70, 16, 54);
        empty.alignment = TextAlignmentOptions.TopLeft;
        empty.textWrappingMode = TextWrappingModes.Normal;
        Button previous = PlainButton("PreviousLibraryPage", explorer, "Previous");
        Bottom((RectTransform)previous.transform, 0, 0, 112, 30);
        Button next = PlainButton("NextLibraryPage", explorer, "Next");
        Bottom((RectTransform)next.transform, 120, 0, 112, 30);

        Image divider = Surface("LibraryDivider", body, ContentEditorColorRole.Transparent);
        divider.raycastTarget = true;
        divider.canvasRenderer.cullTransparentMesh = false;
        Image line = Surface("Line", divider.transform, ContentEditorColorRole.Divider);
        Stretch(line.rectTransform, 4, 0, 4, 0);
        RectTransform inspector = Layer("LibraryInspector", body);
        ContentEditorWorkbenchLayout layout =
            body.gameObject.AddComponent<ContentEditorWorkbenchLayout>();
        Assign(layout, "explorer", explorer);
        Assign(layout, "inspector", inspector);
        Assign(layout, "divider", divider.rectTransform);
        Assign(layout, "navigationBackground", background.rectTransform);
        SplitterView splitter = divider.gameObject.AddComponent<SplitterView>();
        splitter.Configure(body);
        Assign(layout, "splitter", splitter);
        layout.SetSplit(0.28f);

        TextMeshProUGUI detailTitle = Label("LibraryItemTitle", inspector, string.Empty, 28);
        Top(detailTitle.rectTransform, 0, 0, 0, 44);
        AddTooltip(detailTitle);
        TextMeshProUGUI author = Label("LibraryItemAuthor", inspector, string.Empty, 14, true);
        Top(author.rectTransform, 0, 48, 0, 30);
        RectTransform imageRoot = Layer("LibraryItemImage", inspector);
        Rect(imageRoot, 0, 92, 224, 126);
        Image imageBackground = Surface("ImageBackground", imageRoot, ContentEditorColorRole.Panel);
        Stretch(imageBackground.rectTransform);
        RawImage image = Layer("Image", imageRoot).gameObject.AddComponent<RawImage>();
        Stretch(image.rectTransform);
        image.enabled = false;
        image.raycastTarget = false;
        TextMeshProUGUI placeholder = Label(
            "ImagePlaceholder",
            imageRoot,
            "No image supplied",
            13,
            true
        );
        Stretch(placeholder.rectTransform, 12, 0, 12);
        placeholder.alignment = TextAlignmentOptions.Center;
        RemoteImageView remoteImage = imageRoot.gameObject.AddComponent<RemoteImageView>();
        Assign(remoteImage, "image", image);
        Assign(remoteImage, "placeholder", placeholder.gameObject);

        ScrollAreaLayout details = Scroll("LibraryDescriptionScroll", inspector);
        Stretch((RectTransform)details.transform, 0, 240, 0, 64);
        TextMeshProUGUI description = Label(
            "LibraryItemDescription",
            details.ScrollArea.ContentRoot,
            string.Empty,
            15
        );
        Rect(description.rectTransform, 0, 0, 700, 200);
        description.alignment = TextAlignmentOptions.TopLeft;
        description.textWrappingMode = TextWrappingModes.Normal;
        TextMeshProUGUI metadata = Label(
            "LibraryItemMetadata",
            details.ScrollArea.ContentRoot,
            string.Empty,
            14,
            true
        );
        Rect(metadata.rectTransform, 0, 220, 700, 120);
        metadata.alignment = TextAlignmentOptions.TopLeft;
        metadata.textWrappingMode = TextWrappingModes.Normal;
        Button install = Button("InstallLibraryItem", inspector, "Install");
        Bottom((RectTransform)install.transform, 0, 0, 140, 36);
        Button folder = Button("OpenLibraryItemFolder", inspector, "Open folder");
        Bottom((RectTransform)folder.transform, 160, 0, 140, 36);
        Button remove = Button("RemoveLibraryItem", inspector, "Remove");
        Bottom((RectTransform)remove.transform, 320, 0, 140, 36);
        RectTransform actions = Layer("LibraryActions", inspector);
        actions.anchorMin = Vector2.zero;
        actions.anchorMax = Vector2.right;
        actions.offsetMin = Vector2.zero;
        actions.offsetMax = new Vector2(0, 38);
        HorizontalLayoutGroup actionLayout =
            actions.gameObject.AddComponent<HorizontalLayoutGroup>();
        actionLayout.spacing = 12;
        actionLayout.childControlWidth = true;
        actionLayout.childControlHeight = true;
        actionLayout.childForceExpandWidth = false;
        actionLayout.childForceExpandHeight = true;
        foreach (Button button in new[] { install, folder, remove })
        {
            button.transform.SetParent(actions, false);
            LayoutElement size = button.gameObject.AddComponent<LayoutElement>();
            size.minWidth = 90;
            size.preferredWidth = 140;
        }
        TextMeshProUGUI status = Label("LibraryStatus", root, string.Empty, 13, true);
        status.rectTransform.anchorMin = Vector2.zero;
        status.rectTransform.anchorMax = Vector2.right;
        status.rectTransform.offsetMin = new Vector2(20, 0);
        status.rectTransform.offsetMax = new Vector2(-20, 30);
        AddTooltip(status);

        Assign(view, "installedButton", installed);
        Assign(view, "browseButton", browse);
        Assign(view, "updatesButton", updates);
        Assign(view, "refreshButton", refresh);
        Assign(view, "searchButton", searchButton);
        Assign(view, "search", search);
        Assign(view, "scroll", scroll);
        Assign(view, "rowTemplate", row);
        Assign(view, "empty", empty);
        Assign(view, "status", status);
        Assign(view, "detail", inspector.gameObject);
        Assign(view, "detailTitle", detailTitle);
        Assign(view, "detailAuthor", author);
        Assign(view, "detailDescription", description);
        Assign(view, "detailMetadata", metadata);
        Assign(view, "detailScroll", details);
        Assign(view, "layout", layout);
        Assign(view, "image", remoteImage);
        Assign(view, "installButton", install);
        Assign(view, "removeButton", remove);
        Assign(view, "folderButton", folder);
        Assign(view, "cancelButton", cancel);
        Assign(view, "previousButton", previous);
        Assign(view, "nextButton", next);
        Assign(view, "resumeButton", resume);
    }

    /// <summary>
    /// Authors a metadata row using the same palette and shared list contract as the content tree.
    /// </summary>
    /// <param name="parent">The shared scroll content.</param>
    /// <returns>The reusable inactive row template.</returns>
    private static ContentLibraryRowView BuildRow(Transform parent)
    {
        RectTransform root = Layer("ContentLibraryRowTemplate", parent);
        Rect(root, 0, 0, 360, 88);
        RawImage hit = root.gameObject.AddComponent<RawImage>();
        BindColor(hit, ContentEditorColorRole.Transparent);
        ContentLibraryRowView view = root.gameObject.AddComponent<ContentLibraryRowView>();
        TextMeshProUGUI title = Label("Name", root, string.Empty, 15);
        Top(title.rectTransform, 10, 8, 10, 24);
        AddTooltip(title);
        TextMeshProUGUI summary = Label("Summary", root, string.Empty, 13, true);
        Top(summary.rectTransform, 10, 34, 10, 20);
        AddTooltip(summary);
        TextMeshProUGUI status = Label("Status", root, string.Empty, 12, true);
        Top(status.rectTransform, 10, 59, 10, 20);
        Assign(view, "hitArea", hit);
        Assign(view, "title", title);
        Assign(view, "summary", summary);
        Assign(view, "status", status);
        return view;
    }

    /// <summary>
    /// Reuses the standard overflow tooltip without changing authored font sizes.
    /// </summary>
    /// <param name="text">The bounded label.</param>
    private static void AddTooltip(TextMeshProUGUI text)
    {
        RectTransform original = text.rectTransform;
        RectTransform wrapper = Layer(text.name + "HitArea", original.parent);
        wrapper.anchorMin = original.anchorMin;
        wrapper.anchorMax = original.anchorMax;
        wrapper.pivot = original.pivot;
        wrapper.sizeDelta = original.sizeDelta;
        wrapper.anchoredPosition = original.anchoredPosition;
        Image surface = wrapper.gameObject.AddComponent<Image>();
        surface.color = Color.clear;
        text.transform.SetParent(wrapper, false);
        Stretch(text.rectTransform);
        OverflowTextTooltip tooltip = text.gameObject.AddComponent<OverflowTextTooltip>();
        tooltip.Configure(text);
        wrapper.gameObject.AddComponent<OverflowTextTooltipTrigger>().Configure(tooltip);
    }

    /// <summary>
    /// Anchors a control to its pane's right edge.
    /// </summary>
    /// <param name="rect">The control.</param>
    /// <param name="right">The distance from its left edge to the pane's right edge.</param>
    /// <param name="top">The top offset.</param>
    /// <param name="width">The fixed width.</param>
    /// <param name="height">The fixed height.</param>
    private static void Right(RectTransform rect, float right, float top, float width, float height)
    {
        Rect(rect, 0, top, width, height);
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.anchoredPosition = new Vector2(-right, -top);
    }

    /// <summary>
    /// Stretches a fixed-height label across the top of its pane.
    /// </summary>
    /// <param name="rect">The label.</param>
    /// <param name="left">The left inset.</param>
    /// <param name="top">The top offset.</param>
    /// <param name="right">The right inset.</param>
    /// <param name="height">The fixed height.</param>
    private static void Top(RectTransform rect, float left, float top, float right, float height)
    {
        Stretch(rect, left, top, right, 0);
        rect.anchorMin = Vector2.up;
        rect.offsetMin = new Vector2(left, -top - height);
    }

    /// <summary>
    /// Anchors a control to the pane's bottom edge.
    /// </summary>
    /// <param name="rect">The control.</param>
    /// <param name="left">The left offset.</param>
    /// <param name="bottom">The bottom inset.</param>
    /// <param name="width">The fixed width.</param>
    /// <param name="height">The fixed height.</param>
    private static void Bottom(
        RectTransform rect,
        float left,
        float bottom,
        float width,
        float height
    )
    {
        Rect(rect, left, 0, width, height);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.anchoredPosition = new Vector2(left, bottom + height);
    }
}
