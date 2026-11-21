using System.Collections.Generic;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;
using UnityEngine;

/// <summary>
/// Defines the complete presentation theme for a faction.
/// </summary>
[PersistableObject]
public class FactionTheme
{
    public string FactionInstanceID { get; set; }

    public SpriteSheetAnimationTheme SaveIndicatorAnimation { get; set; }

    public string FactionPrimaryColorHex { get; set; }

    public FactionColorScheme ColorScheme { get; set; }

    public BattleParticipantTheme BattleParticipant { get; set; }

    public bool UseUpperButtonLayout { get; set; }

    public string SavedGameSlotIconImagePath { get; set; }

    public FactionArtworkDefaults ArtworkDefaults { get; set; }

    public string IntroCutscenePath { get; set; }

    public string VictoryCutscenePath { get; set; }

    public string DefeatCutscenePath { get; set; }

    public string HeadquartersDestroyedCutscenePath { get; set; }

    public List<PlanetDestructionCutscene> PlanetDestructionCutscenes { get; set; } = new();

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

    public StrategyCheckboxTheme StrategyCheckboxTheme { get; set; }

    public WindowTitleTheme WindowTitleTheme { get; set; }

    public StrategyWindowsTheme StrategyWindows { get; set; }

    public MissionIconSetTheme MissionIcons { get; set; }

    public PlanetOverlayTheme PlanetOverlayTheme { get; set; }

    public PlanetWindowTheme PlanetWindowTheme { get; set; }

    private Color primaryColor;
    private bool colorParsed;

    /// <summary>
    /// Gets the scheme primary color with the legacy primary color and white as fallbacks.
    /// </summary>
    /// <returns>The cached primary color.</returns>
    public Color GetPrimaryColor()
    {
        if (!colorParsed)
        {
            primaryColor = ThemeColorParser.Parse(
                ColorScheme?.PrimaryColorHex,
                ThemeColorParser.Parse(FactionPrimaryColorHex, Color.white)
            );
            colorParsed = true;
        }

        return primaryColor;
    }

    /// <summary>
    /// Gets the color applied to shared selection-frame masks.
    /// </summary>
    /// <returns>The configured selection color or the faction primary color.</returns>
    public Color GetSelectionColor() =>
        ThemeColorParser.Parse(ColorScheme?.SelectionColorHex, GetPrimaryColor());

    /// <summary>
    /// Gets the color applied to the shared active-window title mask.
    /// </summary>
    /// <returns>The configured active-title color or the faction primary color.</returns>
    public Color GetActiveWindowTitleColor() =>
        ThemeColorParser.Parse(ColorScheme?.ActiveWindowTitleColorHex, GetPrimaryColor());

    /// <summary>
    /// Gets the color applied to the shared inactive-window title mask.
    /// </summary>
    /// <returns>The configured inactive-title color or the active-title color.</returns>
    public Color GetInactiveWindowTitleColor() =>
        ThemeColorParser.Parse(
            ColorScheme?.InactiveWindowTitleColorHex,
            GetActiveWindowTitleColor()
        );

    /// <summary>
    /// Gets the color applied to the shared active manufacturing-lane mask.
    /// </summary>
    /// <returns>The configured active-lane color or the faction primary color.</returns>
    public Color GetActiveBuildingPaneColor() =>
        ThemeColorParser.Parse(ColorScheme?.ActiveBuildingPaneColorHex, GetPrimaryColor());

    /// <summary>
    /// Gets the color applied to the shared inactive manufacturing-lane mask.
    /// </summary>
    /// <returns>The configured inactive-lane color or the active-lane color.</returns>
    public Color GetInactiveBuildingPaneColor() =>
        ThemeColorParser.Parse(
            ColorScheme?.InactiveBuildingPaneColorHex,
            GetActiveBuildingPaneColor()
        );

    /// <summary>
    /// Gets the color applied to shared faction planet-marker masks.
    /// </summary>
    /// <returns>The configured planet-marker color or the faction primary color.</returns>
    public Color GetPlanetMarkerColor() =>
        ThemeColorParser.Parse(ColorScheme?.PlanetMarkerColorHex, GetPrimaryColor());

    /// <summary>
    /// Resolves the color of a mask only for themes that opt into a color scheme.
    /// </summary>
    /// <param name="mask">The interface role the color is applied to.</param>
    /// <returns>The resolved color, or null to use the supplied artwork directly.</returns>
    public Color? GetMaskTint(FactionMaskColor mask)
    {
        if (ColorScheme == null)
            return null;
        return mask switch
        {
            FactionMaskColor.SelectionColorHex => GetSelectionColor(),
            FactionMaskColor.ActiveWindowTitleColorHex => GetActiveWindowTitleColor(),
            FactionMaskColor.InactiveWindowTitleColorHex => GetInactiveWindowTitleColor(),
            FactionMaskColor.ActiveBuildingPaneColorHex => GetActiveBuildingPaneColor(),
            FactionMaskColor.InactiveBuildingPaneColorHex => GetInactiveBuildingPaneColor(),
            FactionMaskColor.PlanetMarkerColorHex => GetPlanetMarkerColor(),
            _ => null,
        };
    }
}

/// <summary>
/// Identifies the color setting responsible for a rendered faction mask.
/// </summary>
public enum FactionMaskColor
{
    None,
    SelectionColorHex,
    ActiveWindowTitleColorHex,
    InactiveWindowTitleColorHex,
    ActiveBuildingPaneColorHex,
    InactiveBuildingPaneColorHex,
    PlanetMarkerColorHex,
}

/// <summary>
/// Defines faction colors applied to shared interface masks.
/// </summary>
[PersistableObject]
public sealed class FactionColorScheme
{
    public string PrimaryColorHex { get; set; }

    public string SelectionColorHex { get; set; }

    public string ActiveWindowTitleColorHex { get; set; }

    public string InactiveWindowTitleColorHex { get; set; }

    public string ActiveBuildingPaneColorHex { get; set; }

    public string InactiveBuildingPaneColorHex { get; set; }

    public string PlanetMarkerColorHex { get; set; }
}

/// <summary>
/// Defines optional faction backgrounds retained for unit artwork workflows.
/// </summary>
[PersistableObject]
public sealed class FactionArtworkDefaults
{
    public string OfficerBackgroundImagePath { get; set; }
    public string CapitalShipBackgroundImagePath { get; set; }
    public string StarfighterBackgroundImagePath { get; set; }

    /// <summary>
    /// Gets the configured backdrop for the specified unit type.
    /// </summary>
    /// <param name="unitKind">The serialized unit type.</param>
    /// <returns>The backdrop address, if configured for this type.</returns>
    public string GetBackgroundImagePath(string unitKind) =>
        unitKind switch
        {
            nameof(Officer) => OfficerBackgroundImagePath,
            nameof(CapitalShip) => CapitalShipBackgroundImagePath,
            nameof(Starfighter) => StarfighterBackgroundImagePath,
            _ => null,
        };
}

/// <summary>
/// Provides the serialized collection root for faction themes.
/// </summary>
[PersistableObject]
public sealed class FactionThemes : List<FactionTheme> { }

/// <summary>
/// Maps one planet-destroying ship type to its presentation movie.
/// </summary>
[PersistableObject(Name = "Cutscene")]
public sealed class PlanetDestructionCutscene
{
    [PersistableAttribute]
    public string ShipTypeID { get; set; }

    [PersistableAttribute]
    public string Path { get; set; }
}
