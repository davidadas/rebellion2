using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class UILayout
{
    public static void SetImage(RawImage image, Texture texture, int x, int y)
    {
        int width = texture == null ? 0 : texture.width;
        int height = texture == null ? 0 : texture.height;
        SetImage(image, texture, x, y, width, height);
    }

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

    public static void SetImageTexture(RawImage image, Texture texture)
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = false;
    }

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

    public static void SetInteractiveImageTexture(RawImage image, Texture texture)
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = texture != null;
    }

    public static void SetTextContent(TextMeshProUGUI text, string value, Color32 color)
    {
        text.text = value ?? string.Empty;
        text.color = color;
        text.raycastTarget = false;
        text.gameObject.SetActive(true);
    }

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

    public static int GetAlignedX(int x, int width, TextAnchor anchor)
    {
        return anchor is TextAnchor.UpperCenter or TextAnchor.MiddleCenter ? x - width / 2 : x;
    }

    public static void SetSourceRect(RectTransform rect, int x, int y, int width, int height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    public static void SetSourcePosition(RectTransform rect, int x, int y)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.localScale = Vector3.one;
    }

    public static void CopySourceRect(RectTransform target, RectTransform source)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.localScale = Vector3.one;
    }

    public static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    public static RectInt GetSourceRect(RectTransform rect)
    {
        return new RectInt(
            Mathf.RoundToInt(rect.anchoredPosition.x),
            Mathf.RoundToInt(-rect.anchoredPosition.y),
            Mathf.RoundToInt(rect.sizeDelta.x),
            Mathf.RoundToInt(rect.sizeDelta.y)
        );
    }

    public static Vector2Int GetFittedImageSize(Texture texture, RectInt slot)
    {
        if (texture.width <= 0 || texture.height <= 0 || slot.width <= 0 || slot.height <= 0)
            return Vector2Int.zero;

        float scale = Mathf.Min(
            1f,
            slot.width / (float)texture.width,
            slot.height / (float)texture.height
        );
        return new Vector2Int(
            Mathf.Max(1, Mathf.RoundToInt(texture.width * scale)),
            Mathf.Max(1, Mathf.RoundToInt(texture.height * scale))
        );
    }

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
