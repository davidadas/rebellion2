using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ContentEditorControlBuilder;

/// <summary>
/// Authors fixed-ratio image framing with a live output preview and explicit confirmation.
/// </summary>
internal static class ContentEditorImageCropBuilder
{
    /// <summary>
    /// Creates a reusable crop dialog above its owning workflow.
    /// </summary>
    /// <param name="parent">The owning overlay.</param>
    /// <returns>The authored crop presentation.</returns>
    internal static ContentEditorImageCropView Build(Transform parent)
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
                "Move the box. Drag a corner to resize.",
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
}
