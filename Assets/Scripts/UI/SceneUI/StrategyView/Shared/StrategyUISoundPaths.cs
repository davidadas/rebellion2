using System.Collections.Generic;

/// <summary>
/// Defines shared non-themed sound resource paths used by strategy UI features.
/// </summary>
internal static class StrategyUISoundPaths
{
    public const string ControlPress = "Application/Strategy/Audio/Controls/control-press";

    public const string SectorWindowOpen =
        "Application/Strategy/Audio/Controls/planet-system-panel-open";

    public const string SectorWindowClose =
        "Application/Strategy/Audio/Controls/sector-window-close";

    public const string GalacticInformationOpen =
        "Application/Strategy/Audio/Controls/galactic-information-open";

    public const string GalacticInformationControl = "Application/Strategy/Audio/Controls/open";

    public const string PlanetaryAssault =
        "Application/Strategy/Audio/Messages/message-planetary-assault";

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
