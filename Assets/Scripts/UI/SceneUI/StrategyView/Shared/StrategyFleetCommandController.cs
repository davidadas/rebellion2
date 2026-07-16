using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

/// <summary>
/// Executes fleet mutations shared by fleet and planet-system UI features.
/// </summary>
public sealed class StrategyFleetCommandController
{
    private readonly Func<GameRoot> getGame;
    private readonly Func<Planet, IReadOnlyList<Fleet>, BombardmentResult> executeBombardment;

    /// <summary>
    /// Creates a fleet command controller for the active game.
    /// </summary>
    /// <param name="getGame">Returns the active game.</param>
    /// <param name="executeBombardment">Executes and routes one bombardment command.</param>
    public StrategyFleetCommandController(
        Func<GameRoot> getGame,
        Func<Planet, IReadOnlyList<Fleet>, BombardmentResult> executeBombardment
    )
    {
        this.getGame = getGame ?? throw new ArgumentNullException(nameof(getGame));
        this.executeBombardment =
            executeBombardment ?? throw new ArgumentNullException(nameof(executeBombardment));
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
            && StrategyContextMenuAvailability.PlayerControlsItems(sourceItems, playerFactionId)
            && game.CreateFleetFromCapitalShips(ships) != null;
    }

    /// <summary>
    /// Executes orbital bombardment for selected fleets at one planet.
    /// </summary>
    /// <param name="items">The selected fleets.</param>
    /// <param name="targetPlanet">The requested target planet snapshot.</param>
    /// <returns>True when bombardment was executed.</returns>
    public bool TryExecutePlanetaryBombardment(IReadOnlyList<ISceneNode> items, Planet targetPlanet)
    {
        List<Fleet> fleets = items?.OfType<Fleet>().ToList() ?? new List<Fleet>();
        Planet liveTarget = ResolvePlanet(targetPlanet);
        if (fleets.Count == 0 || liveTarget == null)
            return false;

        return executeBombardment(liveTarget, fleets) != null;
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
}
