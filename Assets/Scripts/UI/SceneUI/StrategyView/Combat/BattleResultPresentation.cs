using Rebellion.Game.Results;
using Rebellion.Game.Units;

/// <summary>
/// Resolves combat-result ownership, outcomes, and themed presentation assets.
/// </summary>
internal static class BattleResultPresentation
{
    /// <summary>
    /// Returns the result fleet represented by an owner identifier.
    /// </summary>
    /// <param name="result">The completed combat result.</param>
    /// <param name="ownerInstanceId">The requested owner identifier.</param>
    /// <returns>The matching result fleet, or null when the owner is not represented.</returns>
    internal static Fleet GetFleetForOwner(SpaceCombatResult result, string ownerInstanceId)
    {
        if (result == null || string.IsNullOrEmpty(ownerInstanceId))
            return null;
        if (
            result.AttackerOwnerInstanceID == ownerInstanceId
            || result.AttackerFleet?.GetOwnerInstanceID() == ownerInstanceId
        )
            return result.AttackerFleet;
        if (
            result.DefenderOwnerInstanceID == ownerInstanceId
            || result.DefenderFleet?.GetOwnerInstanceID() == ownerInstanceId
        )
            return result.DefenderFleet;
        return null;
    }

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
        if (
            ownerInstanceId == result.AttackerOwnerInstanceID
            || ownerInstanceId == result.AttackerFleet?.GetOwnerInstanceID()
        )
            return CombatSide.Attacker;
        if (
            ownerInstanceId == result.DefenderOwnerInstanceID
            || ownerInstanceId == result.DefenderFleet?.GetOwnerInstanceID()
        )
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
            CombatSide.Attacker => FirstNonBlank(
                result?.AttackerOwnerInstanceID,
                result?.AttackerFleet?.GetOwnerInstanceID()
            ),
            CombatSide.Defender => FirstNonBlank(
                result?.DefenderOwnerInstanceID,
                result?.DefenderFleet?.GetOwnerInstanceID()
            ),
            _ => null,
        };
    }
}
