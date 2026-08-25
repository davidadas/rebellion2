using System.Collections.Generic;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

/// <summary>
/// Builds context-menu commands for one planet-sector window hit.
/// </summary>
internal static class PlanetSectorWindowContextMenuBuilder
{
    /// <summary>
    /// Creates context commands for one planet-sector hit.
    /// </summary>
    /// <param name="hit">The active semantic planet hit.</param>
    /// <param name="fleetItems">The player-controlled fleet items at the hit planet.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <param name="mobileHeadquarters">The mobile headquarters selected at the hit planet.</param>
    /// <param name="canBombard">Whether the fleets can bombard the hit planet.</param>
    /// <param name="canDestroyPlanet">Whether the fleets can destroy the hit planet.</param>
    /// <param name="canAssault">Whether the fleets can assault the hit planet.</param>
    /// <returns>The available context commands in display order.</returns>
    public static List<StrategyMenuCommand> Create(
        PlanetSectorWindowHit hit,
        List<ISceneNode> fleetItems,
        string playerFactionId,
        Building mobileHeadquarters = null,
        bool canBombard = false,
        bool canDestroyPlanet = false,
        bool canAssault = false
    )
    {
        if (hit?.GalaxyMapPlanet == null)
            return CreatePlanetInformationCommands(false);
        if (mobileHeadquarters != null)
        {
            return new List<StrategyMenuCommand>
            {
                new StrategyMenuCommand(StrategyMenuAction.Move, "Move", true),
                new StrategyMenuCommand(StrategyMenuAction.MoveConfirm, "Confirmed Move", true),
                new StrategyMenuCommand(StrategyMenuAction.Encyclopedia, "Encyclopedia", true),
                new StrategyMenuCommand(StrategyMenuAction.Status, "Status", true),
            };
        }
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
                canDestroyPlanet,
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
    /// Creates fleet commands for one planet-sector fleet overlay.
    /// </summary>
    /// <param name="fleetItems">The player-controlled fleet items at the planet.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <param name="canBombard">Whether the fleets can bombard the planet.</param>
    /// <param name="canDestroyPlanet">Whether the fleets can destroy the planet.</param>
    /// <param name="canAssault">Whether the fleets can assault the planet.</param>
    /// <returns>The fleet commands.</returns>
    private static List<StrategyMenuCommand> CreateFleetCommands(
        List<ISceneNode> fleetItems,
        string playerFactionId,
        bool canBombard,
        bool canDestroyPlanet,
        bool canAssault
    )
    {
        bool canCommandFleets = StrategyContextMenuAvailability.CanMoveItems(
            fleetItems,
            playerFactionId
        );
        bool playerControlsFleets = StrategyContextMenuAvailability.PlayerControlsItems(
            fleetItems,
            playerFactionId
        );
        bool canShowSingleFleetInfo = fleetItems?.Count == 1;
        bool hasWaypoints =
            fleetItems?.Exists(fleet => fleet is Fleet routeFleet && routeFleet.Waypoints.Count > 0)
            == true;
        bool allFleetsAreMoving =
            fleetItems?.Count > 0
            && fleetItems.TrueForAll(fleet => fleet is Fleet { Movement: not null });
        return new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(StrategyMenuAction.Move, "Move", canCommandFleets),
            new StrategyMenuCommand(
                StrategyMenuAction.MoveConfirm,
                "Confirmed Move",
                canCommandFleets
            ),
            new StrategyMenuCommand(
                StrategyMenuAction.WaypointMove,
                hasWaypoints ? "Clear Waypoints" : "Waypoint Move",
                playerControlsFleets && (canCommandFleets || allFleetsAreMoving || hasWaypoints)
            ),
            StrategyBombardmentMenuBuilder.Build(
                canCommandFleets && canBombard,
                canCommandFleets && canDestroyPlanet
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
