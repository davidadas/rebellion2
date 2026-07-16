using Rebellion.Util.Serialization;

/// <summary>
/// Defines tile, list, state, and badge artwork for strategy units.
/// </summary>
[PersistableObject]
public class UnitTileIcons
{
    /// <summary>
    /// Gets or sets the fleet tile image path.
    /// </summary>
    public string FleetTileImagePath { get; set; }

    /// <summary>
    /// Gets or sets the mission tile image path.
    /// </summary>
    public string MissionTileImagePath { get; set; }

    /// <summary>
    /// Gets or sets the default tile image path.
    /// </summary>
    public string DefaultTileImagePath { get; set; }

    /// <summary>
    /// Gets or sets the fleet list icon image path.
    /// </summary>
    public string FleetListIconImagePath { get; set; }

    /// <summary>
    /// Gets or sets the fleet list enroute icon image path.
    /// </summary>
    public string FleetListEnrouteIconImagePath { get; set; }

    /// <summary>
    /// Gets or sets the fleet list damaged icon image path.
    /// </summary>
    public string FleetListDamagedIconImagePath { get; set; }

    /// <summary>
    /// Gets or sets the fleet list selection image path.
    /// </summary>
    public string FleetListSelectionImagePath { get; set; }

    /// <summary>
    /// Gets or sets the fleet detail selection image path.
    /// </summary>
    public string FleetDetailSelectionImagePath { get; set; }

    /// <summary>
    /// Gets or sets the fleet construction small image path.
    /// </summary>
    public string FleetConstructionSmallImagePath { get; set; }

    /// <summary>
    /// Gets or sets the fleet construction large image path.
    /// </summary>
    public string FleetConstructionLargeImagePath { get; set; }

    /// <summary>
    /// Gets or sets the fleet starfighters badge image path.
    /// </summary>
    public string FleetStarfightersBadgeImagePath { get; set; }

    /// <summary>
    /// Gets or sets the fleet troops badge image path.
    /// </summary>
    public string FleetTroopsBadgeImagePath { get; set; }

    /// <summary>
    /// Gets or sets the fleet personnel badge image path.
    /// </summary>
    public string FleetPersonnelBadgeImagePath { get; set; }
}

/// <summary>
/// Defines normal and hover artwork for a planet overlay icon.
/// </summary>
[PersistableObject]
public class OverlayIconTheme
{
    /// <summary>
    /// Gets or sets the normal image path.
    /// </summary>
    public string NormalImagePath { get; set; }

    /// <summary>
    /// Gets or sets the hover image path.
    /// </summary>
    public string HoverImagePath { get; set; }
}

/// <summary>
/// Groups the supported planet overlay icon themes.
/// </summary>
[PersistableObject]
public class PlanetOverlayIcons
{
    /// <summary>
    /// Gets or sets the fleets.
    /// </summary>
    public OverlayIconTheme Fleets { get; set; }

    /// <summary>
    /// Gets or sets the defenses.
    /// </summary>
    public OverlayIconTheme Defenses { get; set; }

    /// <summary>
    /// Gets or sets the buildings.
    /// </summary>
    public OverlayIconTheme Buildings { get; set; }

    /// <summary>
    /// Gets or sets the missions.
    /// </summary>
    public OverlayIconTheme Missions { get; set; }
}

/// <summary>
/// Defines galaxy and planet-system overlay presentation.
/// </summary>
[PersistableObject]
public class PlanetOverlayTheme
{
    /// <summary>
    /// Gets or sets the planet overlay icons.
    /// </summary>
    public PlanetOverlayIcons PlanetOverlayIcons { get; set; }

    /// <summary>
    /// Gets or sets the unit tile icons.
    /// </summary>
    public UnitTileIcons UnitTileIcons { get; set; }

    /// <summary>
    /// Gets or sets the galaxy headquarters image path.
    /// </summary>
    public string GalaxyHeadquartersImagePath { get; set; }

    /// <summary>
    /// Gets or sets the planet system headquarters image path.
    /// </summary>
    public string PlanetSystemHeadquartersImagePath { get; set; }
}

/// <summary>
/// Defines the mission-tab presentation within a planet window.
/// </summary>
[PersistableObject]
public class MissionsPaneTheme
{
    /// <summary>
    /// Gets or sets the mission tabs.
    /// </summary>
    public MissionTabsTheme MissionTabs { get; set; }
}

/// <summary>
/// Defines primary and secondary participant mission tabs.
/// </summary>
[PersistableObject]
public class MissionTabsTheme
{
    /// <summary>
    /// Gets or sets the primary participants.
    /// </summary>
    public FleetTabIconSet PrimaryParticipants { get; set; }

    /// <summary>
    /// Gets or sets the secondary participants.
    /// </summary>
    public FleetTabIconSet SecondaryParticipants { get; set; }
}

/// <summary>
/// Groups the presentation themes for each planet-window pane.
/// </summary>
[PersistableObject]
public class PlanetWindowTheme
{
    /// <summary>
    /// Gets or sets the buildings pane.
    /// </summary>
    public BuildingsPaneTheme BuildingsPane { get; set; }

    /// <summary>
    /// Gets or sets the fleets pane.
    /// </summary>
    public FleetsPaneTheme FleetsPane { get; set; }

    /// <summary>
    /// Gets or sets the garrison panel.
    /// </summary>
    public GarrisonPanelTheme GarrisonPanel { get; set; }

    /// <summary>
    /// Gets or sets the missions pane.
    /// </summary>
    public MissionsPaneTheme MissionsPane { get; set; }
}

/// <summary>
/// Defines construction-header artwork.
/// </summary>
[PersistableObject]
public class ConstructionHeaderTheme
{
    /// <summary>
    /// Gets or sets the image path.
    /// </summary>
    public string ImagePath { get; set; }
}

/// <summary>
/// Defines building tabs, construction header, and manufacturing-lane presentation.
/// </summary>
[PersistableObject]
public class BuildingsPaneTheme
{
    /// <summary>
    /// Gets or sets the buildings tabs.
    /// </summary>
    public BuildingsTabsTheme BuildingsTabs { get; set; }

    /// <summary>
    /// Gets or sets the construction header.
    /// </summary>
    public ConstructionHeaderTheme ConstructionHeader { get; set; }

    /// <summary>
    /// Gets or sets the manufacturing lane state.
    /// </summary>
    public ManufacturingLaneStateTheme ManufacturingLaneState { get; set; }
}

/// <summary>
/// Defines production-tab presentation for the buildings pane.
/// </summary>
[PersistableObject]
public class BuildingsTabsTheme
{
    /// <summary>
    /// Gets or sets the production.
    /// </summary>
    public FleetTabIconSet Production { get; set; }
}

/// <summary>
/// Defines active and inactive manufacturing-lane artwork.
/// </summary>
[PersistableObject]
public class ManufacturingLaneStateTheme
{
    /// <summary>
    /// Gets or sets the active image path.
    /// </summary>
    public string ActiveImagePath { get; set; }

    /// <summary>
    /// Gets or sets the inactive image path.
    /// </summary>
    public string InactiveImagePath { get; set; }
}

/// <summary>
/// Defines fleet-pane artwork.
/// </summary>
[PersistableObject]
public class FleetsPaneTheme
{
    /// <summary>
    /// Gets or sets the fleets image path.
    /// </summary>
    public string FleetsImagePath { get; set; }
}

/// <summary>
/// Defines normal, selected, and disabled tab artwork.
/// </summary>
[PersistableObject]
public class FleetTabIconSet
{
    /// <summary>
    /// Gets or sets the normal image path.
    /// </summary>
    public string NormalImagePath { get; set; }

    /// <summary>
    /// Gets or sets the selected image path.
    /// </summary>
    public string SelectedImagePath { get; set; }

    /// <summary>
    /// Gets or sets the disabled image path.
    /// </summary>
    public string DisabledImagePath { get; set; }
}

/// <summary>
/// Defines the garrison-panel tabs for supported unit and defense categories.
/// </summary>
[PersistableObject]
public class GarrisonPanelTheme
{
    /// <summary>
    /// Gets or sets the officers.
    /// </summary>
    public FleetTabIconSet Officers { get; set; }

    /// <summary>
    /// Gets or sets the starfighters.
    /// </summary>
    public FleetTabIconSet Starfighters { get; set; }

    /// <summary>
    /// Gets or sets the regiments.
    /// </summary>
    public FleetTabIconSet Regiments { get; set; }

    /// <summary>
    /// Gets or sets the shields.
    /// </summary>
    public FleetTabIconSet Shields { get; set; }

    /// <summary>
    /// Gets or sets the weapons.
    /// </summary>
    public FleetTabIconSet Weapons { get; set; }
}
