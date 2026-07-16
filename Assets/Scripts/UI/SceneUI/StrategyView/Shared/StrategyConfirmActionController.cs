using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

/// <summary>
/// Executes the game mutations accepted through strategy confirmation dialogs.
/// </summary>
public sealed class StrategyConfirmActionController
{
    private readonly GameManager gameManager;
    private readonly Action<string> playSfx;

    /// <summary>
    /// Creates a confirmation action controller for the active game.
    /// </summary>
    /// <param name="gameManager">The active game manager.</param>
    /// <param name="playSfx">Plays an optional confirmation-result sound path.</param>
    public StrategyConfirmActionController(GameManager gameManager, Action<string> playSfx)
    {
        this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
        this.playSfx = playSfx ?? throw new ArgumentNullException(nameof(playSfx));
    }

    /// <summary>
    /// Removes the supplied live units from the game.
    /// </summary>
    /// <param name="items">The units selected for scrapping.</param>
    /// <returns>True when a valid item collection was processed.</returns>
    public bool ExecuteScrap(IReadOnlyList<ISceneNode> items)
    {
        List<ISceneNode> liveItems = ToLiveSceneNodeList(items);
        return liveItems.Count == items?.Count
            && gameManager.MaintenanceSystem.Scrap(
                liveItems.OfType<IManufacturable>().ToList(),
                gameManager.GetPlayerFaction()?.InstanceID
            );
    }

    /// <summary>
    /// Removes the supplied live personnel from the game.
    /// </summary>
    /// <param name="items">The personnel selected for retirement.</param>
    /// <returns>True when a valid item collection was processed.</returns>
    public bool ExecuteRetire(IReadOnlyList<ISceneNode> items)
    {
        List<ISceneNode> liveItems = ToLiveSceneNodeList(items);
        return liveItems.Count == items?.Count
            && gameManager
                .GetGame()
                .RetireOfficers(
                    liveItems.OfType<Officer>().ToList(),
                    gameManager.GetPlayerFaction()?.InstanceID
                );
    }

    /// <summary>
    /// Cancels the supplied queued manufacturing items through the manufacturing system.
    /// </summary>
    /// <param name="items">The queued items selected for cancellation.</param>
    /// <returns>True when at least one queued item was cancelled.</returns>
    public bool ExecuteStopConstruction(IReadOnlyList<ISceneNode> items)
    {
        if (items == null)
            return false;

        string playerFactionId = gameManager.GetPlayerFaction()?.InstanceID;
        bool cancelled = false;
        foreach (ISceneNode item in items)
        {
            ISceneNode liveItem = ResolveLiveNode(item);
            if (
                liveItem is IManufacturable manufacturable
                && gameManager.ManufacturingSystem.CancelManufacturing(
                    manufacturable,
                    playerFactionId
                )
            )
            {
                cancelled = true;
            }
        }

        return cancelled;
    }

    /// <summary>
    /// Moves a validated selection to its confirmed destination.
    /// </summary>
    /// <param name="target">The selected destination.</param>
    /// <param name="sourceItems">The selected units.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <returns>True when the move request was accepted.</returns>
    public bool TryExecuteMove(
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> sourceItems,
        string playerFactionId
    )
    {
        List<ISceneNode> items = ToLiveSceneNodeList(sourceItems);
        if (!StrategyContextMenuAvailability.CanMoveItems(items, playerFactionId))
            return false;

        ContainerNode destination = ResolveMoveDestination(
            items,
            ResolveLiveNode(target?.GetMoveDestination()),
            out Fleet createdDestinationFleet
        );
        if (destination == null)
            return false;

        if (
            !TryGetMoveUnits(
                items,
                destination,
                out List<IMovable> movables,
                out List<Fleet> sourceFleets
            )
        )
        {
            RemoveEmptyCreatedFleet(createdDestinationFleet);
            return false;
        }

        gameManager.RequestMove(movables, destination);
        PlayMoveVoice(movables);
        RemoveEmptyCreatedFleet(createdDestinationFleet);
        RemoveEmptySourceFleets(sourceFleets);
        return true;
    }

    /// <summary>
    /// Calculates the displayed transit duration for a prospective move.
    /// </summary>
    /// <param name="items">The selected units.</param>
    /// <param name="target">The selected destination.</param>
    /// <returns>The transit duration, or minus one when no route can be calculated.</returns>
    public int GetMoveTransitTimeInDays(
        IReadOnlyList<ISceneNode> items,
        StrategyMissionTarget target
    )
    {
        List<IMovable> movables = ToLiveSceneNodeList(items).OfType<IMovable>().ToList();
        ContainerNode destination = ResolveLiveNode(target?.GetMoveDestination()) as ContainerNode;
        return gameManager.TryGetTransitTicks(movables, destination, out int transitTicks)
            ? transitTicks
            : -1;
    }

    /// <summary>
    /// Resolves the concrete destination node and creates a fleet when capital ships target a planet.
    /// </summary>
    /// <param name="items">The live selected items.</param>
    /// <param name="destination">The live requested destination.</param>
    /// <param name="createdDestinationFleet">Receives a fleet created for the move.</param>
    /// <returns>The concrete movement destination.</returns>
    private ContainerNode ResolveMoveDestination(
        IReadOnlyList<ISceneNode> items,
        ISceneNode destination,
        out Fleet createdDestinationFleet
    )
    {
        createdDestinationFleet = null;
        if (destination is not Planet planet)
            return destination as ContainerNode;

        List<CapitalShip> capitalShips = items.OfType<CapitalShip>().ToList();
        if (capitalShips.Count == 0)
            return planet;

        string ownerInstanceId = capitalShips[0].GetOwnerInstanceID();
        if (
            string.IsNullOrEmpty(ownerInstanceId)
            || capitalShips.Any(ship => ship.GetOwnerInstanceID() != ownerInstanceId)
        )
            return null;

        createdDestinationFleet = gameManager
            .GetGame()
            .CreateFleetAtPlanet(planet, ownerInstanceId);
        return createdDestinationFleet;
    }

    /// <summary>
    /// Expands selected fleets into movable ships and records source fleets that may become empty.
    /// </summary>
    /// <param name="items">The live selected items.</param>
    /// <param name="destination">The concrete movement destination.</param>
    /// <param name="movables">Receives the units submitted to movement.</param>
    /// <param name="sourceFleets">Receives source fleets that may require removal.</param>
    /// <returns>True when every selected item can participate in the move.</returns>
    private static bool TryGetMoveUnits(
        IReadOnlyList<ISceneNode> items,
        ContainerNode destination,
        out List<IMovable> movables,
        out List<Fleet> sourceFleets
    )
    {
        movables = new List<IMovable>();
        sourceFleets = new List<Fleet>();
        Fleet destinationFleet = destination as Fleet;
        foreach (ISceneNode item in items)
        {
            if (item is Fleet fleet && destinationFleet != null)
            {
                if (fleet == destinationFleet)
                    return false;

                sourceFleets.Add(fleet);
                movables.AddRange(fleet.CapitalShips);
                continue;
            }

            if (item is not IMovable movable || item == destination)
                return false;

            if (item is CapitalShip capitalShip && capitalShip.GetParent() is Fleet parentFleet)
                sourceFleets.Add(parentFleet);

            movables.Add(movable);
        }

        return movables.Count == items.Count || destinationFleet != null && movables.Count > 0;
    }

    /// <summary>
    /// Removes source fleets left empty by an accepted move.
    /// </summary>
    /// <param name="sourceFleets">The candidate source fleets.</param>
    private void RemoveEmptySourceFleets(IEnumerable<Fleet> sourceFleets)
    {
        foreach (
            Fleet fleet in sourceFleets.Distinct().Where(fleet => fleet.CapitalShips.Count == 0)
        )
        {
            gameManager.GetGame().RemoveEmptyFleet(fleet);
        }
    }

    /// <summary>
    /// Removes an unused destination fleet created for a rejected move.
    /// </summary>
    /// <param name="fleet">The candidate destination fleet.</param>
    private void RemoveEmptyCreatedFleet(Fleet fleet)
    {
        gameManager.GetGame().RemoveEmptyFleet(fleet);
    }

    /// <summary>
    /// Plays the selected officer's order acknowledgment for a single-officer move.
    /// </summary>
    /// <param name="movables">The accepted movable units.</param>
    private void PlayMoveVoice(IReadOnlyList<IMovable> movables)
    {
        if (movables == null || movables.Count != 1 || movables[0] is not Officer officer)
            return;

        string voicePath = officer.GetVoicePath(
            OfficerVoiceLineType.Order,
            gameManager.GetGame()?.Random
        );
        if (!string.IsNullOrEmpty(voicePath))
            playSfx(voicePath);
    }

    /// <summary>
    /// Resolves every supplied snapshot node to the current game graph.
    /// </summary>
    /// <param name="items">The snapshot nodes.</param>
    /// <returns>The live nodes, or an empty list when any node cannot be resolved.</returns>
    private List<ISceneNode> ToLiveSceneNodeList(IReadOnlyList<ISceneNode> items)
    {
        List<ISceneNode> liveItems = new List<ISceneNode>();
        if (items == null)
            return liveItems;

        foreach (ISceneNode item in items)
        {
            ISceneNode liveItem = ResolveLiveNode(item);
            if (liveItem == null)
                return new List<ISceneNode>();

            liveItems.Add(liveItem);
        }

        return liveItems;
    }

    /// <summary>
    /// Resolves one snapshot node to the active game graph.
    /// </summary>
    /// <param name="node">The snapshot node.</param>
    /// <returns>The live node, or null when unavailable.</returns>
    private ISceneNode ResolveLiveNode(ISceneNode node)
    {
        return node == null
            ? null
            : gameManager.GetGame()?.GetSceneNodeByInstanceID<ISceneNode>(node.InstanceID);
    }
}
