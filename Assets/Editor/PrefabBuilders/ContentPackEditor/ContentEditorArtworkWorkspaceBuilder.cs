using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ContentEditorControlBuilder;

/// <summary>
/// Authors the complete artwork editing screen with the existing editor palette and controls.
/// </summary>
internal static class ContentEditorArtworkWorkspaceBuilder
{
    /// <summary>
    /// Creates the artwork editor as an opaque screen above the active unit page.
    /// </summary>
    /// <param name="parent">The editor workbench.</param>
    /// <returns>The workspace view.</returns>
    internal static ContentEditorArtworkWorkspace Build(Transform parent)
    {
        Image root = Surface("ArtworkWorkspace", parent, ContentEditorColorRole.Background);
        root.raycastTarget = true;
        Stretch(root.rectTransform);
        ContentEditorArtworkWorkspace view =
            root.gameObject.AddComponent<ContentEditorArtworkWorkspace>();
        RectTransform panel = Layer("Workspace", root.transform);
        Stretch(panel, 17, 14, 17, 14);
        RectTransform header = Surface(
            "ArtworkHeader",
            panel,
            ContentEditorColorRole.Background
        ).rectTransform;
        RectTransform outputs = Surface(
            "OutputBar",
            panel,
            ContentEditorColorRole.Panel
        ).rectTransform;
        RectTransform sidebar = Surface(
            "LayerInspector",
            panel,
            ContentEditorColorRole.Panel
        ).rectTransform;
        RectTransform canvasBar = Surface(
            "CanvasBar",
            panel,
            ContentEditorColorRole.Background
        ).rectTransform;
        RectTransform layerActions = Layer("LayerActions", sidebar);
        Assign(view, "panel", panel);
        Assign(view, "header", header);
        Assign(view, "outputBar", outputs);
        Assign(view, "sidebar", sidebar);
        Assign(view, "canvasBar", canvasBar);
        Assign(view, "layerActions", layerActions);

        Assign(view, "title", Label("ArtworkTitle", header, "", 26));
        Assign(view, "detail", Label("ArtworkUnit", header, "", 15, true));
        Assign(view, "status", Label("ArtworkStatus", header, "", 14, true));
        Assign(view, "back", Secondary("ArtworkBack", header, "‹ Artwork"));
        Assign(view, "save", Button("SaveArtwork", header, "Save artwork"));
        Assign(view, "undo", Secondary("UndoArtwork", header, "Undo"));
        Assign(view, "redo", Secondary("RedoArtwork", header, "Redo"));

        Button[] tabs = new Button[3];
        for (int i = 0; i < tabs.Length; i++)
            tabs[i] = Secondary("ArtworkOutput" + i, outputs, "");
        Assign(view, "outputButtons", tabs);
        Assign(view, "draftButton", Secondary("DraftPreview", outputs, "Draft"));
        Assign(view, "currentButton", Secondary("CurrentPreview", outputs, "Current in pack"));

        Assign(view, "fit", Secondary("FitArtwork", canvasBar, "Fit"));
        Assign(view, "actual", Secondary("ActualPixels", canvasBar, "100%"));
        TMP_Text hint = Label("CanvasStatus", canvasBar, "", 14, true);
        hint.alignment = TextAlignmentOptions.MidlineRight;
        Assign(view, "canvasHint", hint);
        Assign(view, "artworkCanvas", BuildCanvas(panel));
        canvasBar.SetAsLastSibling();

        Assign(view, "layerCaption", Label("LayersHeading", sidebar, "Layers", 18));
        Assign(view, "add", Secondary("AddArtworkLayer", sidebar, "Add layer"));
        Assign(view, "importLayer", Secondary("ImportArtworkLayer", sidebar, "Add image…"));
        ScrollAreaLayout scroll = Scroll("ArtworkLayers", sidebar);
        Assign(view, "layerScroll", scroll);
        Assign(view, "rowTemplate", BuildLayerRow(sidebar));
        Assign(view, "duplicate", Secondary("DuplicateArtworkLayer", layerActions, "Duplicate"));
        Assign(view, "remove", Secondary("RemoveArtworkLayer", layerActions, "Remove"));
        Assign(view, "forward", Secondary("MoveLayerForward", layerActions, "Up"));
        Assign(view, "backward", Secondary("MoveLayerBackward", layerActions, "Down"));
        Assign(view, "positionCaption", Label("PositionHeading", sidebar, "Position", 17));
        TMP_Text scope = Label("PositionScope", sidebar, "", 14, true);
        scope.alignment = TextAlignmentOptions.MidlineRight;
        Assign(view, "scope", scope);
        Assign(view, "xCaption", Label("XCaption", sidebar, "X", 14, true));
        Assign(view, "yCaption", Label("YCaption", sidebar, "Y", 14, true));
        TMP_InputField x = Input("LayerX", sidebar, "");
        TMP_InputField y = Input("LayerY", sidebar, "");
        x.contentType = y.contentType = TMP_InputField.ContentType.IntegerNumber;
        Assign(view, "xInput", x);
        Assign(view, "yInput", y);
        RectTransform scale = Layer("LayerScale", sidebar);
        Assign(view, "scaleControls", scale);
        Assign(view, "scaleCaption", Label("ScaleHeading", scale, "Scale", 17));
        TMP_Text percent = Label("ScalePercent", scale, "", 15);
        percent.alignment = TextAlignmentOptions.Midline;
        Assign(view, "scaleValue", percent);
        Assign(view, "decreaseScale", Secondary("DecreaseLayerScale", scale, "−"));
        Assign(view, "increaseScale", Secondary("IncreaseLayerScale", scale, "+"));
        Assign(view, "rebuild", Secondary("RebuildArtwork", sidebar, "Rebuild artwork"));
        Assign(view, "varyShipWakes", Secondary("VaryShipWakes", sidebar, "Vary engine trails"));
        sidebar.SetAsLastSibling();
        root.gameObject.SetActive(false);
        return view;
    }

    /// <summary>
    /// Creates the clipped artwork stage with reusable backing, foreground and selection outline.
    /// </summary>
    /// <param name="parent">The workspace container.</param>
    /// <returns>The stage's rendering and gesture view.</returns>
    private static ContentEditorArtworkCanvas BuildCanvas(Transform parent)
    {
        Image viewport = Surface("ArtworkCanvas", parent, new Color32(40, 37, 42, 255));
        viewport.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        ContentEditorArtworkCanvas view =
            viewport.gameObject.AddComponent<ContentEditorArtworkCanvas>();
        RectTransform art = Layer("Artwork", viewport.transform);
        art.anchorMin = art.anchorMax = art.pivot = new Vector2(0.5f, 0.5f);
        art.gameObject.AddComponent<RectMask2D>();
        RawImage backing = CanvasImage("Backing", art);
        RawImage subject = CanvasImage("Foreground", art);
        RectTransform selection = Layer("ArtworkSelection", art);
        const float thickness = 1.2f;
        Image top = OutlineEdge("Top", selection);
        Stretch(top.rectTransform);
        top.rectTransform.anchorMin = Vector2.up;
        top.rectTransform.offsetMin = new Vector2(0, -thickness);
        Image bottom = OutlineEdge("Bottom", selection);
        Stretch(bottom.rectTransform);
        bottom.rectTransform.anchorMax = Vector2.right;
        bottom.rectTransform.offsetMax = new Vector2(0, thickness);
        Image left = OutlineEdge("Left", selection);
        Stretch(left.rectTransform);
        left.rectTransform.anchorMax = Vector2.up;
        left.rectTransform.offsetMax = new Vector2(thickness, 0);
        Image right = OutlineEdge("Right", selection);
        Stretch(right.rectTransform);
        right.rectTransform.anchorMin = Vector2.right;
        right.rectTransform.offsetMin = new Vector2(-thickness, 0);
        Assign(view, "artwork", art);
        Assign(view, "backing", backing);
        Assign(view, "subject", subject);
        Assign(view, "selection", selection);
        selection.gameObject.SetActive(false);
        return view;
    }

    /// <summary>
    /// Creates a nonblocking line visible against the artwork and dark canvas.
    /// </summary>
    /// <param name="name">The side of the selected layer.</param>
    /// <param name="parent">The bounds rectangle.</param>
    /// <returns>The selection line.</returns>
    private static Image OutlineEdge(string name, Transform parent) =>
        Surface(name, parent, new Color32(255, 185, 150, 255));

    /// <summary>
    /// Creates one noninteractive, full-frame artwork image.
    /// </summary>
    /// <param name="name">The layer object's name.</param>
    /// <param name="parent">The composition canvas.</param>
    /// <returns>The artwork image.</returns>
    private static RawImage CanvasImage(string name, Transform parent)
    {
        RawImage image = Layer(name, parent).gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        Stretch(image.rectTransform);
        return image;
    }

    /// <summary>
    /// Authors the pooled layer row and its visibility and selection targets.
    /// </summary>
    /// <param name="parent">The workspace inspector.</param>
    /// <returns>The inactive row template.</returns>
    private static ContentEditorArtworkLayerRow BuildLayerRow(Transform parent)
    {
        RectTransform root = Layer("ArtworkLayerRow", parent);
        ContentEditorArtworkLayerRow row =
            root.gameObject.AddComponent<ContentEditorArtworkLayerRow>();
        Button visibility = Secondary("LayerVisibility", root, "Hide");
        RectTransform visibleRect = (RectTransform)visibility.transform;
        Stretch(visibleRect);
        visibleRect.anchorMax = Vector2.up;
        visibleRect.offsetMax = new Vector2(53, 0);
        Button select = Secondary("SelectLayer", root, "");
        Stretch((RectTransform)select.transform, 59);
        TMP_Text caption = select.GetComponentInChildren<TMP_Text>();
        Stretch(caption.rectTransform, 66, 0, 12);
        caption.alignment = TextAlignmentOptions.MidlineLeft;
        Image chip = Surface("ThumbnailBacking", select.transform, new Color32(34, 31, 37, 255));
        RectTransform chipRect = chip.rectTransform;
        Stretch(chipRect, 7, 7, 0, 7);
        chipRect.anchorMax = Vector2.up;
        chipRect.offsetMax = new Vector2(60, -7);
        RawImage thumb = CanvasImage("Thumbnail", chip.transform);
        AspectRatioFitter fitter = thumb.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        Assign(row, "select", select);
        Assign(row, "visibility", visibility);
        Assign(row, "caption", caption);
        Assign(row, "visibilityCaption", visibility.GetComponentInChildren<TMP_Text>());
        Assign(row, "thumbnail", thumb);
        root.gameObject.SetActive(false);
        return row;
    }

    /// <summary>
    /// Styles a secondary command with readable text and a quiet surface in every editor palette.
    /// </summary>
    /// <param name="name">The control's name.</param>
    /// <param name="parent">The owning surface.</param>
    /// <param name="text">The visible label.</param>
    /// <returns>The interactive command.</returns>
    private static Button Secondary(string name, Transform parent, string text)
    {
        Button button = Button(name, parent, text);
        button
            .targetGraphic.GetComponent<ContentEditorColorBinding>()
            .SetRole(ContentEditorColorRole.Background);
        button
            .GetComponentInChildren<TMP_Text>()
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(ContentEditorColorRole.Text);
        return button;
    }
}
