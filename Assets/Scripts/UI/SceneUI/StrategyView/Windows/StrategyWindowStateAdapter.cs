using System;
using Rebellion.Game.UIState;

/// <summary>
/// Adapts one authored strategy-window view type to generic persisted window state.
/// </summary>
/// <typeparam name="TView">The authored view type owned by the adapter.</typeparam>
public sealed class StrategyWindowStateAdapter<TView> : IStrategyWindowStateAdapter
    where TView : class
{
    private readonly Func<TView, string> getTargetInstanceID;
    private readonly Func<WindowState, bool> restore;
    private readonly Func<TView, string, UIWindow, int, WindowState> capture;

    public string WindowTypeID { get; }

    /// <summary>
    /// Creates an adapter for one stable strategy-window type.
    /// </summary>
    /// <param name="windowTypeID">The stable serialized window type identifier.</param>
    /// <param name="getTargetInstanceID">Resolves the represented game object.</param>
    /// <param name="restore">Restores the authored window from persisted state.</param>
    public StrategyWindowStateAdapter(
        string windowTypeID,
        Func<TView, string> getTargetInstanceID,
        Func<WindowState, bool> restore,
        Func<TView, string, UIWindow, int, WindowState> capture = null
    )
    {
        if (string.IsNullOrWhiteSpace(windowTypeID))
            throw new ArgumentException("Window type ID is required.", nameof(windowTypeID));

        WindowTypeID = windowTypeID;
        this.getTargetInstanceID =
            getTargetInstanceID ?? throw new ArgumentNullException(nameof(getTargetInstanceID));
        this.restore = restore ?? throw new ArgumentNullException(nameof(restore));
        this.capture = capture ?? DefaultCapture;
    }

    /// <summary>
    /// Captures one supported runtime window into persisted state.
    /// </summary>
    /// <param name="window">The runtime window to inspect.</param>
    /// <param name="zOrder">The runtime window stacking position.</param>
    /// <param name="state">The captured persisted window state.</param>
    /// <returns>True when this adapter owns the window and captured it.</returns>
    public bool TryCapture(UIWindow window, int zOrder, out WindowState state)
    {
        state = null;
        if (window == null || !window.TryGetContent(out TView view))
            return false;

        string targetInstanceID = getTargetInstanceID(view);
        if (string.IsNullOrEmpty(targetInstanceID))
            return false;

        state = capture(view, targetInstanceID, window, zOrder);
        return state != null;
    }

    /// <inheritdoc />
    public bool Restore(WindowState state)
    {
        return state != null && restore(state);
    }

    private WindowState DefaultCapture(
        TView _,
        string targetInstanceID,
        UIWindow window,
        int zOrder
    )
    {
        return new WindowState(
            WindowTypeID,
            targetInstanceID,
            window.X,
            window.Y,
            window.Width,
            window.Height,
            zOrder
        );
    }
}
