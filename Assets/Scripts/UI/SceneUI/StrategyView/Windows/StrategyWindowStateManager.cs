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
    /// Attempts to capture a supported runtime window.
    /// </summary>
    /// <param name="window">The runtime window to inspect.</param>
    /// <param name="state">The captured state when the window is supported.</param>
    /// <returns>True when this adapter owns the window.</returns>
    bool TryCapture(UIWindow window, out WindowState state);

    /// <summary>
    /// Attempts to restore one persisted window.
    /// </summary>
    /// <param name="state">The persisted window state.</param>
    /// <returns>True when the window was restored.</returns>
    bool Restore(WindowState state);
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
                if (!adapter.TryCapture(window, out WindowState state) || state == null)
                    continue;

                state.WindowTypeID = adapter.WindowTypeID;
                state.X = window.X;
                state.Y = window.Y;
                state.Width = window.Width;
                state.Height = window.Height;
                state.ZOrder = zOrder;
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
        foreach (WindowState state in states.OrderBy(state => state.ZOrder).ToList())
        {
            if (
                state != null
                && !string.IsNullOrEmpty(state.WindowTypeID)
                && adapters.TryGetValue(state.WindowTypeID, out IStrategyWindowStateAdapter adapter)
            )
            {
                adapter.Restore(state);
            }
        }
    }
}
