using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.UIState;

/// <summary>
/// Converts one registered strategy-window type between its runtime and persisted forms.
/// </summary>
public interface IStrategyWindowStateAdapter
{
    string WindowTypeID { get; }

    /// <summary>
    /// Attempts to capture one runtime window into persisted state.
    /// </summary>
    /// <param name="window">The runtime window to inspect.</param>
    /// <param name="zOrder">The runtime window stacking position.</param>
    /// <param name="state">The captured persisted window state.</param>
    /// <returns>True when this adapter owns the window and captured it.</returns>
    bool TryCapture(UIWindow window, int zOrder, out WindowState state);

    /// <summary>
    /// Attempts to restore one persisted window.
    /// </summary>
    /// <param name="state">The persisted window state.</param>
    /// <returns>The restored runtime window, or null when restoration failed.</returns>
    UIWindow Restore(WindowState state);
}

/// <summary>
/// Captures and restores extensible strategy-window state through registered adapters.
/// </summary>
public sealed class StrategyWindowStateManager
{
    private readonly Dictionary<string, IStrategyWindowStateAdapter> adapters = new Dictionary<
        string,
        IStrategyWindowStateAdapter
    >(StringComparer.Ordinal);
    private readonly UIWindowManager windowManager;
    private IList<WindowState> states;

    /// <summary>
    /// Creates a state manager for one player's saved window collection.
    /// </summary>
    /// <param name="windowManager">The runtime strategy-window registry.</param>
    /// <param name="states">The player's persisted window collection.</param>
    public StrategyWindowStateManager(UIWindowManager windowManager, IList<WindowState> states)
    {
        this.windowManager =
            windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        this.states = states ?? throw new ArgumentNullException(nameof(states));
    }

    /// <summary>
    /// Registers the adapter responsible for one stable window type identifier.
    /// </summary>
    /// <param name="adapter">The window adapter to register.</param>
    public void Register(IStrategyWindowStateAdapter adapter)
    {
        if (adapter == null)
            throw new ArgumentNullException(nameof(adapter));
        if (string.IsNullOrWhiteSpace(adapter.WindowTypeID))
            throw new ArgumentException("Window type ID is required.", nameof(adapter));
        if (!adapters.TryAdd(adapter.WindowTypeID, adapter))
            throw new InvalidOperationException(
                $"Window type '{adapter.WindowTypeID}' is already registered."
            );
    }

    /// <summary>
    /// Replaces the persisted collection after the active game changes.
    /// </summary>
    /// <param name="newStates">The replacement player's saved windows.</param>
    public void Reset(IList<WindowState> newStates)
    {
        states = newStates ?? throw new ArgumentNullException(nameof(newStates));
    }

    /// <summary>
    /// Replaces persisted state with the currently registered supported windows.
    /// </summary>
    public void Capture()
    {
        List<WindowState> captured = new List<WindowState>();
        for (int zOrder = 0; zOrder < windowManager.Windows.Count; zOrder++)
        {
            UIWindow window = windowManager.Windows[zOrder];
            if (window?.Modal != false)
                continue;

            foreach (IStrategyWindowStateAdapter adapter in adapters.Values)
            {
                if (!adapter.TryCapture(window, zOrder, out WindowState state))
                    continue;

                captured.Add(state);
                break;
            }
        }

        states.Clear();
        foreach (WindowState state in captured)
            states.Add(state);
    }

    /// <summary>
    /// Restores all saved windows whose type adapters and targets are currently available.
    /// </summary>
    public void Restore()
    {
        List<(WindowState State, UIWindow Window)> restored = new List<(WindowState, UIWindow)>();
        foreach (
            WindowState state in states
                .Where(state => state != null)
                .OrderBy(state => state.GetZOrder())
                .ToList()
        )
        {
            if (
                !string.IsNullOrEmpty(state.GetWindowTypeID())
                && adapters.TryGetValue(
                    state.GetWindowTypeID(),
                    out IStrategyWindowStateAdapter adapter
                )
            )
            {
                UIWindow window = adapter.Restore(state);
                if (window == null)
                    continue;

                window.Resize(state.GetWidth(), state.GetHeight());
                restored.Add((state, window));
            }
        }

        List<UIWindow> orderedWindows = windowManager
            .Windows.Where(window => restored.All(item => item.Window != window))
            .ToList();
        foreach (
            (WindowState State, UIWindow Window) restoredWindow in restored.OrderBy(item =>
                item.State.GetZOrder()
            )
        )
        {
            int index = Math.Clamp(restoredWindow.State.GetZOrder(), 0, orderedWindows.Count);
            orderedWindows.Insert(index, restoredWindow.Window);
        }

        windowManager.SetStackOrder(orderedWindows);
    }
}
