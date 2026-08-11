using System;
using Rebellion.Game.Results;
using Rebellion.Game.Tactical;

/// <summary>
/// Carries one pending fleet encounter between the strategy and tactical scenes.
/// </summary>
public static class TacticalBattleLaunchContext
{
    /// <summary>
    /// Gets the tactical battle scene name.
    /// </summary>
    public const string SceneName = "TacticalBattle";

    /// <summary>
    /// Gets the encounter selected for player command.
    /// </summary>
    public static PendingCombatResult Encounter { get; private set; }

    /// <summary>
    /// Gets whether a tactical session is waiting to resume after a temporary scene transition.
    /// </summary>
    public static bool HasRetainedSession => retainedSession != null;

    private static TacticalBattleSession retainedSession;
    private static SpaceCombatResult completedResult;

    /// <summary>
    /// Stores a pending encounter for the tactical scene.
    /// </summary>
    /// <param name="encounter">The pending encounter to command.</param>
    public static void Open(PendingCombatResult encounter)
    {
        Encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
        retainedSession = null;
        completedResult = null;
    }

    /// <summary>
    /// Retains the active tactical simulation while a temporary application screen is open.
    /// </summary>
    /// <param name="session">The tactical session to retain.</param>
    public static void RetainSession(TacticalBattleSession session)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));
        if (!ReferenceEquals(session.Encounter, Encounter))
        {
            throw new ArgumentException(
                "The retained tactical session does not represent the pending encounter.",
                nameof(session)
            );
        }

        retainedSession = session;
    }

    /// <summary>
    /// Takes the retained tactical session when the tactical scene resumes.
    /// </summary>
    /// <returns>The retained session, or null when the encounter has not started.</returns>
    public static TacticalBattleSession TakeRetainedSession()
    {
        TacticalBattleSession session = retainedSession;
        retainedSession = null;
        return session;
    }

    /// <summary>
    /// Stores the completed tactical result for presentation after returning to strategy.
    /// </summary>
    /// <param name="result">The completed tactical combat result.</param>
    public static void Complete(SpaceCombatResult result)
    {
        completedResult = result ?? throw new ArgumentNullException(nameof(result));
        Encounter = null;
        retainedSession = null;
    }

    /// <summary>
    /// Takes the completed tactical result when the strategy scene resumes.
    /// </summary>
    /// <returns>The completed result, or null when no result is waiting.</returns>
    public static SpaceCombatResult TakeCompletedResult()
    {
        SpaceCombatResult result = completedResult;
        completedResult = null;
        return result;
    }

    /// <summary>
    /// Clears the encounter after tactical combat has completed or been abandoned.
    /// </summary>
    public static void Clear()
    {
        Encounter = null;
        retainedSession = null;
        completedResult = null;
    }
}
