using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Samples unit artwork backdrops from the gameplay windows and their authored image slots.
/// </summary>
internal static class ContentEditorGameplayArtworkBackground
{
    /// <summary>
    /// Resolves the first Fleet card, selected-ship banner, Status image, or battle-result slot.
    /// </summary>
    /// <param name="windows">The toolkit's existing gameplay-window prefab copies.</param>
    /// <param name="assets">The working pack's resolved assets.</param>
    /// <param name="theme">The unit faction's effective theme.</param>
    /// <param name="field">The normal or status image whose gameplay context is being previewed.</param>
    /// <param name="readable">Whether the backdrop will be sampled by the artwork generator.</param>
    /// <param name="imageSize">The generated output size, or null to measure the current artwork.</param>
    /// <returns>The gameplay texture and the region behind the represented image.</returns>
    internal static (Texture2D Texture, Rect Uv) Resolve(
        Transform windows,
        ContentAssets assets,
        FactionTheme theme,
        ContentPackEditorField field,
        bool readable = false,
        Vector2Int? imageSize = null
    )
    {
        bool capitalShip = field.Record.Kind == "CapitalShip";
        bool ship = field.Record.Kind is "CapitalShip" or "Starfighter";
        if (!ship && field.MemberName != "DisplayImagePath")
            return default;
        Transform window;
        RectTransform backdrop;
        RectInt slot;
        string address;
        bool fitImage = false;
        bool nativeBackdropSize = false;
        switch (field.MemberName)
        {
            case "BattleResultImagePath":
            case "BattleResultInTransitImagePath":
            case "BattleResultDamagedImagePath":
                window = Require(windows, "BattleAlertWindow");
                backdrop = Require(window, "PanelBackgroundImage");
                RectTransform column = Find(window, "ResultStandardOperationalColumn");
                RectTransform result = Find(window, "ResultStandardItemTemplate");
                slot = Bounds(column, window);
                slot.position -= UILayout.GetSourceRect((RectTransform)column.parent).position;
                RectInt image = UILayout.GetSourceRect(Require(result, "ImageSlot"));
                slot = new RectInt(slot.x + image.x, slot.y + image.y, image.width, image.height);
                BattleAlertWindowTheme battle = theme?.StrategyWindows?.BattleAlert;
                address = !string.IsNullOrWhiteSpace(battle?.ResultListBackgroundImagePath)
                    ? battle.ResultListBackgroundImagePath
                    : battle?.SummaryBackgroundImagePath;
                fitImage = true;
                break;
            case "SmallDisplayImagePath":
            case "InTransitSmallImagePath":
            case "DamagedSmallImagePath":
                window = Require(windows, "FleetWindow");
                RectTransform row = Find(window, "FleetDetailItemTemplate");
                RectTransform entity = Require(row, "EntityImage");
                slot = Bounds(entity, window);
                // The runtime vertical layout starts its first row at the content origin.
                RectInt rowBounds = UILayout.GetSourceRect(row);
                slot.position -=
                    rowBounds.position + UILayout.GetSourceRect((RectTransform)row.parent).position;
                if (capitalShip)
                    slot.y -= 1;
                backdrop = Require(window, "DetailBackgroundImage");
                address = theme?.StrategyWindows?.Fleet?.DetailBackgroundImagePath;
                fitImage = true;
                break;
            case "DisplayImagePath":
            case "InTransitImagePath":
            case "DamagedImagePath":
                window = Require(windows, capitalShip ? "FleetWindow" : "StatusWindow");
                backdrop = Require(
                    window,
                    capitalShip ? "DetailBackgroundImage" : "BackgroundImage"
                );
                slot = Bounds(
                    capitalShip
                        ? Require(window, "BannerImage")
                        : Find(window, "StatusImageAreaTemplate"),
                    window
                );
                address = capitalShip
                    ? theme?.StrategyWindows?.Fleet?.DetailBackgroundImagePath
                    : theme?.StrategyWindows?.Status?.BackgroundImagePath;
                fitImage = !capitalShip;
                nativeBackdropSize = !capitalShip;
                break;
            default:
                throw new ArgumentException("The image has no gameplay preview.", nameof(field));
        }
        if (string.IsNullOrWhiteSpace(address))
            return default;
        Texture2D texture = readable
            ? assets.GetReadableTexture(address)
            : assets.GetTexture(address);
        if (texture == null)
            return default;
        if (fitImage)
        {
            Vector2Int pixels;
            if (imageSize.HasValue)
                pixels = imageSize.Value;
            else if (
                !ContentPackEditorFieldMetadata.TryGetExpectedImageDimensions(field, out pixels)
            )
                throw new InvalidOperationException(
                    "The unit artwork has no configured image size."
                );
            slot = Fit(slot, pixels);
        }
        RectInt background = Bounds(backdrop, window);
        if (nativeBackdropSize)
            background.size = UILayout.GetTextureSourceSize(texture);
        return (
            texture,
            new Rect(
                (slot.x - background.x) / (float)background.width,
                1f - (slot.yMax - background.y) / (float)background.height,
                slot.width / (float)background.width,
                slot.height / (float)background.height
            )
        );
    }

    /// <summary>
    /// Fits artwork using the same source-unit rounding and centering as gameplay cards.
    /// </summary>
    /// <param name="slot">The authored image slot.</param>
    /// <param name="pixels">The output's pixel dimensions.</param>
    /// <returns>The centered rectangle occupied by the image.</returns>
    private static RectInt Fit(RectInt slot, Vector2Int pixels)
    {
        Vector2Int source = new(UILayout.ToSourceUnits(pixels.x), UILayout.ToSourceUnits(pixels.y));
        float scale = Mathf.Min(1f, slot.width / (float)source.x, slot.height / (float)source.y);
        Vector2Int size = new(
            Mathf.Max(1, Mathf.RoundToInt(source.x * scale)),
            Mathf.Max(1, Mathf.RoundToInt(source.y * scale))
        );
        return new RectInt(
            slot.x + (slot.width - size.x) / 2,
            slot.y + (slot.height - size.y) / 2,
            size.x,
            size.y
        );
    }

    /// <summary>
    /// Measures an authored rectangle relative to its gameplay window.
    /// </summary>
    /// <param name="rect">The slot or background to measure.</param>
    /// <param name="window">The window containing the rectangle.</param>
    /// <returns>The window-relative rectangle in source coordinates.</returns>
    private static RectInt Bounds(RectTransform rect, Transform window)
    {
        RectInt bounds = UILayout.GetSourceRect(rect);
        for (Transform parent = rect.parent; parent != window; parent = parent.parent)
            bounds.position += UILayout.GetSourceRect((RectTransform)parent).position;
        return bounds;
    }

    /// <summary>
    /// Finds one named authored template within a gameplay window.
    /// </summary>
    /// <param name="window">The window containing the template.</param>
    /// <param name="name">The stable template name.</param>
    /// <returns>The authored template rectangle.</returns>
    private static RectTransform Find(Transform window, string name) =>
        window.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == name);

    /// <summary>
    /// Requires a direct authored child of the supplied gameplay window or template.
    /// </summary>
    /// <param name="parent">The authored parent.</param>
    /// <param name="name">The stable child name.</param>
    /// <returns>The child's rectangle.</returns>
    private static RectTransform Require(Transform parent, string name) =>
        parent.Find(name) as RectTransform
        ?? throw new MissingReferenceException($"{parent.name}/{name} is missing.");
}
