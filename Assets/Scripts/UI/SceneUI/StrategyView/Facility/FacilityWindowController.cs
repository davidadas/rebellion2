using System;
using System.Collections.Generic;

public sealed class FacilityWindowController : IStrategyContextMenuProvider, ITargetingReceiver
{
    private readonly ConstructionWindowController constructionWindowController;
    private readonly Func<IEnumerable<UIWindow>> windowsProvider;
    private StrategyUIRuntime runtime;

    public FacilityWindowController(
        ConstructionWindowController constructionWindowController,
        Func<IEnumerable<UIWindow>> windowsProvider
    )
    {
        this.constructionWindowController =
            constructionWindowController
            ?? throw new ArgumentNullException(nameof(constructionWindowController));
        this.windowsProvider =
            windowsProvider ?? throw new ArgumentNullException(nameof(windowsProvider));
    }

    public void Initialize(StrategyUIRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public bool TryBuildContextMenu(
        StrategyContextMenuProviderContext context,
        out StrategyContextMenuData menu
    )
    {
        menu = null;
        List<StrategyMenuCommand> commands = context.Actions.TryGetWindowView(
            context.Window,
            out FacilityWindowView view
        )
            ? view.BuildContextMenu(
                context.EventData,
                string.Equals(
                    view.GalaxyMapPlanet?.Planet?.OwnerInstanceID,
                    context.Actions.PlayerFactionId,
                    StringComparison.Ordinal
                ),
                context.Actions.PlayerFactionId
            )
            : new List<StrategyMenuCommand>();
        if (commands.Count == 0)
            return false;

        menu = new StrategyContextMenuData(
            context.Window,
            context.X,
            context.Y,
            context.Layout.FacilityMenuWidth,
            commands
        );
        return true;
    }

    public void BeginContextTargeting(StrategyContextMenuProviderContext context, int action)
    {
        if (
            action == StrategyContextMenuActions.Destination
            && context.Actions.TryGetWindowView(context.Window, out FacilityWindowView view)
        )
            BeginDestinationTargeting(context.Window, view, context.X, context.Y);
    }

    public void BeginDestinationTargeting(
        UIWindow window,
        FacilityWindowView view,
        int sourceX,
        int sourceY
    )
    {
        if (runtime == null || window == null || view == null)
            return;

        int buildPanel = view.GetContextManufacturingPanel();
        if (buildPanel < 1)
            return;

        runtime.Targeting.Begin(
            new TargetingRequest(
                StrategyWindowTargetingSource.GetPrompt(StrategyContextMenuActions.Destination),
                new FacilityDestinationTargetingSource(window, buildPanel),
                this
            ),
            sourceX,
            sourceY
        );
    }

    public void OnTargetSelected(TargetingRequest request, object target)
    {
        if (
            request?.Source is not FacilityDestinationTargetingSource source
            || target is not StrategyMissionTarget missionTarget
        )
            return;

        constructionWindowController.SetManufacturingDestination(
            source.Window,
            missionTarget,
            source.BuildPanel,
            windowsProvider()
        );
    }

    public void OnTargetingCancelled(TargetingRequest request) { }

    private sealed class FacilityDestinationTargetingSource
    {
        public FacilityDestinationTargetingSource(UIWindow window, int buildPanel)
        {
            Window = window;
            BuildPanel = buildPanel;
        }

        public UIWindow Window { get; }
        public int BuildPanel { get; }
    }
}
