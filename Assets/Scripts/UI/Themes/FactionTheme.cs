using System.Collections.Generic;
using Rebellion.Util.Attributes;
using UnityEngine;

[PersistableObject]
public class PlanetIcons
{
    public string Small;
    public string Medium;
    public string Large;
    public string XL;
}

[PersistableObject]
public class TacticalHUDLayout
{
    public string ImagePath { get; set; }
    public RectLayout TickCounterTextLayout { get; set; }
    public RectLayout RawMaterialsTextLayout { get; set; }
    public RectLayout RefinedMaterialsTextLayout { get; set; }
    public RectLayout MaintenanceTextLayout { get; set; }
}

[PersistableObject]
public class GalaxyBackground
{
    public string ImagePath { get; set; }
    public RectLayout ImageLayout { get; set; }
    public RectLayout MapViewportLayout { get; set; }
    public PlanetIcons PlanetIcons;
}

[PersistableObject]
public class UnitTileIcons
{
    public string FleetTileImagePath { get; set; }
    public string MissionTileImagePath { get; set; }
    public string DefaultTileImagePath { get; set; }
}

[PersistableObject]
public class OverlayIconTheme
{
    public string NormalImagePath { get; set; }
    public string HoverImagePath { get; set; }
}

[PersistableObject]
public class PlanetOverlayIcons
{
    public OverlayIconTheme Fleets { get; set; }
    public OverlayIconTheme Defenses { get; set; }
    public OverlayIconTheme Buildings { get; set; }
    public OverlayIconTheme Missions { get; set; }
}

[PersistableObject]
public class PlanetOverlayTheme
{
    public PlanetOverlayIcons PlanetOverlayIcons { get; set; }

    public UnitTileIcons UnitTileIcons { get; set; }
}

[PersistableObject]
public class PlanetWindowTheme
{
    public BuildingsPaneTheme BuildingsPane { get; set; }

    public FleetsPaneTheme FleetsPane { get; set; }

    public GarrisonPanelTheme GarrisonPanel { get; set; }
}

[PersistableObject]
public class ConstructionHeaderTheme
{
    public string ImagePath { get; set; }
}

[PersistableObject]
public class BuildingsPaneTheme
{
    public BuildingsTabsTheme BuildingsTabs { get; set; }

    public ConstructionHeaderTheme ConstructionHeader { get; set; }
}

[PersistableObject]
public class BuildingsTabsTheme
{
    public FleetTabIconSet Production { get; set; }
}

[PersistableObject]
public class FleetsPaneTheme
{
    public string FleetsImagePath { get; set; }

    public FleetTabsTheme FleetTabs { get; set; }
}

[PersistableObject]
public class FleetTabsTheme
{
    public FleetTabIconSet CapitalShips { get; set; }
    public FleetTabIconSet Starfighters { get; set; }
    public FleetTabIconSet Regiments { get; set; }
    public FleetTabIconSet Officers { get; set; }
}

[PersistableObject]
public class FleetTabIconSet
{
    public string NormalImagePath { get; set; }
    public string SelectedImagePath { get; set; }
    public string DisabledImagePath { get; set; }
}

[PersistableObject]
public class GarrisonPanelTheme
{
    public FleetTabIconSet Officers { get; set; }
    public FleetTabIconSet Starfighters { get; set; }
    public FleetTabIconSet Regiments { get; set; }
    public FleetTabIconSet Shields { get; set; }
    public FleetTabIconSet Weapons { get; set; }
}

[PersistableObject]
public class FactionTheme
{
    public string FactionInstanceID;
    public string FactionPrimaryColorHex;

    public string IntroCutscenePath { get; set; }
    public string VictoryCutscenePath { get; set; }
    public string DefeatCutscenePath { get; set; }

    public TacticalHUDLayout TacticalHUDLayout { get; set; }
    public GalaxyBackground GalaxyBackground { get; set; }

    public PlanetOverlayTheme PlanetOverlayTheme { get; set; }
    public PlanetWindowTheme PlanetWindowTheme { get; set; }

    public string UIFleetsButtonImagePath { get; set; }
    public string UIPlanetsButtonImagePath { get; set; }
    public string UIOfficersButtonImagePath { get; set; }
    public string UIResearchButtonImagePath { get; set; }

    public List<string> DroidAnimationFramePaths { get; set; } = new List<string>();

    public float DroidPositionX { get; set; }
    public float DroidPositionY { get; set; }

    public Dictionary<string, string> GenericVoiceLinePaths { get; set; } =
        new Dictionary<string, string>();

    private Color primaryColor;
    private bool colorParsed;

    public Color GetPrimaryColor()
    {
        if (!colorParsed)
        {
            if (!FactionPrimaryColorHex.StartsWith("#"))
                FactionPrimaryColorHex = "#" + FactionPrimaryColorHex;

            if (!ColorUtility.TryParseHtmlString(FactionPrimaryColorHex, out primaryColor))
                primaryColor = Color.white;

            colorParsed = true;
        }

        return primaryColor;
    }

    public string GetFleetTileImagePath()
    {
        return PlanetOverlayTheme?.UnitTileIcons?.FleetTileImagePath;
    }

    public string GetMissionTileImagePath()
    {
        return PlanetOverlayTheme?.UnitTileIcons?.MissionTileImagePath;
    }

    public string GetFleetsPaneImagePath()
    {
        return PlanetWindowTheme?.FleetsPane?.FleetsImagePath;
    }

    public FleetTabsTheme GetFleetTabsTheme()
    {
        return PlanetWindowTheme?.FleetsPane?.FleetTabs;
    }

    public GarrisonPanelTheme GetGarrisonPanelTheme()
    {
        return PlanetWindowTheme?.GarrisonPanel;
    }

    public BuildingsPaneTheme GetBuildingsPaneTheme()
    {
        return PlanetWindowTheme?.BuildingsPane;
    }
}

[PersistableObject]
public sealed class FactionThemes : List<FactionTheme> { }
