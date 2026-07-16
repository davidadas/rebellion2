using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

/// <summary>
/// Builds Defense-window context commands from an already-resolved selection.
/// </summary>
internal static class DefenseWindowContextMenuBuilder
{
    /// <summary>
    /// Builds the exact Defense context-menu command ordering for a selection category.
    /// </summary>
    /// <param name="selectedItems">The context-targeted items.</param>
    /// <param name="hitItem">The item directly under the context pointer.</param>
    /// <param name="canMove">Whether the complete selection can move.</param>
    /// <param name="playerControlsItem">Whether the player controls the selection.</param>
    /// <param name="canCreateMission">Whether the selection can create a mission.</param>
    /// <param name="canRetire">Whether the selected personnel can retire.</param>
    /// <returns>The ordered context-menu commands.</returns>
    public static List<StrategyMenuCommand> Build(
        IReadOnlyList<ISceneNode> selectedItems,
        ISceneNode hitItem,
        bool canMove,
        bool playerControlsItem,
        bool canCreateMission,
        bool canRetire
    )
    {
        if (selectedItems == null || selectedItems.Count == 0)
        {
            return new List<StrategyMenuCommand>
            {
                new StrategyMenuCommand(
                    StrategyContextMenuActions.Encyclopedia,
                    "Encyclopedia",
                    false
                ),
                new StrategyMenuCommand(StrategyContextMenuActions.Status, "Status", false),
            };
        }

        if (selectedItems.All(item => item is Officer || item is SpecialForces))
        {
            return new List<StrategyMenuCommand>
            {
                new StrategyMenuCommand(StrategyContextMenuActions.Move, "Move", canMove),
                new StrategyMenuCommand(
                    StrategyContextMenuActions.MoveConfirm,
                    "Confirmed Move",
                    canMove
                ),
                new StrategyMenuCommand(
                    StrategyContextMenuActions.CreateMission,
                    "Mission",
                    canCreateMission
                ),
                new StrategyMenuCommand(
                    StrategyContextMenuActions.Encyclopedia,
                    "Encyclopedia",
                    true
                ),
                new StrategyMenuCommand(StrategyContextMenuActions.Status, "Status", true),
                new StrategyMenuCommand(StrategyContextMenuActions.Retire, "Retire", canRetire),
            };
        }

        if (hitItem is Regiment || hitItem is Starfighter)
        {
            bool underConstruction = AreUnderConstruction(selectedItems);
            return new List<StrategyMenuCommand>
            {
                new StrategyMenuCommand(StrategyContextMenuActions.Move, "Move", canMove),
                new StrategyMenuCommand(
                    StrategyContextMenuActions.MoveConfirm,
                    "Confirmed Move",
                    canMove
                ),
                new StrategyMenuCommand(
                    StrategyContextMenuActions.Encyclopedia,
                    "Encyclopedia",
                    true
                ),
                new StrategyMenuCommand(StrategyContextMenuActions.Status, "Status", true),
                new StrategyMenuCommand(
                    underConstruction
                        ? StrategyContextMenuActions.Stop
                        : StrategyContextMenuActions.Scrap,
                    underConstruction ? "Stop" : "Scrap",
                    underConstruction ? playerControlsItem : canMove
                ),
            };
        }

        bool selectionUnderConstruction = AreUnderConstruction(selectedItems);
        return new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(StrategyContextMenuActions.Encyclopedia, "Encyclopedia", true),
            new StrategyMenuCommand(StrategyContextMenuActions.Status, "Status", true),
            new StrategyMenuCommand(
                selectionUnderConstruction
                    ? StrategyContextMenuActions.Stop
                    : StrategyContextMenuActions.Scrap,
                selectionUnderConstruction ? "Stop" : "Scrap",
                playerControlsItem
            ),
        };
    }

    /// <summary>
    /// Reports whether every selected item is still being manufactured.
    /// </summary>
    /// <param name="items">The selected scene nodes.</param>
    /// <returns>True when all selected items are under construction.</returns>
    private static bool AreUnderConstruction(IReadOnlyList<ISceneNode> items)
    {
        return items.Count > 0
            && items.All(item =>
                item is IManufacturable { ManufacturingStatus: ManufacturingStatus.Building }
            );
    }
}
