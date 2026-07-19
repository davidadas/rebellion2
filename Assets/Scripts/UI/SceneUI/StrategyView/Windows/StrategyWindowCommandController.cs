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
        List<ISceneNode> sourceItems = CopyItems(items);
        if (!TryMove(target, sourceItems))
            return false;

        RefreshAfterMutation(sourceWindow);
        return true;
    }

    /// <inheritdoc />
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
            GetPlayerFactionId(),
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

    /// <inheritdoc />
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
                    && gameManager.MaintenanceSystem.Scrap(manufacturables, GetPlayerFactionId())
                )
                {
                    RefreshAfterMutation(sourceWindow);
                }
            }
        );
    }

    /// <inheritdoc />
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
                        GetPlayerFactionId()
                    )
                )
                {
                    RefreshAfterMutation(sourceWindow);
                }
            }
        );
    }

    /// <inheritdoc />
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
                if (gameManager.PersonnelSystem.Retire(sourceItems, GetPlayerFactionId()))
                    RefreshAfterMutation(sourceWindow);
            }
        );
    }

    /// <inheritdoc />
    public bool CanRetire(IReadOnlyList<ISceneNode> items)
    {
        return gameManager.PersonnelSystem.CanRetire(items, GetPlayerFactionId());
    }

    /// <summary>
    /// Executes a validated selection move through the movement system.
    /// </summary>
    /// <param name="target">The requested strategy target.</param>
    /// <param name="items">The selected scene nodes.</param>
    /// <returns>True when the movement order was accepted.</returns>
    private bool TryMove(StrategyMissionTarget target, IReadOnlyList<ISceneNode> items)
    {
        bool moved = gameManager.MovementSystem.TryRequestMove(
            items,
            target?.GetMoveDestination() as ContainerNode,
            GetPlayerFactionId()
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
    private string GetPlayerFactionId()
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
