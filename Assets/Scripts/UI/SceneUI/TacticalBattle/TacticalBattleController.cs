using System;
using System.Collections.Generic;
using System.Threading;
using Rebellion.Game.Results;
using Rebellion.Game.Tactical;
using UnityEngine;

/// <summary>
/// Owns the tactical battle scene lifecycle and its isolated simulation session.
/// </summary>
public sealed class TacticalBattleController : MonoBehaviour
{
    private bool isCompleting;
    private bool isReady;
    private bool playerPaused;
    private readonly CancellationTokenSource shutdown = new CancellationTokenSource();
    private TacticalBattleSide playerSide;
    private TacticalBattleRenderer battleRenderer;
    private TacticalBattleView view;

    /// <summary>
    /// Gets the active tactical session.
    /// </summary>
    internal TacticalBattleSession Session { get; private set; }

    /// <summary>
    /// Gets the command group most recently selected by the player.
    /// </summary>
    internal TacticalShipGroup SelectedGroup { get; private set; }

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
        view = GetComponentInChildren<TacticalBattleView>(true);
        if (view == null)
            throw new MissingReferenceException("Tactical battle view is missing.");
        battleRenderer = GetComponentInChildren<TacticalBattleRenderer>(true);
        if (battleRenderer == null)
            throw new MissingReferenceException("Tactical battle renderer is missing.");
    }

    /// <summary>
    /// Resolves the player faction's external tactical presentation and begins accepting input.
    /// </summary>
    private async void Start()
    {
        AppBootstrap bootstrap = AppBootstrap.Instance;
        GameManager gameManager = bootstrap.GetRuntime()?.GetActiveGameManager();
        if (gameManager == null)
            throw new InvalidOperationException("Tactical combat requires an active game.");

        FactionTheme theme = new FactionThemeLibrary(
            bootstrap.GetContentPack().GameData.FactionThemes
        ).GetTheme(gameManager.GetPlayerFaction().InstanceID);
        view.InitializeContent(
            bootstrap.GetContentAssets(),
            theme.TacticalBattle
                ?? throw new InvalidOperationException(
                    $"Faction '{theme.FactionInstanceID}' requires a tactical battle theme."
                )
        );
        string playerFactionId = gameManager.GetPlayerFaction().InstanceID;
        playerSide =
            Session.Encounter.AttackerOwnerInstanceID == playerFactionId
                ? TacticalBattleSide.Attacker
                : TacticalBattleSide.Defender;
        view.SetGroupAvailability(
            Session.GetTaskForces(playerSide).Count,
            Session.GetFighterGroups(playerSide).Count
        );
        view.TaskForceSelected += SelectTaskForce;
        view.FighterGroupSelected += SelectFighterGroup;
        view.PauseToggled += TogglePlayerPause;
        await battleRenderer.InitializeAsync(
            Session,
            bootstrap.GetContentModelCache(),
            bootstrap.GetContentAssets(),
            shutdown.Token
        );
        isReady = true;
    }

    /// <summary>
    /// Advances the isolated tactical simulation and commits it when combat ends.
    /// </summary>
    private void Update()
    {
        if (Session == null || isCompleting || !isReady)
            return;

        Session.Advance(Time.deltaTime);
        battleRenderer.Synchronize();
        if (Session.IsComplete)
            CompleteBattle();
    }

    /// <summary>
    /// Releases tactical view subscriptions and any pause hold owned by the player control.
    /// </summary>
    private void OnDestroy()
    {
        shutdown.Cancel();
        shutdown.Dispose();
        if (view != null)
        {
            view.TaskForceSelected -= SelectTaskForce;
            view.FighterGroupSelected -= SelectFighterGroup;
            view.PauseToggled -= TogglePlayerPause;
        }
        if (playerPaused)
            Session?.Resume();
    }

    /// <summary>
    /// Toggles the player's independently nested tactical pause hold.
    /// </summary>
    private void TogglePlayerPause()
    {
        if (playerPaused)
            Session.Resume();
        else
            Session.Pause();

        playerPaused = !playerPaused;
        view.SetPaused(Session.IsPaused);
    }

    /// <summary>
    /// Selects one populated capital task-force slot.
    /// </summary>
    /// <param name="index">The zero-based HUD slot.</param>
    private void SelectTaskForce(int index)
    {
        SelectedGroup = GetGroupAt(Session.GetTaskForces(playerSide), index);
    }

    /// <summary>
    /// Selects one populated fighter-type slot.
    /// </summary>
    /// <param name="index">The zero-based HUD slot.</param>
    private void SelectFighterGroup(int index)
    {
        SelectedGroup = GetGroupAt(Session.GetFighterGroups(playerSide), index);
    }

    /// <summary>
    /// Resolves a fixed HUD slot and rejects stale or unavailable input.
    /// </summary>
    /// <param name="groups">The populated command slots.</param>
    /// <param name="index">The requested slot index.</param>
    /// <returns>The selected command group.</returns>
    private static TacticalShipGroup GetGroupAt(IReadOnlyList<TacticalShipGroup> groups, int index)
    {
        if (index < 0 || index >= groups.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        return groups[index];
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
