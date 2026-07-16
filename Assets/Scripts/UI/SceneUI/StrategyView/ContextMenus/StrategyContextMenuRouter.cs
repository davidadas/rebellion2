using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

/// <summary>
/// Builds, presents, and routes strategy context menus without owning feature state.
/// </summary>
public sealed class StrategyContextMenuRouter : ICancelable
{
    private readonly StrategyContextMenuPresenter contextMenuPresenter;
    private readonly ContextMenuController contextMenuController;
    private readonly UIWindowManager windowManager;
    private readonly List<IStrategyContextMenuProvider> providers =
        new List<IStrategyContextMenuProvider>();

    /// <summary>
    /// Gets whether either context-menu implementation is currently open.
    /// </summary>
    internal bool IsOpen => contextMenuController.IsOpen || contextMenuPresenter.Open;

    /// <summary>
    /// Creates a context-menu router for the authored presenter and feature providers.
    /// </summary>
    /// <param name="contextMenuPresenter">The authored context-menu presenter.</param>
    /// <param name="contextMenuController">The runtime command-selection controller.</param>
    /// <param name="windowManager">Resolves registered windows beneath pointer events.</param>
    /// <param name="providers">The feature providers in routing priority order.</param>
    public StrategyContextMenuRouter(
        StrategyContextMenuPresenter contextMenuPresenter,
        ContextMenuController contextMenuController,
        UIWindowManager windowManager,
        IEnumerable<IStrategyContextMenuProvider> providers
    )
    {
        this.contextMenuPresenter =
            contextMenuPresenter ?? throw new ArgumentNullException(nameof(contextMenuPresenter));
        this.contextMenuController =
            contextMenuController ?? throw new ArgumentNullException(nameof(contextMenuController));
        this.windowManager =
            windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));

        foreach (IStrategyContextMenuProvider provider in providers)
        {
            if (provider == null)
                throw new ArgumentException(
                    "Context-menu providers cannot contain null.",
                    nameof(providers)
                );

            this.providers.Add(provider);
        }
    }

    /// <summary>
    /// Opens a context menu for the strategy window beneath a pointer event.
    /// </summary>
    /// <param name="eventData">The source pointer event.</param>
    /// <param name="x">The source-space horizontal menu position.</param>
    /// <param name="y">The source-space vertical menu position.</param>
    public void OpenContextMenu(PointerEventData eventData, int x, int y)
    {
        contextMenuController.Cancel();
        OpenContextMenuCore(windowManager.GetWindow(eventData), eventData, x, y);
    }

    /// <summary>
    /// Opens a context menu for a known strategy window.
    /// </summary>
    /// <param name="window">The source strategy window.</param>
    /// <param name="eventData">The source pointer event.</param>
    /// <param name="x">The source-space horizontal menu position.</param>
    /// <param name="y">The source-space vertical menu position.</param>
    public void OpenContextMenu(UIWindow window, PointerEventData eventData, int x, int y)
    {
        contextMenuController.Cancel();
        OpenContextMenuCore(window, eventData, x, y);
    }

    /// <summary>
    /// Completes a selection owned by the runtime context-menu controller.
    /// </summary>
    /// <param name="command">The selected semantic command.</param>
    public void SelectRuntimeContextMenu(StrategyMenuCommand command)
    {
        if (!contextMenuController.TrySelectCommand(command))
            contextMenuController.Cancel();

        contextMenuPresenter.Reset();
    }

    /// <summary>
    /// Presents a feature-owned runtime context-menu request.
    /// </summary>
    /// <param name="request">The feature-owned request and completion receiver.</param>
    /// <param name="x">The source-space horizontal menu position.</param>
    /// <param name="y">The source-space vertical menu position.</param>
    /// <param name="width">The authored base menu width.</param>
    public void OpenRuntimeContextMenu(ContextMenuRequest request, int x, int y, int width)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        List<StrategyMenuCommand> commands = GetStrategyCommands(request.Commands);
        contextMenuController.Open(request);
        contextMenuPresenter.Show(new StrategyContextMenuData(null, x, y, width, commands));
    }

    /// <summary>
    /// Cancels any active runtime or authored context menu.
    /// </summary>
    /// <returns>True when either context-menu implementation was cancelled.</returns>
    public bool TryCancel()
    {
        bool requestCancelled = contextMenuController.TryCancel();
        bool viewCancelled = contextMenuPresenter.TryCancel();
        return requestCancelled || viewCancelled;
    }

    /// <summary>
    /// Builds and presents a context menu after runtime-request state has been cleared.
    /// </summary>
    /// <param name="window">The source strategy window.</param>
    /// <param name="eventData">The source pointer event.</param>
    /// <param name="x">The source-space horizontal menu position.</param>
    /// <param name="y">The source-space vertical menu position.</param>
    private void OpenContextMenuCore(UIWindow window, PointerEventData eventData, int x, int y)
    {
        if (window == null)
        {
            contextMenuPresenter.Reset();
            return;
        }

        StrategyContextMenuProviderContext context = new StrategyContextMenuProviderContext(
            window,
            contextMenuPresenter.Layout,
            eventData,
            x,
            y
        );
        foreach (IStrategyContextMenuProvider provider in providers)
        {
            if (
                provider.TryCreateContextMenu(
                    context,
                    out ContextMenuRequest request,
                    out int width
                )
            )
            {
                contextMenuController.Open(request);
                contextMenuPresenter.Show(
                    new StrategyContextMenuData(
                        window,
                        x,
                        y,
                        width,
                        GetStrategyCommands(request.Commands)
                    )
                );
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

    /// <summary>
    /// Validates and projects shared commands into Strategy menu commands.
    /// </summary>
    /// <param name="commands">The feature-owned shared commands.</param>
    /// <returns>The commands in their original order.</returns>
    private static List<StrategyMenuCommand> GetStrategyCommands(
        IReadOnlyList<IContextMenuCommand> commands
    )
    {
        List<StrategyMenuCommand> strategyCommands = new List<StrategyMenuCommand>(
            commands?.Count ?? 0
        );
        if (commands == null)
            return strategyCommands;

        for (int index = 0; index < commands.Count; index++)
        {
            if (commands[index] is not StrategyMenuCommand strategyCommand)
            {
                throw new ArgumentException(
                    "Strategy context-menu requests must contain StrategyMenuCommand instances.",
                    nameof(commands)
                );
            }

            strategyCommands.Add(strategyCommand);
        }

        return strategyCommands;
    }
}
