using Rebellion.Util.Serialization;

/// <summary>
/// Defines Finder controls, result backgrounds, labels, and overlays.
/// </summary>
[PersistableObject]
public class FinderWindowTheme
{
    /// <summary>
    /// Gets or sets the all systems button.
    /// </summary>
    public WindowButtonImageTheme AllSystemsButton { get; set; }

    /// <summary>
    /// Gets or sets the systems button.
    /// </summary>
    public WindowButtonImageTheme SystemsButton { get; set; }

    /// <summary>
    /// Gets or sets the neutral systems button.
    /// </summary>
    public WindowButtonImageTheme NeutralSystemsButton { get; set; }

    /// <summary>
    /// Gets or sets the unexplored systems button.
    /// </summary>
    public WindowButtonImageTheme UnexploredSystemsButton { get; set; }

    /// <summary>
    /// Gets or sets the fleet button.
    /// </summary>
    public WindowButtonImageTheme FleetButton { get; set; }

    /// <summary>
    /// Gets or sets the ship button.
    /// </summary>
    public WindowButtonImageTheme ShipButton { get; set; }

    /// <summary>
    /// Gets or sets the personnel button.
    /// </summary>
    public WindowButtonImageTheme PersonnelButton { get; set; }

    /// <summary>
    /// Gets or sets the special forces button.
    /// </summary>
    public WindowButtonImageTheme SpecialForcesButton { get; set; }

    /// <summary>
    /// Gets or sets the close button.
    /// </summary>
    public WindowButtonImageTheme CloseButton { get; set; }

    /// <summary>
    /// Gets or sets the target button.
    /// </summary>
    public WindowButtonImageTheme TargetButton { get; set; }

    /// <summary>
    /// Gets or sets the system finder background image path.
    /// </summary>
    public string SystemFinderBackgroundImagePath { get; set; }

    /// <summary>
    /// Gets or sets the ship finder background image path.
    /// </summary>
    public string ShipFinderBackgroundImagePath { get; set; }

    /// <summary>
    /// Gets or sets the fleet finder background image path.
    /// </summary>
    public string FleetFinderBackgroundImagePath { get; set; }

    /// <summary>
    /// Gets or sets the personnel finder background image path.
    /// </summary>
    public string PersonnelFinderBackgroundImagePath { get; set; }

    /// <summary>
    /// Gets or sets the special forces finder background image path.
    /// </summary>
    public string SpecialForcesFinderBackgroundImagePath { get; set; }

    /// <summary>
    /// Gets or sets the troop finder background image path.
    /// </summary>
    public string TroopFinderBackgroundImagePath { get; set; }

    /// <summary>
    /// Gets or sets the systems text.
    /// </summary>
    public string SystemsText { get; set; }

    /// <summary>
    /// Gets or sets the fleets text.
    /// </summary>
    public string FleetsText { get; set; }

    /// <summary>
    /// Gets or sets the ships text.
    /// </summary>
    public string ShipsText { get; set; }

    /// <summary>
    /// Gets or sets the troops text.
    /// </summary>
    public string TroopsText { get; set; }

    /// <summary>
    /// Gets or sets the personnel text.
    /// </summary>
    public string PersonnelText { get; set; }

    /// <summary>
    /// Gets or sets the overlay frame image path.
    /// </summary>
    public string OverlayFrameImagePath { get; set; }

    /// <summary>
    /// Gets or sets the two button strip image path.
    /// </summary>
    public string TwoButtonStripImagePath { get; set; }

    /// <summary>
    /// Gets or sets the four button strip image path.
    /// </summary>
    public string FourButtonStripImagePath { get; set; }
}

/// <summary>
/// Defines Encyclopedia controls and frame artwork.
/// </summary>
[PersistableObject]
public class EncyclopediaWindowTheme
{
    /// <summary>
    /// Gets or sets the all databases button.
    /// </summary>
    public WindowButtonImageTheme AllDatabasesButton { get; set; }

    /// <summary>
    /// Gets or sets the systems button.
    /// </summary>
    public WindowButtonImageTheme SystemsButton { get; set; }

    /// <summary>
    /// Gets or sets the facility button.
    /// </summary>
    public WindowButtonImageTheme FacilityButton { get; set; }

    /// <summary>
    /// Gets or sets the personnel button.
    /// </summary>
    public WindowButtonImageTheme PersonnelButton { get; set; }

    /// <summary>
    /// Gets or sets the ship button.
    /// </summary>
    public WindowButtonImageTheme ShipButton { get; set; }

    /// <summary>
    /// Gets or sets the troop button.
    /// </summary>
    public WindowButtonImageTheme TroopButton { get; set; }

    /// <summary>
    /// Gets or sets the missions button.
    /// </summary>
    public WindowButtonImageTheme MissionsButton { get; set; }

    /// <summary>
    /// Gets or sets the close button.
    /// </summary>
    public WindowButtonImageTheme CloseButton { get; set; }

    /// <summary>
    /// Gets or sets the topic button.
    /// </summary>
    public WindowButtonImageTheme TopicButton { get; set; }

    /// <summary>
    /// Gets or sets the index button.
    /// </summary>
    public WindowButtonImageTheme IndexButton { get; set; }

    /// <summary>
    /// Gets or sets the overlay frame image path.
    /// </summary>
    public string OverlayFrameImagePath { get; set; }

    /// <summary>
    /// Gets or sets the button strip image path.
    /// </summary>
    public string ButtonStripImagePath { get; set; }
}
