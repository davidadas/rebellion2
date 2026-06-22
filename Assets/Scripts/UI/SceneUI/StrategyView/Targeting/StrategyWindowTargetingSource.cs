using System.Collections.Generic;
using System.Linq;
using Rebellion.SceneGraph;

public sealed class StrategyWindowTargetingSource
{
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

    public UIWindow Window { get; }
    public int Action { get; }
    public int SourceX { get; }
    public int SourceY { get; }
    public IReadOnlyList<ISceneNode> Items { get; }

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
