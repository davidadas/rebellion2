using System.Collections.Generic;
using Rebellion.SceneGraph;

/// <summary>
/// Performs game-level mission and movement actions requested by strategy feature windows.
/// </summary>
public interface IStrategyWindowCommandActions
{
    /// <summary>
    /// Opens mission creation for selected participants and a target.
    /// </summary>
    /// <param name="target">The selected mission target.</param>
    /// <param name="items">The selected mission participants.</param>
    void OpenMissionCreateWindow(StrategyMissionTarget target, IReadOnlyList<ISceneNode> items);

    /// <summary>
    /// Executes an immediate move for selected items.
    /// </summary>
    /// <param name="sourceWindow">The strategy window that owns the selection.</param>
    /// <param name="target">The selected move target.</param>
    /// <param name="items">The selected movable items.</param>
    /// <returns>True when the move was executed.</returns>
    bool TryExecuteMove(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    );

    /// <summary>
    /// Opens a confirmed move for selected items.
    /// </summary>
    /// <param name="sourceWindow">The strategy window that owns the selection.</param>
    /// <param name="target">The selected move target.</param>
    /// <param name="items">The selected movable items.</param>
    void OpenMoveConfirmWindow(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    );
}

/// <summary>
/// Opens confirmation flows shared by strategy feature windows.
/// </summary>
public interface IStrategyConfirmationActions
{
    /// <summary>
    /// Determines whether the complete personnel selection may be retired.
    /// </summary>
    /// <param name="items">The selected personnel or their snapshots.</param>
    /// <returns>True when every selected person may be retired.</returns>
    bool CanRetire(IReadOnlyList<ISceneNode> items);

    /// <summary>
    /// Opens scrap confirmation for selected units.
    /// </summary>
    /// <param name="sourceWindow">The strategy window that owns the selection.</param>
    /// <param name="items">The selected units.</param>
    void OpenScrapConfirmWindow(UIWindow sourceWindow, IReadOnlyList<ISceneNode> items);

    /// <summary>
    /// Opens stop-construction confirmation for selected queued items.
    /// </summary>
    /// <param name="sourceWindow">The strategy window that owns the selection.</param>
    /// <param name="items">The selected queued items.</param>
    void OpenStopConstructionConfirmWindow(UIWindow sourceWindow, IReadOnlyList<ISceneNode> items);

    /// <summary>
    /// Opens retirement confirmation for selected personnel.
    /// </summary>
    /// <param name="sourceWindow">The strategy window that owns the selection.</param>
    /// <param name="items">The selected personnel.</param>
    void OpenRetireConfirmWindow(UIWindow sourceWindow, IReadOnlyList<ISceneNode> items);
}
