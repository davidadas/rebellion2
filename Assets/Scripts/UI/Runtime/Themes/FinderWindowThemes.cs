using Rebellion.Util.Serialization;

/// <summary>
/// Defines Finder controls, result backgrounds, labels, and overlays.
/// </summary>
[PersistableObject]
public class FinderWindowTheme
{
    public WindowButtonImageTheme AllSystemsButton { get; set; }

    public WindowButtonImageTheme SystemsButton { get; set; }

    public WindowButtonImageTheme NeutralSystemsButton { get; set; }

    public WindowButtonImageTheme UnexploredSystemsButton { get; set; }

    public WindowButtonImageTheme FleetButton { get; set; }

    public WindowButtonImageTheme ShipButton { get; set; }

    public WindowButtonImageTheme PersonnelButton { get; set; }

    public WindowButtonImageTheme SpecialForcesButton { get; set; }

    public WindowButtonImageTheme CloseButton { get; set; }

    public WindowButtonImageTheme TargetButton { get; set; }

    public string SystemFinderBackgroundImagePath { get; set; }

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

/// <summary>
/// Defines Encyclopedia controls and frame artwork.
/// </summary>
[PersistableObject]
public class EncyclopediaWindowTheme
{
    public WindowButtonImageTheme AllDatabasesButton { get; set; }

    public WindowButtonImageTheme SystemsButton { get; set; }

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
