using System;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;

/// <summary>
/// Adapts a detached combat report to the shared battle-result presentation.
/// </summary>
internal sealed class BattleResultPresentation
{
    private readonly CombatReport report;

    internal BattleResultCategory DefaultCategory =>
        report.CombatType == CombatReportType.PlanetaryAssault
            ? BattleResultCategory.Troops
            : BattleResultCategory.CapitalShips;

    internal string AttackerOwnerInstanceID => report.AttackerOwnerInstanceID;

    internal string DefenderOwnerInstanceID => report.DefenderOwnerInstanceID;

    internal string PlanetInstanceID => report.PlanetInstanceID;

    internal string SoundEffectPath =>
        report.CombatType == CombatReportType.PlanetaryAssault
            ? StrategyUISoundPaths.PlanetaryAssault
            : null;

    internal string Title => report.Title;

    internal bool UsesPlanetaryLayout => report.CombatType != CombatReportType.SpaceBattle;

    /// <summary>
    /// Creates a presentation adapter for a detached combat report.
    /// </summary>
    /// <param name="report">The faction-visible report.</param>
    private BattleResultPresentation(CombatReport report)
    {
        this.report = report ?? throw new ArgumentNullException(nameof(report));
    }

    /// <summary>
    /// Creates a presentation adapter for a detached combat report.
    /// </summary>
    /// <param name="report">The faction-visible report.</param>
    /// <returns>The combat-report presentation.</returns>
    internal static BattleResultPresentation Create(CombatReport report)
    {
        return new BattleResultPresentation(report);
    }

    /// <summary>
    /// Returns the music played when this result opens.
    /// </summary>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="playerFactionId">The current player faction identifier.</param>
    /// <returns>The configured music path, or null when no music applies.</returns>
    internal string GetMusicPath(BattleAlertWindowTheme theme, string playerFactionId)
    {
        if (theme == null || report.CombatType != CombatReportType.SpaceBattle)
            return null;

        CombatSide? playerSide = GetSideForOwner(playerFactionId);
        if (!playerSide.HasValue || report.Winner == CombatSide.Draw)
            return FirstNonBlank(theme.ResultDrawMusicPath, theme.ResultMusicPath);

        return report.Winner == playerSide.Value
            ? FirstNonBlank(theme.ResultVictoryMusicPath, theme.ResultMusicPath)
            : FirstNonBlank(theme.ResultDefeatMusicPath, theme.ResultMusicPath);
    }

    /// <summary>
    /// Returns the outcome summary frozen into the delivered report.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="playerFactionId">The current player faction identifier.</param>
    /// <returns>The displayed result summary.</returns>
    internal string GetSummary(UIContext uiContext, string playerFactionId)
    {
        _ = uiContext;
        _ = playerFactionId;
        return report.Body ?? string.Empty;
    }

    /// <summary>
    /// Returns the summary artwork for this report.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <returns>The selected summary artwork path.</returns>
    internal string GetSummaryImagePath(UIContext uiContext, BattleAlertWindowTheme theme)
    {
        return report.CombatType switch
        {
            CombatReportType.SpaceBattle => GetSpaceSummaryImagePath(uiContext, theme),
            CombatReportType.Bombardment => GetBombardmentSummaryImagePath(theme),
            CombatReportType.PlanetaryAssault => FirstNonBlank(
                uiContext
                    ?.GetTheme(report.AttackerOwnerInstanceID)
                    ?.StrategyWindows?.BattleAlert?.PlanetaryAssaultImagePath,
                theme?.PlanetaryAssaultImagePath,
                theme?.ResultSummaryImagePath
            ),
            _ => theme?.ResultSummaryImagePath,
        };
    }

    /// <summary>
    /// Projects one owner and category into result-table rows.
    /// </summary>
    /// <param name="projector">The table projector.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="ownerInstanceId">The represented owner identifier.</param>
    /// <param name="category">The selected result category.</param>
    /// <returns>The operational and destroyed result columns.</returns>
    internal BattleResultTableRenderData ProjectTable(
        BattleResultTableProjector projector,
        UIContext uiContext,
        string ownerInstanceId,
        BattleResultCategory category
    )
    {
        return projector.ProjectReport(uiContext, report, ownerInstanceId, category);
    }

    /// <summary>
    /// Returns the first nonblank string from an ordered fallback list.
    /// </summary>
    /// <param name="values">The ordered candidate values.</param>
    /// <returns>The first nonblank value, or null when none exists.</returns>
    internal static string FirstNonBlank(params string[] values)
    {
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    /// <summary>
    /// Selects fleet-engagement summary artwork from the saved winner and withdrawal state.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The theme.</param>
    /// <returns>The requested space summary image path.</returns>
    private string GetSpaceSummaryImagePath(UIContext uiContext, BattleAlertWindowTheme theme)
    {
        if (report.Winner == CombatSide.Draw)
            return theme?.ResultSummaryImagePath;

        CombatSide losingSide =
            report.Winner == CombatSide.Attacker ? CombatSide.Defender : CombatSide.Attacker;
        if (GetOutcome(losingSide) == SpaceCombatSideOutcome.Withdrawn)
        {
            return FirstNonBlank(
                GetParticipantImagePath(uiContext, losingSide, victorious: false),
                GetParticipantImagePath(uiContext, report.Winner, victorious: true),
                theme?.ResultSummaryImagePath
            );
        }

        return FirstNonBlank(
            GetParticipantImagePath(uiContext, report.Winner, victorious: true),
            theme?.ResultSummaryImagePath
        );
    }

    /// <summary>
    /// Selects bombardment artwork using every persisted loss indicator.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The requested bombardment summary image path.</returns>
    private string GetBombardmentSummaryImagePath(BattleAlertWindowTheme theme)
    {
        if (
            report.AttackingUnits?.Exists(unit => unit != null && (unit.Damaged || unit.Destroyed))
            == true
        )
            return theme?.BombardmentAttackerLossesImagePath;
        if (
            report.PlanetDestroyed
            || report.HeadquartersDestroyed
            || report.EnergyCapacityDamage > 0
            || report.AllocatedEnergyDamage > 0
            || report.DefendingUnits?.Exists(unit => unit?.Destroyed == true) == true
        )
            return theme?.BombardmentTargetLossesImagePath;
        return theme?.BombardmentNoLossesImagePath;
    }

    /// <summary>
    /// Resolves faction-specific victory or defeat artwork for one saved combat side.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="side">The represented combat side.</param>
    /// <param name="victorious">Whether victory artwork is requested.</param>
    /// <returns>The requested participant image path.</returns>
    private string GetParticipantImagePath(UIContext uiContext, CombatSide side, bool victorious)
    {
        string ownerInstanceId =
            side == CombatSide.Attacker
                ? report.AttackerOwnerInstanceID
                : report.DefenderOwnerInstanceID;
        BattleParticipantTheme participant = uiContext
            ?.GetTheme(ownerInstanceId)
            ?.BattleParticipant;
        return victorious ? participant?.VictoriousImagePath : participant?.DefeatedImagePath;
    }

    /// <summary>
    /// Returns one side's saved fleet outcome.
    /// </summary>
    /// <param name="side">The represented combat side.</param>
    /// <returns>The saved side outcome.</returns>
    private SpaceCombatSideOutcome GetOutcome(CombatSide side)
    {
        return side == CombatSide.Attacker ? report.AttackerOutcome : report.DefenderOutcome;
    }

    /// <summary>
    /// Returns the combat side owned by one faction.
    /// </summary>
    /// <param name="ownerInstanceId">The faction identifier.</param>
    /// <returns>The matching combat side, or null when the faction did not participate.</returns>
    private CombatSide? GetSideForOwner(string ownerInstanceId)
    {
        if (string.IsNullOrEmpty(ownerInstanceId))
            return null;
        if (
            string.Equals(ownerInstanceId, report.AttackerOwnerInstanceID, StringComparison.Ordinal)
        )
            return CombatSide.Attacker;
        if (
            string.Equals(ownerInstanceId, report.DefenderOwnerInstanceID, StringComparison.Ordinal)
        )
            return CombatSide.Defender;
        return null;
    }
}
