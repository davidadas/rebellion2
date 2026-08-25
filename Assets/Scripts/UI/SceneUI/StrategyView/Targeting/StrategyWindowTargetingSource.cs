using System.Collections.Generic;
using System.Linq;
using Rebellion.SceneGraph;

/// <summary>
/// Captures the source window, hotspot, command, selection, and transient waypoint plan for
/// one targeting session.
/// </summary>
public sealed class StrategyWindowTargetingSource
{
    private readonly List<string> waypointPlanetIds = new List<string>();

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
        StrategyMenuAction action,
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

    public UIWindow Window { get; }

    public StrategyMenuAction Action { get; }

    public int SourceX { get; }

    public int SourceY { get; }

    public IReadOnlyList<ISceneNode> Items { get; }

    public IReadOnlyList<string> WaypointPlanetIds => waypointPlanetIds;

    /// <summary>
    /// Appends one planet identifier to this session's uncommitted waypoint plan.
    /// </summary>
    /// <param name="planetInstanceId">The planned destination planet identifier.</param>
    /// <returns>True when the identifier was appended.</returns>
    internal bool TryAppendWaypoint(string planetInstanceId)
    {
        if (string.IsNullOrEmpty(planetInstanceId))
            return false;

        waypointPlanetIds.Add(planetInstanceId);
        return true;
    }

    /// <summary>
    /// Removes the most recently planned waypoint.
    /// </summary>
    /// <returns>True when a waypoint was removed.</returns>
    internal bool TryRemoveLastWaypoint()
    {
        if (waypointPlanetIds.Count == 0)
            return false;

        waypointPlanetIds.RemoveAt(waypointPlanetIds.Count - 1);
        return true;
    }

    /// <summary>
    /// Gets the targeting prompt for one semantic strategy command.
    /// </summary>
    /// <param name="action">The semantic command identifier.</param>
    /// <returns>The displayed targeting prompt.</returns>
    public static string GetPrompt(StrategyMenuAction action)
    {
        return action switch
        {
            StrategyMenuAction.CreateMission => "Select mission target",
            StrategyMenuAction.Destination => "Select destination",
            StrategyMenuAction.Move or StrategyMenuAction.MoveConfirm => "Select move destination",
            StrategyMenuAction.WaypointMove =>
                "Select waypoints; press Enter to move or Escape to undo",
            _ => "Select target",
        };
    }
}
