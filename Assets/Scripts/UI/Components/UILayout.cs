using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Applies source-coordinate layout and presentation data to authored UGUI controls.
/// </summary>
public static class UILayout
{
    public const float HdPixelsPerSourceUnit = 4.5f;

    /// <summary>
    /// Assigns an image texture and sizes it from the texture's source dimensions.
    /// </summary>
    /// <param name="image">The image to update.</param>
    /// <param name="texture">The texture to display.</param>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    public static void SetImage(RawImage image, Texture texture, int x, int y)
    {
        Vector2Int size = GetTextureSourceSize(texture);
        int width = size.x;
        int height = size.y;
        SetImage(image, texture, x, y, width, height);
    }

    /// <summary>
    /// Assigns an image texture and explicit source-space bounds.
    /// </summary>
    /// <param name="image">The image to update.</param>
    /// <param name="texture">The texture to display.</param>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    public static void SetImage(
        RawImage image,
        Texture texture,
        int x,
        int y,
        int width,
        int height
    )
    {
        SetImageTexture(image, texture);
        if (texture != null)
            SetSourceRect(image.rectTransform, x, y, width, height);
    }

    /// <summary>
    /// Assigns a noninteractive texture and synchronizes image visibility.
    /// </summary>
    /// <param name="image">The image to update.</param>
    /// <param name="texture">The texture to display.</param>
    public static void SetImageTexture(RawImage image, Texture texture)
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = false;
    }

    /// <summary>
    /// Fits and centers a texture within a source-space slot.
    /// </summary>
    /// <param name="image">The image to update.</param>
    /// <param name="texture">The texture to display.</param>
    /// <param name="slot">The available source-space slot.</param>
    public static void SetCenteredImage(RawImage image, Texture texture, RectInt slot)
    {
        SetImageTexture(image, texture);
        if (texture == null)
            return;

        Vector2Int size = GetFittedImageSize(texture, slot);
        SetSourceRect(
            image.rectTransform,
            slot.x + (slot.width - size.x) / 2,
            slot.y + (slot.height - size.y) / 2,
            size.x,
            size.y
        );
    }

    /// <summary>
    /// Fits and horizontally centers a texture while preserving the slot's top coordinate.
    /// </summary>
    /// <param name="image">The image to update.</param>
    /// <param name="texture">The texture to display.</param>
    /// <param name="slot">The available source-space slot.</param>
    public static void SetHorizontallyCenteredImage(RawImage image, Texture texture, RectInt slot)
    {
        SetImageTexture(image, texture);
        if (texture == null)
            return;

        Vector2Int size = GetFittedImageSize(texture, slot);
        SetSourceRect(
            image.rectTransform,
            slot.x + (slot.width - size.x) / 2,
            slot.y,
            size.x,
            size.y
        );
    }

    /// <summary>
    /// Assigns an interactive texture and synchronizes image visibility and raycasting.
    /// </summary>
    /// <param name="image">The image to update.</param>
    /// <param name="texture">The texture to display.</param>
    public static void SetInteractiveImageTexture(RawImage image, Texture texture)
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = texture != null;
    }

    /// <summary>
    /// Preserves an image's authored right edge while resizing it to a texture.
    /// </summary>
    /// <param name="image">The image to resize.</param>
    /// <param name="texture">The displayed texture.</param>
    public static void SetRightAlignedImageSize(RawImage image, Texture texture)
    {
        if (image == null || texture == null)
            return;

        RectInt authoredRect = GetSourceRect(image.rectTransform);
        Vector2Int size = GetTextureSourceSize(texture);
        if (size.x <= 0 || size.y <= 0)
            return;

        SetSourceRect(
            image.rectTransform,
            authoredRect.x + authoredRect.width - size.x,
            authoredRect.y,
            size.x,
            size.y
        );
    }

    /// <summary>
    /// Assigns dynamic text content without changing authored color, typography, or bounds.
    /// </summary>
    /// <param name="text">The text component to update.</param>
    /// <param name="value">The displayed value.</param>
    public static void SetTextContent(TextMeshProUGUI text, string value)
    {
        text.text = value ?? string.Empty;
        text.raycastTarget = false;
        text.gameObject.SetActive(true);
    }

    /// <summary>
    /// Assigns dynamic text content and color without changing authored typography or bounds.
    /// </summary>
    /// <param name="text">The text component to update.</param>
    /// <param name="value">The displayed value.</param>
    /// <param name="color">The displayed color.</param>
    public static void SetTextContent(TextMeshProUGUI text, string value, Color32 color)
    {
        SetTextContent(text, value);
        text.color = color;
    }

    /// <summary>
    /// Copies authored typography from a template and applies explicit source-space bounds.
    /// </summary>
    /// <param name="text">The text component to update.</param>
    /// <param name="template">The authored typography template.</param>
    /// <param name="value">The displayed value.</param>
    /// <param name="color">The displayed color.</param>
    /// <param name="rect">The displayed source-space bounds.</param>
    public static void SetTemplateText(
        TextMeshProUGUI text,
        TextMeshProUGUI template,
        string value,
        Color32 color,
        RectInt rect
    )
    {
        SetTextContent(text, value, color);
        text.fontSize = template.fontSize;
        text.textWrappingMode = template.textWrappingMode;
        text.overflowMode = template.overflowMode;
        text.maskable = template.maskable;
        text.alignment = template.alignment;
        SetSourceRect(text.rectTransform, rect.x, rect.y, rect.width, rect.height);
    }

    /// <summary>
    /// Copies authored typography and bounds from a template.
    /// </summary>
    /// <param name="text">The text component to update.</param>
    /// <param name="template">The authored typography and bounds template.</param>
    /// <param name="value">The displayed value.</param>
    /// <param name="color">The displayed color.</param>
    public static void SetTemplateText(
        TextMeshProUGUI text,
        TextMeshProUGUI template,
        string value,
        Color32 color
    )
    {
        SetTextContent(text, value, color);
        text.fontSize = template.fontSize;
        text.textWrappingMode = template.textWrappingMode;
        text.overflowMode = template.overflowMode;
        text.maskable = template.maskable;
        text.alignment = template.alignment;
        CopySourceRect(text.rectTransform, template.rectTransform);
    }

    /// <summary>
    /// Wraps multiline text at word boundaries using authored TextMesh Pro metrics.
    /// </summary>
    /// <param name="template">The authored typography used to measure each candidate line.</param>
    /// <param name="text">The source text to wrap.</param>
    /// <param name="maximumWidth">The maximum measured line width.</param>
    /// <returns>The normalized and wrapped display lines.</returns>
    public static List<string> WrapText(TextMeshProUGUI template, string text, int maximumWidth)
    {
        if (template == null)
            throw new ArgumentNullException(nameof(template));
        if (maximumWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumWidth));

        List<string> lines = new List<string>();
        if (string.IsNullOrEmpty(text))
            return lines;

        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] sourceLines = normalized.Split('\n');
        for (int index = 0; index < sourceLines.Length; index++)
            AppendWrappedLine(lines, template, sourceLines[index], maximumWidth);

        return lines;
    }

    /// <summary>
    /// Applies dynamic text content, typography, alignment, and source-space bounds.
    /// </summary>
    /// <param name="textField">The text component to update.</param>
    /// <param name="text">The displayed value.</param>
    /// <param name="x">The source-space horizontal reference position.</param>
    /// <param name="y">The source-space vertical position.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <param name="color">The displayed color.</param>
    /// <param name="fontSize">The source-space font size.</param>
    /// <param name="anchor">The text alignment.</param>
    public static void SetText(
        TextMeshProUGUI textField,
        string text,
        int x,
        int y,
        int width,
        int height,
        Color32 color,
        int fontSize,
        TextAnchor anchor
    )
    {
        textField.text = text ?? string.Empty;
        textField.color = color;
        textField.fontSize = fontSize;
        textField.textWrappingMode = TextWrappingModes.NoWrap;
        textField.overflowMode = TextOverflowModes.Overflow;
        textField.maskable = true;
        textField.raycastTarget = false;
        textField.alignment = GetTextAlignment(anchor);
        textField.gameObject.SetActive(true);
        SetSourceRect(textField.rectTransform, GetAlignedX(x, width, anchor), y, width, height);
    }

    /// <summary>
    /// Converts a horizontal alignment reference into a top-left rectangle coordinate.
    /// </summary>
    /// <param name="x">The horizontal reference position.</param>
    /// <param name="width">The rectangle width.</param>
    /// <param name="anchor">The requested text alignment.</param>
    /// <returns>The rectangle's left coordinate.</returns>
    public static int GetAlignedX(int x, int width, TextAnchor anchor)
    {
        return anchor is TextAnchor.UpperCenter or TextAnchor.MiddleCenter ? x - width / 2 : x;
    }

    /// <summary>
    /// Applies a fixed top-left source-space rectangle.
    /// </summary>
    /// <param name="rect">The transform to update.</param>
    /// <param name="x">The left coordinate.</param>
    /// <param name="y">The top coordinate.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public static void SetSourceRect(RectTransform rect, int x, int y, int width, int height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Applies a fixed top-left source-space position without changing size.
    /// </summary>
    /// <param name="rect">The transform to update.</param>
    /// <param name="x">The left coordinate.</param>
    /// <param name="y">The top coordinate.</param>
    public static void SetSourcePosition(RectTransform rect, int x, int y)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Copies source-space anchors, pivot, position, and size between transforms.
    /// </summary>
    /// <param name="target">The transform to update.</param>
    /// <param name="source">The authored transform to copy.</param>
    public static void CopySourceRect(RectTransform target, RectTransform source)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.localScale = Vector3.one;
    }

    /// <summary>
    /// Stretches a transform to all edges of its parent.
    /// </summary>
    /// <param name="rect">The transform to update.</param>
    public static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Anchors a fixed-height rectangle to the parent's top edge.
    /// </summary>
    /// <param name="rect">The transform to update.</param>
    /// <param name="left">The left inset.</param>
    /// <param name="top">The top inset.</param>
    /// <param name="right">The right inset.</param>
    /// <param name="height">The height.</param>
    public static void SetTopStretchRect(
        RectTransform rect,
        int left,
        int top,
        int right,
        int height
    )
    {
        rect.anchorMin = Vector2.up;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Anchors a fixed-height rectangle to the parent's bottom edge.
    /// </summary>
    /// <param name="rect">The transform to update.</param>
    /// <param name="left">The left inset.</param>
    /// <param name="bottom">The bottom inset.</param>
    /// <param name="right">The right inset.</param>
    /// <param name="height">The height.</param>
    public static void SetBottomStretchRect(
        RectTransform rect,
        int left,
        int bottom,
        int right,
        int height
    )
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.right;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, bottom + height);
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Anchors a fixed-width rectangle to the parent's left edge.
    /// </summary>
    /// <param name="rect">The transform to update.</param>
    /// <param name="left">The left inset.</param>
    /// <param name="top">The top inset.</param>
    /// <param name="bottom">The bottom inset.</param>
    /// <param name="width">The width.</param>
    public static void SetLeftStretchRect(
        RectTransform rect,
        int left,
        int top,
        int bottom,
        int width
    )
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.up;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(left + width, -top);
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Anchors a fixed-width rectangle to the parent's right edge.
    /// </summary>
    /// <param name="rect">The transform to update.</param>
    /// <param name="right">The right inset.</param>
    /// <param name="top">The top inset.</param>
    /// <param name="bottom">The bottom inset.</param>
    /// <param name="width">The width.</param>
    public static void SetRightStretchRect(
        RectTransform rect,
        int right,
        int top,
        int bottom,
        int width
    )
    {
        rect.anchorMin = Vector2.right;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-right - width, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Anchors a fixed-size rectangle to the parent's top-right corner.
    /// </summary>
    /// <param name="rect">The transform to update.</param>
    /// <param name="right">The right inset.</param>
    /// <param name="top">The top inset.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public static void SetTopRightRect(
        RectTransform rect,
        int right,
        int top,
        int width,
        int height
    )
    {
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-right, -top);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Reads a top-left source-space rectangle from an authored transform.
    /// </summary>
    /// <param name="rect">The transform to inspect.</param>
    /// <returns>The source-space rectangle.</returns>
    public static RectInt GetSourceRect(RectTransform rect)
    {
        return new RectInt(
            Mathf.RoundToInt(rect.anchoredPosition.x),
            Mathf.RoundToInt(-rect.anchoredPosition.y),
            Mathf.RoundToInt(rect.sizeDelta.x),
            Mathf.RoundToInt(rect.sizeDelta.y)
        );
    }

    /// <summary>
    /// Resolves the usable source-space dimensions of an authored transform.
    /// </summary>
    /// <param name="rect">The transform to measure.</param>
    /// <returns>The positive source-space dimensions, or zero for an invalid transform.</returns>
    public static Vector2Int GetSourceSize(RectTransform rect)
    {
        if (rect == null)
            return Vector2Int.zero;

        int width = Mathf.RoundToInt(rect.sizeDelta.x);
        int height = Mathf.RoundToInt(rect.sizeDelta.y);
        if (width <= 0)
            width = Mathf.RoundToInt(rect.rect.width);
        if (height <= 0)
            height = Mathf.RoundToInt(rect.rect.height);

        return width > 0 && height > 0 ? new Vector2Int(width, height) : Vector2Int.zero;
    }

    /// <summary>
    /// Converts a pointer event into top-left source-space coordinates within an authored surface.
    /// </summary>
    /// <param name="surface">The source-space surface.</param>
    /// <param name="eventData">The pointer event to convert.</param>
    /// <param name="sourcePosition">Receives the bounded source-space position.</param>
    /// <returns>True when the pointer lies within a surface with valid dimensions.</returns>
    public static bool TryGetSourcePosition(
        RectTransform surface,
        PointerEventData eventData,
        out Vector2Int sourcePosition
    )
    {
        sourcePosition = Vector2Int.zero;
        return eventData != null
            && TryGetSourcePosition(
                surface,
                eventData.position,
                eventData.pressEventCamera,
                out sourcePosition
            );
    }

    /// <summary>
    /// Converts a screen position into top-left source-space coordinates within an authored surface.
    /// </summary>
    /// <param name="surface">The source-space surface.</param>
    /// <param name="screenPosition">The screen-space position to convert.</param>
    /// <param name="camera">The camera associated with the pointer event.</param>
    /// <param name="sourcePosition">Receives the bounded source-space position.</param>
    /// <returns>True when the position lies within a surface with valid dimensions.</returns>
    public static bool TryGetSourcePosition(
        RectTransform surface,
        Vector2 screenPosition,
        Camera camera,
        out Vector2Int sourcePosition
    )
    {
        sourcePosition = Vector2Int.zero;
        if (
            surface == null
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                surface,
                screenPosition,
                camera,
                out Vector2 localPosition
            )
        )
            return false;

        Vector2Int size = GetSourceSize(surface);
        if (size == Vector2Int.zero)
            return false;

        sourcePosition = new Vector2Int(
            Mathf.RoundToInt(localPosition.x + size.x / 2f),
            Mathf.RoundToInt(size.y / 2f - localPosition.y)
        );
        return sourcePosition.x >= 0
            && sourcePosition.x < size.x
            && sourcePosition.y >= 0
            && sourcePosition.y < size.y;
    }

    /// <summary>
    /// Fits a texture within a source-space slot without enlarging it.
    /// </summary>
    /// <param name="texture">The texture to measure.</param>
    /// <param name="slot">The available source-space slot.</param>
    /// <returns>The fitted source-space size.</returns>
    public static Vector2Int GetFittedImageSize(Texture texture, RectInt slot)
    {
        if (
            texture == null
            || texture.width <= 0
            || texture.height <= 0
            || slot.width <= 0
            || slot.height <= 0
        )
            return Vector2Int.zero;

        Vector2Int sourceSize = GetTextureSourceSize(texture);
        if (sourceSize.x <= 0 || sourceSize.y <= 0)
            return Vector2Int.zero;

        float scale = Mathf.Min(
            1f,
            slot.width / (float)sourceSize.x,
            slot.height / (float)sourceSize.y
        );
        return new Vector2Int(
            Mathf.Max(1, Mathf.RoundToInt(sourceSize.x * scale)),
            Mathf.Max(1, Mathf.RoundToInt(sourceSize.y * scale))
        );
    }

    /// <summary>
    /// Converts a texture's pixel dimensions into source-space units.
    /// </summary>
    /// <param name="texture">The texture to measure.</param>
    /// <returns>The source-space texture size.</returns>
    public static Vector2Int GetTextureSourceSize(Texture texture)
    {
        if (texture == null)
            return Vector2Int.zero;

        return new Vector2Int(ToSourceUnits(texture.width), ToSourceUnits(texture.height));
    }

    /// <summary>
    /// Converts a texture's pixel width into source-space units.
    /// </summary>
    /// <param name="texture">The texture to measure.</param>
    /// <returns>The source-space texture width.</returns>
    public static int GetTextureSourceWidth(Texture texture)
    {
        return texture == null ? 0 : ToSourceUnits(texture.width);
    }

    /// <summary>
    /// Converts a texture's pixel height into source-space units.
    /// </summary>
    /// <param name="texture">The texture to measure.</param>
    /// <returns>The source-space texture height.</returns>
    public static int GetTextureSourceHeight(Texture texture)
    {
        return texture == null ? 0 : ToSourceUnits(texture.height);
    }

    /// <summary>
    /// Converts HD texture pixels into source-space units.
    /// </summary>
    /// <param name="texturePixels">The texture dimension in pixels.</param>
    /// <returns>The corresponding source-space dimension.</returns>
    public static int ToSourceUnits(int texturePixels)
    {
        if (texturePixels <= 0)
            return 0;

        return Mathf.Max(1, Mathf.RoundToInt(texturePixels / HdPixelsPerSourceUnit));
    }

    /// <summary>
    /// Creates drag-preview geometry relative to the source rectangle under the pointer.
    /// </summary>
    /// <param name="texture">The preview texture.</param>
    /// <param name="sourceRect">The source-space preview rectangle.</param>
    /// <param name="sourceX">The source-space pointer x-coordinate.</param>
    /// <param name="sourceY">The source-space pointer y-coordinate.</param>
    /// <returns>The drag preview, or null when no drawable preview exists.</returns>
    public static DragPreview CreateDragPreview(
        Texture texture,
        RectInt sourceRect,
        int sourceX,
        int sourceY
    )
    {
        if (texture == null || sourceRect.width <= 0 || sourceRect.height <= 0)
            return null;

        return new DragPreview(
            texture,
            sourceRect.width,
            sourceRect.height,
            sourceX - sourceRect.x,
            sourceY - sourceRect.y
        );
    }

    /// <summary>
    /// Appends one measured source line while preserving tabular rows.
    /// </summary>
    /// <param name="lines">The destination display lines.</param>
    /// <param name="template">The authored typography used for measurement.</param>
    /// <param name="sourceLine">The source line to append.</param>
    /// <param name="maximumWidth">The maximum measured line width.</param>
    private static void AppendWrappedLine(
        List<string> lines,
        TextMeshProUGUI template,
        string sourceLine,
        int maximumWidth
    )
    {
        sourceLine ??= string.Empty;
        if (
            sourceLine.IndexOf('\t') >= 0
            || template.GetPreferredValues(sourceLine).x <= maximumWidth
        )
        {
            lines.Add(sourceLine);
            return;
        }

        string[] words = sourceLine.Split(' ');
        string line = words[0];
        for (int index = 1; index < words.Length; index++)
        {
            string word = words[index];
            string candidate = line.Length == 0 ? word : line + " " + word;
            if (template.GetPreferredValues(candidate).x > maximumWidth)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = candidate;
            }
        }

        if (line.Length > 0)
            lines.Add(line);
    }

    /// <summary>
    /// Maps a Unity text anchor to its TextMesh Pro alignment.
    /// </summary>
    /// <param name="anchor">The Unity text anchor.</param>
    /// <returns>The corresponding TextMesh Pro alignment.</returns>
    private static TextAlignmentOptions GetTextAlignment(TextAnchor anchor)
    {
        return anchor switch
        {
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleRight => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            _ => TextAlignmentOptions.TopLeft,
        };
    }
}
