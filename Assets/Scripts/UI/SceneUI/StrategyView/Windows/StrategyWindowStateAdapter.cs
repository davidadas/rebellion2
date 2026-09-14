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
        Func<WindowState, bool> restore
    )
    {
        if (string.IsNullOrWhiteSpace(windowTypeID))
            throw new ArgumentException("Window type ID is required.", nameof(windowTypeID));

        WindowTypeID = windowTypeID;
        this.getTargetInstanceID =
            getTargetInstanceID ?? throw new ArgumentNullException(nameof(getTargetInstanceID));
        this.restore = restore ?? throw new ArgumentNullException(nameof(restore));
    }

    /// <inheritdoc />
    public bool TryGetTargetInstanceID(UIWindow window, out string targetInstanceID)
    {
        targetInstanceID = null;
        if (window == null || !window.TryGetContent(out TView view))
            return false;

        targetInstanceID = getTargetInstanceID(view);
        if (string.IsNullOrEmpty(targetInstanceID))
            return false;

        return true;
    }

    /// <inheritdoc />
    public bool Restore(WindowState state)
    {
        return state != null && restore(state);
    }
}
