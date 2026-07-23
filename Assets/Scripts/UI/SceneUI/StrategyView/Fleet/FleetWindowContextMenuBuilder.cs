using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

/// <summary>
/// Projects fleet-window selections into their exact ordered context-menu commands.
/// </summary>
internal static class FleetWindowContextMenuBuilder
{
    /// <summary>
    /// Builds the ordered command set for one fleet-window selection.
    /// </summary>
    /// <param name="items">The selected scene nodes.</param>
    /// <param name="playerControlsItems">Whether all selected items are player controlled.</param>
    /// <param name="canMove">Whether the selected items can move.</param>
    /// <param name="canCreateMission">Whether the selected personnel can start a mission.</param>
    /// <param name="canRetire">Whether the selected personnel can retire.</param>
    /// <param name="canBombard">Whether the selected fleet can bombard its current planet.</param>
    /// <param name="canDestroySystem">Whether the selected fleet can destroy its current planet.</param>
    /// <param name="canAssault">Whether the selected fleet can assault its current planet.</param>
    /// <returns>The ordered context-menu commands.</returns>
    public static List<StrategyMenuCommand> Build(
        IReadOnlyList<ISceneNode> items,
        bool playerControlsItems,
        bool canMove,
        bool canCreateMission,
        bool canRetire,
        bool canBombard = false,
        bool canDestroySystem = false,
        bool canAssault = false
    )
    {
        if (items == null || items.Count == 0)
            return BuildUnavailableInformationCommands();

        int fleetCount = items.OfType<Fleet>().Count();
        int shipCount = items.OfType<CapitalShip>().Count();
        if (fleetCount > 0 || shipCount > 0)
            return BuildFleetAndCapitalShipCommands(
                items,
                fleetCount,
                shipCount,
                playerControlsItems,
                canMove,
                canBombard,
                canDestroySystem,
                canAssault
            );

        int fighterCount = items.OfType<Starfighter>().Count();
        int troopCount = items.OfType<Regiment>().Count();
        if (fighterCount > 0 || troopCount > 0)
            return BuildTransportedUnitCommands(items, playerControlsItems, canMove);

        List<ISceneNode> personnel = items
            .Where(item => item is Officer || item is SpecialForces)
            .ToList();
        return personnel.Count > 0
            ? BuildPersonnelCommands(personnel, canMove, canCreateMission, canRetire)
            : BuildUnavailableInformationCommands();
    }

    /// <summary>
    /// Builds commands shared by fleet and capital-ship selections.
    /// </summary>
    /// <param name="items">The selected fleet or capital-ship nodes.</param>
    /// <param name="fleetCount">The selected fleet count.</param>
    /// <param name="shipCount">The selected capital-ship count.</param>
    /// <param name="playerControlsItems">Whether all selected items are player controlled.</param>
    /// <param name="canMove">Whether the complete selection can move.</param>
    /// <param name="canBombard">Whether the selected fleet can bombard its current planet.</param>
    /// <param name="canDestroySystem">Whether the selected fleet can destroy its current planet.</param>
    /// <param name="canAssault">Whether the selected fleet can assault its current planet.</param>
    /// <returns>The ordered commands.</returns>
    private static List<StrategyMenuCommand> BuildFleetAndCapitalShipCommands(
        IReadOnlyList<ISceneNode> items,
        int fleetCount,
        int shipCount,
        bool playerControlsItems,
        bool canMove,
        bool canBombard,
        bool canDestroySystem,
        bool canAssault
    )
    {
        int itemCount = fleetCount + shipCount;
        List<StrategyMenuCommand> commands = new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(StrategyMenuAction.Move, "Move", canMove),
            new StrategyMenuCommand(StrategyMenuAction.MoveConfirm, "Confirmed Move", canMove),
        };

        if (fleetCount > 0 && shipCount == 0)
        {
            commands.Add(
                StrategyBombardmentMenuBuilder.Build(
                    playerControlsItems && canBombard,
                    playerControlsItems && canDestroySystem
                )
            );
            commands.Add(
                new StrategyMenuCommand(
                    StrategyMenuAction.PlanetaryAssault,
                    "Planetary Assault",
                    playerControlsItems && canAssault
                )
            );
        }
        else if (fleetCount == 0)
        {
            commands.Add(
                new StrategyMenuCommand(StrategyMenuAction.CreateFleet, "Create Fleet", canMove)
            );
        }

        commands.Add(
            new StrategyMenuCommand(
                StrategyMenuAction.Rename,
                "Rename",
                itemCount == 1 && playerControlsItems
            )
        );
        commands.Add(
            new StrategyMenuCommand(StrategyMenuAction.Encyclopedia, "Encyclopedia", itemCount == 1)
        );
        commands.Add(new StrategyMenuCommand(StrategyMenuAction.Status, "Status", itemCount == 1));
        AddScrapOrStopCommand(
            commands,
            items,
            AreUnderConstruction(items) ? playerControlsItems : canMove
        );
        return commands;
    }

    /// <summary>
    /// Builds commands for starfighter and regiment selections.
    /// </summary>
    /// <param name="items">The selected transported units.</param>
    /// <param name="playerControlsItems">Whether the player controls all selected units.</param>
    /// <param name="canMove">Whether all selected items can move.</param>
    /// <returns>The ordered commands.</returns>
    private static List<StrategyMenuCommand> BuildTransportedUnitCommands(
        IReadOnlyList<ISceneNode> items,
        bool playerControlsItems,
        bool canMove
    )
    {
        List<StrategyMenuCommand> commands = new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(StrategyMenuAction.Move, "Move", canMove),
            new StrategyMenuCommand(StrategyMenuAction.MoveConfirm, "Confirmed Move", canMove),
            new StrategyMenuCommand(
                StrategyMenuAction.Encyclopedia,
                "Encyclopedia",
                items.Count == 1
            ),
            new StrategyMenuCommand(StrategyMenuAction.Status, "Status", items.Count == 1),
        };
        AddScrapOrStopCommand(
            commands,
            items,
            AreUnderConstruction(items) ? playerControlsItems : canMove
        );
        return commands;
    }

    /// <summary>
    /// Builds commands for officer and special-forces selections.
    /// </summary>
    /// <param name="personnel">The selected personnel.</param>
    /// <param name="canMove">Whether all selected personnel can move.</param>
    /// <param name="canCreateMission">Whether the selection can start a mission.</param>
    /// <param name="canRetire">Whether all selected personnel can retire.</param>
    /// <returns>The ordered commands.</returns>
    private static List<StrategyMenuCommand> BuildPersonnelCommands(
        IReadOnlyList<ISceneNode> personnel,
        bool canMove,
        bool canCreateMission,
        bool canRetire
    )
    {
        List<StrategyMenuCommand> commands = new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(StrategyMenuAction.Move, "Move", canMove),
            new StrategyMenuCommand(StrategyMenuAction.MoveConfirm, "Confirmed Move", canMove),
            new StrategyMenuCommand(StrategyMenuAction.CreateMission, "Mission", canCreateMission),
        };
        if (!personnel.OfType<SpecialForces>().Any())
        {
            commands.Add(
                new StrategyMenuCommand("Command", canMove, new List<StrategyMenuCommand>())
            );
        }
        commands.Add(
            new StrategyMenuCommand(
                StrategyMenuAction.Encyclopedia,
                "Encyclopedia",
                personnel.Count == 1
            )
        );
        commands.Add(
            new StrategyMenuCommand(StrategyMenuAction.Status, "Status", personnel.Count == 1)
        );
        commands.Add(new StrategyMenuCommand(StrategyMenuAction.Retire, "Retire ", canRetire));
        return commands;
    }

    /// <summary>
    /// Builds disabled information commands for an unsupported or empty selection.
    /// </summary>
    /// <returns>The ordered disabled information commands.</returns>
    private static List<StrategyMenuCommand> BuildUnavailableInformationCommands()
    {
        return new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(StrategyMenuAction.Encyclopedia, "Encyclopedia", false),
            new StrategyMenuCommand(StrategyMenuAction.Status, "Status", false),
        };
    }

    /// <summary>
    /// Adds the destructive command appropriate to the selected manufacturing state.
    /// </summary>
    /// <param name="commands">The destination command list.</param>
    /// <param name="items">The selected scene nodes.</param>
    /// <param name="enabled">Whether the command can execute.</param>
    private static void AddScrapOrStopCommand(
        List<StrategyMenuCommand> commands,
        IReadOnlyList<ISceneNode> items,
        bool enabled
    )
    {
        bool underConstruction = AreUnderConstruction(items);
        commands.Add(
            new StrategyMenuCommand(
                underConstruction ? StrategyMenuAction.Stop : StrategyMenuAction.Scrap,
                underConstruction ? "Stop" : "Scrap",
                enabled
            )
        );
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
