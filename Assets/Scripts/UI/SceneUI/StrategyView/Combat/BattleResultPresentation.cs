using System;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;

/// <summary>
/// Adapts one supported combat result to the shared battle-result presentation.
/// </summary>
internal abstract class BattleResultPresentation
{
    /// <summary>
    /// Returns the default category selected when this result opens.
    /// </summary>
    internal abstract BattleResultCategory DefaultCategory { get; }

    internal abstract string AttackerOwnerInstanceID { get; }

    internal abstract string DefenderOwnerInstanceID { get; }

    /// <summary>
    /// Resolves the live planet represented by this result when it remains available.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <returns>The live planet, or null when it no longer exists.</returns>
    internal abstract Planet GetPlanet(UIContext uiContext);

    /// <summary>
    /// Returns the sound effect played when this result opens.
    /// </summary>
    internal virtual string SoundEffectPath => null;

    /// <summary>
    /// Returns the title displayed for this result.
    /// </summary>
    internal abstract string Title { get; }

    /// <summary>
    /// Returns whether this result uses planetary categories and layouts.
    /// </summary>
    internal abstract bool UsesPlanetaryLayout { get; }

    /// <summary>
    /// Creates a presentation adapter for a supported combat result.
    /// </summary>
    /// <param name="result">The completed combat result.</param>
    /// <returns>The result-specific presentation adapter.</returns>
    internal static BattleResultPresentation Create(GameResult result)
    {
        return result switch
        {
            SpaceCombatResult spaceCombat => new SpaceCombatPresentation(spaceCombat),
            BombardmentResult bombardment => new BombardmentPresentation(bombardment),
            PlanetaryAssaultResult assault => new PlanetaryAssaultPresentation(assault),
            null => throw new ArgumentNullException(nameof(result)),
            _ => throw new ArgumentException("Unsupported battle result.", nameof(result)),
        };
    }

    /// <summary>
    /// Creates a presentation adapter for an outcome detached into a saved message.
    /// </summary>
    /// <param name="report">The saved completed encounter.</param>
    /// <returns>The durable report presentation.</returns>
    internal static BattleResultPresentation Create(CombatReport report)
    {
        return report == null
            ? throw new ArgumentNullException(nameof(report))
            : new SavedCombatReportPresentation(report);
    }

    /// <summary>
    /// Returns the music played when this result opens.
    /// </summary>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="playerFactionId">The current player faction identifier.</param>
    /// <returns>The configured music path, or null when no music applies.</returns>
    internal virtual string GetMusicPath(BattleAlertWindowTheme theme, string playerFactionId)
    {
        return null;
    }

    /// <summary>
    /// Builds the summary text for this result.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="playerFactionId">The current player faction identifier.</param>
    /// <returns>The displayed result summary.</returns>
    internal abstract string GetSummary(UIContext uiContext, string playerFactionId);

    /// <summary>
    /// Returns the summary artwork for this result.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <returns>The selected summary artwork path.</returns>
    internal abstract string GetSummaryImagePath(UIContext uiContext, BattleAlertWindowTheme theme);

    /// <summary>
    /// Projects one owner and category into result-table rows.
    /// </summary>
    /// <param name="projector">The table projector.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="ownerInstanceId">The represented owner identifier.</param>
    /// <param name="category">The selected result category.</param>
    /// <returns>The operational and destroyed result columns.</returns>
    internal abstract BattleResultTableRenderData ProjectTable(
        BattleResultTableProjector projector,
        UIContext uiContext,
        string ownerInstanceId,
        BattleResultCategory category
    );

    /// <summary>
    /// Returns the combat side represented by an owner identifier.
    /// </summary>
    /// <param name="result">The completed combat result.</param>
    /// <param name="ownerInstanceId">The requested owner identifier.</param>
    /// <returns>The matching side, or null when the owner is not represented.</returns>
    internal static CombatSide? GetSideForOwner(SpaceCombatResult result, string ownerInstanceId)
    {
        if (result == null || string.IsNullOrEmpty(ownerInstanceId))
            return null;
        if (ownerInstanceId == result.AttackerOwnerInstanceID)
            return CombatSide.Attacker;
        if (ownerInstanceId == result.DefenderOwnerInstanceID)
            return CombatSide.Defender;
        return null;
    }

    /// <summary>
    /// Returns the completed outcome for one combat side.
    /// </summary>
    /// <param name="result">The completed combat result.</param>
    /// <param name="side">The requested combat side.</param>
    /// <returns>The completed side outcome.</returns>
    internal static SpaceCombatSideOutcome GetOutcome(SpaceCombatResult result, CombatSide side)
    {
        if (result == null)
            return SpaceCombatSideOutcome.Unknown;

        return side switch
        {
            CombatSide.Attacker => result.AttackerOutcome,
            CombatSide.Defender => result.DefenderOutcome,
            _ => SpaceCombatSideOutcome.Unknown,
        };
    }

    /// <summary>
    /// Returns the opposing side of an attacker or defender.
    /// </summary>
    /// <param name="side">The known combat side.</param>
    /// <returns>The opposing side, or null for a draw.</returns>
    internal static CombatSide? GetOpposingSide(CombatSide side)
    {
        return side switch
        {
            CombatSide.Attacker => CombatSide.Defender,
            CombatSide.Defender => CombatSide.Attacker,
            _ => null,
        };
    }

    /// <summary>
    /// Returns victory artwork, or the withdrawing faction's defeated artwork for withdrawal.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="result">The completed combat result.</param>
    /// <returns>The selected summary artwork path.</returns>
    internal static string GetSummaryImagePath(
        UIContext uiContext,
        BattleAlertWindowTheme theme,
        SpaceCombatResult result
    )
    {
        if (theme == null || result == null || result.Winner == CombatSide.Draw)
            return theme?.ResultSummaryImagePath;

        CombatSide? losingSide = GetOpposingSide(result.Winner);
        if (
            losingSide.HasValue
            && GetOutcome(result, losingSide.Value) == SpaceCombatSideOutcome.Withdrawn
        )
        {
            return FirstNonBlank(
                GetDefeatedImagePath(uiContext, result, losingSide.Value),
                GetVictoryImagePath(uiContext, result, result.Winner),
                theme.ResultSummaryImagePath
            );
        }

        return FirstNonBlank(
            GetVictoryImagePath(uiContext, result, result.Winner),
            theme.ResultSummaryImagePath
        );
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
    /// Returns victory artwork for the owner represented by one combat side.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="result">The completed combat result.</param>
    /// <param name="side">The victorious combat side.</param>
    /// <returns>The configured victory artwork path.</returns>
    private static string GetVictoryImagePath(
        UIContext uiContext,
        SpaceCombatResult result,
        CombatSide side
    )
    {
        string ownerInstanceId = GetOwnerIDForSide(result, side);
        return string.IsNullOrEmpty(ownerInstanceId)
            ? null
            : uiContext?.GetTheme(ownerInstanceId)?.BattleParticipant?.VictoriousImagePath;
    }

    /// <summary>
    /// Returns defeated artwork for the owner represented by one combat side.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="result">The completed combat result.</param>
    /// <param name="side">The defeated or withdrawing combat side.</param>
    /// <returns>The configured defeated artwork path.</returns>
    private static string GetDefeatedImagePath(
        UIContext uiContext,
        SpaceCombatResult result,
        CombatSide side
    )
    {
        string ownerInstanceId = GetOwnerIDForSide(result, side);
        return string.IsNullOrEmpty(ownerInstanceId)
            ? null
            : uiContext?.GetTheme(ownerInstanceId)?.BattleParticipant?.DefeatedImagePath;
    }

    /// <summary>
    /// Gets the owner identifier represented by a result-window force panel.
    /// </summary>
    /// <param name="panel">The selected result-window panel.</param>
    /// <returns>The represented attacker or defender owner identifier.</returns>
    internal string GetOwnerInstanceID(BattleResultPanel panel)
    {
        return panel == BattleResultPanel.SecondForces
            ? DefenderOwnerInstanceID
            : AttackerOwnerInstanceID;
    }

    /// <summary>
    /// Returns the owner identifier represented by one combat side.
    /// </summary>
    /// <param name="result">The completed combat result.</param>
    /// <param name="side">The requested combat side.</param>
    /// <returns>The represented owner identifier.</returns>
    private static string GetOwnerIDForSide(SpaceCombatResult result, CombatSide side)
    {
        return side switch
        {
            CombatSide.Attacker => result?.AttackerOwnerInstanceID,
            CombatSide.Defender => result?.DefenderOwnerInstanceID,
            _ => null,
        };
    }

    /// <summary>
    /// Adapts a completed space-combat result.
    /// </summary>
    private sealed class SpaceCombatPresentation : BattleResultPresentation
    {
        private readonly SpaceCombatResult result;

        /// <summary>
        /// Creates a space-combat presentation adapter.
        /// </summary>
        /// <param name="result">The completed space-combat result.</param>
        internal SpaceCombatPresentation(SpaceCombatResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        internal override BattleResultCategory DefaultCategory => BattleResultCategory.CapitalShips;

        internal override string AttackerOwnerInstanceID => result.AttackerOwnerInstanceID;

        internal override string DefenderOwnerInstanceID => result.DefenderOwnerInstanceID;

        internal override Planet GetPlanet(UIContext uiContext) => result.Planet;

        internal override string Title =>
            $"Battle at {BattleAlertWindowProjector.GetPlanetName(result.Planet)}";

        internal override bool UsesPlanetaryLayout => false;

        /// <summary>
        /// Returns completed space-combat music from the player's perspective.
        /// </summary>
        /// <param name="theme">The active battle-alert theme.</param>
        /// <param name="playerFactionId">The current player faction identifier.</param>
        /// <returns>The configured result music path.</returns>
        internal override string GetMusicPath(BattleAlertWindowTheme theme, string playerFactionId)
        {
            if (theme == null)
                return null;

            CombatSide? playerSide = GetSideForOwner(result, playerFactionId);
            if (!playerSide.HasValue || result.Winner == CombatSide.Draw)
                return FirstNonBlank(theme.ResultDrawMusicPath, theme.ResultMusicPath);

            return result.Winner == playerSide.Value
                ? FirstNonBlank(theme.ResultVictoryMusicPath, theme.ResultMusicPath)
                : FirstNonBlank(theme.ResultDefeatMusicPath, theme.ResultMusicPath);
        }

        /// <summary>
        /// Builds the completed space-combat summary.
        /// </summary>
        /// <param name="uiContext">The current strategy UI context.</param>
        /// <param name="playerFactionId">The current player faction identifier.</param>
        /// <returns>The displayed result summary.</returns>
        internal override string GetSummary(UIContext uiContext, string playerFactionId)
        {
            return BattleAlertWindowProjector.GetSpaceResultSummary(
                uiContext,
                result,
                playerFactionId
            );
        }

        /// <summary>
        /// Returns completed space-combat summary artwork.
        /// </summary>
        /// <param name="uiContext">The current strategy UI context.</param>
        /// <param name="theme">The active battle-alert theme.</param>
        /// <returns>The selected summary artwork path.</returns>
        internal override string GetSummaryImagePath(
            UIContext uiContext,
            BattleAlertWindowTheme theme
        )
        {
            return BattleResultPresentation.GetSummaryImagePath(uiContext, theme, result);
        }

        /// <summary>
        /// Projects completed space-combat rows.
        /// </summary>
        /// <param name="projector">The table projector.</param>
        /// <param name="uiContext">The current strategy UI context.</param>
        /// <param name="ownerInstanceId">The represented owner identifier.</param>
        /// <param name="category">The selected result category.</param>
        /// <returns>The operational and destroyed result columns.</returns>
        internal override BattleResultTableRenderData ProjectTable(
            BattleResultTableProjector projector,
            UIContext uiContext,
            string ownerInstanceId,
            BattleResultCategory category
        )
        {
            return projector.ProjectSpaceCombat(uiContext, result, ownerInstanceId, category);
        }
    }

    /// <summary>
    /// Adapts a completed orbital-bombardment result.
    /// </summary>
    private sealed class BombardmentPresentation : BattleResultPresentation
    {
        private readonly BombardmentResult result;

        /// <summary>
        /// Creates an orbital-bombardment presentation adapter.
        /// </summary>
        /// <param name="result">The completed bombardment result.</param>
        internal BombardmentPresentation(BombardmentResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        internal override BattleResultCategory DefaultCategory => BattleResultCategory.CapitalShips;

        internal override string AttackerOwnerInstanceID => result.AttackerOwnerInstanceID;

        internal override string DefenderOwnerInstanceID => result.DefenderOwnerInstanceID;

        internal override Planet GetPlanet(UIContext uiContext) => result.Planet;

        internal override string Title =>
            $"Orbital bombardment of {BattleAlertWindowProjector.GetPlanetName(result.Planet)}";

        internal override bool UsesPlanetaryLayout => true;

        /// <summary>
        /// Builds the completed orbital-bombardment summary.
        /// </summary>
        /// <param name="uiContext">The current strategy UI context.</param>
        /// <param name="playerFactionId">The current player faction identifier.</param>
        /// <returns>The displayed result summary.</returns>
        internal override string GetSummary(UIContext uiContext, string playerFactionId)
        {
            return BattleAlertWindowProjector.GetBombardmentSummary(uiContext, result);
        }

        /// <summary>
        /// Returns orbital-bombardment summary artwork.
        /// </summary>
        /// <param name="uiContext">The current strategy UI context.</param>
        /// <param name="theme">The active battle-alert theme.</param>
        /// <returns>The selected summary artwork path.</returns>
        internal override string GetSummaryImagePath(
            UIContext uiContext,
            BattleAlertWindowTheme theme
        )
        {
            return BattleAlertWindowProjector.GetBombardmentSummaryImagePath(theme, result);
        }

        /// <summary>
        /// Projects completed orbital-bombardment rows.
        /// </summary>
        /// <param name="projector">The table projector.</param>
        /// <param name="uiContext">The current strategy UI context.</param>
        /// <param name="ownerInstanceId">The represented owner identifier.</param>
        /// <param name="category">The selected result category.</param>
        /// <returns>The operational and destroyed result columns.</returns>
        internal override BattleResultTableRenderData ProjectTable(
            BattleResultTableProjector projector,
            UIContext uiContext,
            string ownerInstanceId,
            BattleResultCategory category
        )
        {
            return projector.ProjectBombardment(uiContext, result, ownerInstanceId, category);
        }
    }

    /// <summary>
    /// Adapts a completed planetary-assault result.
    /// </summary>
    private sealed class PlanetaryAssaultPresentation : BattleResultPresentation
    {
        private readonly PlanetaryAssaultResult result;

        /// <summary>
        /// Creates a planetary-assault presentation adapter.
        /// </summary>
        /// <param name="result">The completed planetary-assault result.</param>
        internal PlanetaryAssaultPresentation(PlanetaryAssaultResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        internal override BattleResultCategory DefaultCategory => BattleResultCategory.Troops;

        internal override string AttackerOwnerInstanceID => result.AttackerOwnerInstanceID;

        internal override string DefenderOwnerInstanceID => result.DefenderOwnerInstanceID;

        internal override Planet GetPlanet(UIContext uiContext) => result.Planet;

        internal override string SoundEffectPath => StrategyUISoundPaths.PlanetaryAssault;

        internal override string Title =>
            $"Assault on {BattleAlertWindowProjector.GetPlanetName(result.Planet)}";

        internal override bool UsesPlanetaryLayout => true;

        /// <summary>
        /// Builds the completed planetary-assault summary.
        /// </summary>
        /// <param name="uiContext">The current strategy UI context.</param>
        /// <param name="playerFactionId">The current player faction identifier.</param>
        /// <returns>The displayed result summary.</returns>
        internal override string GetSummary(UIContext uiContext, string playerFactionId)
        {
            return BattleAlertWindowProjector.GetPlanetaryAssaultSummary(uiContext, result);
        }

        /// <summary>
        /// Returns planetary-assault summary artwork.
        /// </summary>
        /// <param name="uiContext">The current strategy UI context.</param>
        /// <param name="theme">The active battle-alert theme.</param>
        /// <returns>The selected summary artwork path.</returns>
        internal override string GetSummaryImagePath(
            UIContext uiContext,
            BattleAlertWindowTheme theme
        )
        {
            return FirstNonBlank(
                uiContext
                    ?.GetTheme(result.AttackerOwnerInstanceID)
                    ?.StrategyWindows?.BattleAlert?.PlanetaryAssaultImagePath,
                theme?.PlanetaryAssaultImagePath,
                theme?.ResultSummaryImagePath
            );
        }

        /// <summary>
        /// Projects completed planetary-assault rows.
        /// </summary>
        /// <param name="projector">The table projector.</param>
        /// <param name="uiContext">The current strategy UI context.</param>
        /// <param name="ownerInstanceId">The represented owner identifier.</param>
        /// <param name="category">The selected result category.</param>
        /// <returns>The operational and destroyed result columns.</returns>
        internal override BattleResultTableRenderData ProjectTable(
            BattleResultTableProjector projector,
            UIContext uiContext,
            string ownerInstanceId,
            BattleResultCategory category
        )
        {
            return projector.ProjectPlanetaryAssault(uiContext, result, ownerInstanceId, category);
        }
    }

    /// <summary>
    /// Adapts the detached completed outcome persisted by a combat message.
    /// </summary>
    private sealed class SavedCombatReportPresentation : BattleResultPresentation
    {
        private readonly CombatReport report;

        /// <summary>
        /// Creates presentation for one durable combat report.
        /// </summary>
        internal SavedCombatReportPresentation(CombatReport report)
        {
            this.report = report ?? throw new ArgumentNullException(nameof(report));
        }

        internal override BattleResultCategory DefaultCategory =>
            report.Type == CombatReportType.PlanetaryAssault
                ? BattleResultCategory.Troops
                : BattleResultCategory.CapitalShips;

        internal override string AttackerOwnerInstanceID => report.AttackerOwnerInstanceID;

        internal override string DefenderOwnerInstanceID => report.DefenderOwnerInstanceID;

        internal override string SoundEffectPath =>
            report.Type == CombatReportType.PlanetaryAssault
                ? StrategyUISoundPaths.PlanetaryAssault
                : null;

        internal override string Title =>
            FirstNonBlank(report.Title, GetDefaultTitle(report.Type, report.PlanetName));

        internal override bool UsesPlanetaryLayout => report.Type != CombatReportType.SpaceBattle;

        /// <summary>
        /// Resolves the report location against current game state without making the report depend on it.
        /// </summary>
        internal override Planet GetPlanet(UIContext uiContext)
        {
            return string.IsNullOrEmpty(report.PlanetInstanceID)
                ? null
                : uiContext?.Game?.GetSceneNodeByInstanceID<Planet>(report.PlanetInstanceID);
        }

        /// <summary>
        /// Returns completed fleet-engagement music from the saved recipient perspective.
        /// </summary>
        internal override string GetMusicPath(BattleAlertWindowTheme theme, string playerFactionId)
        {
            if (theme == null || report.Type != CombatReportType.SpaceBattle)
                return null;
            if (report.Winner == CombatSide.Draw)
                return FirstNonBlank(theme.ResultDrawMusicPath, theme.ResultMusicPath);

            CombatSide? playerSide = GetSideForOwner(report, playerFactionId);
            return playerSide.HasValue && playerSide.Value == report.Winner
                ? FirstNonBlank(theme.ResultVictoryMusicPath, theme.ResultMusicPath)
                : FirstNonBlank(theme.ResultDefeatMusicPath, theme.ResultMusicPath);
        }

        /// <summary>
        /// Returns the outcome summary frozen into the delivered message.
        /// </summary>
        internal override string GetSummary(UIContext uiContext, string playerFactionId)
        {
            return report.Summary ?? string.Empty;
        }

        /// <summary>
        /// Selects completed-result artwork from the saved outcome rather than current combat state.
        /// </summary>
        internal override string GetSummaryImagePath(
            UIContext uiContext,
            BattleAlertWindowTheme theme
        )
        {
            return report.Type switch
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
        /// Projects the saved participating-unit lists into the established result table.
        /// </summary>
        internal override BattleResultTableRenderData ProjectTable(
            BattleResultTableProjector projector,
            UIContext uiContext,
            string ownerInstanceId,
            BattleResultCategory category
        )
        {
            return projector.ProjectReport(uiContext, report, ownerInstanceId, category);
        }

        /// <summary>
        /// Selects fleet-engagement summary artwork from the saved winner and withdrawal state.
        /// </summary>
        private string GetSpaceSummaryImagePath(UIContext uiContext, BattleAlertWindowTheme theme)
        {
            if (report.Winner == CombatSide.Draw)
                return theme?.ResultSummaryImagePath;

            CombatSide losingSide =
                report.Winner == CombatSide.Attacker ? CombatSide.Defender : CombatSide.Attacker;
            if (GetOutcome(report, losingSide) == SpaceCombatSideOutcome.Withdrawn)
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
        private string GetBombardmentSummaryImagePath(BattleAlertWindowTheme theme)
        {
            if (
                report.AttackingUnits?.Exists(unit =>
                    unit != null && (unit.Damaged || unit.Destroyed)
                ) == true
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
        private string GetParticipantImagePath(
            UIContext uiContext,
            CombatSide side,
            bool victorious
        )
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
        private static SpaceCombatSideOutcome GetOutcome(CombatReport report, CombatSide side)
        {
            return side == CombatSide.Attacker ? report.AttackerOutcome : report.DefenderOutcome;
        }

        /// <summary>
        /// Returns the saved side represented by one faction identifier.
        /// </summary>
        private static CombatSide? GetSideForOwner(CombatReport report, string ownerInstanceId)
        {
            if (string.IsNullOrEmpty(ownerInstanceId))
                return null;
            if (ownerInstanceId == report.AttackerOwnerInstanceID)
                return CombatSide.Attacker;
            if (ownerInstanceId == report.DefenderOwnerInstanceID)
                return CombatSide.Defender;
            return null;
        }

        /// <summary>
        /// Builds a fallback title for older reports that do not store resolved text.
        /// </summary>
        private static string GetDefaultTitle(CombatReportType type, string planetName)
        {
            return type switch
            {
                CombatReportType.Bombardment => $"Orbital bombardment of {planetName}",
                CombatReportType.PlanetaryAssault => $"Assault on {planetName}",
                _ => $"Battle at {planetName}",
            };
        }
    }
}
