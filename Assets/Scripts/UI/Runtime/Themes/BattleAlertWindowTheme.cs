using Rebellion.Util.Serialization;

/// <summary>
/// Defines battle-alert frames, music, force labels, result artwork, and controls.
/// </summary>
[PersistableObject]
public class BattleAlertWindowTheme
{
    /// <summary>
    /// Gets or sets the frame image path.
    /// </summary>
    public string FrameImagePath { get; set; }

    /// <summary>
    /// Gets or sets the result frame image path.
    /// </summary>
    public string ResultFrameImagePath { get; set; }

    /// <summary>
    /// Gets or sets the result summary image path.
    /// </summary>
    public string ResultSummaryImagePath { get; set; }

    /// <summary>
    /// Gets or sets the battle music path.
    /// </summary>
    public string BattleMusicPath { get; set; }

    /// <summary>
    /// Gets or sets the result music path.
    /// </summary>
    public string ResultMusicPath { get; set; }

    /// <summary>
    /// Gets or sets the result victory music path.
    /// </summary>
    public string ResultVictoryMusicPath { get; set; }

    /// <summary>
    /// Gets or sets the result defeat music path.
    /// </summary>
    public string ResultDefeatMusicPath { get; set; }

    /// <summary>
    /// Gets or sets the result draw music path.
    /// </summary>
    public string ResultDrawMusicPath { get; set; }

    /// <summary>
    /// Gets or sets the summary background image path.
    /// </summary>
    public string SummaryBackgroundImagePath { get; set; }

    /// <summary>
    /// Gets or sets the list background image path.
    /// </summary>
    public string ListBackgroundImagePath { get; set; }

    /// <summary>
    /// Gets or sets the result list background image path.
    /// </summary>
    public string ResultListBackgroundImagePath { get; set; }

    /// <summary>
    /// Gets or sets the result personnel list background image path.
    /// </summary>
    public string ResultPersonnelListBackgroundImagePath { get; set; }

    /// <summary>
    /// Gets or sets the result direct background image path.
    /// </summary>
    public string ResultDirectBackgroundImagePath { get; set; }

    /// <summary>
    /// Gets or sets the first forces owner instance ID.
    /// </summary>
    public string FirstForcesOwnerInstanceID { get; set; }

    /// <summary>
    /// Gets or sets the second forces owner instance ID.
    /// </summary>
    public string SecondForcesOwnerInstanceID { get; set; }

    /// <summary>
    /// Gets or sets the first forces defeated image path.
    /// </summary>
    public string FirstForcesDefeatedImagePath { get; set; }

    /// <summary>
    /// Gets or sets the first forces victorious image path.
    /// </summary>
    public string FirstForcesVictoriousImagePath { get; set; }

    /// <summary>
    /// Gets or sets the second forces defeated image path.
    /// </summary>
    public string SecondForcesDefeatedImagePath { get; set; }

    /// <summary>
    /// Gets or sets the second forces victorious image path.
    /// </summary>
    public string SecondForcesVictoriousImagePath { get; set; }

    /// <summary>
    /// Gets or sets the first forces header text.
    /// </summary>
    public string FirstForcesHeaderText { get; set; }

    /// <summary>
    /// Gets or sets the second forces header text.
    /// </summary>
    public string SecondForcesHeaderText { get; set; }

    /// <summary>
    /// Gets or sets the first forces summary label.
    /// </summary>
    public string FirstForcesSummaryLabel { get; set; }

    /// <summary>
    /// Gets or sets the second forces summary label.
    /// </summary>
    public string SecondForcesSummaryLabel { get; set; }

    /// <summary>
    /// Gets or sets the summary button.
    /// </summary>
    public WindowButtonImageTheme SummaryButton { get; set; }

    /// <summary>
    /// Gets or sets the first forces button.
    /// </summary>
    public WindowButtonImageTheme FirstForcesButton { get; set; }

    /// <summary>
    /// Gets or sets the second forces button.
    /// </summary>
    public WindowButtonImageTheme SecondForcesButton { get; set; }

    /// <summary>
    /// Gets or sets the system assets button.
    /// </summary>
    public WindowButtonImageTheme SystemAssetsButton { get; set; }

    /// <summary>
    /// Gets or sets the result summary button.
    /// </summary>
    public WindowButtonImageTheme ResultSummaryButton { get; set; }

    /// <summary>
    /// Gets or sets the first forces result defeated button.
    /// </summary>
    public WindowButtonImageTheme FirstForcesResultDefeatedButton { get; set; }

    /// <summary>
    /// Gets or sets the first forces result victorious button.
    /// </summary>
    public WindowButtonImageTheme FirstForcesResultVictoriousButton { get; set; }

    /// <summary>
    /// Gets or sets the second forces result defeated button.
    /// </summary>
    public WindowButtonImageTheme SecondForcesResultDefeatedButton { get; set; }

    /// <summary>
    /// Gets or sets the second forces result victorious button.
    /// </summary>
    public WindowButtonImageTheme SecondForcesResultVictoriousButton { get; set; }

    /// <summary>
    /// Gets or sets the result direct button.
    /// </summary>
    public WindowButtonImageTheme ResultDirectButton { get; set; }

    /// <summary>
    /// Gets or sets the result capital ships button.
    /// </summary>
    public WindowButtonImageTheme ResultCapitalShipsButton { get; set; }

    /// <summary>
    /// Gets or sets the result starfighters button.
    /// </summary>
    public WindowButtonImageTheme ResultStarfightersButton { get; set; }

    /// <summary>
    /// Gets or sets the result troops button.
    /// </summary>
    public WindowButtonImageTheme ResultTroopsButton { get; set; }

    /// <summary>
    /// Gets or sets the result personnel button.
    /// </summary>
    public WindowButtonImageTheme ResultPersonnelButton { get; set; }

    /// <summary>
    /// Gets or sets the result direct system button.
    /// </summary>
    public WindowButtonImageTheme ResultDirectSystemButton { get; set; }

    /// <summary>
    /// Gets or sets the result direct fleet button.
    /// </summary>
    public WindowButtonImageTheme ResultDirectFleetButton { get; set; }

    /// <summary>
    /// Gets or sets the retreat button.
    /// </summary>
    public WindowButtonImageTheme RetreatButton { get; set; }

    /// <summary>
    /// Gets or sets the auto resolve button.
    /// </summary>
    public WindowButtonImageTheme AutoResolveButton { get; set; }

    /// <summary>
    /// Gets or sets the take command button.
    /// </summary>
    public WindowButtonImageTheme TakeCommandButton { get; set; }

    /// <summary>
    /// Gets or sets the result close button.
    /// </summary>
    public WindowButtonImageTheme ResultCloseButton { get; set; }
}
