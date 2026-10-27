using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.SceneGraph;
using Rebellion.Util.DependencyInjection;

/// <summary>
/// Executes fleet mutations shared by fleet and planet-sector UI features.
/// </summary>
public sealed class StrategyFleetCommandController
{
    private readonly IServiceLocator services;
    private readonly Func<string> getPlayerFactionId;

    /// <summary>
    /// Creates a fleet command controller for the active game.
    /// </summary>
    /// <param name="services">Resolves services for the active game.</param>
    /// <param name="getPlayerFactionId">Returns the player faction identifier.</param>
    public StrategyFleetCommandController(IServiceLocator services, Func<string> getPlayerFactionId)
    {
        this.services = services ?? throw new ArgumentNullException(nameof(services));
        this.getPlayerFactionId =
            getPlayerFactionId ?? throw new ArgumentNullException(nameof(getPlayerFactionId));
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
        FleetCommands fleetSystem = services.GetService<FleetCommands>();
        string playerFactionId = getPlayerFactionId();
        return fleetSystem != null
            && !string.IsNullOrEmpty(playerFactionId)
            && ships.Count > 0
            && ships.Count == sourceItems.Count
            && fleetSystem.CreateFleet(ships, playerFactionId) != null;
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
        if (!TryCreateCommand(items, targetPlanet, out List<Fleet> fleets))
            return false;

        if (action.TryGetBombardmentType(out BombardmentType type))
            return services.GetService<BombardmentCommands>() != null
                && services.GetService<BombardmentQueries>()?.CanExecute(fleets, targetPlanet, type)
                    == true;

        return action == StrategyMenuAction.PlanetaryAssault
            && services.GetService<PlanetaryAssaultQueries>()?.CanExecute(fleets, targetPlanet)
                == true;
    }

    /// <summary>
    /// Executes one planetary combat command for selected fleets at one planet.
    /// </summary>
    /// <param name="items">The selected fleets.</param>
    /// <param name="targetPlanet">The requested target planet snapshot.</param>
    /// <param name="action">The requested planetary combat action.</param>
    /// <returns>The faction-visible combat report, or null when the command cannot execute.</returns>
    public CombatReport ExecutePlanetaryCombat(
        IReadOnlyList<ISceneNode> items,
        Planet targetPlanet,
        StrategyMenuAction action
    )
    {
        if (!TryCreateCommand(items, targetPlanet, out List<Fleet> fleets))
            return null;

        string playerFactionId = getPlayerFactionId();
        if (string.IsNullOrEmpty(playerFactionId))
            return null;

        if (action.TryGetBombardmentType(out BombardmentType type))
            return services
                .GetService<BombardmentCommands>()
                ?.TryExecuteReport(fleets, targetPlanet, type, playerFactionId);

        return action == StrategyMenuAction.PlanetaryAssault
            ? services
                .GetService<PlanetaryAssaultCommands>()
                ?.TryExecuteReport(fleets, targetPlanet, playerFactionId)
            : null;
    }

    /// <summary>
    /// Creates a complete fleet selection for a combat command.
    /// </summary>
    /// <param name="items">The selected scene nodes.</param>
    /// <param name="targetPlanet">The requested target planet snapshot.</param>
    /// <param name="fleets">Receives the selected fleet snapshots.</param>
    /// <returns>True when every selected item is a uniquely identified fleet.</returns>
    private static bool TryCreateCommand(
        IReadOnlyList<ISceneNode> items,
        Planet targetPlanet,
        out List<Fleet> fleets
    )
    {
        fleets = new List<Fleet>();
        if (items?.Count < 1 || string.IsNullOrEmpty(targetPlanet?.InstanceID))
            return false;

        HashSet<string> fleetIds = new HashSet<string>();
        foreach (ISceneNode item in items)
        {
            if (
                item is not Fleet fleet
                || string.IsNullOrEmpty(fleet.InstanceID)
                || !fleetIds.Add(fleet.InstanceID)
            )
                return false;

            fleets.Add(fleet);
        }

        return true;
    }
}
