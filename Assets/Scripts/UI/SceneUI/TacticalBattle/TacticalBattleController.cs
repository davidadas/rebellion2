using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Rebellion.Game.Results;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;
using UnityEngine;

/// <summary>
/// Owns the tactical battle scene lifecycle and its isolated simulation session.
/// </summary>
public sealed class TacticalBattleController : MonoBehaviour
{
    private bool isCompleting;
    private bool isReady;
    private bool playerPaused;
    private bool withdrawalConfirmationOpen;
    private TacticalBehavior? pendingFighterOrder;
    private TacticalBehavior? pendingManeuver;
    private TacticalFormation pendingFormation;
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

        Session = TacticalBattleSession.Create(encounter, gameManager.GetGame().Random);
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
        view.NavigationSetVisibilityToggled += ToggleNavigationSetVisibility;
        view.FighterOrderSelected += SelectPendingFighterOrder;
        view.FighterOrderAssigned += AssignPendingFighterOrder;
        view.FighterOrderCancelled += CancelPendingFighterOrder;
        view.ManeuverSelected += SelectPendingManeuver;
        view.FormationSelected += SelectPendingFormation;
        view.ManeuverAssigned += AssignPendingManeuver;
        view.ManeuverCancelled += CancelPendingManeuver;
        view.PauseToggled += TogglePlayerPause;
        view.WithdrawalRequested += RequestWithdrawal;
        view.WithdrawalConfirmed += ConfirmWithdrawal;
        view.WithdrawalCancelled += CancelWithdrawal;
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
            view.NavigationSetVisibilityToggled -= ToggleNavigationSetVisibility;
            view.FighterOrderSelected -= SelectPendingFighterOrder;
            view.FighterOrderAssigned -= AssignPendingFighterOrder;
            view.FighterOrderCancelled -= CancelPendingFighterOrder;
            view.ManeuverSelected -= SelectPendingManeuver;
            view.FormationSelected -= SelectPendingFormation;
            view.ManeuverAssigned -= AssignPendingManeuver;
            view.ManeuverCancelled -= CancelPendingManeuver;
            view.PauseToggled -= TogglePlayerPause;
            view.WithdrawalRequested -= RequestWithdrawal;
            view.WithdrawalConfirmed -= ConfirmWithdrawal;
            view.WithdrawalCancelled -= CancelWithdrawal;
        }
        if (playerPaused)
            Session?.Resume();
        if (withdrawalConfirmationOpen)
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
    /// Pauses the simulation and opens the dedicated tactical withdrawal confirmation panel.
    /// </summary>
    private void RequestWithdrawal()
    {
        if (withdrawalConfirmationOpen)
            return;

        withdrawalConfirmationOpen = true;
        Session.Pause();
        view.ShowWithdrawalConfirmation();
    }

    /// <summary>
    /// Assigns withdrawal to every command group on the played side and resumes the simulation.
    /// </summary>
    private void ConfirmWithdrawal()
    {
        if (!withdrawalConfirmationOpen)
            return;

        Session.OrderWithdrawal(playerSide);
        CloseWithdrawalConfirmation();
    }

    /// <summary>
    /// Dismisses the withdrawal confirmation without changing tactical orders.
    /// </summary>
    private void CancelWithdrawal()
    {
        if (!withdrawalConfirmationOpen)
            return;

        CloseWithdrawalConfirmation();
    }

    /// <summary>
    /// Releases the confirmation pause hold and restores the tactical command surface.
    /// </summary>
    private void CloseWithdrawalConfirmation()
    {
        view.HideWithdrawalConfirmation();
        withdrawalConfirmationOpen = false;
        Session.Resume();
        view.SetPaused(Session.IsPaused);
    }

    /// <summary>
    /// Selects one populated capital task-force slot.
    /// </summary>
    /// <param name="index">The zero-based HUD slot.</param>
    private void SelectTaskForce(int index)
    {
        SelectedGroup = GetGroupAt(Session.GetTaskForces(playerSide), index);
        pendingFighterOrder = null;
        view.HideFighterOrders();
        pendingManeuver = SelectedGroup.Behavior;
        pendingFormation = SelectedGroup.Formation;
        view.ShowManeuvers(pendingFormation);
    }

    /// <summary>
    /// Selects one populated fighter-type slot.
    /// </summary>
    /// <param name="index">The zero-based HUD slot.</param>
    private void SelectFighterGroup(int index)
    {
        SelectedGroup = GetGroupAt(Session.GetFighterGroups(playerSide), index);
        pendingManeuver = null;
        view.HideManeuvers();
        pendingFighterOrder = SelectedGroup.Behavior;
        bool canRecover = SelectedGroup.Units.Any(unit => unit.RecoveryTarget?.IsActive == true);
        bool canAttackDeathStar = Session.Units.Any(unit =>
            unit.Side != playerSide
            && unit.IsActive
            && unit.Unit is CapitalShip { IsDeathStar: true }
        );
        view.ShowFighterOrders(canRecover, canAttackDeathStar);
    }

    /// <summary>
    /// Toggles the source-ordered waypoint shell represented by one HUD button.
    /// </summary>
    /// <param name="buttonIndex">The zero-based HUD button index.</param>
    private void ToggleNavigationSetVisibility(int buttonIndex)
    {
        int setIndex = Session.NavigationGrid.GetSetIndexForButton(buttonIndex);
        bool visible = Session.NavigationGrid.ToggleVisibility(setIndex);
        battleRenderer.SetNavigationSetVisible(setIndex, visible);
    }

    /// <summary>
    /// Stores a fighter order without changing the active group command before confirmation.
    /// </summary>
    /// <param name="behavior">The pending fighter order.</param>
    private void SelectPendingFighterOrder(TacticalBehavior behavior)
    {
        if (
            SelectedGroup is not { } selectedGroup
            || selectedGroup.Units.Any(unit => unit.Kind != TacticalUnitKind.Fighters)
        )
            return;

        pendingFighterOrder = behavior;
    }

    /// <summary>
    /// Applies the confirmed fighter order and closes its command panel.
    /// </summary>
    private void AssignPendingFighterOrder()
    {
        if (SelectedGroup == null || pendingFighterOrder == null)
            return;

        SelectedGroup.SetBehavior(pendingFighterOrder.Value);
        pendingFighterOrder = null;
        view.HideFighterOrders();
    }

    /// <summary>
    /// Discards the pending fighter order and closes its command panel.
    /// </summary>
    private void CancelPendingFighterOrder()
    {
        pendingFighterOrder = null;
        view.HideFighterOrders();
    }

    /// <summary>
    /// Stores a capital-ship maneuver without changing the active group command.
    /// </summary>
    /// <param name="behavior">The pending maneuver.</param>
    private void SelectPendingManeuver(TacticalBehavior behavior)
    {
        if (
            SelectedGroup is not { } selectedGroup
            || selectedGroup.Units.Any(unit => unit.Kind != TacticalUnitKind.CapitalShip)
        )
            return;

        pendingManeuver = behavior;
    }

    /// <summary>
    /// Stores a capital-ship formation without changing the active group formation.
    /// </summary>
    /// <param name="formation">The pending formation.</param>
    private void SelectPendingFormation(TacticalFormation formation)
    {
        if (
            SelectedGroup is not { } selectedGroup
            || selectedGroup.Units.Any(unit => unit.Kind != TacticalUnitKind.CapitalShip)
        )
            return;

        pendingFormation = formation;
    }

    /// <summary>
    /// Applies the pending capital-ship maneuver and formation.
    /// </summary>
    private void AssignPendingManeuver()
    {
        if (SelectedGroup == null || pendingManeuver == null)
            return;

        SelectedGroup.SetBehavior(pendingManeuver.Value);
        SelectedGroup.SetFormation(pendingFormation);
        pendingManeuver = null;
        view.HideManeuvers();
    }

    /// <summary>
    /// Discards the pending capital-ship maneuver and formation.
    /// </summary>
    private void CancelPendingManeuver()
    {
        pendingManeuver = null;
        view.HideManeuvers();
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
