using System.Collections.Generic;
using Rebellion.Util.Serialization;
using UnityEngine;

/// <summary>
/// Defines the complete presentation theme for a faction.
/// </summary>
[PersistableObject]
public class FactionTheme
{
    public string FactionInstanceID { get; set; }

    public string FactionPrimaryColorHex { get; set; }

    public MainMenuFactionTheme MainMenu { get; set; }

    public BattleParticipantTheme BattleParticipant { get; set; }

    public bool UseUpperButtonLayout { get; set; }

    public string SavedGameSlotIconImagePath { get; set; }

    public string IntroCutscenePath { get; set; }

    public string VictoryCutscenePath { get; set; }

    public string DefeatCutscenePath { get; set; }

    public string HeadquartersDestroyedCutscenePath { get; set; }

    public TacticalHUDLayout TacticalHUDLayout { get; set; }

    public StrategyAdvisorTheme StrategyAdvisor { get; set; }

    public StrategyBriefingTheme StrategyBriefing { get; set; }

    public GalaxyBackground GalaxyBackground { get; set; }

    public GalacticInformationDisplayTheme GalacticInformationDisplay { get; set; }

    public StrategyWindowPlacements StrategyWindowPlacements { get; set; }

    public StrategyWindowSoundTheme StrategyWindowSounds { get; set; }

    public StrategyMusicTheme StrategyMusic { get; set; }

    public StrategyBookmarkLayout StrategyBookmarkLayout { get; set; }

    public StrategyBookmarkIcons StrategyBookmarkIcons { get; set; }

    public ConfirmDialogTheme ConfirmDialogTheme { get; set; }

    public StrategyContextMenuTheme StrategyContextMenuTheme { get; set; }

    public WindowTitleTheme WindowTitleTheme { get; set; }

    public StrategyWindowsTheme StrategyWindows { get; set; }

    public MissionIconSetTheme MissionIcons { get; set; }

    public PlanetOverlayTheme PlanetOverlayTheme { get; set; }

    public PlanetWindowTheme PlanetWindowTheme { get; set; }

    private Color primaryColor;
    private bool colorParsed;

    /// <summary>
    /// Gets the parsed primary color, falling back to white for invalid configuration.
    /// </summary>
    /// <returns>The cached primary color.</returns>
    public Color GetPrimaryColor()
    {
        if (!colorParsed)
        {
            primaryColor = ThemeColorParser.Parse(FactionPrimaryColorHex, Color.white);
            colorParsed = true;
        }

        return primaryColor;
    }
}

/// <summary>
/// Provides the serialized collection root for faction themes.
/// </summary>
[PersistableObject]
public sealed class FactionThemes : List<FactionTheme> { }

[PersistableObject]
public sealed class MainMenuFactionTheme
{
    public string LaunchAnimationRoot { get; set; }

    public int LaunchAnimationFrameCount { get; set; }

    public float LaunchAnimationFrameIntervalSeconds { get; set; }

    /// <summary>
    /// Builds the content address for one configured launch-animation frame.
    /// </summary>
    /// <param name="frameIndex">The one-based animation frame index.</param>
    /// <returns>The animation frame content address.</returns>
    public string GetLaunchAnimationFramePath(int frameIndex)
    {
        return $"{LaunchAnimationRoot}/frame-{frameIndex:D3}";
    }
}
