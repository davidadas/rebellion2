using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.SceneGraph;
using UnityEngine.EventSystems;

public interface IStrategyContextMenuSource
{
    UIWindow Window { get; }
}

public interface IFleetWindowActions
{
    void OpenFleetStatusWindow(UIWindow sourceWindow, IReadOnlyList<ISceneNode> items);
    void OpenFleetScrapConfirmWindow(UIWindow sourceWindow, IReadOnlyList<ISceneNode> items);
    void OpenFleetRetireConfirmWindow(UIWindow sourceWindow, IReadOnlyList<ISceneNode> items);
    void OpenFleetMissionCreateWindow(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    );
    bool TryExecuteFleetMove(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    );
    void OpenFleetMoveConfirmWindow(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    );
}

public sealed class FleetWindowController
    : IStrategyContextMenuProvider,
        IContextMenuReceiver,
        ITargetingReceiver
{
    private StrategyUIRuntime runtime;
    private IFleetWindowActions actions;

    public void Initialize(StrategyUIRuntime runtime, IFleetWindowActions actions)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
    }

    public bool TryBuildContextMenu(
        StrategyContextMenuProviderContext context,
        out StrategyContextMenuData menu
    )
    {
        menu = null;
        return context.Actions.TryGetWindowView(context.Window, out FleetWindowView view)
            && TryBuildContextMenu(
                context.Window,
                view,
                context.X,
                context.Y,
                context.EventData,
                context.Actions.PlayerFactionId,
                context.Actions.GameManager,
                out menu
            );
    }

    public void BeginContextTargeting(StrategyContextMenuProviderContext context, int action) { }

    public bool TryBuildContextMenu(
        UIWindow window,
        FleetWindowView view,
        int x,
        int y,
        PointerEventData eventData,
        string playerFactionId,
        GameManager gameManager,
        out StrategyContextMenuData menu
    )
    {
        menu = null;
        if (runtime == null || window == null || view == null)
            return false;

        view.CaptureContextTarget(eventData);
        List<ISceneNode> items = view.GetContextItems();
        List<StrategyMenuCommand> commands = view.BuildContextMenu(
            items,
            StrategyContextMenuAvailability.PlayerControlsItems(items, playerFactionId),
            StrategyContextMenuAvailability.CanMoveItems(items, playerFactionId),
            StrategyContextMenuAvailability.CanCreateMission(items, playerFactionId, gameManager),
            StrategyContextMenuAvailability.CanRetireFleet(items, playerFactionId)
        );
        if (commands.Count == 0)
            return false;

        FleetContextMenuSource source = new FleetContextMenuSource(window, x, y, items);
        runtime.ContextMenus.Open(
            new ContextMenuRequest(source, commands.Cast<IContextMenuCommand>().ToList(), this)
        );
        menu = new StrategyContextMenuData(
            window,
            x,
            y,
            view.GetContextMenuWidth(commands),
            commands
        );
        return true;
    }

    public void OnContextMenuCommandSelected(
        ContextMenuRequest request,
        IContextMenuCommand command
    )
    {
        if (
            request?.Source is not FleetContextMenuSource source
            || command is not StrategyMenuCommand menuCommand
        )
            return;

        switch (menuCommand.Action)
        {
            case StrategyContextMenuActions.Status:
                actions.OpenFleetStatusWindow(source.Window, source.Items);
                break;
            case StrategyContextMenuActions.Scrap:
                actions.OpenFleetScrapConfirmWindow(source.Window, source.Items);
                break;
            case StrategyContextMenuActions.Retire:
                actions.OpenFleetRetireConfirmWindow(source.Window, source.Items);
                break;
            case StrategyContextMenuActions.CreateMission:
            case StrategyContextMenuActions.Move:
            case StrategyContextMenuActions.MoveConfirm:
                runtime.Targeting.Begin(
                    new TargetingRequest(
                        GetTargetingPrompt(menuCommand.Action),
                        new StrategyWindowTargetingSource(
                            source.Window,
                            menuCommand.Action,
                            source.HotspotX,
                            source.HotspotY,
                            source.Items
                        ),
                        this
                    ),
                    source.HotspotX,
                    source.HotspotY
                );
                break;
        }
    }

    public void OnContextMenuCancelled(ContextMenuRequest request) { }

    public void OnTargetSelected(TargetingRequest request, object target)
    {
        if (
            request?.Source is not StrategyWindowTargetingSource source
            || target is not StrategyMissionTarget missionTarget
        )
            return;

        switch (source.Action)
        {
            case StrategyContextMenuActions.CreateMission:
                actions.OpenFleetMissionCreateWindow(source.Window, missionTarget, source.Items);
                break;
            case StrategyContextMenuActions.Move:
                actions.TryExecuteFleetMove(source.Window, missionTarget, source.Items);
                break;
            case StrategyContextMenuActions.MoveConfirm:
                actions.OpenFleetMoveConfirmWindow(source.Window, missionTarget, source.Items);
                break;
        }
    }

    public void OnTargetingCancelled(TargetingRequest request) { }

    private static string GetTargetingPrompt(int action)
    {
        return action switch
        {
            StrategyContextMenuActions.CreateMission => "Select mission target",
            StrategyContextMenuActions.Move or StrategyContextMenuActions.MoveConfirm =>
                "Select move destination",
            _ => "Select target",
        };
    }

    private sealed class FleetContextMenuSource : IStrategyContextMenuSource
    {
        public FleetContextMenuSource(
            UIWindow window,
            int hotspotX,
            int hotspotY,
            IReadOnlyList<ISceneNode> items
        )
        {
            Window = window;
            HotspotX = hotspotX;
            HotspotY = hotspotY;
            Items = items?.ToList() ?? new List<ISceneNode>();
        }

        public UIWindow Window { get; }
        public int HotspotX { get; }
        public int HotspotY { get; }
        public IReadOnlyList<ISceneNode> Items { get; }
    }
}
