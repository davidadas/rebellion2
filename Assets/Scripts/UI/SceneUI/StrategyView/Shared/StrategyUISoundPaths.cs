using System.Collections.Generic;

/// <summary>
/// Defines shared non-themed sound resource paths used by strategy UI features.
/// </summary>
internal static class StrategyUISoundPaths
{
    public const string ControlPress = "application/strategy/audio/controls/control-press";

    public const string SectorWindowOpen =
        "application/strategy/audio/controls/planet-system-panel-open";

    public const string SectorWindowClose =
        "application/strategy/audio/controls/sector-window-close";

    public const string GalacticInformationOpen =
        "application/strategy/audio/controls/galactic-information-open";

    public const string GalacticInformationControl = "application/strategy/audio/controls/open";

    public const string PlanetaryAssault =
        "application/strategy/audio/messages/message-planetary-assault";

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
