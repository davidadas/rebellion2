using System;
using UnityEngine;

/// <summary>
/// Selects gameplay-window backgrounds for toolkit artwork using each view's own measurements.
/// </summary>
internal static class ContentEditorGameplayArtworkBackground
{
    /// <summary>
    /// Resolves the backdrop behind a unit artwork slot in its matching gameplay window.
    /// </summary>
    /// <param name="windows">The toolkit's existing gameplay-window copies.</param>
    /// <param name="assets">The working pack's resolved assets.</param>
    /// <param name="theme">The unit faction's effective theme.</param>
    /// <param name="field">The artwork slot being previewed.</param>
    /// <param name="readable">Whether software compositing needs readable pixels.</param>
    /// <param name="imageSize">The generated or current artwork dimensions.</param>
    /// <returns>The backdrop and the region measured by its owning view.</returns>
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
        if (
            field.Record.Kind is not ("CapitalShip" or "Starfighter")
            && field.MemberName != "DisplayImagePath"
        )
            return default;
        Vector2Int pixels;
        if (imageSize.HasValue)
            pixels = imageSize.Value;
        else if (!ContentPackEditorFieldMetadata.TryGetExpectedImageDimensions(field, out pixels))
            throw new InvalidOperationException("The unit artwork has no configured image size.");

        switch (field.MemberName)
        {
            case "BattleResultImagePath":
            case "BattleResultInTransitImagePath":
            case "BattleResultDamagedImagePath":
                Texture2D result = Load(
                    assets,
                    theme?.StrategyWindows?.BattleAlert?.GetResultListBackgroundImagePath(false),
                    readable
                );
                return result == null
                    ? default
                    : (
                        result,
                        windows
                            .GetComponentInChildren<BattleAlertWindowView>(true)
                            .GetResultArtworkBackgroundUv(pixels)
                    );
            case "SmallDisplayImagePath":
            case "InTransitSmallImagePath":
            case "DamagedSmallImagePath":
                Texture2D detail = Load(
                    assets,
                    theme?.StrategyWindows?.Fleet?.DetailBackgroundImagePath,
                    readable
                );
                return detail == null
                    ? default
                    : (
                        detail,
                        windows
                            .GetComponentInChildren<FleetWindowView>(true)
                            .GetDetailArtworkBackgroundUv(
                                pixels,
                                capitalShip
                                    ? FleetWindowTab.CapitalShips
                                    : FleetWindowTab.Starfighters
                            )
                    );
            case "DisplayImagePath":
            case "InTransitImagePath":
            case "DamagedImagePath":
                if (capitalShip)
                {
                    Texture2D banner = Load(
                        assets,
                        theme?.StrategyWindows?.Fleet?.DetailBackgroundImagePath,
                        readable
                    );
                    return banner == null
                        ? default
                        : (
                            banner,
                            windows
                                .GetComponentInChildren<FleetWindowView>(true)
                                .GetBannerArtworkBackgroundUv()
                        );
                }
                Texture2D status = Load(
                    assets,
                    theme?.StrategyWindows?.Status?.BackgroundImagePath,
                    readable
                );
                return status == null
                    ? default
                    : (
                        status,
                        windows
                            .GetComponentInChildren<StatusWindowView>(true)
                            .GetArtworkBackgroundUv(pixels, status)
                    );
            default:
                throw new ArgumentException("The image has no gameplay preview.", nameof(field));
        }
    }

    /// <summary>
    /// Loads a borrowed backdrop with the pixel access required by this preview.
    /// </summary>
    /// <param name="assets">The working pack's resolved assets.</param>
    /// <param name="address">The configured content address.</param>
    /// <param name="readable">Whether software compositing needs readable pixels.</param>
    /// <returns>The backdrop, or null when none is configured.</returns>
    private static Texture2D Load(ContentAssets assets, string address, bool readable) =>
        string.IsNullOrWhiteSpace(address) ? null
        : readable ? assets.GetReadableTexture(address)
        : assets.GetTexture(address);
}
