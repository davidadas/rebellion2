using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Util.Serialization;
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
    public SourceRectLayout TickCounterSourceLayout { get; set; }
    public SourceRectLayout RawMaterialsSourceLayout { get; set; }
    public SourceRectLayout RefinedMaterialsSourceLayout { get; set; }
    public SourceRectLayout MaintenanceSourceLayout { get; set; }
    public SourceRectLayout SpeedIndicatorSourceLayout { get; set; }
    public SourceRectLayout SpeedContextSourceLayout { get; set; }
    public SpeedIndicatorTheme SpeedIndicators { get; set; }
    public List<StrategyHudMessageNotificationTheme> MessageNotifications { get; set; } =
        new List<StrategyHudMessageNotificationTheme>();
    public List<StrategyHudButtonTheme> Buttons { get; set; } = new List<StrategyHudButtonTheme>();
}

[PersistableObject]
public class SourceRectLayout
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

[PersistableObject]
public class StrategyHudButtonTheme
{
    public int Action { get; set; }
    public string PressedImagePath { get; set; }
    public SourceRectLayout PressedImageLayout { get; set; }
    public SourceRectLayout HitArea { get; set; }
}

[PersistableObject]
public class StrategyHudMessageNotificationTheme
{
    public MessageType MessageType { get; set; }
    public int Tab { get; set; }
    public string DefaultImagePath { get; set; }
    public string HighlightedImagePath { get; set; }
    public SourceRectLayout SourceLayout { get; set; }
}

[PersistableObject]
public class SpeedIndicatorTheme
{
    public string PausedImagePath { get; set; }
    public string VerySlowImagePath { get; set; }
    public string SlowImagePath { get; set; }
    public string MediumImagePath { get; set; }
    public string FastImagePath { get; set; }

    public string GetImagePath(int sourceSpeed)
    {
        return sourceSpeed switch
        {
            1 => VerySlowImagePath,
            2 => SlowImagePath,
            3 => MediumImagePath,
            4 => FastImagePath,
            _ => PausedImagePath,
        };
    }
}

[PersistableObject]
public class GalaxyBackground
{
    public string ImagePath { get; set; }
    public SourcePointLayout SourcePosition { get; set; }
    public SourcePointLayout StarOffset { get; set; }
    public PlanetIcons PlanetIcons;
}

[PersistableObject]
public class SourcePointLayout
{
    public int X { get; set; }
    public int Y { get; set; }

    public Vector2Int ToVector2Int()
    {
        return new Vector2Int(X, Y);
    }
}

[PersistableObject]
public class StrategyWindowPlacements
{
    public SourcePointLayout SectorLeftPosition { get; set; }
    public SourcePointLayout SectorRightPosition { get; set; }
    public SourcePointLayout UtilityWindowPosition { get; set; }
    public SourcePointLayout StatusWindowPosition { get; set; }
    public SourcePointLayout ConfirmWindowPosition { get; set; }
    public SourcePointLayout MissionCreateOffset { get; set; }
}

[PersistableObject]
public class StrategyBookmarkLayout
{
    public int StartX { get; set; }
    public int StartY { get; set; }
    public int Width { get; set; }
    public int ListHeight { get; set; }
    public int ItemHeight { get; set; }
    public int IconOffsetY { get; set; }
    public int IconWidth { get; set; }
    public int IconHeight { get; set; }
    public int LabelOffsetX { get; set; }
    public int LabelOffsetY { get; set; }
}

[PersistableObject]
public class StrategyBookmarkIcons
{
    public string FacilityImagePath { get; set; }
    public string DefenseImagePath { get; set; }
    public string FleetImagePath { get; set; }
    public string MissionImagePath { get; set; }
}

[PersistableObject]
public class ConfirmDialogTheme
{
    public string BackgroundImagePath { get; set; }
    public string MoveTitleImagePath { get; set; }
    public string ScrapTitleImagePath { get; set; }
    public string RetireTitleImagePath { get; set; }
}

[PersistableObject]
public class StrategyContextMenuTheme
{
    public string ArrowOnImagePath { get; set; }
    public string ArrowOffImagePath { get; set; }
    public string CheckMarkImagePath { get; set; }
}

[PersistableObject]
public class WindowTitleTheme
{
    public string ActiveImagePath { get; set; }
    public string InactiveImagePath { get; set; }
}

[PersistableObject]
public class WindowTabImageTheme
{
    public string ActiveImagePath { get; set; }
    public string InactiveImagePath { get; set; }
    public string DisabledImagePath { get; set; }
    public string EmptyImagePath { get; set; }

    public string GetImagePath(int state)
    {
        return state switch
        {
            0 => ActiveImagePath,
            1 => InactiveImagePath,
            _ => DisabledImagePath,
        };
    }

    public string GetImagePath(bool enabled, bool active)
    {
        if (active)
            return ActiveImagePath;

        return enabled ? InactiveImagePath : DisabledImagePath;
    }

    public string GetImagePathForContent(bool hasItems, bool active)
    {
        if (active)
            return ActiveImagePath;

        return hasItems ? InactiveImagePath : EmptyImagePath;
    }
}

[PersistableObject]
public class WindowButtonImageTheme
{
    public string UpImagePath { get; set; }
    public string DownImagePath { get; set; }
    public string DisabledImagePath { get; set; }
    public SourceRectLayout SourceLayout { get; set; }

    public string GetImagePath(bool pressed)
    {
        return pressed ? DownImagePath : UpImagePath;
    }
}

[PersistableObject]
public class FacilityWindowTheme
{
    public WindowTabImageTheme ControlTab { get; set; }
    public string SelectionImagePath { get; set; }
    public List<FacilityConstructionImageTheme> ConstructionImages { get; set; } =
        new List<FacilityConstructionImageTheme>();

    public string GetConstructionImagePath(string typeId)
    {
        if (string.IsNullOrEmpty(typeId))
            return null;

        foreach (FacilityConstructionImageTheme image in ConstructionImages)
        {
            if (image?.TypeID == typeId)
                return image.ImagePath;
        }

        return null;
    }
}

[PersistableObject]
public class FacilityConstructionImageTheme
{
    public string TypeID { get; set; }
    public string ImagePath { get; set; }
}

[PersistableObject]
public class DefenseWindowTheme
{
    public WindowTabImageTheme PersonnelTab { get; set; }
    public WindowTabImageTheme TroopTab { get; set; }
    public WindowTabImageTheme FighterTab { get; set; }
    public string SelectionImagePath { get; set; }
}

[PersistableObject]
public class FleetWindowTheme
{
    public string DetailBackgroundImagePath { get; set; }
    public string BannerImagePath { get; set; }
    public FleetWindowTabsTheme Tabs { get; set; }
}

[PersistableObject]
public class FleetWindowTabsTheme
{
    public WindowTabImageTheme CapitalShips { get; set; }
    public WindowTabImageTheme Starfighters { get; set; }
    public WindowTabImageTheme Regiments { get; set; }
    public WindowTabImageTheme Officers { get; set; }
}

[PersistableObject]
public class MissionsWindowTheme
{
    public WindowTabImageTheme AgentsTab { get; set; }
    public WindowTabImageTheme DecoysTab { get; set; }
    public string SelectionImagePath { get; set; }
}

[PersistableObject]
public class MissionCreateWindowTheme
{
    public string TitleImagePath { get; set; }
    public WindowTabImageTheme MissionTab { get; set; }
    public WindowTabImageTheme PersonnelTab { get; set; }
    public string AgentsHeaderImagePath { get; set; }
    public string DecoysHeaderImagePath { get; set; }
}

[PersistableObject]
public class StatusWindowTheme
{
    public string BackgroundImagePath { get; set; }
    public string FleetBannerImagePath { get; set; }
    public string FleetBannerEnrouteImagePath { get; set; }
    public string FleetBannerDamagedImagePath { get; set; }
    public string ShipyardImagePath { get; set; }
    public string ConstructionImagePath { get; set; }
    public string TrainingImagePath { get; set; }
    public string FactionConstructionImagePath { get; set; }
    public string EnrouteImagePath { get; set; }
    public string PersonnelBackgroundImagePath { get; set; }
}

[PersistableObject]
public class MissionIconSetTheme
{
    public List<MissionIconTheme> Icons { get; set; } = new List<MissionIconTheme>();

    public string GetImagePath(string key, bool small)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        foreach (MissionIconTheme icon in Icons)
        {
            if (icon?.Key == key)
                return icon.GetImagePath(small);
        }

        return null;
    }
}

[PersistableObject]
public class MissionIconTheme
{
    public string Key { get; set; }
    public string LargeImagePath { get; set; }
    public string SmallImagePath { get; set; }

    public string GetImagePath(bool small)
    {
        return small ? SmallImagePath : LargeImagePath;
    }
}

[PersistableObject]
public class MessagesWindowTheme
{
    public WindowButtonImageTheme SupportButton { get; set; }
    public WindowButtonImageTheme FleetButton { get; set; }
    public WindowButtonImageTheme MissionsButton { get; set; }
    public WindowButtonImageTheme AdviceButton { get; set; }
    public WindowButtonImageTheme CloseButton { get; set; }
    public WindowButtonImageTheme DisplayButton { get; set; }
    public WindowButtonImageTheme IndexButton { get; set; }
    public WindowButtonImageTheme SignalButton { get; set; }
    public WindowButtonImageTheme SignalTargetButton { get; set; }
    public WindowButtonImageTheme ChatCommandButton { get; set; }
    public string LoyaltyIconImagePath { get; set; }
    public string MissionIconImagePath { get; set; }
    public string SelectionImagePath { get; set; }
    public string OverlayFrameImagePath { get; set; }
    public string ButtonStripImagePath { get; set; }
    public string SelectedRowTextColorHex { get; set; }
    public List<MessageDetailImageTheme> DetailImages { get; set; } =
        new List<MessageDetailImageTheme>();

    private Color selectedRowTextColor;
    private bool selectedRowTextColorParsed;

    public string GetDetailImagePath(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        foreach (MessageDetailImageTheme image in DetailImages)
        {
            if (image?.Key == key)
                return image.ImagePath;
        }

        return null;
    }

    public Color GetSelectedRowTextColor()
    {
        if (!selectedRowTextColorParsed)
        {
            string colorHex = SelectedRowTextColorHex;
            if (string.IsNullOrEmpty(colorHex))
                colorHex = "FFFFFF";
            if (!colorHex.StartsWith("#"))
                colorHex = "#" + colorHex;
            if (!ColorUtility.TryParseHtmlString(colorHex, out selectedRowTextColor))
                selectedRowTextColor = Color.white;
            selectedRowTextColorParsed = true;
        }

        return selectedRowTextColor;
    }
}

[PersistableObject]
public class MessageDetailImageTheme
{
    public string Key { get; set; }
    public string ImagePath { get; set; }
}

[PersistableObject]
public class FinderWindowTheme
{
    public WindowButtonImageTheme SystemsButton { get; set; }
    public WindowButtonImageTheme NeutralSystemsButton { get; set; }
    public WindowButtonImageTheme FleetButton { get; set; }
    public WindowButtonImageTheme ShipButton { get; set; }
    public WindowButtonImageTheme PersonnelButton { get; set; }
    public WindowButtonImageTheme SpecialForcesButton { get; set; }
    public WindowButtonImageTheme CloseButton { get; set; }
    public WindowButtonImageTheme TargetButton { get; set; }
    public string ShipFinderBackgroundImagePath { get; set; }
    public string FleetFinderBackgroundImagePath { get; set; }
    public string PersonnelFinderBackgroundImagePath { get; set; }
    public string SpecialForcesFinderBackgroundImagePath { get; set; }
    public string TroopFinderBackgroundImagePath { get; set; }
    public string SystemsText { get; set; }
    public string FleetsText { get; set; }
    public string ShipsText { get; set; }
    public string TroopsText { get; set; }
    public string PersonnelText { get; set; }
    public string OverlayFrameImagePath { get; set; }
    public string TwoButtonStripImagePath { get; set; }
    public string FourButtonStripImagePath { get; set; }
}

[PersistableObject]
public class EncyclopediaWindowTheme
{
    public WindowButtonImageTheme FacilityButton { get; set; }
    public WindowButtonImageTheme PersonnelButton { get; set; }
    public WindowButtonImageTheme ShipButton { get; set; }
    public WindowButtonImageTheme TroopButton { get; set; }
    public WindowButtonImageTheme MissionsButton { get; set; }
    public WindowButtonImageTheme CloseButton { get; set; }
    public WindowButtonImageTheme TopicButton { get; set; }
    public WindowButtonImageTheme IndexButton { get; set; }
    public string OverlayFrameImagePath { get; set; }
    public string ButtonStripImagePath { get; set; }
}

[PersistableObject]
public class StrategyWindowsTheme
{
    public FacilityWindowTheme Facility { get; set; }
    public DefenseWindowTheme Defense { get; set; }
    public FleetWindowTheme Fleet { get; set; }
    public MissionsWindowTheme Missions { get; set; }
    public MissionCreateWindowTheme MissionCreate { get; set; }
    public StatusWindowTheme Status { get; set; }
    public MessagesWindowTheme Messages { get; set; }
    public FinderWindowTheme Finder { get; set; }
    public EncyclopediaWindowTheme Encyclopedia { get; set; }
}

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

    public string GalaxyHeadquartersImagePath { get; set; }

    public string PlanetSystemHeadquartersImagePath { get; set; }
}

[PersistableObject]
public class MissionsPaneTheme
{
    public MissionTabsTheme MissionTabs { get; set; }
}

[PersistableObject]
public class MissionTabsTheme
{
    public FleetTabIconSet PrimaryParticipants { get; set; }
    public FleetTabIconSet SecondaryParticipants { get; set; }
}

[PersistableObject]
public class PlanetWindowTheme
{
    public BuildingsPaneTheme BuildingsPane { get; set; }

    public FleetsPaneTheme FleetsPane { get; set; }

    public GarrisonPanelTheme GarrisonPanel { get; set; }

    public MissionsPaneTheme MissionsPane { get; set; }
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

    public ManufacturingLaneStateTheme ManufacturingLaneState { get; set; }
}

[PersistableObject]
public class BuildingsTabsTheme
{
    public FleetTabIconSet Production { get; set; }
}

[PersistableObject]
public class ManufacturingLaneStateTheme
{
    public string ActiveImagePath { get; set; }
    public string InactiveImagePath { get; set; }
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
    public bool UseUpperButtonLayout { get; set; }
    public string SaveMenuReturnStrategyButtonImagePath { get; set; }
    public string SaveMenuSlotIconImagePath { get; set; }

    public string IntroCutscenePath { get; set; }
    public string VictoryCutscenePath { get; set; }
    public string DefeatCutscenePath { get; set; }

    public TacticalHUDLayout TacticalHUDLayout { get; set; }
    public GalaxyBackground GalaxyBackground { get; set; }
    public StrategyWindowPlacements StrategyWindowPlacements { get; set; }
    public StrategyBookmarkLayout StrategyBookmarkLayout { get; set; }
    public StrategyBookmarkIcons StrategyBookmarkIcons { get; set; }
    public ConfirmDialogTheme ConfirmDialogTheme { get; set; }
    public StrategyContextMenuTheme StrategyContextMenuTheme { get; set; }
    public WindowTitleTheme WindowTitleTheme { get; set; }
    public StrategyWindowsTheme StrategyWindows { get; set; }
    public MissionIconSetTheme MissionIcons { get; set; }

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

    public string GetSelectionTileImagePath()
    {
        return PlanetOverlayTheme?.UnitTileIcons?.FleetListSelectionImagePath;
    }

    public string GetFleetListIconImagePath()
    {
        return PlanetOverlayTheme?.UnitTileIcons?.FleetListIconImagePath;
    }

    public string GetFleetListSelectionImagePath()
    {
        return PlanetOverlayTheme?.UnitTileIcons?.FleetListSelectionImagePath;
    }

    public string GetFleetDetailSelectionImagePath()
    {
        return PlanetOverlayTheme?.UnitTileIcons?.FleetDetailSelectionImagePath;
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

    public MissionsPaneTheme GetMissionsPaneTheme()
    {
        return PlanetWindowTheme?.MissionsPane;
    }
}

[PersistableObject]
public sealed class FactionThemes : List<FactionTheme> { }
