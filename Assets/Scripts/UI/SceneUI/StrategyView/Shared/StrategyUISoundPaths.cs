using System.Collections.Generic;
using Rebellion.Game.Advisor;

/// <summary>
/// Defines shared non-themed sound resource paths used by strategy UI features.
/// </summary>
internal static class StrategyUISoundPaths
{
    public const string ControlPress = "Application/Strategy/Audio/Controls/control-press";

    public const string SectorWindowOpen =
        "Application/Strategy/Audio/Controls/planet-sector-panel-open";

    public const string SectorWindowClose =
        "Application/Strategy/Audio/Controls/sector-window-close";

    public const string GalacticInformationOpen =
        "Application/Strategy/Audio/Controls/galactic-information-open";

    public const string GalacticInformationControl = "Application/Strategy/Audio/Controls/open";

    public const string PlanetaryAssault =
        "Application/Strategy/Messages/Audio/message-planetary-assault";

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

        foreach (string path in GetPlanetaryAssaultAdvisorAudioPaths(theme?.StrategyAdvisor))
            yield return path;

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

    /// <summary>
    /// Returns the configured droid and protocol audio paths for the planetary-assault advisor
    /// notification.
    /// </summary>
    /// <param name="advisor">The active faction advisor theme.</param>
    /// <returns>The planetary-assault advisor audio paths.</returns>
    private static IEnumerable<string> GetPlanetaryAssaultAdvisorAudioPaths(
        StrategyAdvisorTheme advisor
    )
    {
        StrategyAdvisorNotificationTheme assault = advisor?.GetNotification(
            AdvisorNotificationType.PlanetaryAssault,
            null,
            AdvisorSubjectNotification.None
        );
        string droidPath = GetAdvisorAnimationAudioPath(advisor, assault?.Droid);
        string protocolPath = GetAdvisorAnimationAudioPath(advisor, assault?.Protocol);
        if (!string.IsNullOrWhiteSpace(droidPath))
            yield return droidPath;
        if (!string.IsNullOrWhiteSpace(protocolPath))
            yield return protocolPath;
    }

    /// <summary>
    /// Resolves one advisor animation's explicit or theme-relative audio path.
    /// </summary>
    /// <param name="advisor">The owning advisor theme.</param>
    /// <param name="animation">The configured advisor animation.</param>
    /// <returns>The resolved audio path, or null when no audio is configured.</returns>
    private static string GetAdvisorAnimationAudioPath(
        StrategyAdvisorTheme advisor,
        StrategyAdvisorAnimationTheme animation
    )
    {
        if (!string.IsNullOrWhiteSpace(animation?.AudioPath))
            return animation.AudioPath.Trim();
        return string.IsNullOrWhiteSpace(animation?.Audio)
            ? null
            : advisor.GetAudioPath(animation.Audio.Trim());
    }
}
