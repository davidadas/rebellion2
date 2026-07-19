using Rebellion.Util.Serialization;

/// <summary>
/// Defines tile, list, state, and badge artwork for strategy units.
/// </summary>
[PersistableObject]
public class UnitTileIcons
{
    public string FleetTileImagePath { get; set; }

    public string MissionTileImagePath { get; set; }

    public string DefaultTileImagePath { get; set; }

    public string FleetListIconImagePath { get; set; }

    public string FleetListEnrouteIconImagePath { get; set; }

    public string FleetListDamagedIconImagePath { get; set; }

    public string FleetListSelectionImagePath { get; set; }

    public string FleetDetailSelectionImagePath { get; set; }

    public string FleetConstructionSmallImagePath { get; set; }

    public string FleetConstructionLargeImagePath { get; set; }

    public string FleetStarfightersBadgeImagePath { get; set; }

    public string FleetTroopsBadgeImagePath { get; set; }

    public string FleetPersonnelBadgeImagePath { get; set; }
}

/// <summary>
/// Defines normal and hover artwork for a planet overlay icon.
/// </summary>
[PersistableObject]
public class OverlayIconTheme
{
    public string NormalImagePath { get; set; }

    public string HoverImagePath { get; set; }
}

/// <summary>
/// Groups the supported planet overlay icon themes.
/// </summary>
[PersistableObject]
public class PlanetOverlayIcons
{
    public OverlayIconTheme Fleets { get; set; }

    public OverlayIconTheme Defenses { get; set; }

    public OverlayIconTheme Buildings { get; set; }

    public OverlayIconTheme Missions { get; set; }
}

/// <summary>
/// Defines galaxy and planet-system overlay presentation.
/// </summary>
[PersistableObject]
public class PlanetOverlayTheme
{
    public PlanetOverlayIcons PlanetOverlayIcons { get; set; }

    public UnitTileIcons UnitTileIcons { get; set; }

    public string GalaxyHeadquartersImagePath { get; set; }

    public string PlanetSystemHeadquartersImagePath { get; set; }
}

/// <summary>
/// Defines the mission-tab presentation within a planet window.
/// </summary>
[PersistableObject]
public class MissionsPaneTheme
{
    public MissionTabsTheme MissionTabs { get; set; }
}

/// <summary>
/// Defines primary and secondary participant mission tabs.
/// </summary>
[PersistableObject]
public class MissionTabsTheme
{
    public FleetTabIconSet PrimaryParticipants { get; set; }

    public FleetTabIconSet SecondaryParticipants { get; set; }
}

/// <summary>
/// Groups the presentation themes for each planet-window pane.
/// </summary>
[PersistableObject]
public class PlanetWindowTheme
{
    public BuildingsPaneTheme BuildingsPane { get; set; }

    public FleetsPaneTheme FleetsPane { get; set; }

    public GarrisonPanelTheme GarrisonPanel { get; set; }

    public MissionsPaneTheme MissionsPane { get; set; }
}

/// <summary>
/// Defines construction-header artwork.
/// </summary>
[PersistableObject]
public class ConstructionHeaderTheme
{
    public string ImagePath { get; set; }
}

/// <summary>
/// Defines building tabs, construction header, and manufacturing-lane presentation.
/// </summary>
[PersistableObject]
public class BuildingsPaneTheme
{
    public BuildingsTabsTheme BuildingsTabs { get; set; }

    public ConstructionHeaderTheme ConstructionHeader { get; set; }

    public ManufacturingLaneStateTheme ManufacturingLaneState { get; set; }
}

/// <summary>
/// Defines production-tab presentation for the buildings pane.
/// </summary>
[PersistableObject]
public class BuildingsTabsTheme
{
    public FleetTabIconSet Production { get; set; }
}

/// <summary>
/// Defines active and inactive manufacturing-lane artwork.
/// </summary>
[PersistableObject]
public class ManufacturingLaneStateTheme
{
    public string ActiveImagePath { get; set; }

    public string InactiveImagePath { get; set; }
}

/// <summary>
/// Defines fleet-pane artwork.
/// </summary>
[PersistableObject]
public class FleetsPaneTheme
{
    public string FleetsImagePath { get; set; }
}

/// <summary>
/// Defines normal, selected, and disabled tab artwork.
/// </summary>
[PersistableObject]
public class FleetTabIconSet
{
    public string NormalImagePath { get; set; }

    public string SelectedImagePath { get; set; }

    public string DisabledImagePath { get; set; }
}

/// <summary>
/// Defines the garrison-panel tabs for supported unit and defense categories.
/// </summary>
[PersistableObject]
public class GarrisonPanelTheme
{
    public FleetTabIconSet Officers { get; set; }

    public FleetTabIconSet Starfighters { get; set; }

    public FleetTabIconSet Regiments { get; set; }

    public FleetTabIconSet Shields { get; set; }

    public FleetTabIconSet Weapons { get; set; }
}
