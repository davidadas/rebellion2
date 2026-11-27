using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies the pack's shared header artwork and placement to an authored planet window.
/// </summary>
public sealed class PlanetWindowHeader : MonoBehaviour
{
    [SerializeField]
    private RawImage title;

    [SerializeField]
    private TextMeshProUGUI caption;

    [SerializeField]
    private RawImagePressVisual openSector;

    [SerializeField]
    private RawImagePressVisual minimize;

    [SerializeField]
    private RawImagePressVisual close;

    private bool captured;
    private Vector2 titleOrigin;
    private Vector2 captionOrigin;
    private Vector2 openSectorOrigin;
    private Vector2 minimizeOrigin;
    private Vector2 closeOrigin;

    public RawImage Title => title;
    public TextMeshProUGUI Caption => caption;
    public RawImagePressVisual OpenSector => openSector;
    public RawImagePressVisual Minimize => minimize;
    public RawImagePressVisual Close => close;

    /// <summary>
    /// Projects each override relative to the authored header, including when a preview is reused.
    /// </summary>
    /// <param name="layout">The pack overrides, if authored.</param>
    /// <param name="assets">The content source for replacement button textures.</param>
    public void Apply(PlanetWindowHeaderLayout layout, IContentAssetSource assets)
    {
        if (!captured)
        {
            titleOrigin = title.rectTransform.anchoredPosition;
            captionOrigin = caption.rectTransform.anchoredPosition;
            openSectorOrigin = ((RectTransform)openSector.transform).anchoredPosition;
            minimizeOrigin = ((RectTransform)minimize.transform).anchoredPosition;
            closeOrigin = ((RectTransform)close.transform).anchoredPosition;
            captured = true;
        }

        SetPosition(title.rectTransform, titleOrigin, layout?.TitleOffset);
        SetPosition(caption.rectTransform, captionOrigin, layout?.CaptionOffset);
        ApplyButton(
            openSector,
            openSectorOrigin,
            layout?.OpenSectorOffset,
            layout?.OpenSectorImagePath,
            layout?.OpenSectorPressedImagePath,
            assets
        );
        ApplyButton(
            minimize,
            minimizeOrigin,
            layout?.MinimizeOffset,
            layout?.MinimizeImagePath,
            layout?.MinimizePressedImagePath,
            assets
        );
        ApplyButton(
            close,
            closeOrigin,
            layout?.CloseOffset,
            layout?.CloseImagePath,
            layout?.ClosePressedImagePath,
            assets
        );
    }

    /// <summary>
    /// Applies a displacement measured right and down in source pixels.
    /// </summary>
    /// <param name="rect">The authored header element.</param>
    /// <param name="origin">Its authored anchored position.</param>
    /// <param name="offset">The optional displacement from that position.</param>
    private static void SetPosition(RectTransform rect, Vector2 origin, SourcePointLayout offset) =>
        rect.anchoredPosition = offset == null ? origin : origin + new Vector2(offset.X, -offset.Y);

    /// <summary>
    /// Positions a header button and loads its current artwork from the active asset store.
    /// </summary>
    /// <param name="button">The authored header button.</param>
    /// <param name="origin">The authored anchored position.</param>
    /// <param name="offset">The optional displacement from that position.</param>
    /// <param name="releasedPath">The optional released artwork override.</param>
    /// <param name="pressedPath">The optional pressed artwork override.</param>
    /// <param name="assets">The active content asset source.</param>
    private static void ApplyButton(
        RawImagePressVisual button,
        Vector2 origin,
        SourcePointLayout offset,
        string releasedPath,
        string pressedPath,
        IContentAssetSource assets
    )
    {
        var (released, pressed) = button
            .GetComponent<ContentPressVisualBinding>()
            .ResolveTextures(assets);
        SetPosition((RectTransform)button.transform, origin, offset);
        button.SetTextures(
            ResolveOverride(assets, releasedPath) ?? released,
            ResolveOverride(assets, pressedPath) ?? pressed
        );
    }

    /// <summary>
    /// Looks up an image when the pack supplies an override address.
    /// </summary>
    /// <param name="assets">The active content asset source.</param>
    /// <param name="path">The optional texture address.</param>
    /// <returns>The replacement texture, if present.</returns>
    private static Texture2D ResolveOverride(IContentAssetSource assets, string path) =>
        string.IsNullOrWhiteSpace(path) ? null : assets.GetTexture(path);
}
