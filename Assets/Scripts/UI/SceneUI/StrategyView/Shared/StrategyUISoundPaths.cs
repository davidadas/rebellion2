using System.Collections.Generic;

/// <summary>
/// Defines shared non-themed sound resource paths used by strategy UI features.
/// </summary>
internal static class StrategyUISoundPaths
{
    public const string ControlPress = "Audio/SFX/StrategyView/sfx_strategyview_control_press";

    public const string SectorWindowOpen =
        "Audio/SFX/StrategyView/sfx_strategyview_planet_system_panel_open";

    public const string SectorWindowClose =
        "Audio/SFX/StrategyView/sfx_strategyview_sector_window_close";

    public const string GalacticInformationOpen =
        "Audio/SFX/StrategyView/sfx_strategyview_galactic_information_open";

    public const string GalacticInformationControl = "Audio/SFX/StrategyView/sfx_strategyview_open";

    public const string PlanetaryAssault =
        "Audio/SFX/StrategyView/Messages/sfx_strategyview_message_planetary_assault";

    /// <summary>
    /// Enumerates shared and themed sound-effect paths used by the strategy interface.
    /// </summary>
    /// <param name="theme">The active faction theme, or null when only shared paths are available.</param>
    /// <returns>The sound-effect resource paths to preload.</returns>
    internal static IEnumerable<string> GetPreloadPaths(FactionTheme theme)
    {
        yield return ControlPress;
        yield return SectorWindowOpen;
        yield return SectorWindowClose;
        yield return GalacticInformationOpen;
        yield return GalacticInformationControl;
        yield return PlanetaryAssault;

        StrategyWindowSoundTheme windowSounds = theme?.StrategyWindowSounds;
        ConfirmDialogTheme confirmDialog = theme?.ConfirmDialogTheme;
        string[] themedPaths =
        {
            windowSounds?.PlanetWindowOpenSoundPath,
            windowSounds?.PlanetWindowExpandSoundPath,
            windowSounds?.PlanetWindowCollapseSoundPath,
            windowSounds?.PlanetWindowMinimizeSoundPath,
            confirmDialog?.ScrapRetireSoundPath,
            confirmDialog?.StopConstructionSoundPath,
        };
        foreach (string path in themedPaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
                yield return path.Trim();
        }
    }
}
