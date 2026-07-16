using System.Collections.Generic;
using System.Linq;
using Rebellion.SceneGraph;

/// <summary>
/// Captures the immutable source window, hotspot, command, and selection for targeting.
/// </summary>
public sealed class StrategyWindowTargetingSource
{
    /// <summary>
    /// Creates one strategy-window targeting source snapshot.
    /// </summary>
    /// <param name="window">The source strategy window.</param>
    /// <param name="action">The semantic command identifier.</param>
    /// <param name="sourceX">The source-space horizontal hotspot coordinate.</param>
    /// <param name="sourceY">The source-space vertical hotspot coordinate.</param>
    /// <param name="items">The selected scene nodes in source order.</param>
    public StrategyWindowTargetingSource(
        UIWindow window,
        int action,
        int sourceX,
        int sourceY,
        IReadOnlyList<ISceneNode> items
    )
    {
        Window = window;
        Action = action;
        SourceX = sourceX;
        SourceY = sourceY;
        Items = items?.ToList() ?? new List<ISceneNode>();
    }

    /// <summary>
    /// Gets the source strategy window.
    /// </summary>
    public UIWindow Window { get; }

    /// <summary>
    /// Gets the semantic command identifier.
    /// </summary>
    public int Action { get; }

    /// <summary>
    /// Gets the source-space horizontal hotspot coordinate.
    /// </summary>
    public int SourceX { get; }

    /// <summary>
    /// Gets the source-space vertical hotspot coordinate.
    /// </summary>
    public int SourceY { get; }

    /// <summary>
    /// Gets an isolated copy of the selected scene nodes.
    /// </summary>
    public IReadOnlyList<ISceneNode> Items { get; }

    /// <summary>
    /// Gets the targeting prompt for one semantic strategy command.
    /// </summary>
    /// <param name="action">The semantic command identifier.</param>
    /// <returns>The displayed targeting prompt.</returns>
    public static string GetPrompt(int action)
    {
        return action switch
        {
            StrategyContextMenuActions.CreateMission => "Select mission target",
            StrategyContextMenuActions.Destination => "Select destination",
            StrategyContextMenuActions.Move or StrategyContextMenuActions.MoveConfirm =>
                "Select move destination",
            _ => "Select target",
        };
    }
}
