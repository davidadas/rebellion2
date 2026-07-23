using System.Collections.Generic;
using Rebellion.SceneGraph;

/// <summary>
/// Builds context-menu commands for one planet-system window hit.
/// </summary>
internal static class PlanetSystemWindowContextMenuBuilder
{
    /// <summary>
    /// Creates context commands for one planet-system hit.
    /// </summary>
    /// <param name="hit">The active semantic planet hit.</param>
    /// <param name="fleetItems">The player-controlled fleet items at the hit planet.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <param name="canBombard">Whether the fleets can bombard the hit planet.</param>
    /// <param name="canDestroySystem">Whether the fleets can destroy the hit planet.</param>
    /// <param name="canAssault">Whether the fleets can assault the hit planet.</param>
    /// <returns>The available context commands in display order.</returns>
    public static List<StrategyMenuCommand> Create(
        PlanetSystemWindowHit hit,
        List<ISceneNode> fleetItems,
        string playerFactionId,
        bool canBombard = false,
        bool canDestroySystem = false,
        bool canAssault = false
    )
    {
        if (hit?.GalaxyMapPlanet == null)
            return CreatePlanetInformationCommands(false);
        if (hit.PlanetImage || hit.Icon == PlanetIcon.None)
            return CreatePlanetInformationCommands(true);

        return hit.Icon switch
        {
            PlanetIcon.Facility => CreatePlanetInformationCommands(true),
            PlanetIcon.Defense => CreatePlanetInformationCommands(true),
            PlanetIcon.Fleet => CreateFleetCommands(
                fleetItems,
                playerFactionId,
                canBombard,
                canDestroySystem,
                canAssault
            ),
            PlanetIcon.Mission => new List<StrategyMenuCommand>
            {
                new StrategyMenuCommand(StrategyMenuAction.Encyclopedia, "Encyclopedia", false),
                new StrategyMenuCommand(StrategyMenuAction.Status, "Status", false),
                new StrategyMenuCommand(StrategyMenuAction.Abort, "Abort", false),
            },
            _ => CreatePlanetInformationCommands(false),
        };
    }

    /// <summary>
    /// Creates planet information commands.
    /// </summary>
    /// <param name="enabled">Whether planet information is available.</param>
    /// <returns>The planet information commands.</returns>
    private static List<StrategyMenuCommand> CreatePlanetInformationCommands(bool enabled)
    {
        return new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(StrategyMenuAction.Encyclopedia, "Encyclopedia", enabled),
            new StrategyMenuCommand(StrategyMenuAction.Status, "Status", enabled),
        };
    }

    /// <summary>
    /// Creates fleet commands for one planet-system fleet overlay.
    /// </summary>
    /// <param name="fleetItems">The player-controlled fleet items at the planet.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <param name="canBombard">Whether the fleets can bombard the planet.</param>
    /// <param name="canDestroySystem">Whether the fleets can destroy the planet.</param>
    /// <param name="canAssault">Whether the fleets can assault the planet.</param>
    /// <returns>The fleet commands.</returns>
    private static List<StrategyMenuCommand> CreateFleetCommands(
        List<ISceneNode> fleetItems,
        string playerFactionId,
        bool canBombard,
        bool canDestroySystem,
        bool canAssault
    )
    {
        bool canCommandFleets = StrategyContextMenuAvailability.CanMoveItems(
            fleetItems,
            playerFactionId
        );
        bool canShowSingleFleetInfo = fleetItems?.Count == 1;
        return new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(StrategyMenuAction.Move, "Move", canCommandFleets),
            new StrategyMenuCommand(
                StrategyMenuAction.MoveConfirm,
                "Confirmed Move",
                canCommandFleets
            ),
            StrategyBombardmentMenuBuilder.Build(
                canCommandFleets && canBombard,
                canCommandFleets && canDestroySystem
            ),
            new StrategyMenuCommand(
                StrategyMenuAction.PlanetaryAssault,
                "Planetary Assault",
                canCommandFleets && canAssault
            ),
            new StrategyMenuCommand(StrategyMenuAction.Encyclopedia, "Encyclopedia", false),
            new StrategyMenuCommand(StrategyMenuAction.Status, "Status", canShowSingleFleetInfo),
            new StrategyMenuCommand(StrategyMenuAction.Scrap, "Scrap", canCommandFleets),
        };
    }
}
