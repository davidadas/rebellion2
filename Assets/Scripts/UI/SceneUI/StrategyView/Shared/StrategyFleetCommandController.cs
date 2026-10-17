using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;
using Rebellion.Util.DependencyInjection;

/// <summary>
/// Executes fleet mutations shared by fleet and planet-sector UI features.
/// </summary>
public sealed class StrategyFleetCommandController
{
    private readonly IServiceLocator services;

    /// <summary>
    /// Creates a fleet command controller for the active game.
    /// </summary>
    /// <param name="services">Resolves services for the active game.</param>
    public StrategyFleetCommandController(IServiceLocator services)
    {
        this.services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Creates a fleet from a controlled capital-ship selection.
    /// </summary>
    /// <param name="items">The selected capital ships.</param>
    /// <returns>True when a fleet was created.</returns>
    public bool TryCreateFleetFromCapitalShips(IReadOnlyList<ISceneNode> items)
    {
        List<ISceneNode> sourceItems =
            items?.Where(item => item != null).ToList() ?? new List<ISceneNode>();
        List<CapitalShip> ships = sourceItems.OfType<CapitalShip>().ToList();
        GameRoot game = services.GetService<GameRoot>();
        FleetCommands fleetSystem = services.GetService<FleetCommands>();
        string playerFactionId = game?.GetPlayerFaction()?.InstanceID;
        return game != null
            && fleetSystem != null
            && ships.Count > 0
            && ships.Count == sourceItems.Count
            && fleetSystem.CreateFromCapitalShips(ships, playerFactionId) != null;
    }

    /// <summary>
    /// Determines whether selected fleets can execute one planetary combat command.
    /// </summary>
    /// <param name="items">The selected fleets.</param>
    /// <param name="targetPlanet">The requested target planet snapshot.</param>
    /// <param name="action">The requested planetary combat action.</param>
    /// <returns>True when the planetary combat command can execute.</returns>
    public bool CanExecutePlanetaryCombat(
        IReadOnlyList<ISceneNode> items,
        Planet targetPlanet,
        StrategyMenuAction action
    )
    {
        if (!TryGetCommandIds(items, targetPlanet, out List<string> fleetIds, out string planetId))
            return false;

        PlayerPlanetaryCombatCommands commands =
            services.GetService<PlayerPlanetaryCombatCommands>();
        if (action.TryGetBombardmentType(out BombardmentType type))
            return commands?.CanBombard(fleetIds, planetId, type) == true;

        return action == StrategyMenuAction.PlanetaryAssault
            && commands?.CanAssault(fleetIds, planetId) == true;
    }

    /// <summary>
    /// Executes one planetary combat command for selected fleets at one planet.
    /// </summary>
    /// <param name="items">The selected fleets.</param>
    /// <param name="targetPlanet">The requested target planet snapshot.</param>
    /// <param name="action">The requested planetary combat action.</param>
    /// <returns>The completed combat result, or null when the command cannot execute.</returns>
    public CombatReport ExecutePlanetaryCombat(
        IReadOnlyList<ISceneNode> items,
        Planet targetPlanet,
        StrategyMenuAction action
    )
    {
        if (!TryGetCommandIds(items, targetPlanet, out List<string> fleetIds, out string planetId))
            return null;

        PlayerPlanetaryCombatCommands commands =
            services.GetService<PlayerPlanetaryCombatCommands>();
        if (action.TryGetBombardmentType(out BombardmentType type))
            return commands?.TryBombard(fleetIds, planetId, type);

        return action == StrategyMenuAction.PlanetaryAssault
            ? commands?.TryAssault(fleetIds, planetId)
            : null;
    }

    /// <summary>
    /// Resolves a visible planet snapshot against the active game graph.
    /// </summary>
    /// <param name="planet">The visible planet snapshot.</param>
    /// <returns>The live planet, or null.</returns>
    public Planet ResolvePlanet(Planet planet)
    {
        return string.IsNullOrEmpty(planet?.InstanceID)
            ? null
            : services.GetService<GameRoot>()?.GetSceneNodeByInstanceID<Planet>(planet.InstanceID);
    }

    /// <summary>
    /// Copies a complete visible fleet selection and target into stable command identifiers.
    /// </summary>
    /// <param name="items">The selected scene nodes.</param>
    /// <param name="targetPlanet">The requested target planet snapshot.</param>
    /// <param name="fleetIds">Receives the selected fleet identifiers.</param>
    /// <param name="planetId">Receives the target planet identifier.</param>
    /// <returns>True when every selected item has a unique fleet identifier.</returns>
    private static bool TryGetCommandIds(
        IReadOnlyList<ISceneNode> items,
        Planet targetPlanet,
        out List<string> fleetIds,
        out string planetId
    )
    {
        fleetIds = new List<string>();
        planetId = targetPlanet?.InstanceID;
        if (items?.Count < 1 || string.IsNullOrEmpty(planetId))
            return false;

        HashSet<string> uniqueFleetIds = new HashSet<string>();
        foreach (ISceneNode item in items)
        {
            if (
                item is not Fleet fleet
                || string.IsNullOrEmpty(fleet.InstanceID)
                || !uniqueFleetIds.Add(fleet.InstanceID)
            )
                return false;

            fleetIds.Add(fleet.InstanceID);
        }

        return true;
    }
}
