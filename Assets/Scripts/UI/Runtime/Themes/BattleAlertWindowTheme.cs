using Rebellion.Util.Serialization;

/// <summary>
/// Defines battle-alert frames, music, force labels, result artwork, and controls.
/// </summary>
[PersistableObject]
public class BattleAlertWindowTheme
{
    public string FrameImagePath { get; set; }

    public string ResultFrameImagePath { get; set; }

    public string ResultSummaryImagePath { get; set; }

    public string PlanetaryAssaultImagePath { get; set; }

    public string BombardmentNoLossesImagePath { get; set; }

    public string BombardmentAttackerLossesImagePath { get; set; }

    public string BombardmentTargetLossesImagePath { get; set; }

    public string BattleMusicPath { get; set; }

    public string ResultMusicPath { get; set; }

    public string ResultVictoryMusicPath { get; set; }

    public string ResultDefeatMusicPath { get; set; }

    public string ResultDrawMusicPath { get; set; }

    public string SummaryBackgroundImagePath { get; set; }

    public string ListBackgroundImagePath { get; set; }

    public string ResultListBackgroundImagePath { get; set; }

    public string ResultPersonnelListBackgroundImagePath { get; set; }

    public string ResultDirectBackgroundImagePath { get; set; }

    public string FirstForcesOwnerInstanceID { get; set; }

    public string SecondForcesOwnerInstanceID { get; set; }

    public string FirstForcesDefeatedImagePath { get; set; }

    public string FirstForcesVictoriousImagePath { get; set; }

    public string SecondForcesDefeatedImagePath { get; set; }

    public string SecondForcesVictoriousImagePath { get; set; }

    public string FirstForcesHeaderText { get; set; }

    public string SecondForcesHeaderText { get; set; }

    public string FirstForcesSummaryLabel { get; set; }

    public string SecondForcesSummaryLabel { get; set; }

    public WindowButtonImageTheme SummaryButton { get; set; }

    public WindowButtonImageTheme FirstForcesButton { get; set; }

    public WindowButtonImageTheme SecondForcesButton { get; set; }

    public WindowButtonImageTheme SystemAssetsButton { get; set; }

    public WindowButtonImageTheme ResultSummaryButton { get; set; }

    public WindowButtonImageTheme FirstForcesResultDefeatedButton { get; set; }

    public WindowButtonImageTheme FirstForcesResultVictoriousButton { get; set; }

    public WindowButtonImageTheme SecondForcesResultDefeatedButton { get; set; }

    public WindowButtonImageTheme SecondForcesResultVictoriousButton { get; set; }

    public WindowButtonImageTheme ResultDirectButton { get; set; }

    public WindowButtonImageTheme ResultCapitalShipsButton { get; set; }

    public WindowButtonImageTheme ResultStarfightersButton { get; set; }

    public WindowButtonImageTheme ResultTroopsButton { get; set; }

    public WindowButtonImageTheme ResultPersonnelButton { get; set; }

    public WindowButtonImageTheme ResultManufacturingButton { get; set; }

    public WindowButtonImageTheme ResultDefenseButton { get; set; }

    public SourceRectLayout PlanetaryResultCapitalShipsLayout { get; set; }

    public SourceRectLayout PlanetaryResultStarfightersLayout { get; set; }

    public SourceRectLayout PlanetaryResultManufacturingLayout { get; set; }

    public SourceRectLayout PlanetaryResultDefenseLayout { get; set; }

    public SourceRectLayout PlanetaryResultTroopsLayout { get; set; }

    public SourceRectLayout PlanetaryResultPersonnelLayout { get; set; }

    public WindowButtonImageTheme ResultDirectSystemButton { get; set; }

    public WindowButtonImageTheme ResultDirectFleetButton { get; set; }

    public WindowButtonImageTheme RetreatButton { get; set; }

    public WindowButtonImageTheme AutoResolveButton { get; set; }

    public WindowButtonImageTheme TakeCommandButton { get; set; }

    public WindowButtonImageTheme ResultCloseButton { get; set; }
}
