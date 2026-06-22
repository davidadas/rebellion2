using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

public sealed class StrategyConfirmActionController
{
    private readonly GameManager gameManager;

    public StrategyConfirmActionController(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }

    public bool TryInitializeScrapConfirmWindow(
        ConfirmDialogWindowView view,
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> sourceItems
    )
    {
        List<ISceneNode> items = ToSceneNodeList(sourceItems);
        if (view == null || sourceWindow == null || items.Count == 0)
            return false;

        view.InitializeWindow(sourceWindow, ConfirmDialogKind.Scrap, items, null);
        return true;
    }

    public bool TryInitializeRetireConfirmWindow(
        ConfirmDialogWindowView view,
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> sourceItems,
        string playerFactionId
    )
    {
        List<ISceneNode> items = ToSceneNodeList(sourceItems);
        if (
            view == null
            || sourceWindow == null
            || !StrategyContextMenuAvailability.CanRetireFleet(items, playerFactionId)
        )
            return false;

        view.InitializeWindow(sourceWindow, ConfirmDialogKind.Retire, items, null);
        return true;
    }

    public bool TryInitializeMoveConfirmWindow(
        ConfirmDialogWindowView view,
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> sourceItems,
        string playerFactionId
    )
    {
        List<ISceneNode> items = ToSceneNodeList(sourceItems);
        if (
            view == null
            || sourceWindow == null
            || !StrategyContextMenuAvailability.CanMoveItems(items, playerFactionId)
        )
            return false;

        view.InitializeWindow(sourceWindow, ConfirmDialogKind.Move, items, target);
        return true;
    }

    public bool ExecuteScrap(ConfirmDialogWindowView view)
    {
        if (view == null)
            return false;

        foreach (ISceneNode item in view.Items.ToList())
        {
            ISceneNode liveItem = ResolveLiveNode(item);
            if (liveItem?.GetParent() != null)
                gameManager.GetGame().DetachNode(liveItem);
        }

        return true;
    }

    public bool ExecuteRetire(ConfirmDialogWindowView view)
    {
        if (view == null)
            return false;

        foreach (ISceneNode item in view.Items.ToList())
        {
            ISceneNode liveItem = ResolveLiveNode(item);
            if (liveItem?.GetParent() != null)
                gameManager.GetGame().DetachNode(liveItem);
        }

        return true;
    }

    public bool TryExecuteMove(
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> sourceItems,
        string playerFactionId
    )
    {
        List<ISceneNode> items = ToLiveSceneNodeList(sourceItems);
        if (!StrategyContextMenuAvailability.CanMoveItems(items, playerFactionId))
            return false;

        ISceneNode destination = ResolveMoveDestination(
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
        RemoveEmptyCreatedFleet(createdDestinationFleet);
        RemoveEmptySourceFleets(sourceFleets);
        return true;
    }

    private ISceneNode ResolveMoveDestination(
        List<ISceneNode> items,
        ISceneNode destination,
        out Fleet createdDestinationFleet
    )
    {
        createdDestinationFleet = null;
        if (destination is not Planet planet)
            return destination;

        List<CapitalShip> capitalShips = items.OfType<CapitalShip>().ToList();
        if (capitalShips.Count == 0)
            return destination;

        string ownerInstanceId = capitalShips[0].GetOwnerInstanceID();
        if (
            string.IsNullOrEmpty(ownerInstanceId)
            || capitalShips.Any(ship => ship.GetOwnerInstanceID() != ownerInstanceId)
        )
            return null;

        createdDestinationFleet = gameManager.CreateFleetAtPlanet(planet, ownerInstanceId);
        return createdDestinationFleet;
    }

    private static bool TryGetMoveUnits(
        List<ISceneNode> items,
        ISceneNode destination,
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

            if (item is not IMovable movable)
                return false;

            if (item == destination)
                return false;

            if (item is CapitalShip capitalShip && capitalShip.GetParent() is Fleet parentFleet)
                sourceFleets.Add(parentFleet);

            movables.Add(movable);
        }

        return movables.Count == items.Count || destinationFleet != null && movables.Count > 0;
    }

    private void RemoveEmptySourceFleets(List<Fleet> sourceFleets)
    {
        foreach (
            Fleet fleet in sourceFleets.Distinct().Where(fleet => fleet.CapitalShips.Count == 0)
        )
        {
            if (fleet.GetParent() != null)
                gameManager.GetGame().DetachNode(fleet);
        }
    }

    private void RemoveEmptyCreatedFleet(Fleet fleet)
    {
        if (fleet == null || fleet.CapitalShips.Count > 0 || fleet.GetParent() == null)
            return;

        gameManager.GetGame().DetachNode(fleet);
    }

    private static List<ISceneNode> ToSceneNodeList(IReadOnlyList<ISceneNode> items)
    {
        return items?.ToList() ?? new List<ISceneNode>();
    }

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

    private ISceneNode ResolveLiveNode(ISceneNode node)
    {
        if (node == null)
            return null;

        return gameManager.GetGame()?.GetSceneNodeByInstanceID<ISceneNode>(node.InstanceID);
    }
}
