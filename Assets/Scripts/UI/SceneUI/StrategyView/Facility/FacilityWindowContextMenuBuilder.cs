using System;
using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

/// <summary>
/// Projects facility-window context state into its exact ordered command set.
/// </summary>
internal static class FacilityWindowContextMenuBuilder
{
    /// <summary>
    /// Builds commands for the active facility tab and pointer target.
    /// </summary>
    /// <param name="planet">The represented planet.</param>
    /// <param name="activeTab">The active facility tab.</param>
    /// <param name="contextManufacturingTab">The targeted manufacturing facility tab.</param>
    /// <param name="contextInventoryItem">The targeted inventory building.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <returns>The ordered context commands.</returns>
    public static List<StrategyMenuCommand> Build(
        Planet planet,
        FacilityWindowTab activeTab,
        FacilityWindowTab? contextManufacturingTab,
        Building contextInventoryItem,
        string playerFactionId
    )
    {
        bool playerControlsPlanet =
            planet != null
            && !string.IsNullOrEmpty(playerFactionId)
            && string.Equals(planet.OwnerInstanceID, playerFactionId, StringComparison.Ordinal);
        if (activeTab == FacilityWindowTab.Manufacturing && contextManufacturingTab.HasValue)
        {
            return BuildManufacturingCommands(
                planet,
                contextManufacturingTab.Value,
                playerControlsPlanet
            );
        }

        if (activeTab != FacilityWindowTab.Manufacturing && contextInventoryItem != null)
            return BuildInventoryCommands(contextInventoryItem, playerControlsPlanet);

        return new List<StrategyMenuCommand>();
    }

    /// <summary>
    /// Builds commands for one manufacturing lane in authored display order.
    /// </summary>
    /// <param name="planet">The represented planet.</param>
    /// <param name="manufacturingTab">The targeted manufacturing facility tab.</param>
    /// <param name="playerControlsPlanet">Whether the player controls the represented planet.</param>
    /// <returns>The ordered manufacturing commands.</returns>
    private static List<StrategyMenuCommand> BuildManufacturingCommands(
        Planet planet,
        FacilityWindowTab manufacturingTab,
        bool playerControlsPlanet
    )
    {
        return new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(
                StrategyMenuAction.Build,
                "Build",
                CanBuildFromCard(planet, manufacturingTab, playerControlsPlanet)
            ),
            new StrategyMenuCommand(
                StrategyMenuAction.Stop,
                "Stop",
                CanStopFromCard(planet, manufacturingTab, playerControlsPlanet)
            ),
            new StrategyMenuCommand(
                StrategyMenuAction.Destination,
                "Destination",
                playerControlsPlanet
            ),
            new StrategyMenuCommand(StrategyMenuAction.None, "Rename", false),
            new StrategyMenuCommand(StrategyMenuAction.Encyclopedia, "Encyclopedia", true),
            new StrategyMenuCommand(StrategyMenuAction.Status, "Status", true),
            new StrategyMenuCommand(StrategyMenuAction.None, "Reserved", false),
        };
    }

    /// <summary>
    /// Determines whether a controlled planet can manufacture from one facility lane.
    /// </summary>
    /// <param name="planet">The represented planet.</param>
    /// <param name="manufacturingTab">The manufacturing facility tab.</param>
    /// <param name="playerControlsPlanet">Whether the player controls the represented planet.</param>
    /// <returns>True when a matching production facility is available.</returns>
    private static bool CanBuildFromCard(
        Planet planet,
        FacilityWindowTab manufacturingTab,
        bool playerControlsPlanet
    )
    {
        ManufacturingType? type = ConstructionOrderController.GetManufacturingType(
            manufacturingTab
        );
        return playerControlsPlanet
            && type.HasValue
            && planet.GetProductionFacilityCount(type.Value) > 0;
    }

    /// <summary>
    /// Determines whether a controlled facility lane has manufacturing orders to stop.
    /// </summary>
    /// <param name="planet">The represented planet.</param>
    /// <param name="manufacturingTab">The manufacturing facility tab.</param>
    /// <param name="playerControlsPlanet">Whether the player controls the represented planet.</param>
    /// <returns>True when the matching manufacturing queue is not empty.</returns>
    private static bool CanStopFromCard(
        Planet planet,
        FacilityWindowTab manufacturingTab,
        bool playerControlsPlanet
    )
    {
        ManufacturingType? type = ConstructionOrderController.GetManufacturingType(
            manufacturingTab
        );
        return playerControlsPlanet
            && type.HasValue
            && planet.ManufacturingQueue.TryGetValue(type.Value, out List<IManufacturable> queue)
            && queue.Count > 0;
    }

    /// <summary>
    /// Builds commands for one facility inventory item in authored display order.
    /// </summary>
    /// <param name="building">The targeted facility inventory item.</param>
    /// <param name="playerControlsPlanet">Whether the player controls the represented planet.</param>
    /// <returns>The ordered inventory commands.</returns>
    private static List<StrategyMenuCommand> BuildInventoryCommands(
        Building building,
        bool playerControlsPlanet
    )
    {
        bool underConstruction = building?.GetManufacturingStatus() == ManufacturingStatus.Building;
        return new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(StrategyMenuAction.Encyclopedia, "Encyclopedia", true),
            new StrategyMenuCommand(StrategyMenuAction.Status, "Status", true),
            new StrategyMenuCommand(
                underConstruction ? StrategyMenuAction.Stop : StrategyMenuAction.Scrap,
                underConstruction ? "Stop" : "Scrap",
                playerControlsPlanet
            ),
        };
    }
}
