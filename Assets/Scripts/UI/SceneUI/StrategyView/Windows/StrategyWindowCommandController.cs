using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.PlayerRequests;
using Rebellion.SceneGraph;
using Rebellion.Util.DependencyInjection;
using Rebellion.Util.Random;

/// <summary>
/// Executes and finalizes commands shared by strategy feature windows.
/// </summary>
public sealed class StrategyWindowCommandController
    : IStrategyWindowCommandActions,
        IStrategyConfirmationActions
{
    private readonly MissionCreateWindowController missionCreateWindowController;
    private readonly ConfirmDialogWindowController confirmDialogWindowController;
    private readonly IServiceLocator services;
    private readonly IPlayerRequestDispatcher playerRequests;
    private readonly IRandomNumberProvider presentationRandom = new SystemRandomProvider(
        Environment.TickCount
    );
    private readonly Func<string> getPlayerFactionId;
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
    /// <param name="services">Resolves commands and queries for the active game.</param>
    /// <param name="playerRequests">Completes player orders through the session action boundary.</param>
    /// <param name="getPlayerFactionId">Returns the player faction identifier.</param>
    /// <param name="playSfx">Plays an optional officer response.</param>
    /// <param name="clearWindowSelection">Clears selection owned by a source window.</param>
    /// <param name="rebuildSnapshot">Rebuilds the visible strategy snapshot.</param>
    /// <param name="markDirty">Invalidates the strategy presentation.</param>
    /// <param name="playInvalidOrderRejected">Plays the advisor's invalid-order rejection.</param>
    /// <param name="playInTransitOrderRejected">Plays the advisor's in-transit rejection.</param>
    /// <param name="playUnitUnderConstructionOrderRejected">Plays the advisor's under-construction rejection.</param>
    public StrategyWindowCommandController(
        MissionCreateWindowController missionCreateWindowController,
        ConfirmDialogWindowController confirmDialogWindowController,
        IServiceLocator services,
        IPlayerRequestDispatcher playerRequests,
        Func<string> getPlayerFactionId,
        Action<string> playSfx,
        Action<UIWindow> clearWindowSelection,
        Action rebuildSnapshot,
        Action markDirty,
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
        this.services = services ?? throw new ArgumentNullException(nameof(services));
        this.playerRequests =
            playerRequests ?? throw new ArgumentNullException(nameof(playerRequests));
        this.getPlayerFactionId =
            getPlayerFactionId ?? throw new ArgumentNullException(nameof(getPlayerFactionId));
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
    public async void ExecuteTargetedCommand(
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
                await TryExecuteMoveAsync(source.Window, target, source.Items);
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
    /// Resolves a completed item drop to mission creation or movement.
    /// </summary>
    /// <param name="sourceWindow">The strategy window that owns the dragged selection.</param>
    /// <param name="target">The exact drop target.</param>
    /// <param name="items">The dragged scene nodes.</param>
    public async void ExecuteItemDrop(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    )
    {
        string playerFactionId = GetPlayerFactionID();
        ISceneNode destination = target?.Item ?? target?.Planet?.Planet;
        bool opensMission =
            target?.Planet?.Planet != null
            && destination != null
            && destination is not Fleet
            && !string.IsNullOrEmpty(playerFactionId)
            && missionCreateWindowController.CanOpen(target, items, playerFactionId)
            && (
                target.MissionCreationRequested
                || !string.Equals(
                    destination.GetOwnerInstanceID(),
                    playerFactionId,
                    StringComparison.Ordinal
                )
            );

        if (opensMission)
            OpenMissionCreateWindow(target, items);
        else
            await TryExecuteMoveAsync(sourceWindow, target, items);
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
        if (target?.Item == null || !missionCreateWindowController.Open(target, items))
        {
            playInvalidOrderRejected();
            return;
        }
    }

    /// <summary>
    /// Executes an immediate move for selected items.
    /// </summary>
    /// <param name="sourceWindow">The strategy window that owns the selection.</param>
    /// <param name="target">The selected move target.</param>
    /// <param name="items">The selected movable items.</param>
    /// <returns>True when the move was executed.</returns>
    public async Task<bool> TryExecuteMoveAsync(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    )
    {
        List<ISceneNode> sourceItems = CopyItems(items);
        if (!await TryMoveAsync(target, sourceItems))
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
        if (ContainsInTransitUnit(sourceItems))
        {
            playInTransitOrderRejected();
            return;
        }

        if (TargetsCapitalShipUnderConstruction(target))
        {
            playUnitUnderConstructionOrderRejected();
            return;
        }

        ContainerNode destination = target?.GetMoveDestination() as ContainerNode;
        if (!ChangesDestination(sourceItems, destination))
            return;

        MovementQueries movementQueries = services.GetService<MovementQueries>();
        if (
            movementQueries == null
            || !movementQueries.TryGetSelectionTransitTicks(
                sourceItems,
                destination,
                GetPlayerFactionID(),
                out int transitTimeInDays
            )
        )
        {
            playInvalidOrderRejected();
            return;
        }

        confirmDialogWindowController.OpenMove(
            sourceItems,
            transitTimeInDays,
            async () =>
            {
                bool moved = await TryMoveAsync(target, sourceItems);
                if (moved)
                    RefreshAfterMutation(sourceWindow);
                return moved;
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
            services
                .GetService<MovementQueries>()
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
    public async Task<bool> TryCommitFleetWaypointPlanAsync(StrategyWindowTargetingSource source)
    {
        if (
            source?.Action != StrategyMenuAction.WaypointMove
            || !(
                await playerRequests.ExecuteAsync(
                    new SetFleetRouteRequest
                    {
                        ItemIDs = source.Items.Select(item => item?.InstanceID).ToArray(),
                        WaypointPlanetIDs = source.WaypointPlanetIds.ToArray(),
                    }
                )
            ).Accepted
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
    public async Task<bool> ClearFleetWaypointsAsync(IReadOnlyList<ISceneNode> items)
    {
        if (
            !(
                await playerRequests.ExecuteAsync(
                    new ClearFleetRouteRequest
                    {
                        ItemIDs = items?.Select(item => item?.InstanceID).ToArray(),
                    }
                )
            ).Accepted
        )
            return false;

        RefreshAfterWaypointMutation();
        return true;
    }

    /// <summary>
    /// Opens abort confirmation for a complete active mission selection.
    /// </summary>
    /// <param name="sourceWindow">The strategy window that owns the selection.</param>
    /// <param name="items">The selected missions.</param>
    public void OpenMissionAbortConfirmWindow(
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> items
    )
    {
        List<string> missionInstanceIds = GetMissionInstanceIds(items);
        if (!TryResolveAbortMissions(missionInstanceIds, out List<Mission> missions))
            return;

        confirmDialogWindowController.OpenMissionAbort(
            missions,
            () => TryAbortMissionsAsync(missionInstanceIds, sourceWindow)
        );
    }

    /// <summary>
    /// Aborts one active player-controlled mission without opening another confirmation.
    /// </summary>
    /// <param name="missionInstanceId">The persistent mission identifier.</param>
    /// <returns>True when the mission remained eligible and was aborted.</returns>
    public Task<bool> TryAbortMissionAsync(string missionInstanceId)
    {
        if (string.IsNullOrEmpty(missionInstanceId))
            return Task.FromResult(false);

        return TryAbortMissionsAsync(new[] { missionInstanceId }, null);
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
            sourceItems,
            async () =>
            {
                List<IManufacturable> manufacturables = sourceItems
                    .OfType<IManufacturable>()
                    .ToList();
                if (
                    manufacturables.Count == sourceItems.Count
                    && (
                        await playerRequests.ExecuteAsync(
                            new ScrapRequest
                            {
                                ItemIDs = sourceItems.Select(item => item?.InstanceID).ToArray(),
                            }
                        )
                    ).Accepted
                )
                {
                    RefreshAfterMutation(sourceWindow);
                    return true;
                }
                return false;
            }
        );
    }

    /// <summary>
    /// Opens stop-construction confirmation for selected queued items.
    /// </summary>
    /// <param name="sourceWindow">The strategy window that owns the selection.</param>
    /// <param name="items">The selected queued items.</param>
    /// <param name="onStopped">Runs after at least one selected item is successfully cancelled.</param>
    public void OpenStopConstructionConfirmWindow(
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> items,
        Action onStopped = null
    )
    {
        List<ISceneNode> sourceItems = CopyItems(items);
        confirmDialogWindowController.OpenStopConstruction(
            sourceItems,
            async () =>
            {
                List<IManufacturable> manufacturables = sourceItems
                    .OfType<IManufacturable>()
                    .ToList();
                if (
                    manufacturables.Count == sourceItems.Count
                    && (
                        await playerRequests.ExecuteAsync(
                            new CancelManufacturingRequest
                            {
                                ItemIDs = sourceItems.Select(item => item?.InstanceID).ToArray(),
                            }
                        )
                    ).Accepted
                )
                {
                    onStopped?.Invoke();
                    RefreshAfterMutation(sourceWindow);
                    return true;
                }
                return false;
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
            sourceItems,
            async () =>
            {
                if (
                    (
                        await playerRequests.ExecuteAsync(
                            new RetirePersonnelRequest
                            {
                                ItemIDs = sourceItems.Select(item => item?.InstanceID).ToArray(),
                            }
                        )
                    ).Accepted
                )
                {
                    RefreshAfterMutation(sourceWindow);
                    return true;
                }
                return false;
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
        return services.GetService<PersonnelQueries>()?.CanRetire(items, GetPlayerFactionID())
            == true;
    }

    /// <summary>
    /// Executes a validated selection move through the movement system.
    /// </summary>
    /// <param name="target">The requested strategy target.</param>
    /// <param name="items">The selected scene nodes.</param>
    /// <returns>True when the movement order was accepted.</returns>
    private async Task<bool> TryMoveAsync(
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    )
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
        PlayerRequest action =
            headquarters?.BuildingType == BuildingType.Headquarters
                ? new RelocateHeadquartersRequest
                {
                    HeadquartersID = headquarters.InstanceID,
                    DestinationPlanetID = target?.Planet?.Planet?.InstanceID,
                }
                : new MoveRequest
                {
                    ItemIDs = items?.Select(item => item?.InstanceID).ToArray(),
                    DestinationID = destination?.InstanceID,
                };
        bool moved = (await playerRequests.ExecuteAsync(action)).Accepted;
        if (moved)
            PlayMoveVoice(items);

        return moved;
    }

    /// <summary>
    /// Returns whether the requested destination is a visible capital ship still being built.
    /// </summary>
    /// <param name="target">The target.</param>
    /// <returns>True when the destination is an unfinished capital ship; otherwise false.</returns>
    private bool TargetsCapitalShipUnderConstruction(StrategyMissionTarget target)
    {
        if (target?.GetMoveDestination() is not CapitalShip destination)
            return false;

        return destination.ManufacturingStatus != ManufacturingStatus.Complete;
    }

    /// <summary>
    /// Returns whether at least one selected unit would change its immediate container.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="destination">The destination.</param>
    /// <returns>True when at least one selected unit would change containers; otherwise false.</returns>
    private bool ChangesDestination(IReadOnlyList<ISceneNode> items, ContainerNode destination)
    {
        if (items == null || items.Count == 0 || destination == null)
            return true;

        return items.Any(item => item?.GetParent()?.InstanceID != destination.InstanceID);
    }

    /// <summary>
    /// Returns whether the selected units include one already traveling through hyperspace.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <returns>True when at least one selected unit is in hyperspace; otherwise false.</returns>
    private bool ContainsInTransitUnit(IReadOnlyList<ISceneNode> items)
    {
        return items?.Any(item => item is IMovable movable && movable.GetTransitMovement() != null)
            == true;
    }

    /// <summary>
    /// Plays an available order acknowledgment for a single selected officer.
    /// </summary>
    /// <param name="items">The selected scene nodes.</param>
    private void PlayMoveVoice(IReadOnlyList<ISceneNode> items)
    {
        if (items == null || items.Count != 1 || items[0] is not Officer selectedOfficer)
            return;

        string voicePath = selectedOfficer.GetVoicePath(
            OfficerVoiceLineType.Order,
            presentationRandom
        );
        if (!string.IsNullOrEmpty(voicePath))
            playSfx(voicePath);
    }

    /// <summary>
    /// Captures unique mission identities from one context-menu selection.
    /// </summary>
    /// <param name="items">The selected scene nodes.</param>
    /// <returns>The mission instance identifiers in selection order.</returns>
    private static List<string> GetMissionInstanceIds(IReadOnlyList<ISceneNode> items)
    {
        if (
            items == null
            || items.Count == 0
            || items.Any(item =>
                item is not Mission mission || string.IsNullOrEmpty(mission.InstanceID)
            )
        )
            return new List<string>();

        return items
            .Cast<Mission>()
            .Select(mission => mission.InstanceID)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Resolves and validates a complete mission selection against the current game.
    /// </summary>
    /// <param name="missionInstanceIds">The persistent mission identifiers.</param>
    /// <param name="missions">Receives the resolved active missions.</param>
    /// <returns>True when every mission remains available for abort.</returns>
    private bool TryResolveAbortMissions(
        IReadOnlyList<string> missionInstanceIds,
        out List<Mission> missions
    )
    {
        GameRoot game = services.GetService<GameRoot>();
        missions =
            missionInstanceIds
                ?.Select(instanceId => game?.GetSceneNodeByInstanceID<Mission>(instanceId))
                .Where(mission => mission != null)
                .ToList()
            ?? new List<Mission>();
        return missionInstanceIds != null
            && missions.Count == missionInstanceIds.Count
            && StrategyContextMenuAvailability.CanAbortMissions(
                missions.Cast<ISceneNode>().ToList(),
                GetPlayerFactionID()
            );
    }

    /// <summary>
    /// Revalidates and aborts a complete mission selection through the shared command pipeline.
    /// </summary>
    /// <param name="missionInstanceIds">The persistent mission identifiers.</param>
    /// <param name="sourceWindow">The optional source window whose selection should be cleared.</param>
    /// <returns>True when every mission was aborted.</returns>
    private async Task<bool> TryAbortMissionsAsync(
        IReadOnlyList<string> missionInstanceIds,
        UIWindow sourceWindow
    )
    {
        if (!TryResolveAbortMissions(missionInstanceIds, out List<Mission> missions))
            return false;

        string voicePath = GetMissionAbortVoicePath(missions);
        if (
            !(
                await playerRequests.ExecuteAsync(
                    new AbortMissionsRequest { ItemIDs = missionInstanceIds.ToArray() }
                )
            ).Accepted
        )
            return false;

        if (!string.IsNullOrEmpty(voicePath))
            playSfx(voicePath);

        if (sourceWindow != null)
            RefreshAfterMutation(sourceWindow);
        else
        {
            rebuildSnapshot();
            markDirty();
        }

        return true;
    }

    /// <summary>
    /// Selects one mission-abort response from the validated mission selection.
    /// </summary>
    /// <param name="missions">The missions being aborted.</param>
    /// <returns>The selected voice path, or null.</returns>
    private string GetMissionAbortVoicePath(IReadOnlyList<Mission> missions)
    {
        Officer speaker = missions
            ?.SelectMany(mission =>
                mission.GetMainParticipants().Concat(mission.GetDecoyParticipants())
            )
            .OfType<Officer>()
            .FirstOrDefault(officer => officer.HasVoicePath(OfficerVoiceLineType.MissionAbort));
        return speaker?.GetVoicePath(OfficerVoiceLineType.MissionAbort, presentationRandom);
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
        return getPlayerFactionId() ?? string.Empty;
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
