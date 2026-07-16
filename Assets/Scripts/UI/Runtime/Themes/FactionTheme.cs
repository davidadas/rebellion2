using System.Collections.Generic;
using Rebellion.Util.Serialization;
using UnityEngine;

/// <summary>
/// Defines the complete presentation theme for a faction.
/// </summary>
[PersistableObject]
public class FactionTheme
{
    /// <summary>
    /// Gets or sets the faction instance identifier represented by this theme.
    /// </summary>
    public string FactionInstanceID { get; set; }

    /// <summary>
    /// Gets or sets the faction's primary color in hexadecimal notation.
    /// </summary>
    public string FactionPrimaryColorHex { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether upper button layout is used.
    /// </summary>
    public bool UseUpperButtonLayout { get; set; }

    /// <summary>
    /// Gets or sets the save menu return strategy button image path.
    /// </summary>
    public string SaveMenuReturnStrategyButtonImagePath { get; set; }

    /// <summary>
    /// Gets or sets the save menu return strategy button pressed image path.
    /// </summary>
    public string SaveMenuReturnStrategyButtonPressedImagePath { get; set; }

    /// <summary>
    /// Gets or sets the save menu slot icon image path.
    /// </summary>
    public string SaveMenuSlotIconImagePath { get; set; }

    /// <summary>
    /// Gets or sets the intro cutscene path.
    /// </summary>
    public string IntroCutscenePath { get; set; }

    /// <summary>
    /// Gets or sets the victory cutscene path.
    /// </summary>
    public string VictoryCutscenePath { get; set; }

    /// <summary>
    /// Gets or sets the defeat cutscene path.
    /// </summary>
    public string DefeatCutscenePath { get; set; }

    /// <summary>
    /// Gets or sets the tactical HUD layout.
    /// </summary>
    public TacticalHUDLayout TacticalHUDLayout { get; set; }

    /// <summary>
    /// Gets or sets the strategy advisor.
    /// </summary>
    public StrategyAdvisorTheme StrategyAdvisor { get; set; }

    /// <summary>
    /// Gets or sets the galaxy background.
    /// </summary>
    public GalaxyBackground GalaxyBackground { get; set; }

    /// <summary>
    /// Gets or sets the galactic information display.
    /// </summary>
    public GalacticInformationDisplayTheme GalacticInformationDisplay { get; set; }

    /// <summary>
    /// Gets or sets the strategy window placements.
    /// </summary>
    public StrategyWindowPlacements StrategyWindowPlacements { get; set; }

    /// <summary>
    /// Gets or sets the strategy window sounds.
    /// </summary>
    public StrategyWindowSoundTheme StrategyWindowSounds { get; set; }

    /// <summary>
    /// Gets or sets the strategy bookmark layout.
    /// </summary>
    public StrategyBookmarkLayout StrategyBookmarkLayout { get; set; }

    /// <summary>
    /// Gets or sets the strategy bookmark icons.
    /// </summary>
    public StrategyBookmarkIcons StrategyBookmarkIcons { get; set; }

    /// <summary>
    /// Gets or sets the confirm dialog theme.
    /// </summary>
    public ConfirmDialogTheme ConfirmDialogTheme { get; set; }

    /// <summary>
    /// Gets or sets the strategy context menu theme.
    /// </summary>
    public StrategyContextMenuTheme StrategyContextMenuTheme { get; set; }

    /// <summary>
    /// Gets or sets the window title theme.
    /// </summary>
    public WindowTitleTheme WindowTitleTheme { get; set; }

    /// <summary>
    /// Gets or sets the strategy windows.
    /// </summary>
    public StrategyWindowsTheme StrategyWindows { get; set; }

    /// <summary>
    /// Gets or sets the mission icons.
    /// </summary>
    public MissionIconSetTheme MissionIcons { get; set; }

    /// <summary>
    /// Gets or sets the planet overlay theme.
    /// </summary>
    public PlanetOverlayTheme PlanetOverlayTheme { get; set; }

    /// <summary>
    /// Gets or sets the planet window theme.
    /// </summary>
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
