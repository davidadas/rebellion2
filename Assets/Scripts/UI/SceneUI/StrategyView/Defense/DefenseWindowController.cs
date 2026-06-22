using System;
using System.Collections.Generic;
using Rebellion.SceneGraph;

public sealed class DefenseWindowController : IStrategyContextMenuProvider, ITargetingReceiver
{
    private StrategyUIRuntime runtime;
    private IStrategyWindowCommandActions actions;

    public void Initialize(StrategyUIRuntime runtime, IStrategyWindowCommandActions actions)
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
        List<StrategyMenuCommand> commands = new List<StrategyMenuCommand>();
        if (context.Actions.TryGetWindowView(context.Window, out DefenseWindowView view))
        {
            view.CaptureContextTarget(context.EventData);
            List<ISceneNode> items = view.GetContextItems(out ISceneNode hitItem);
            commands = view.BuildContextMenu(
                items,
                hitItem,
                StrategyContextMenuAvailability.CanMoveItems(
                    items,
                    context.Actions.PlayerFactionId
                ),
                StrategyContextMenuAvailability.PlayerControlsItems(
                    items,
                    context.Actions.PlayerFactionId
                ),
                StrategyContextMenuAvailability.CanCreateMission(
                    items,
                    context.Actions.PlayerFactionId,
                    context.Actions.GameManager
                ),
                StrategyContextMenuAvailability.CanRetireFleet(
                    items,
                    context.Actions.PlayerFactionId
                )
            );
        }

        if (commands.Count == 0)
            return false;

        menu = new StrategyContextMenuData(
            context.Window,
            context.X,
            context.Y,
            context.Layout.DefenseMenuWidth,
            commands
        );
        return true;
    }

    public void BeginContextTargeting(StrategyContextMenuProviderContext context, int action)
    {
        if (context.Actions.TryGetWindowView(context.Window, out DefenseWindowView view))
            BeginContextTargeting(context.Window, view, action, context.X, context.Y);
    }

    public void BeginContextTargeting(
        UIWindow window,
        DefenseWindowView view,
        int action,
        int sourceX,
        int sourceY
    )
    {
        if (runtime == null || window == null || view == null)
            return;

        IReadOnlyList<ISceneNode> items = view.GetContextItems();
        runtime.Targeting.Begin(
            new TargetingRequest(
                StrategyWindowTargetingSource.GetPrompt(action),
                new StrategyWindowTargetingSource(window, action, sourceX, sourceY, items),
                this
            ),
            sourceX,
            sourceY
        );
    }

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
                actions.OpenMissionCreateWindow(source.Window, missionTarget, source.Items);
                break;
            case StrategyContextMenuActions.Move:
                actions.TryExecuteMove(source.Window, missionTarget, source.Items);
                break;
            case StrategyContextMenuActions.MoveConfirm:
                actions.OpenMoveConfirmWindow(source.Window, missionTarget, source.Items);
                break;
        }
    }

    public void OnTargetingCancelled(TargetingRequest request) { }
}
