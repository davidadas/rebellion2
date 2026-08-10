using System;
using Rebellion.Game.Results;
using Rebellion.Game.Tactical;
using UnityEngine;

/// <summary>
/// Owns the tactical battle scene lifecycle and its isolated simulation session.
/// </summary>
public sealed class TacticalBattleController : MonoBehaviour
{
    private bool isCompleting;

    /// <summary>
    /// Gets the active tactical session.
    /// </summary>
    internal TacticalBattleSession Session { get; private set; }

    /// <summary>
    /// Resolves the pending encounter and initializes its tactical state.
    /// </summary>
    private void Awake()
    {
        AppBootstrap bootstrap = AppBootstrap.EnsureExists();
        GameManager gameManager = bootstrap.GetRuntime()?.GetActiveGameManager();
        if (gameManager == null)
            throw new InvalidOperationException("Tactical combat requires an active game.");

        PendingCombatResult encounter = TacticalBattleLaunchContext.Encounter;
        if (encounter == null)
            throw new InvalidOperationException("No tactical encounter was selected.");
        if (
            !gameManager.SpaceCombatSystem.TryGetPendingCombat(
                out PendingCombatResult pendingEncounter
            )
            || !ReferenceEquals(encounter.AttackerFleet, pendingEncounter.AttackerFleet)
            || !ReferenceEquals(encounter.DefenderFleet, pendingEncounter.DefenderFleet)
        )
        {
            throw new InvalidOperationException(
                "The selected tactical encounter is no longer pending."
            );
        }

        Session = TacticalBattleSession.Create(encounter);
    }

    /// <summary>
    /// Commits a finished tactical battle and returns to the strategy scene.
    /// </summary>
    public void CompleteBattle()
    {
        if (isCompleting)
            return;
        if (Session?.IsComplete != true)
            throw new InvalidOperationException("Tactical combat is still active.");

        AppBootstrap bootstrap = AppBootstrap.Instance;
        GameManager gameManager = bootstrap.GetRuntime()?.GetActiveGameManager();
        if (gameManager == null)
            throw new InvalidOperationException("Tactical combat requires an active game.");

        isCompleting = true;
        gameManager.ResolveTacticalCombat(Session);
        TacticalBattleLaunchContext.Clear();
        bootstrap.LoadScene(SaveMenuLaunchContext.StrategyViewSceneName);
    }
}
