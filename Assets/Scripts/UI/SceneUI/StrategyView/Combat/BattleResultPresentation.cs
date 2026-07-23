using System;
using Rebellion.Game.Galaxy;
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

    /// <summary>
    /// Returns the planet represented by this result.
    /// </summary>
    internal abstract Planet Planet { get; }

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
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="playerFactionId">The current player faction identifier.</param>
    /// <returns>The displayed result summary.</returns>
    internal abstract string GetSummary(
        UIContext uiContext,
        BattleAlertWindowTheme theme,
        string playerFactionId
    );

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
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="result">The completed combat result.</param>
    /// <returns>The selected summary artwork path.</returns>
    internal static string GetSummaryImagePath(
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
                GetDefeatedImagePath(theme, result, losingSide.Value),
                GetVictoryImagePath(theme, result, result.Winner),
                theme.ResultSummaryImagePath
            );
        }

        return FirstNonBlank(
            GetVictoryImagePath(theme, result, result.Winner),
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
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="result">The completed combat result.</param>
    /// <param name="side">The victorious combat side.</param>
    /// <returns>The configured victory artwork path.</returns>
    private static string GetVictoryImagePath(
        BattleAlertWindowTheme theme,
        SpaceCombatResult result,
        CombatSide side
    )
    {
        string ownerInstanceId = GetOwnerIDForSide(result, side);
        if (ownerInstanceId == theme.FirstForcesOwnerInstanceID)
            return theme.FirstForcesVictoriousImagePath;
        if (ownerInstanceId == theme.SecondForcesOwnerInstanceID)
            return theme.SecondForcesVictoriousImagePath;
        return null;
    }

    /// <summary>
    /// Returns defeated artwork for the owner represented by one combat side.
    /// </summary>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="result">The completed combat result.</param>
    /// <param name="side">The defeated or withdrawing combat side.</param>
    /// <returns>The configured defeated artwork path.</returns>
    private static string GetDefeatedImagePath(
        BattleAlertWindowTheme theme,
        SpaceCombatResult result,
        CombatSide side
    )
    {
        string ownerInstanceId = GetOwnerIDForSide(result, side);
        if (ownerInstanceId == theme.FirstForcesOwnerInstanceID)
            return theme.FirstForcesDefeatedImagePath;
        if (ownerInstanceId == theme.SecondForcesOwnerInstanceID)
            return theme.SecondForcesDefeatedImagePath;
        return null;
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

        internal override Planet Planet => result.Planet;

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
        /// <param name="theme">The active battle-alert theme.</param>
        /// <param name="playerFactionId">The current player faction identifier.</param>
        /// <returns>The displayed result summary.</returns>
        internal override string GetSummary(
            UIContext uiContext,
            BattleAlertWindowTheme theme,
            string playerFactionId
        )
        {
            return BattleAlertWindowProjector.GetSpaceResultSummary(
                uiContext,
                theme,
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
            return BattleResultPresentation.GetSummaryImagePath(theme, result);
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

        internal override Planet Planet => result.Planet;

        internal override string Title =>
            $"Orbital bombardment of {BattleAlertWindowProjector.GetPlanetName(result.Planet)}";

        internal override bool UsesPlanetaryLayout => true;

        /// <summary>
        /// Builds the completed orbital-bombardment summary.
        /// </summary>
        /// <param name="uiContext">The current strategy UI context.</param>
        /// <param name="theme">The active battle-alert theme.</param>
        /// <param name="playerFactionId">The current player faction identifier.</param>
        /// <returns>The displayed result summary.</returns>
        internal override string GetSummary(
            UIContext uiContext,
            BattleAlertWindowTheme theme,
            string playerFactionId
        )
        {
            return BattleAlertWindowProjector.GetBombardmentSummary(uiContext, theme, result);
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

        internal override Planet Planet => result.Planet;

        internal override string SoundEffectPath => StrategyUISoundPaths.PlanetaryAssault;

        internal override string Title =>
            $"Assault on {BattleAlertWindowProjector.GetPlanetName(result.Planet)}";

        internal override bool UsesPlanetaryLayout => true;

        /// <summary>
        /// Builds the completed planetary-assault summary.
        /// </summary>
        /// <param name="uiContext">The current strategy UI context.</param>
        /// <param name="theme">The active battle-alert theme.</param>
        /// <param name="playerFactionId">The current player faction identifier.</param>
        /// <returns>The displayed result summary.</returns>
        internal override string GetSummary(
            UIContext uiContext,
            BattleAlertWindowTheme theme,
            string playerFactionId
        )
        {
            return BattleAlertWindowProjector.GetPlanetaryAssaultSummary(uiContext, theme, result);
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
}
