using Rebellion.Game.Galaxy;
using UnityEngine;

/// <summary>
/// Resolves configured galactic-information filters into shared planet-marker artwork.
/// </summary>
internal static class GalacticInformationMarkerProjector
{
    /// <summary>
    /// Resolves the configured filter for one selected display mode.
    /// </summary>
    /// <param name="playerTheme">The viewing player's presentation theme.</param>
    /// <param name="filterMode">The requested galactic-information filter.</param>
    /// <returns>The configured filter, or null when the display is off or unavailable.</returns>
    public static GalacticInformationFilterTheme ResolveFilter(
        FactionTheme playerTheme,
        GalacticInformationFilterMode filterMode
    )
    {
        return filterMode == GalacticInformationFilterMode.DisplayOff
            ? null
            : playerTheme?.GalacticInformationDisplay?.GetFilter(filterMode);
    }

    /// <summary>
    /// Resolves the marker artwork for one visible planet and evaluated filter result.
    /// </summary>
    /// <param name="context">The current strategy presentation context.</param>
    /// <param name="planet">The represented visible planet.</param>
    /// <param name="marker">The evaluated marker intensity and faction.</param>
    /// <param name="highlightUnexplored">Whether an unexplored planet uses its evaluated marker.</param>
    /// <returns>The resolved marker texture.</returns>
    public static Texture2D ResolveTexture(
        UIContext context,
        Planet planet,
        GalacticInformationMarker marker,
        bool highlightUnexplored = false
    )
    {
        if (planet?.IsUnexploredView == true && !highlightUnexplored)
        {
            return context?.GetTexture(
                context.GetPlayerFactionTheme()?.GalaxyBackground?.UnexploredPlanetIconPath
            );
        }

        if (marker.Mixed)
        {
            return context?.GetTexture(
                context.GetPlayerFactionTheme()?.GalaxyBackground?.PlanetIcons?.Mixed
            );
        }

        PlanetIcons icons = context
            ?.GetTheme(marker.FactionInstanceId)
            ?.GalaxyBackground?.PlanetIcons;
        return context?.GetTexture(GetPlanetIconPath(icons, marker.Index));
    }

    /// <summary>
    /// Selects the best configured marker path for one evaluated intensity.
    /// </summary>
    /// <param name="icons">The themed marker paths.</param>
    /// <param name="markerIndex">The zero-based marker intensity.</param>
    /// <returns>The best configured marker path for the requested intensity.</returns>
    public static string GetPlanetIconPath(PlanetIcons icons, int markerIndex)
    {
        return markerIndex switch
        {
            0 => icons?.Small,
            1 => icons?.Medium ?? icons?.Small,
            2 => icons?.Large ?? icons?.Medium ?? icons?.Small,
            _ => icons?.XL ?? icons?.Large ?? icons?.Medium ?? icons?.Small,
        };
    }
}
