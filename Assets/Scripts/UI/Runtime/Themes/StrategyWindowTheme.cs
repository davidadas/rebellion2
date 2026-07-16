using Rebellion.Util.Serialization;

/// <summary>
/// Defines source-resolution placements for strategy windows.
/// </summary>
[PersistableObject]
public class StrategyWindowPlacements
{
    /// <summary>
    /// Gets or sets the window bounds.
    /// </summary>
    public SourceRectLayout WindowBounds { get; set; }

    /// <summary>
    /// Gets or sets the sector left position.
    /// </summary>
    public SourcePointLayout SectorLeftPosition { get; set; }

    /// <summary>
    /// Gets or sets the sector middle position.
    /// </summary>
    public SourcePointLayout SectorMiddlePosition { get; set; }

    /// <summary>
    /// Gets or sets the sector right position.
    /// </summary>
    public SourcePointLayout SectorRightPosition { get; set; }

    /// <summary>
    /// Gets or sets the utility window position.
    /// </summary>
    public SourcePointLayout UtilityWindowPosition { get; set; }

    /// <summary>
    /// Gets or sets the mission create offset.
    /// </summary>
    public SourcePointLayout MissionCreateOffset { get; set; }
}

/// <summary>
/// Defines strategy window lifecycle sound paths.
/// </summary>
[PersistableObject]
public class StrategyWindowSoundTheme
{
    /// <summary>
    /// Gets or sets the planet window open sound path.
    /// </summary>
    public string PlanetWindowOpenSoundPath { get; set; }

    /// <summary>
    /// Gets or sets the planet window expand sound path.
    /// </summary>
    public string PlanetWindowExpandSoundPath { get; set; }

    /// <summary>
    /// Gets or sets the planet window collapse sound path.
    /// </summary>
    public string PlanetWindowCollapseSoundPath { get; set; }

    /// <summary>
    /// Gets or sets the planet window minimize sound path.
    /// </summary>
    public string PlanetWindowMinimizeSoundPath { get; set; }
}

/// <summary>
/// Defines the bookmark-list source layout.
/// </summary>
[PersistableObject]
public class StrategyBookmarkLayout
{
    /// <summary>
    /// Gets or sets the start x.
    /// </summary>
    public int StartX { get; set; }

    /// <summary>
    /// Gets or sets the start y.
    /// </summary>
    public int StartY { get; set; }

    /// <summary>
    /// Gets or sets the width.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the list height.
    /// </summary>
    public int ListHeight { get; set; }

    /// <summary>
    /// Gets or sets the item height.
    /// </summary>
    public int ItemHeight { get; set; }

    /// <summary>
    /// Gets or sets the icon offset y.
    /// </summary>
    public int IconOffsetY { get; set; }

    /// <summary>
    /// Gets or sets the icon width.
    /// </summary>
    public int IconWidth { get; set; }

    /// <summary>
    /// Gets or sets the icon height.
    /// </summary>
    public int IconHeight { get; set; }

    /// <summary>
    /// Gets or sets the label offset x.
    /// </summary>
    public int LabelOffsetX { get; set; }

    /// <summary>
    /// Gets or sets the label offset y.
    /// </summary>
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
    /// <summary>
    /// Gets or sets the facility image path.
    /// </summary>
    public string FacilityImagePath { get; set; }

    /// <summary>
    /// Gets or sets the defense image path.
    /// </summary>
    public string DefenseImagePath { get; set; }

    /// <summary>
    /// Gets or sets the fleet image path.
    /// </summary>
    public string FleetImagePath { get; set; }

    /// <summary>
    /// Gets or sets the mission image path.
    /// </summary>
    public string MissionImagePath { get; set; }
}

/// <summary>
/// Defines confirmation-dialog artwork and audio.
/// </summary>
[PersistableObject]
public class ConfirmDialogTheme
{
    /// <summary>
    /// Gets or sets the background image path.
    /// </summary>
    public string BackgroundImagePath { get; set; }

    /// <summary>
    /// Gets or sets the move title image path.
    /// </summary>
    public string MoveTitleImagePath { get; set; }

    /// <summary>
    /// Gets or sets the scrap title image path.
    /// </summary>
    public string ScrapTitleImagePath { get; set; }

    /// <summary>
    /// Gets or sets the retire title image path.
    /// </summary>
    public string RetireTitleImagePath { get; set; }

    /// <summary>
    /// Gets or sets the stop-construction title image path.
    /// </summary>
    public string StopConstructionTitleImagePath { get; set; }

    /// <summary>
    /// Gets or sets the scrap retire sound path.
    /// </summary>
    public string ScrapRetireSoundPath { get; set; }

    /// <summary>
    /// Gets or sets the stop-construction sound path.
    /// </summary>
    public string StopConstructionSoundPath { get; set; }
}

/// <summary>
/// Defines submenu and checked-state artwork for strategy context menus.
/// </summary>
[PersistableObject]
public class StrategyContextMenuTheme
{
    /// <summary>
    /// Gets or sets the arrow on image path.
    /// </summary>
    public string ArrowOnImagePath { get; set; }

    /// <summary>
    /// Gets or sets the arrow off image path.
    /// </summary>
    public string ArrowOffImagePath { get; set; }

    /// <summary>
    /// Gets or sets the check mark image path.
    /// </summary>
    public string CheckMarkImagePath { get; set; }
}

/// <summary>
/// Groups the presentation themes for all strategy windows.
/// </summary>
[PersistableObject]
public class StrategyWindowsTheme
{
    /// <summary>
    /// Gets or sets the facility.
    /// </summary>
    public FacilityWindowTheme Facility { get; set; }

    /// <summary>
    /// Gets or sets the defense.
    /// </summary>
    public DefenseWindowTheme Defense { get; set; }

    /// <summary>
    /// Gets or sets the fleet.
    /// </summary>
    public FleetWindowTheme Fleet { get; set; }

    /// <summary>
    /// Gets or sets the missions.
    /// </summary>
    public MissionsWindowTheme Missions { get; set; }

    /// <summary>
    /// Gets or sets the mission create.
    /// </summary>
    public MissionCreateWindowTheme MissionCreate { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public StatusWindowTheme Status { get; set; }

    /// <summary>
    /// Gets or sets the advisor report.
    /// </summary>
    public AdvisorReportWindowTheme AdvisorReport { get; set; }

    /// <summary>
    /// Gets or sets the battle alert.
    /// </summary>
    public BattleAlertWindowTheme BattleAlert { get; set; }

    /// <summary>
    /// Gets or sets the messages.
    /// </summary>
    public MessagesWindowTheme Messages { get; set; }

    /// <summary>
    /// Gets or sets the finder.
    /// </summary>
    public FinderWindowTheme Finder { get; set; }

    /// <summary>
    /// Gets or sets the encyclopedia.
    /// </summary>
    public EncyclopediaWindowTheme Encyclopedia { get; set; }
}
