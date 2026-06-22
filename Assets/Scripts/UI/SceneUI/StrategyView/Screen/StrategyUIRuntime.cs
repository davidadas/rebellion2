using System;

public sealed class StrategyUIRuntime
{
    public StrategyUIRuntime(
        UIContext context,
        TargetingController targeting,
        ContextMenuController contextMenus
    )
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
        ContextMenus = contextMenus ?? throw new ArgumentNullException(nameof(contextMenus));
    }

    public UIContext Context { get; }
    public TargetingController Targeting { get; }
    public ContextMenuController ContextMenus { get; }
}
