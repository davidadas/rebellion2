using System.Collections.Generic;

public sealed class MissionsWindowController : IStrategyContextMenuProvider
{
    public bool TryBuildContextMenu(
        StrategyContextMenuProviderContext context,
        out StrategyContextMenuData menu
    )
    {
        menu = null;
        List<StrategyMenuCommand> commands = context.Actions.TryGetWindowView(
            context.Window,
            out MissionsWindowView view
        )
            ? view.BuildContextMenu()
            : new List<StrategyMenuCommand>();
        if (commands.Count == 0)
            return false;

        menu = new StrategyContextMenuData(
            context.Window,
            context.X,
            context.Y,
            context.Layout.MissionsMenuWidth,
            commands
        );
        return true;
    }

    public void BeginContextTargeting(StrategyContextMenuProviderContext context, int action) { }
}
