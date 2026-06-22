using System;
using System.Collections.Generic;
using Rebellion.SceneGraph;

public sealed class PlanetSystemWindowController : IStrategyContextMenuProvider, ITargetingReceiver
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
        List<StrategyMenuCommand> commands = context.Actions.TryGetWindowView(
            context.Window,
            out PlanetSystemWindowView view
        )
            ? BuildContextMenu(view, context.EventData)
            : new List<StrategyMenuCommand>();
        if (commands.Count == 0)
            return false;

        menu = new StrategyContextMenuData(
            context.Window,
            context.X,
            context.Y,
            context.Layout.PlanetSystemMenuWidth,
            commands
        );
        return true;
    }

    private static List<StrategyMenuCommand> BuildContextMenu(
        PlanetSystemWindowView view,
        UnityEngine.EventSystems.PointerEventData eventData
    )
    {
        view.CaptureContextTarget(eventData);
        return view.BuildContextMenu();
    }

    public void BeginContextTargeting(StrategyContextMenuProviderContext context, int action)
    {
        if (context.Actions.TryGetWindowView(context.Window, out PlanetSystemWindowView view))
            BeginContextTargeting(context.Window, view, action, context.X, context.Y);
    }

    public void BeginContextTargeting(
        UIWindow window,
        PlanetSystemWindowView view,
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
