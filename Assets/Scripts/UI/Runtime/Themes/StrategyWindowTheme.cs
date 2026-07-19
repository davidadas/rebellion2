using Rebellion.Util.Serialization;

/// <summary>
/// Defines source-resolution placements for strategy windows.
/// </summary>
[PersistableObject]
public class StrategyWindowPlacements
{
    public SourceRectLayout WindowBounds { get; set; }

    public SourcePointLayout SectorLeftPosition { get; set; }

    public SourcePointLayout SectorMiddlePosition { get; set; }

    public SourcePointLayout SectorRightPosition { get; set; }

    public SourcePointLayout UtilityWindowPosition { get; set; }

    public SourcePointLayout MissionCreateOffset { get; set; }
}

/// <summary>
/// Defines strategy window lifecycle sound paths.
/// </summary>
[PersistableObject]
public class StrategyWindowSoundTheme
{
    public string PlanetWindowOpenSoundPath { get; set; }

    public string PlanetWindowExpandSoundPath { get; set; }

    public string PlanetWindowCollapseSoundPath { get; set; }

    public string PlanetWindowMinimizeSoundPath { get; set; }
}

/// <summary>
/// Defines the bookmark-list source layout.
/// </summary>
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

    /// <summary>
    /// Derives the authored bookmark capacity from the list and item heights.
    /// </summary>
    /// <returns>The non-negative number of bookmark rows that fit in the list.</returns>
    public int GetSlotCount()
    {
        return ItemHeight > 0 ? System.Math.Max(0, ListHeight / ItemHeight) : 0;
    }
}

/// <summary>
/// Defines bookmark artwork for supported strategy window categories.
/// </summary>
[PersistableObject]
public class StrategyBookmarkIcons
{
    public string FacilityImagePath { get; set; }

    public string DefenseImagePath { get; set; }

    public string FleetImagePath { get; set; }

    public string MissionImagePath { get; set; }
}

/// <summary>
/// Defines confirmation-dialog artwork and audio.
/// </summary>
[PersistableObject]
public class ConfirmDialogTheme
{
    public string BackgroundImagePath { get; set; }

    public string MoveTitleImagePath { get; set; }

    public string ScrapTitleImagePath { get; set; }

    public string RetireTitleImagePath { get; set; }

    public string StopConstructionTitleImagePath { get; set; }

    public string ScrapRetireSoundPath { get; set; }

    public string StopConstructionSoundPath { get; set; }
}

/// <summary>
/// Defines submenu and checked-state artwork for strategy context menus.
/// </summary>
[PersistableObject]
public class StrategyContextMenuTheme
{
    public string ArrowOnImagePath { get; set; }

    public string ArrowOffImagePath { get; set; }

    public string CheckMarkImagePath { get; set; }
}

/// <summary>
/// Groups the presentation themes for all strategy windows.
/// </summary>
[PersistableObject]
public class StrategyWindowsTheme
{
    public FacilityWindowTheme Facility { get; set; }

    public DefenseWindowTheme Defense { get; set; }

    public FleetWindowTheme Fleet { get; set; }

    public MissionsWindowTheme Missions { get; set; }

    public MissionCreateWindowTheme MissionCreate { get; set; }

    public StatusWindowTheme Status { get; set; }

    public AdvisorReportWindowTheme AdvisorReport { get; set; }

    public BattleAlertWindowTheme BattleAlert { get; set; }

    public MessagesWindowTheme Messages { get; set; }

    public FinderWindowTheme Finder { get; set; }

    public EncyclopediaWindowTheme Encyclopedia { get; set; }
}
