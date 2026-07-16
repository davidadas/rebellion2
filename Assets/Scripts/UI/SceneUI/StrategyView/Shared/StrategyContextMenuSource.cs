/// <summary>
/// Exposes the window that initiated one strategy context-menu request.
/// </summary>
public interface IStrategyContextMenuSource
{
    /// <summary>
    /// Gets the window that owns the active context-menu request.
    /// </summary>
    UIWindow Window { get; }
}
