using System.Collections.Generic;
using Rebellion.Game;
using UnityEngine.EventSystems;

public interface IStrategyContextMenuActions
{
    string PlayerFactionId { get; }
    GameManager GameManager { get; }
    UIWindow GetWindow(PointerEventData eventData);
    bool TryGetWindowView<TView>(UIWindow window, out TView view)
        where TView : class;
    void FocusWindow(UIWindow window);
    void OpenConstructionWindow(UIWindow window);
    void StopManufacturingQueue(UIWindow window);
    void OpenEncyclopediaWindow(UIWindow window);
    void OpenStatusWindow(UIWindow window);
    void OpenScrapConfirmWindow(UIWindow window);
    void OpenRetireConfirmWindow(UIWindow window);
}

public sealed class StrategyContextMenuRouter : ICancelable
{
    private readonly StrategyContextMenuPresenter contextMenuPresenter;
    private readonly ContextMenuController contextMenuController;
    private readonly List<IStrategyContextMenuProvider> providers =
        new List<IStrategyContextMenuProvider>();
    private readonly IStrategyContextMenuActions actions;

    public StrategyContextMenuRouter(
        StrategyContextMenuPresenter contextMenuPresenter,
        ContextMenuController contextMenuController,
        IEnumerable<IStrategyContextMenuProvider> providers,
        IStrategyContextMenuActions actions
    )
    {
        this.contextMenuPresenter = contextMenuPresenter;
        this.contextMenuController = contextMenuController;
        this.actions = actions;
        if (providers == null)
            return;

        this.providers.AddRange(providers);
    }

    public void OpenContextMenu(PointerEventData eventData, int x, int y)
    {
        if (contextMenuPresenter == null)
            return;

        contextMenuController?.Cancel();

        OpenContextMenu(actions.GetWindow(eventData), eventData, x, y);
    }

    public void OpenContextMenu(UIWindow window, PointerEventData eventData, int x, int y)
    {
        if (contextMenuPresenter == null)
            return;

        contextMenuController?.Cancel();

        if (window == null)
        {
            contextMenuPresenter.Reset();
            return;
        }

        StrategyContextMenuProviderContext context = new StrategyContextMenuProviderContext(
            window,
            contextMenuPresenter.Layout,
            actions,
            eventData,
            x,
            y
        );
        foreach (IStrategyContextMenuProvider provider in providers)
        {
            if (provider.TryBuildContextMenu(context, out StrategyContextMenuData menu))
            {
                contextMenuPresenter.Show(menu);
                return;
            }
        }

        contextMenuPresenter.Show(
            new StrategyContextMenuData(
                window,
                x,
                y,
                contextMenuPresenter.Layout.FallbackMenuWidth,
                new List<StrategyMenuCommand>
                {
                    new StrategyMenuCommand(
                        StrategyContextMenuActions.Encyclopedia,
                        "Encyclopedia",
                        false
                    ),
                    new StrategyMenuCommand(StrategyContextMenuActions.Status, "Status", false),
                }
            )
        );
    }

    public void SelectRuntimeContextMenu(StrategyMenuCommand command)
    {
        if (contextMenuController?.TrySelectCommand(command) == true)
        {
            contextMenuPresenter.Reset();
            return;
        }

        contextMenuController?.Cancel();
        contextMenuPresenter.Reset();
    }

    public void ExecuteContextAction(int action)
    {
        if (TryExecuteSpeedContextAction(action))
        {
            contextMenuPresenter.Reset();
            return;
        }

        UIWindow window = contextMenuPresenter.Window;
        if (window == null)
        {
            contextMenuPresenter.Reset();
            return;
        }

        if (action == StrategyContextMenuActions.Build)
            actions.OpenConstructionWindow(window);
        else if (action == StrategyContextMenuActions.Stop)
            actions.StopManufacturingQueue(window);
        else if (action == StrategyContextMenuActions.Encyclopedia)
            actions.OpenEncyclopediaWindow(window);
        else if (action == StrategyContextMenuActions.Status)
            actions.OpenStatusWindow(window);
        else if (action == StrategyContextMenuActions.Scrap)
            actions.OpenScrapConfirmWindow(window);
        else if (action == StrategyContextMenuActions.Retire)
            actions.OpenRetireConfirmWindow(window);
        else if (
            action
            is StrategyContextMenuActions.CreateMission
                or StrategyContextMenuActions.Destination
                or StrategyContextMenuActions.Move
                or StrategyContextMenuActions.MoveConfirm
        )
            BeginContextTargeting(
                window,
                action,
                contextMenuPresenter.HotspotX,
                contextMenuPresenter.HotspotY
            );

        contextMenuPresenter.Reset();
    }

    public void OpenSpeedContextMenu(int x, int y)
    {
        if (contextMenuPresenter == null)
            return;

        contextMenuPresenter.OpenSpeedMenu(x, y);
    }

    public bool TryCancel()
    {
        bool requestCancelled = contextMenuController?.TryCancel() == true;
        bool viewCancelled = contextMenuPresenter?.TryCancel() == true;
        return requestCancelled || viewCancelled;
    }

    private bool TryExecuteSpeedContextAction(int action)
    {
        if (!StrategyContextMenuActions.TryGetGameSpeed(action, out TickSpeed speed))
            return false;

        actions.GameManager.SetGameSpeed(speed);
        return true;
    }

    private void BeginContextTargeting(UIWindow window, int action, int x, int y)
    {
        StrategyContextMenuProviderContext context = new StrategyContextMenuProviderContext(
            window,
            contextMenuPresenter.Layout,
            actions,
            null,
            x,
            y
        );
        foreach (IStrategyContextMenuProvider provider in providers)
            provider.BeginContextTargeting(context, action);
    }
}
