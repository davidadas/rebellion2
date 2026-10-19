using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

/// <summary>
/// Represents one fleet route resolved for strategy-map presentation.
/// </summary>
internal sealed class FleetWaypointRoute
{
    internal Fleet Fleet { get; }

    internal Planet OriginPlanet { get; }

    internal Point OriginPosition { get; }

    internal IReadOnlyList<FleetWaypointRouteStop> Stops { get; }

    /// <summary>
    /// Creates a resolved fleet route.
    /// </summary>
    /// <param name="fleet">The fleet represented by the route.</param>
    /// <param name="originPlanet">The planet from which the displayed route begins.</param>
    /// <param name="originPosition">The galaxy position from which the route begins.</param>
    /// <param name="stops">The resolved route stops.</param>
    internal FleetWaypointRoute(
        Fleet fleet,
        Planet originPlanet,
        Point originPosition,
        IReadOnlyList<FleetWaypointRouteStop> stops
    )
    {
        Fleet = fleet;
        OriginPlanet = originPlanet;
        OriginPosition = originPosition;
        Stops = stops;
    }
}

/// <summary>
/// Represents one resolved planet in a fleet waypoint route.
/// </summary>
internal readonly struct FleetWaypointRouteStop
{
    internal int Order { get; }

    internal Planet Planet { get; }

    /// <summary>
    /// Creates a resolved waypoint stop.
    /// </summary>
    /// <param name="order">The one-based position in the route.</param>
    /// <param name="planet">The destination planet.</param>
    internal FleetWaypointRouteStop(int order, Planet planet)
    {
        Order = order;
        Planet = planet;
    }
}

/// <summary>
/// Resolves the committed and preview fleet routes visible to the strategy-map presentation.
/// </summary>
internal static class FleetWaypointRouteResolver
{
    /// <summary>
    /// Resolves the routes visible through fleet selection, display mode, or an active plan.
    /// </summary>
    /// <param name="sectors">The faction-filtered strategy sectors.</param>
    /// <param name="playerFactionId">The viewing player's faction identifier.</param>
    /// <param name="waypointPlan">The active uncommitted waypoint plan, or null.</param>
    /// <param name="selectedFleetInstanceIds">The fleet routes visible through selection.</param>
    /// <param name="showAllRoutes">Whether every player waypoint route is visible.</param>
    /// <returns>The resolved routes in their display order.</returns>
    internal static List<FleetWaypointRoute> Resolve(
        IReadOnlyList<GalaxyMapSector> sectors,
        string playerFactionId,
        StrategyWindowTargetingSource waypointPlan = null,
        IReadOnlyCollection<string> selectedFleetInstanceIds = null,
        bool showAllRoutes = false
    )
    {
        List<FleetWaypointRoute> routes = new List<FleetWaypointRoute>();
        if (sectors == null || string.IsNullOrEmpty(playerFactionId))
            return routes;

        IEnumerable<Fleet> fleets = ResolveVisibleFleets(
            sectors,
            playerFactionId,
            selectedFleetInstanceIds,
            showAllRoutes
        );
        HashSet<string> resolvedFleetIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (Fleet fleet in fleets)
        {
            if (!resolvedFleetIds.Add(fleet.InstanceID))
                continue;

            AddRoute(routes, sectors, fleet, fleet.Waypoints);
        }

        AddPreviewRoutes(routes, resolvedFleetIds, sectors, playerFactionId, waypointPlan);
        return routes;
    }

    /// <summary>
    /// Resolves player fleets whose committed routes should be displayed.
    /// </summary>
    /// <param name="sectors">The faction-filtered strategy sectors.</param>
    /// <param name="playerFactionId">The viewing player's faction identifier.</param>
    /// <param name="selectedFleetInstanceIds">The fleet routes visible through selection.</param>
    /// <param name="showAllRoutes">Whether every player waypoint route is visible.</param>
    /// <returns>The visible fleets.</returns>
    private static IEnumerable<Fleet> ResolveVisibleFleets(
        IReadOnlyList<GalaxyMapSector> sectors,
        string playerFactionId,
        IReadOnlyCollection<string> selectedFleetInstanceIds,
        bool showAllRoutes
    )
    {
        if (showAllRoutes)
        {
            return sectors
                .SelectMany(sector => sector.Planets)
                .SelectMany(planet => planet.Planet.GetChildren<Fleet>())
                .Where(fleet =>
                    string.Equals(fleet.OwnerInstanceID, playerFactionId, StringComparison.Ordinal)
                    && fleet.HasWaypoints()
                );
        }

        return (selectedFleetInstanceIds ?? Array.Empty<string>())
            .Select(fleetId => FindVisibleNode<Fleet>(sectors, fleetId))
            .Where(fleet =>
                fleet != null
                && string.Equals(fleet.OwnerInstanceID, playerFactionId, StringComparison.Ordinal)
                && fleet.HasWaypoints()
            );
    }

    /// <summary>
    /// Adds routes represented by an active, uncommitted waypoint plan.
    /// </summary>
    /// <param name="routes">The resolved route collection.</param>
    /// <param name="resolvedFleetIds">The fleet identifiers already represented.</param>
    /// <param name="sectors">The faction-filtered strategy sectors.</param>
    /// <param name="playerFactionId">The viewing player's faction identifier.</param>
    /// <param name="waypointPlan">The active uncommitted waypoint plan, or null.</param>
    private static void AddPreviewRoutes(
        ICollection<FleetWaypointRoute> routes,
        ISet<string> resolvedFleetIds,
        IReadOnlyList<GalaxyMapSector> sectors,
        string playerFactionId,
        StrategyWindowTargetingSource waypointPlan
    )
    {
        if (
            waypointPlan?.Action != StrategyMenuAction.WaypointMove
            || waypointPlan.WaypointPlanetIds.Count == 0
        )
            return;

        IEnumerable<Fleet> selectedFleets = waypointPlan.Items.OfType<Fleet>();
        if (!selectedFleets.Any())
        {
            selectedFleets = waypointPlan
                .Items.OfType<CapitalShip>()
                .Select(ship => FindVisibleNode<CapitalShip>(sectors, ship.InstanceID))
                .Where(ship => ship != null)
                .Select(ship => ship.GetParentOfType<Fleet>());
        }

        foreach (Fleet selectedFleet in selectedFleets)
        {
            Fleet fleet = string.IsNullOrEmpty(selectedFleet?.InstanceID)
                ? null
                : FindVisibleNode<Fleet>(sectors, selectedFleet.InstanceID);
            if (
                fleet == null
                || !string.Equals(fleet.OwnerInstanceID, playerFactionId, StringComparison.Ordinal)
                || !resolvedFleetIds.Add(fleet.InstanceID)
            )
                continue;

            List<string> previewWaypointIds = new List<string>();
            if (fleet.Movement != null)
            {
                string activeDestinationId = fleet.GetParentOfType<Planet>()?.InstanceID;
                if (!string.IsNullOrEmpty(activeDestinationId))
                    previewWaypointIds.Add(activeDestinationId);
            }

            previewWaypointIds.AddRange(waypointPlan.WaypointPlanetIds);
            AddRoute(routes, sectors, fleet, previewWaypointIds);
        }
    }

    /// <summary>
    /// Resolves and adds one route from its ordered planet identifiers.
    /// </summary>
    /// <param name="routes">The resolved route collection.</param>
    /// <param name="sectors">The faction-filtered strategy sectors.</param>
    /// <param name="fleet">The fleet represented by the route.</param>
    /// <param name="waypointPlanetIds">The ordered waypoint planet identifiers.</param>
    private static void AddRoute(
        ICollection<FleetWaypointRoute> routes,
        IReadOnlyList<GalaxyMapSector> sectors,
        Fleet fleet,
        IReadOnlyList<string> waypointPlanetIds
    )
    {
        if (routes == null || sectors == null || fleet == null || waypointPlanetIds == null)
            return;

        List<FleetWaypointRouteStop> stops = new List<FleetWaypointRouteStop>();
        for (int index = 0; index < waypointPlanetIds.Count; index++)
        {
            Planet waypoint = FindVisiblePlanet(sectors, waypointPlanetIds[index]);
            if (waypoint != null)
                stops.Add(new FleetWaypointRouteStop(index + 1, waypoint));
        }

        if (stops.Count == 0)
            return;

        Planet originPlanet = ResolveOriginPlanet(sectors, fleet);
        Point originPosition =
            fleet.Movement?.OriginPosition
            ?? originPlanet?.GetPosition()
            ?? ((IMovable)fleet).GetPosition();
        routes.Add(new FleetWaypointRoute(fleet, originPlanet, originPosition, stops));
    }

    /// <summary>
    /// Resolves the planet from which a fleet's displayed route begins.
    /// </summary>
    /// <param name="sectors">The faction-filtered strategy sectors.</param>
    /// <param name="fleet">The fleet represented by the route.</param>
    /// <returns>The route's origin planet, or null when it cannot be resolved.</returns>
    private static Planet ResolveOriginPlanet(IReadOnlyList<GalaxyMapSector> sectors, Fleet fleet)
    {
        if (fleet?.Movement == null)
        {
            return FindVisiblePlanet(sectors, fleet?.GetParentOfType<Planet>()?.InstanceID);
        }

        Point originPosition = fleet.Movement.OriginPosition;
        return sectors
            .SelectMany(sector => sector.Planets)
            .Select(planet => planet.Planet)
            .FirstOrDefault(planet => planet.GetPosition() == originPosition);
    }

    /// <summary>
    /// Finds a visible planet by stable identifier.
    /// </summary>
    /// <param name="sectors">The faction-filtered strategy sectors.</param>
    /// <param name="instanceId">The planet identifier.</param>
    /// <returns>The visible planet, or null.</returns>
    private static Planet FindVisiblePlanet(
        IReadOnlyList<GalaxyMapSector> sectors,
        string instanceId
    )
    {
        return string.IsNullOrEmpty(instanceId)
            ? null
            : sectors
                .SelectMany(sector => sector.Planets)
                .Select(planet => planet.Planet)
                .FirstOrDefault(planet => planet.InstanceID == instanceId);
    }

    /// <summary>
    /// Finds a visible scene node by stable identifier.
    /// </summary>
    /// <typeparam name="T">The requested scene-node type.</typeparam>
    /// <param name="sectors">The faction-filtered strategy sectors.</param>
    /// <param name="instanceId">The node identifier.</param>
    /// <returns>The visible node, or null.</returns>
    private static T FindVisibleNode<T>(IReadOnlyList<GalaxyMapSector> sectors, string instanceId)
        where T : class, Rebellion.SceneGraph.ISceneNode
    {
        if (string.IsNullOrEmpty(instanceId))
            return null;

        foreach (
            Planet planet in sectors
                .SelectMany(sector => sector.Planets)
                .Select(item => item.Planet)
        )
        {
            if (planet is T matchingPlanet && matchingPlanet.InstanceID == instanceId)
                return matchingPlanet;

            T match = planet
                .GetChildren<T>(recursive: true, includeDisabled: true)
                .FirstOrDefault(node => node.InstanceID == instanceId);
            if (match != null)
                return match;
        }

        return null;
    }
}
