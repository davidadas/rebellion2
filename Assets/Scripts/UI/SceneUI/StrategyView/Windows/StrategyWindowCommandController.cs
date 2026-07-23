using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

/// <summary>
/// Executes and finalizes commands shared by strategy feature windows.
/// </summary>
public sealed class StrategyWindowCommandController
    : IStrategyWindowCommandActions,
        IStrategyConfirmationActions
{
    private readonly MissionCreateWindowController missionCreateWindowController;
    private readonly ConfirmDialogWindowController confirmDialogWindowController;
    private readonly GameManager gameManager;
    private readonly Action<string> playSfx;
    private readonly Action<UIWindow> clearWindowSelection;
    private readonly Action rebuildSnapshot;
    private readonly Action markDirty;

    /// <summary>
    /// Creates the shared strategy-window command handler.
    /// </summary>
    /// <param name="missionCreateWindowController">Owns mission-creation windows.</param>
    /// <param name="confirmDialogWindowController">Owns confirmation windows.</param>
    /// <param name="gameManager">Owns the active game and its domain systems.</param>
    /// <param name="playSfx">Plays an optional officer response.</param>
    /// <param name="clearWindowSelection">Clears selection owned by a source window.</param>
    /// <param name="rebuildSnapshot">Rebuilds the visible strategy snapshot.</param>
    /// <param name="markDirty">Invalidates the strategy presentation.</param>
    public StrategyWindowCommandController(
        MissionCreateWindowController missionCreateWindowController,
        ConfirmDialogWindowController confirmDialogWindowController,
        GameManager gameManager,
        Action<string> playSfx,
        Action<UIWindow> clearWindowSelection,
        Action rebuildSnapshot,
        Action markDirty
    )
    {
        this.missionCreateWindowController =
            missionCreateWindowController
            ?? throw new ArgumentNullException(nameof(missionCreateWindowController));
        this.confirmDialogWindowController =
            confirmDialogWindowController
            ?? throw new ArgumentNullException(nameof(confirmDialogWindowController));
        this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
        this.playSfx = playSfx ?? throw new ArgumentNullException(nameof(playSfx));
        this.clearWindowSelection =
            clearWindowSelection ?? throw new ArgumentNullException(nameof(clearWindowSelection));
        this.rebuildSnapshot =
            rebuildSnapshot ?? throw new ArgumentNullException(nameof(rebuildSnapshot));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
    }

    /// <summary>
    /// Executes the semantic command completed by one targeting request.
    /// </summary>
    /// <param name="source">The targeting source command and selection.</param>
    /// <param name="target">The selected strategy target.</param>
    public void ExecuteTargetedCommand(
        StrategyWindowTargetingSource source,
        StrategyMissionTarget target
    )
    {
        if (source == null || target == null)
            return;

        switch (source.Action)
        {
            case StrategyMenuAction.CreateMission:
                OpenMissionCreateWindow(target, source.Items);
                break;
            case StrategyMenuAction.Move:
                TryExecuteMove(source.Window, target, source.Items);
                break;
            case StrategyMenuAction.MoveConfirm:
                OpenMoveConfirmWindow(source.Window, target, source.Items);
                break;
        }
    }

    /// <summary>
    /// Opens mission creation for selected participants and a target.
    /// </summary>
    /// <param name="target">The selected mission target.</param>
    /// <param name="items">The selected mission participants.</param>
    public void OpenMissionCreateWindow(
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    )
    {
        missionCreateWindowController.Open(target, items);
    }

    /// <summary>
    /// Executes an immediate move for selected items.
    /// </summary>
    /// <param name="sourceWindow">The strategy window that owns the selection.</param>
    /// <param name="target">The selected move target.</param>
    /// <param name="items">The selected movable items.</param>
    /// <returns>True when the move was executed.</returns>
    public bool TryExecuteMove(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    )
    {
        List<ISceneNode> sourceItems = CopyItems(items);
        if (!TryMove(target, sourceItems))
            return false;

        RefreshAfterMutation(sourceWindow);
        return true;
    }

    /// <summary>
    /// Opens a confirmed move for selected items.
    /// </summary>
    /// <param name="sourceWindow">The strategy window that owns the selection.</param>
    /// <param name="target">The selected move target.</param>
    /// <param name="items">The selected movable items.</param>
    public void OpenMoveConfirmWindow(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    )
    {
        List<ISceneNode> sourceItems = CopyItems(items);
        ContainerNode destination = target?.GetMoveDestination() as ContainerNode;
        int transitTimeInDays = gameManager.MovementSystem.TryGetSelectionTransitTicks(
            sourceItems,
            destination,
            GetPlayerFactionID(),
            out int transitTicks
        )
            ? transitTicks
            : -1;
        confirmDialogWindowController.OpenMove(
            sourceWindow,
            sourceItems,
            transitTimeInDays,
            () =>
            {
                if (TryMove(target, sourceItems))
                    RefreshAfterMutation(sourceWindow);
            }
        );
    }

    /// <summary>
    /// Opens scrap confirmation for selected units.
    /// </summary>
    /// <param name="sourceWindow">The strategy window that owns the selection.</param>
    /// <param name="items">The selected units.</param>
    public void OpenScrapConfirmWindow(UIWindow sourceWindow, IReadOnlyList<ISceneNode> items)
    {
        List<ISceneNode> sourceItems = CopyItems(items);
        confirmDialogWindowController.OpenScrap(
            sourceWindow,
            sourceItems,
            () =>
            {
                List<IManufacturable> manufacturables = sourceItems
                    .OfType<IManufacturable>()
                    .ToList();
                if (
                    manufacturables.Count == sourceItems.Count
                    && gameManager.TryScrap(manufacturables, GetPlayerFactionID())
                )
                {
                    RefreshAfterMutation(sourceWindow);
                }
            }
        );
    }

    /// <summary>
    /// Opens stop-construction confirmation for selected queued items.
    /// </summary>
    /// <param name="sourceWindow">The strategy window that owns the selection.</param>
    /// <param name="items">The selected queued items.</param>
    public void OpenStopConstructionConfirmWindow(
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> items
    )
    {
        List<ISceneNode> sourceItems = CopyItems(items);
        confirmDialogWindowController.OpenStopConstruction(
            sourceWindow,
            sourceItems,
            () =>
            {
                List<IManufacturable> manufacturables = sourceItems
                    .OfType<IManufacturable>()
                    .ToList();
                if (
                    manufacturables.Count == sourceItems.Count
                    && gameManager.ManufacturingSystem.CancelManufacturing(
                        manufacturables,
                        GetPlayerFactionID()
                    )
                )
                {
                    RefreshAfterMutation(sourceWindow);
                }
            }
        );
    }

    /// <summary>
    /// Opens retirement confirmation for selected personnel.
    /// </summary>
    /// <param name="sourceWindow">The strategy window that owns the selection.</param>
    /// <param name="items">The selected personnel.</param>
    public void OpenRetireConfirmWindow(UIWindow sourceWindow, IReadOnlyList<ISceneNode> items)
    {
        List<ISceneNode> sourceItems = CopyItems(items);
        if (!CanRetire(sourceItems))
            return;

        confirmDialogWindowController.OpenRetire(
            sourceWindow,
            sourceItems,
            () =>
            {
                if (gameManager.PersonnelSystem.Retire(sourceItems, GetPlayerFactionID()))
                    RefreshAfterMutation(sourceWindow);
            }
        );
    }

    /// <summary>
    /// Determines whether the complete personnel selection may be retired.
    /// </summary>
    /// <param name="items">The selected personnel or their snapshots.</param>
    /// <returns>True when every selected person may be retired.</returns>
    public bool CanRetire(IReadOnlyList<ISceneNode> items)
    {
        return gameManager.PersonnelSystem.CanRetire(items, GetPlayerFactionID());
    }

    /// <summary>
    /// Executes a validated selection move through the movement system.
    /// </summary>
    /// <param name="target">The requested strategy target.</param>
    /// <param name="items">The selected scene nodes.</param>
    /// <returns>True when the movement order was accepted.</returns>
    private bool TryMove(StrategyMissionTarget target, IReadOnlyList<ISceneNode> items)
    {
        bool moved = gameManager.TryRequestMove(
            items,
            target?.GetMoveDestination() as ContainerNode,
            GetPlayerFactionID()
        );
        if (moved)
            PlayMoveVoice(items);

        return moved;
    }

    /// <summary>
    /// Plays an available order acknowledgment for a single selected officer.
    /// </summary>
    /// <param name="items">The selected scene nodes.</param>
    private void PlayMoveVoice(IReadOnlyList<ISceneNode> items)
    {
        if (items == null || items.Count != 1 || items[0] is not Officer selectedOfficer)
            return;

        Officer officer = gameManager
            .GetGame()
            .GetSceneNodeByInstanceID<Officer>(selectedOfficer.InstanceID);
        string voicePath = officer?.GetVoicePath(
            OfficerVoiceLineType.Order,
            gameManager.GetGame().Random
        );
        if (!string.IsNullOrEmpty(voicePath))
            playSfx(voicePath);
    }

    /// <summary>
    /// Clears source selection and refreshes strategy state after a domain mutation.
    /// </summary>
    /// <param name="sourceWindow">The window that owned the selection.</param>
    private void RefreshAfterMutation(UIWindow sourceWindow)
    {
        clearWindowSelection(sourceWindow);
        rebuildSnapshot();
        markDirty();
    }

    /// <summary>
    /// Gets a non-null player faction identifier for command validation.
    /// </summary>
    /// <returns>The current player faction identifier.</returns>
    private string GetPlayerFactionID()
    {
        return gameManager.GetPlayerFaction()?.InstanceID ?? string.Empty;
    }

    /// <summary>
    /// Copies a selection into stable command storage.
    /// </summary>
    /// <param name="items">The selected scene nodes.</param>
    /// <returns>A non-null list preserving selection order.</returns>
    private static List<ISceneNode> CopyItems(IReadOnlyList<ISceneNode> items)
    {
        return items?.ToList() ?? new List<ISceneNode>();
    }
}
