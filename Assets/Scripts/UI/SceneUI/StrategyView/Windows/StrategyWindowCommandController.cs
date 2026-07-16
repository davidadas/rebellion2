using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.SceneGraph;

/// <summary>
/// Executes mission and movement commands shared by strategy feature windows.
/// </summary>
public sealed class StrategyWindowCommandController : IStrategyWindowCommandActions
{
    private readonly MissionCreateWindowController missionCreateWindowController;
    private readonly StrategyConfirmActionController confirmActionController;
    private readonly ConfirmDialogWindowController confirmDialogWindowController;
    private readonly Func<string> getPlayerFactionId;
    private readonly Action<UIWindow> clearWindowSelection;
    private readonly Action rebuildSnapshot;
    private readonly Action markDirty;

    /// <summary>
    /// Creates the shared strategy-window command handler.
    /// </summary>
    /// <param name="missionCreateWindowController">Owns mission-creation windows.</param>
    /// <param name="confirmActionController">Executes confirmed game actions.</param>
    /// <param name="confirmDialogWindowController">Owns confirmation windows.</param>
    /// <param name="getPlayerFactionId">Returns the current player faction identifier.</param>
    /// <param name="clearWindowSelection">Clears selection owned by a source window.</param>
    /// <param name="rebuildSnapshot">Rebuilds the visible strategy snapshot.</param>
    /// <param name="markDirty">Invalidates the strategy presentation.</param>
    public StrategyWindowCommandController(
        MissionCreateWindowController missionCreateWindowController,
        StrategyConfirmActionController confirmActionController,
        ConfirmDialogWindowController confirmDialogWindowController,
        Func<string> getPlayerFactionId,
        Action<UIWindow> clearWindowSelection,
        Action rebuildSnapshot,
        Action markDirty
    )
    {
        this.missionCreateWindowController =
            missionCreateWindowController
            ?? throw new ArgumentNullException(nameof(missionCreateWindowController));
        this.confirmActionController =
            confirmActionController
            ?? throw new ArgumentNullException(nameof(confirmActionController));
        this.confirmDialogWindowController =
            confirmDialogWindowController
            ?? throw new ArgumentNullException(nameof(confirmDialogWindowController));
        this.getPlayerFactionId =
            getPlayerFactionId ?? throw new ArgumentNullException(nameof(getPlayerFactionId));
        this.clearWindowSelection =
            clearWindowSelection ?? throw new ArgumentNullException(nameof(clearWindowSelection));
        this.rebuildSnapshot =
            rebuildSnapshot ?? throw new ArgumentNullException(nameof(rebuildSnapshot));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
    }

    /// <inheritdoc />
    public void OpenMissionCreateWindow(
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    )
    {
        missionCreateWindowController.Open(target, items);
    }

    /// <inheritdoc />
    public bool TryExecuteMove(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    )
    {
        List<ISceneNode> sourceItems = items?.ToList() ?? new List<ISceneNode>();
        if (!confirmActionController.TryExecuteMove(target, sourceItems, GetPlayerFactionId()))
            return false;

        clearWindowSelection(sourceWindow);
        rebuildSnapshot();
        markDirty();
        return true;
    }

    /// <inheritdoc />
    public void OpenMoveConfirmWindow(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    )
    {
        confirmDialogWindowController.OpenMove(sourceWindow, target, items, GetPlayerFactionId());
    }

    /// <summary>
    /// Gets a non-null player faction identifier for command validation.
    /// </summary>
    /// <returns>The current player faction identifier.</returns>
    private string GetPlayerFactionId()
    {
        return getPlayerFactionId() ?? string.Empty;
    }
}
