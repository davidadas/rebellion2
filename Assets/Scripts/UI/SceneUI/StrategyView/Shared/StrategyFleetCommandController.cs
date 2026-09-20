using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Commands;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;

/// <summary>
/// Executes fleet mutations shared by fleet and planet-sector UI features.
/// </summary>
public sealed class StrategyFleetCommandController
{
    private readonly Func<GameRoot> getGame;
    private readonly GameSession gameSession;

    /// <summary>
    /// Creates a fleet command controller for the active game.
    /// </summary>
    /// <param name="getGame">Returns the active game state.</param>
    /// <param name="gameSession">The active simulation session.</param>
    public StrategyFleetCommandController(Func<GameRoot> getGame, GameSession gameSession)
    {
        this.getGame = getGame ?? throw new ArgumentNullException(nameof(getGame));
        this.gameSession = gameSession ?? throw new ArgumentNullException(nameof(gameSession));
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
        GameRoot game = getGame();
        string playerFactionId = game?.GetPlayerFaction()?.InstanceID;
        return game != null
            && ships.Count > 0
            && ships.Count == sourceItems.Count
            && gameSession
                .Execute(
                    new CreateFleetCommand { Ships = ships, OwnerInstanceID = playerFactionId }
                )
                .Accepted;
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
        if (!TryResolveCommand(items, targetPlanet, out List<Fleet> fleets, out Planet liveTarget))
            return false;

        if (action.TryGetBombardmentType(out BombardmentType type))
            return gameSession.Queries.CanBombard(fleets, liveTarget, type);

        return action == StrategyMenuAction.PlanetaryAssault
            && gameSession.Queries.CanAssault(fleets, liveTarget);
    }

    /// <summary>
    /// Executes one planetary combat command for selected fleets at one planet.
    /// </summary>
    /// <param name="items">The selected fleets.</param>
    /// <param name="targetPlanet">The requested target planet snapshot.</param>
    /// <param name="action">The requested planetary combat action.</param>
    /// <returns>The completed combat result, or null when the command cannot execute.</returns>
    public GameResult ExecutePlanetaryCombat(
        IReadOnlyList<ISceneNode> items,
        Planet targetPlanet,
        StrategyMenuAction action
    )
    {
        if (!TryResolveCommand(items, targetPlanet, out List<Fleet> fleets, out Planet liveTarget))
            return null;

        if (action.TryGetBombardmentType(out BombardmentType type))
        {
            GameCommandResult outcome = gameSession.Execute(
                new BombardCommand
                {
                    Fleets = fleets,
                    Planet = liveTarget,
                    Type = type,
                }
            );
            return outcome.Results.OfType<BombardmentResult>().FirstOrDefault();
        }

        if (action != StrategyMenuAction.PlanetaryAssault)
            return null;
        GameCommandResult assault = gameSession.Execute(
            new AssaultPlanetCommand { Fleets = fleets, Planet = liveTarget }
        );
        return assault.Results.OfType<PlanetaryAssaultResult>().FirstOrDefault();
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
            : getGame()?.GetSceneNodeByInstanceID<Planet>(planet.InstanceID);
    }

    /// <summary>
    /// Resolves a complete fleet selection and live target planet for a combat command.
    /// </summary>
    /// <param name="items">The selected scene nodes.</param>
    /// <param name="targetPlanet">The requested target planet snapshot.</param>
    /// <param name="fleets">Receives the live selected fleets.</param>
    /// <param name="liveTarget">Receives the live target planet.</param>
    /// <returns>True when every selected item is a fleet and the target is live.</returns>
    private bool TryResolveCommand(
        IReadOnlyList<ISceneNode> items,
        Planet targetPlanet,
        out List<Fleet> fleets,
        out Planet liveTarget
    )
    {
        fleets = new List<Fleet>();
        liveTarget = null;
        GameRoot game = getGame();
        if (game == null || items?.Count < 1 || string.IsNullOrEmpty(targetPlanet?.InstanceID))
            return false;

        liveTarget = game.GetSceneNodeByInstanceID<Planet>(targetPlanet.InstanceID);
        if (liveTarget == null)
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

            Fleet liveFleet = game.GetSceneNodeByInstanceID<Fleet>(fleet.InstanceID);
            if (liveFleet == null)
                return false;

            fleets.Add(liveFleet);
        }

        return true;
    }
}
