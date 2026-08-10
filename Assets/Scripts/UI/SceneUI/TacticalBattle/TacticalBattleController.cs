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
    private bool observing;
    private bool playerPaused;
    private bool withdrawalConfirmationOpen;
    private TacticalBehavior? pendingMissionOrder;
    private TacticalBehavior? pendingManeuver;
    private TacticalFormation pendingFormation;
    private TacticalUnitState selectedCapitalShip;
    private readonly CancellationTokenSource shutdown = new CancellationTokenSource();
    private TacticalBattleSide playerSide;
    private TacticalBattleRenderer battleRenderer;
    private TacticalCameraRig cameraRig;
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
        cameraRig = GetComponentInChildren<TacticalCameraRig>(true);
        if (cameraRig == null)
            throw new MissingReferenceException("Tactical camera rig is missing.");
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
        TacticalBattleTheme tacticalTheme =
            theme.TacticalBattle
            ?? throw new InvalidOperationException(
                $"Faction '{theme.FactionInstanceID}' requires a tactical battle theme."
            );
        view.InitializeContent(bootstrap.GetContentAssets(), tacticalTheme);
        cameraRig.Initialize(tacticalTheme.InitialCameraYaw);
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
        view.MissionOrderSelected += SelectPendingMissionOrder;
        view.MissionOrderAssigned += AssignPendingMissionOrder;
        view.MissionOrderCancelled += CancelPendingMissionOrder;
        view.ManeuverSelected += SelectPendingManeuver;
        view.FormationSelected += SelectPendingFormation;
        view.ManeuverAssigned += AssignPendingManeuver;
        view.ManeuverCancelled += CancelPendingManeuver;
        view.PreviousCapitalShipRequested += SelectPreviousCapitalShip;
        view.NextCapitalShipRequested += SelectNextCapitalShip;
        view.CapitalShipMissionsRequested += ShowSelectedTaskForceMissions;
        view.CapitalShipManeuversRequested += ShowSelectedTaskForceManeuvers;
        view.PauseToggled += TogglePlayerPause;
        view.GameOptionsRequested += OpenGameOptions;
        view.WithdrawalRequested += RequestWithdrawal;
        view.WithdrawalConfirmed += ConfirmWithdrawal;
        view.WithdrawalCancelled += CancelWithdrawal;
        view.ImmediateResultRequested += ResolveImmediately;
        view.CommandModeToggled += ToggleCommandMode;
        view.GameOptionsClosed += CloseGameOptions;
        battleRenderer.NavigationPointSelected += SelectNavigationPoint;
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
        IReadOnlyList<TacticalCombatEvent> combatEvents = Session.DrainEvents();
        battleRenderer.PresentEvents(combatEvents);
        if (
            selectedCapitalShip != null
            && combatEvents.Any(combatEvent =>
                combatEvent.Source == selectedCapitalShip
                || combatEvent.Target == selectedCapitalShip
            )
        )
        {
            RefreshCapitalShipStatus();
        }
        battleRenderer.Synchronize();
        if (Session.IsComplete && !battleRenderer.HasActiveCombatEffects)
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
            view.MissionOrderSelected -= SelectPendingMissionOrder;
            view.MissionOrderAssigned -= AssignPendingMissionOrder;
            view.MissionOrderCancelled -= CancelPendingMissionOrder;
            view.ManeuverSelected -= SelectPendingManeuver;
            view.FormationSelected -= SelectPendingFormation;
            view.ManeuverAssigned -= AssignPendingManeuver;
            view.ManeuverCancelled -= CancelPendingManeuver;
            view.PreviousCapitalShipRequested -= SelectPreviousCapitalShip;
            view.NextCapitalShipRequested -= SelectNextCapitalShip;
            view.CapitalShipMissionsRequested -= ShowSelectedTaskForceMissions;
            view.CapitalShipManeuversRequested -= ShowSelectedTaskForceManeuvers;
            view.PauseToggled -= TogglePlayerPause;
            view.GameOptionsRequested -= OpenGameOptions;
            view.WithdrawalRequested -= RequestWithdrawal;
            view.WithdrawalConfirmed -= ConfirmWithdrawal;
            view.WithdrawalCancelled -= CancelWithdrawal;
            view.ImmediateResultRequested -= ResolveImmediately;
            view.CommandModeToggled -= ToggleCommandMode;
            view.GameOptionsClosed -= CloseGameOptions;
        }
        if (battleRenderer != null)
            battleRenderer.NavigationPointSelected -= SelectNavigationPoint;
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
    /// Opens the original tactical game-options right panel.
    /// </summary>
    private void OpenGameOptions()
    {
        view.ShowGameOptions();
    }

    /// <summary>
    /// Closes the tactical game-options right panel.
    /// </summary>
    private void CloseGameOptions()
    {
        view.HideGameOptions();
        SelectedGroup = null;
        selectedCapitalShip = null;
        battleRenderer.SetNavigationRoute(Array.Empty<TacticalNavPoint>());
    }

    /// <summary>
    /// Switches the played side between player commands and autonomous observation.
    /// </summary>
    private void ToggleCommandMode()
    {
        observing = !observing;
        SelectedGroup = null;
        selectedCapitalShip = null;
        battleRenderer.SetNavigationRoute(Array.Empty<TacticalNavPoint>());
        view.SetObserving(observing);
    }

    /// <summary>
    /// Finishes the active tactical simulation without replacing its accumulated state.
    /// </summary>
    private void ResolveImmediately()
    {
        if (isCompleting || Session.IsComplete)
            return;

        view.HideGameOptions();
        Session.ResolveImmediately();
        Session.DrainEvents();
        battleRenderer.Synchronize();
        CompleteBattle();
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
        pendingMissionOrder = null;
        view.HideMissionOrders();
        pendingManeuver = SelectedGroup.Behavior;
        pendingFormation = SelectedGroup.Formation;
        selectedCapitalShip = SelectedGroup.Units.FirstOrDefault(unit => unit.IsActive);
        battleRenderer.SetNavigationRoute(SelectedGroup.NavigationPoints);
        SetSelectedCapitalShipSubject();
        RefreshCapitalShipStatus();
        view.HideManeuvers();
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
        selectedCapitalShip = null;
        view.HideCapitalShipStatus();
        pendingMissionOrder = SelectedGroup.Behavior;
        battleRenderer.SetNavigationRoute(SelectedGroup.NavigationPoints);
        SetCameraSubject(SelectedGroup);
        bool canRecover = SelectedGroup.Units.Any(unit => unit.RecoveryTarget?.IsActive == true);
        bool canAttackDeathStar = Session.Units.Any(unit =>
            unit.Side != playerSide
            && unit.IsActive
            && unit.Unit is CapitalShip { IsDeathStar: true }
        );
        view.ShowMissionOrders(canRecover, canAttackDeathStar);
    }

    /// <summary>
    /// Updates the camera's reset subject from the selected command group's active units.
    /// </summary>
    /// <param name="group">The selected tactical command group.</param>
    private void SetCameraSubject(TacticalShipGroup group)
    {
        TacticalUnitState[] activeUnits = group.Units.Where(unit => unit.IsActive).ToArray();
        if (activeUnits.Length == 0)
            return;

        Vector3 subject = Vector3.zero;
        foreach (TacticalUnitState unit in activeUnits)
            subject += ToUnityVector(Session.GetPresentationPosition(unit));

        cameraRig.SetSelectedSubject(subject / activeUnits.Length);
    }

    /// <summary>
    /// Selects the previous active capital ship in the current task force.
    /// </summary>
    private void SelectPreviousCapitalShip()
    {
        CycleSelectedCapitalShip(-1);
    }

    /// <summary>
    /// Selects the next active capital ship in the current task force.
    /// </summary>
    private void SelectNextCapitalShip()
    {
        CycleSelectedCapitalShip(1);
    }

    /// <summary>
    /// Cycles the selected capital ship within the current task force.
    /// </summary>
    /// <param name="offset">The signed source-order selection offset.</param>
    private void CycleSelectedCapitalShip(int offset)
    {
        if (SelectedGroup == null || selectedCapitalShip == null)
            return;

        TacticalUnitState[] activeShips = SelectedGroup
            .Units.Where(unit => unit.IsActive && unit.Kind == TacticalUnitKind.CapitalShip)
            .ToArray();
        if (activeShips.Length < 2)
            return;

        int currentIndex = Array.IndexOf(activeShips, selectedCapitalShip);
        int nextIndex = (currentIndex + offset + activeShips.Length) % activeShips.Length;
        selectedCapitalShip = activeShips[nextIndex];
        SetSelectedCapitalShipSubject();
        RefreshCapitalShipStatus();
    }

    /// <summary>
    /// Opens the maneuver controls for the selected capital ship's task force.
    /// </summary>
    private void ShowSelectedTaskForceManeuvers()
    {
        if (selectedCapitalShip == null || SelectedGroup == null)
            return;

        view.ShowManeuvers(pendingFormation);
    }

    /// <summary>
    /// Opens the mission-order controls for the selected capital ship's task force.
    /// </summary>
    private void ShowSelectedTaskForceMissions()
    {
        if (selectedCapitalShip == null || SelectedGroup == null)
            return;

        pendingMissionOrder = SelectedGroup.Behavior;
        view.ShowMissionOrders(false, false);
    }

    /// <summary>
    /// Refreshes the selected capital ship's source-resolution status panel.
    /// </summary>
    private void RefreshCapitalShipStatus()
    {
        if (selectedCapitalShip == null || SelectedGroup == null)
            return;

        if (!selectedCapitalShip.IsActive)
        {
            selectedCapitalShip = SelectedGroup.Units.FirstOrDefault(unit =>
                unit.IsActive && unit.Kind == TacticalUnitKind.CapitalShip
            );
            if (selectedCapitalShip == null)
            {
                view.HideCapitalShipStatus();
                return;
            }
        }

        int activeShipCount = SelectedGroup.Units.Count(unit =>
            unit.IsActive && unit.Kind == TacticalUnitKind.CapitalShip
        );
        view.ShowCapitalShipStatus(selectedCapitalShip, activeShipCount > 1);
    }

    /// <summary>
    /// Makes the selected capital ship the tactical camera's reset subject.
    /// </summary>
    private void SetSelectedCapitalShipSubject()
    {
        if (selectedCapitalShip == null)
            return;

        cameraRig.SetSelectedSubject(
            ToUnityVector(Session.GetPresentationPosition(selectedCapitalShip))
        );
    }

    /// <summary>
    /// Converts a simulation vector into the tactical scene coordinate system.
    /// </summary>
    /// <param name="vector">The simulation-space vector.</param>
    /// <returns>The corresponding Unity vector.</returns>
    private static Vector3 ToUnityVector(System.Numerics.Vector3 vector)
    {
        return new Vector3(vector.X, vector.Y, vector.Z);
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
    /// Replaces or edits the selected group's ordered navigation route.
    /// </summary>
    /// <param name="point">The selected waypoint.</param>
    /// <param name="editRoute">Whether to toggle the point within the current route.</param>
    private void SelectNavigationPoint(TacticalNavPoint point, bool editRoute)
    {
        if (SelectedGroup == null)
            return;

        if (!editRoute)
        {
            SelectedGroup.ReplaceNavigationPoints(new[] { point });
        }
        else if (SelectedGroup.NavigationPoints.Contains(point))
        {
            SelectedGroup.RemoveNavigationPoint(point);
        }
        else
        {
            SelectedGroup.AddNavigationPoint(point);
        }

        battleRenderer.SetNavigationRoute(SelectedGroup.NavigationPoints);
    }

    /// <summary>
    /// Stores a mission order without changing the active group command before confirmation.
    /// </summary>
    /// <param name="behavior">The pending mission order.</param>
    private void SelectPendingMissionOrder(TacticalBehavior behavior)
    {
        if (
            SelectedGroup is not { } selectedGroup
            || selectedGroup.Units.Count == 0
            || selectedGroup.Units.Any(unit => unit.Kind != selectedGroup.Units[0].Kind)
            || selectedGroup.Units[0].Kind
                is not (TacticalUnitKind.CapitalShip or TacticalUnitKind.Fighters)
        )
            return;

        pendingMissionOrder = behavior;
    }

    /// <summary>
    /// Applies the confirmed mission order and closes its command panel.
    /// </summary>
    private void AssignPendingMissionOrder()
    {
        if (SelectedGroup == null || pendingMissionOrder == null)
            return;

        SelectedGroup.SetBehavior(pendingMissionOrder.Value);
        pendingMissionOrder = null;
        view.HideMissionOrders();
    }

    /// <summary>
    /// Discards the pending mission order and closes its command panel.
    /// </summary>
    private void CancelPendingMissionOrder()
    {
        pendingMissionOrder = null;
        view.HideMissionOrders();
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
