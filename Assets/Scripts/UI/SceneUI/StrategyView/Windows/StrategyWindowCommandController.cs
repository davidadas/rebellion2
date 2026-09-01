using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Extensions;

/// <summary>
/// Executes and finalizes commands shared by strategy feature windows.
/// </summary>
public sealed class StrategyWindowCommandController
    : IStrategyWindowCommandActions,
        IStrategyConfirmationActions
{
    private readonly MissionCreateWindowController missionCreateWindowController;
    private readonly ConfirmDialogWindowController confirmDialogWindowController;
    private readonly Func<GameRoot> getGame;
    private readonly Func<MovementSystem> getMovementSystem;
    private readonly Func<HeadquartersSystem> getHeadquartersSystem;
    private readonly Func<MaintenanceSystem> getMaintenanceSystem;
    private readonly Func<ManufacturingSystem> getManufacturingSystem;
    private readonly Func<PersonnelSystem> getPersonnelSystem;
    private readonly Action<string> playSfx;
    private readonly Action<UIWindow> clearWindowSelection;
    private readonly Action rebuildSnapshot;
    private readonly Action markDirty;
    private readonly Action playInvalidOrderRejected;
    private readonly Action playInTransitOrderRejected;
    private readonly Action playUnitUnderConstructionOrderRejected;

    /// <summary>
    /// Creates the shared strategy-window command handler.
    /// </summary>
    /// <param name="missionCreateWindowController">Owns mission-creation windows.</param>
    /// <param name="confirmDialogWindowController">Owns confirmation windows.</param>
    /// <param name="getGame">Returns the active game state.</param>
    /// <param name="getMovementSystem">Returns the active movement system.</param>
    /// <param name="getMaintenanceSystem">Returns the active maintenance system.</param>
    /// <param name="getManufacturingSystem">Returns the active manufacturing system.</param>
    /// <param name="getPersonnelSystem">Returns the active personnel system.</param>
    /// <param name="playSfx">Plays an optional officer response.</param>
    /// <param name="clearWindowSelection">Clears selection owned by a source window.</param>
    /// <param name="rebuildSnapshot">Rebuilds the visible strategy snapshot.</param>
    /// <param name="markDirty">Invalidates the strategy presentation.</param>
    /// <param name="getHeadquartersSystem">Returns the mobile-headquarters system.</param>
    /// <param name="playInvalidOrderRejected">Plays the advisor's invalid-order rejection.</param>
    /// <param name="playInTransitOrderRejected">Plays the advisor's in-transit rejection.</param>
    /// <param name="playUnitUnderConstructionOrderRejected">Plays the advisor's under-construction rejection.</param>
    public StrategyWindowCommandController(
        MissionCreateWindowController missionCreateWindowController,
        ConfirmDialogWindowController confirmDialogWindowController,
        Func<GameRoot> getGame,
        Func<MovementSystem> getMovementSystem,
        Func<MaintenanceSystem> getMaintenanceSystem,
        Func<ManufacturingSystem> getManufacturingSystem,
        Func<PersonnelSystem> getPersonnelSystem,
        Action<string> playSfx,
        Action<UIWindow> clearWindowSelection,
        Action rebuildSnapshot,
        Action markDirty,
        Func<HeadquartersSystem> getHeadquartersSystem = null,
        Action playInvalidOrderRejected = null,
        Action playInTransitOrderRejected = null,
        Action playUnitUnderConstructionOrderRejected = null
    )
    {
        this.missionCreateWindowController =
            missionCreateWindowController
            ?? throw new ArgumentNullException(nameof(missionCreateWindowController));
        this.confirmDialogWindowController =
            confirmDialogWindowController
            ?? throw new ArgumentNullException(nameof(confirmDialogWindowController));
        this.getGame = getGame ?? throw new ArgumentNullException(nameof(getGame));
        this.getMovementSystem =
            getMovementSystem ?? throw new ArgumentNullException(nameof(getMovementSystem));
        this.getHeadquartersSystem = getHeadquartersSystem;
        this.getMaintenanceSystem =
            getMaintenanceSystem ?? throw new ArgumentNullException(nameof(getMaintenanceSystem));
        this.getManufacturingSystem =
            getManufacturingSystem
            ?? throw new ArgumentNullException(nameof(getManufacturingSystem));
        this.getPersonnelSystem =
            getPersonnelSystem ?? throw new ArgumentNullException(nameof(getPersonnelSystem));
        this.playSfx = playSfx ?? throw new ArgumentNullException(nameof(playSfx));
        this.clearWindowSelection =
            clearWindowSelection ?? throw new ArgumentNullException(nameof(clearWindowSelection));
        this.rebuildSnapshot =
            rebuildSnapshot ?? throw new ArgumentNullException(nameof(rebuildSnapshot));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
        this.playInvalidOrderRejected = playInvalidOrderRejected ?? (() => { });
        this.playInTransitOrderRejected = playInTransitOrderRejected ?? (() => { });
        this.playUnitUnderConstructionOrderRejected =
            playUnitUnderConstructionOrderRejected ?? (() => { });
    }

    /// <summary>
    /// Executes the semantic command completed by one targeting request.
    /// </summary>
    /// <param name="source">The targeting source command and selection.</param>
    /// <param name="target">The selected strategy target.</param>
    public void ExecuteTargetedCommand(
        StrategyWindowTargetingSource source,
        StrategyMissionTarget target
    )
    {
        if (source == null || target == null)
            return;

        switch (source.Action)
        {
            case StrategyMenuAction.CreateMission:
                OpenMissionCreateWindow(target, source.Items);
                break;
            case StrategyMenuAction.Move:
                TryExecuteMove(source.Window, target, source.Items);
                break;
            case StrategyMenuAction.MoveConfirm:
                OpenMoveConfirmWindow(source.Window, target, source.Items);
                break;
            case StrategyMenuAction.WaypointMove:
                TryAppendFleetWaypoint(source, target);
                break;
        }
    }

    /// <summary>
    /// Opens mission creation for selected participants and a target.
    /// </summary>
    /// <param name="target">The selected mission target.</param>
    /// <param name="items">The selected mission participants.</param>
    public void OpenMissionCreateWindow(
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    )
    {
        if (target?.Item == null)
        {
            playInvalidOrderRejected();
            return;
        }

        missionCreateWindowController.Open(target, items);
    }

    /// <summary>
    /// Executes an immediate move for selected items.
    /// </summary>
    /// <param name="sourceWindow">The strategy window that owns the selection.</param>
    /// <param name="target">The selected move target.</param>
    /// <param name="items">The selected movable items.</param>
    /// <returns>True when the move was executed.</returns>
    public bool TryExecuteMove(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    )
    {
        List<ISceneNode> sourceItems = CopyItems(items);
        if (!TryMove(target, sourceItems))
            return false;

        RefreshAfterMutation(sourceWindow);
        return true;
    }

    /// <summary>
    /// Opens a confirmed move for selected items.
    /// </summary>
    /// <param name="sourceWindow">The strategy window that owns the selection.</param>
    /// <param name="target">The selected move target.</param>
    /// <param name="items">The selected movable items.</param>
    public void OpenMoveConfirmWindow(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    )
    {
        List<ISceneNode> sourceItems = CopyItems(items);
        ContainerNode destination = target?.GetMoveDestination() as ContainerNode;
        MovementSystem movementSystem = getMovementSystem();
        int transitTimeInDays =
            movementSystem != null
            && movementSystem.TryGetSelectionTransitTicks(
                sourceItems,
                destination,
                GetPlayerFactionID(),
                out int transitTicks
            )
                ? transitTicks
                : -1;
        confirmDialogWindowController.OpenMove(
            sourceWindow,
            sourceItems,
            transitTimeInDays,
            () =>
            {
                if (TryMove(target, sourceItems))
                    RefreshAfterMutation(sourceWindow);
            }
        );
    }

    /// <summary>
    /// Appends a selected planet to the active uncommitted waypoint plan.
    /// </summary>
    /// <param name="source">The active waypoint targeting session.</param>
    /// <param name="target">The selected waypoint planet.</param>
    /// <returns>True when the waypoint was appended to the plan.</returns>
    public bool TryAppendFleetWaypoint(
        StrategyWindowTargetingSource source,
        StrategyMissionTarget target
    )
    {
        Planet destination = target?.Planet?.Planet;
        if (source?.Action != StrategyMenuAction.WaypointMove || destination == null)
            return false;

        List<string> proposedWaypoints = source.WaypointPlanetIds.ToList();
        proposedWaypoints.Add(destination.InstanceID);
        if (
            getMovementSystem()
                ?.CanSetFleetWaypointRoute(source.Items, proposedWaypoints, GetPlayerFactionID())
            != true
        )
            return false;

        bool appended = source.TryAppendWaypoint(destination.InstanceID);
        if (appended)
            markDirty();
        return appended;
    }

    /// <summary>
    /// Commits an active waypoint plan and starts its first leg when the fleets are stationary.
    /// </summary>
    /// <param name="source">The active waypoint targeting session.</param>
    /// <returns>True when the route was committed.</returns>
    public bool TryCommitFleetWaypointPlan(StrategyWindowTargetingSource source)
    {
        if (
            source?.Action != StrategyMenuAction.WaypointMove
            || getMovementSystem()
                ?.TrySetFleetWaypointRoute(
                    source.Items,
                    source.WaypointPlanetIds,
                    GetPlayerFactionID()
                ) != true
        )
            return false;

        RefreshAfterWaypointMutation();
        return true;
    }

    /// <summary>
    /// Removes the newest stop from an active waypoint plan and refreshes its preview.
    /// </summary>
    /// <param name="source">The active waypoint targeting session.</param>
    /// <returns>True when waypoints remain and targeting should stay active.</returns>
    public bool TryUndoFleetWaypointPlan(StrategyWindowTargetingSource source)
    {
        if (source?.Action != StrategyMenuAction.WaypointMove || !source.TryRemoveLastWaypoint())
            return false;

        markDirty();
        return source.WaypointPlanetIds.Count > 0;
    }

    /// <summary>
    /// Clears selected fleet waypoint routes while preserving any active movement leg.
    /// </summary>
    /// <param name="items">The selected fleets or their visible snapshots.</param>
    /// <returns>True when at least one waypoint was cleared.</returns>
    public bool ClearFleetWaypoints(IReadOnlyList<ISceneNode> items)
    {
        if (getMovementSystem()?.ClearFleetWaypoints(items, GetPlayerFactionID()) != true)
            return false;

        RefreshAfterWaypointMutation();
        return true;
    }

    /// <summary>
    /// Opens scrap confirmation for selected units.
    /// </summary>
    /// <param name="sourceWindow">The strategy window that owns the selection.</param>
    /// <param name="items">The selected units.</param>
    public void OpenScrapConfirmWindow(UIWindow sourceWindow, IReadOnlyList<ISceneNode> items)
    {
        List<ISceneNode> sourceItems = CopyItems(items);
        confirmDialogWindowController.OpenScrap(
            sourceWindow,
            sourceItems,
            () =>
            {
                List<IManufacturable> manufacturables = sourceItems
                    .OfType<IManufacturable>()
                    .ToList();
                if (
                    manufacturables.Count == sourceItems.Count
                    && getMaintenanceSystem()?.TryScrap(manufacturables, GetPlayerFactionID())
                        == true
                )
                {
                    RefreshAfterMutation(sourceWindow);
                }
            }
        );
    }

    /// <summary>
    /// Opens stop-construction confirmation for selected queued items.
    /// </summary>
    /// <param name="sourceWindow">The strategy window that owns the selection.</param>
    /// <param name="items">The selected queued items.</param>
    public void OpenStopConstructionConfirmWindow(
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> items
    )
    {
        List<ISceneNode> sourceItems = CopyItems(items);
        confirmDialogWindowController.OpenStopConstruction(
            sourceWindow,
            sourceItems,
            () =>
            {
                List<IManufacturable> manufacturables = sourceItems
                    .OfType<IManufacturable>()
                    .ToList();
                if (
                    manufacturables.Count == sourceItems.Count
                    && getManufacturingSystem()
                        ?.CancelManufacturing(manufacturables, GetPlayerFactionID()) == true
                )
                {
                    RefreshAfterMutation(sourceWindow);
                }
            }
        );
    }

    /// <summary>
    /// Opens retirement confirmation for selected personnel.
    /// </summary>
    /// <param name="sourceWindow">The strategy window that owns the selection.</param>
    /// <param name="items">The selected personnel.</param>
    public void OpenRetireConfirmWindow(UIWindow sourceWindow, IReadOnlyList<ISceneNode> items)
    {
        List<ISceneNode> sourceItems = CopyItems(items);
        if (!CanRetire(sourceItems))
            return;

        confirmDialogWindowController.OpenRetire(
            sourceWindow,
            sourceItems,
            () =>
            {
                if (getPersonnelSystem()?.Retire(sourceItems, GetPlayerFactionID()) == true)
                    RefreshAfterMutation(sourceWindow);
            }
        );
    }

    /// <summary>
    /// Determines whether the complete personnel selection may be retired.
    /// </summary>
    /// <param name="items">The selected personnel or their snapshots.</param>
    /// <returns>True when every selected person may be retired.</returns>
    public bool CanRetire(IReadOnlyList<ISceneNode> items)
    {
        return getPersonnelSystem()?.CanRetire(items, GetPlayerFactionID()) == true;
    }

    /// <summary>
    /// Executes a validated selection move through the movement system.
    /// </summary>
    /// <param name="target">The requested strategy target.</param>
    /// <param name="items">The selected scene nodes.</param>
    /// <returns>True when the movement order was accepted.</returns>
    private bool TryMove(StrategyMissionTarget target, IReadOnlyList<ISceneNode> items)
    {
        if (ContainsInTransitUnit(items))
        {
            playInTransitOrderRejected();
            return false;
        }

        if (TargetsCapitalShipUnderConstruction(target))
        {
            playUnitUnderConstructionOrderRejected();
            return false;
        }

        ContainerNode destination = target?.GetMoveDestination() as ContainerNode;
        if (!ChangesDestination(items, destination))
            return false;

        Building headquarters = items?.Count == 1 ? items[0] as Building : null;
        bool moved =
            headquarters?.BuildingType == BuildingType.Headquarters
                ? getHeadquartersSystem?.Invoke()?.TryRelocate(headquarters, target?.Planet?.Planet)
                    == true
                : getMovementSystem()?.TryRequestMove(items, destination, GetPlayerFactionID())
                    == true;
        if (moved)
            PlayMoveVoice(items);

        return moved;
    }

    /// <summary>
    /// Returns whether the requested destination is a live capital ship still being built.
    /// </summary>
    private bool TargetsCapitalShipUnderConstruction(StrategyMissionTarget target)
    {
        if (target?.GetMoveDestination() is not CapitalShip destination)
            return false;

        CapitalShip liveDestination = getGame()
            ?.GetSceneNodeByInstanceID<CapitalShip>(destination.InstanceID);
        return (liveDestination ?? destination).ManufacturingStatus != ManufacturingStatus.Complete;
    }

    /// <summary>
    /// Returns whether at least one selected live unit would change its immediate container.
    /// </summary>
    private bool ChangesDestination(IReadOnlyList<ISceneNode> items, ContainerNode destination)
    {
        if (items == null || items.Count == 0 || destination == null)
            return true;

        GameRoot game = getGame();
        return items.Any(item =>
        {
            ISceneNode liveItem = game?.GetSceneNodeByInstanceID<ISceneNode>(item?.InstanceID);
            return liveItem?.GetParent()?.InstanceID != destination.InstanceID;
        });
    }

    /// <summary>
    /// Returns whether the selected live units include one already traveling through hyperspace.
    /// </summary>
    private bool ContainsInTransitUnit(IReadOnlyList<ISceneNode> items)
    {
        GameRoot game = getGame();
        return items?.Any(item =>
                game?.GetSceneNodeByInstanceID<ISceneNode>(item?.InstanceID) is IMovable movable
                && movable.GetTransitMovement() != null
            ) == true;
    }

    /// <summary>
    /// Plays an available order acknowledgment for a single selected officer.
    /// </summary>
    /// <param name="items">The selected scene nodes.</param>
    private void PlayMoveVoice(IReadOnlyList<ISceneNode> items)
    {
        if (items == null || items.Count != 1 || items[0] is not Officer selectedOfficer)
            return;

        GameRoot game = getGame();
        Officer officer = game?.GetSceneNodeByInstanceID<Officer>(selectedOfficer.InstanceID);
        string voicePath = officer?.GetVoicePath(OfficerVoiceLineType.Order, game.Random);
        if (!string.IsNullOrEmpty(voicePath))
            playSfx(voicePath);
    }

    /// <summary>
    /// Clears source selection and refreshes strategy state after a domain mutation.
    /// </summary>
    /// <param name="sourceWindow">The window that owned the selection.</param>
    private void RefreshAfterMutation(UIWindow sourceWindow)
    {
        clearWindowSelection(sourceWindow);
        rebuildSnapshot();
        markDirty();
    }

    /// <summary>
    /// Rebuilds route projection without clearing the selection used by repeated waypoint targeting.
    /// </summary>
    private void RefreshAfterWaypointMutation()
    {
        rebuildSnapshot();
        markDirty();
    }

    /// <summary>
    /// Gets a non-null player faction identifier for command validation.
    /// </summary>
    /// <returns>The current player faction identifier.</returns>
    private string GetPlayerFactionID()
    {
        return getGame()?.GetPlayerFaction()?.InstanceID ?? string.Empty;
    }

    /// <summary>
    /// Copies a selection into stable command storage.
    /// </summary>
    /// <param name="items">The selected scene nodes.</param>
    /// <returns>A non-null list preserving selection order.</returns>
    private static List<ISceneNode> CopyItems(IReadOnlyList<ISceneNode> items)
    {
        return items?.ToList() ?? new List<ISceneNode>();
    }
}
