using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;

/// <summary>
/// Executes fleet mutations shared by fleet and planet-system UI features.
/// </summary>
public sealed class StrategyFleetCommandController
{
    private readonly Func<GameRoot> getGame;
    private readonly Func<FleetSystem> getFleetSystem;
    private readonly Func<Planet, IReadOnlyList<Fleet>, BombardmentType, bool> canBombard;
    private readonly Func<Planet, IReadOnlyList<Fleet>, bool> canAssault;
    private readonly Func<
        Planet,
        IReadOnlyList<Fleet>,
        BombardmentType,
        BombardmentResult
    > executeBombardment;
    private readonly Func<Planet, IReadOnlyList<Fleet>, PlanetaryAssaultResult> executeAssault;

    /// <summary>
    /// Creates a fleet command controller for the active game.
    /// </summary>
    /// <param name="getGame">Returns the active game.</param>
    /// <param name="getFleetSystem">Returns the active fleet system.</param>
    /// <param name="canBombard">Determines whether a bombardment command can execute.</param>
    /// <param name="executeBombardment">Executes and routes one bombardment command.</param>
    /// <param name="canAssault">Determines whether an assault command can execute.</param>
    /// <param name="executeAssault">Executes and routes one assault command.</param>
    public StrategyFleetCommandController(
        Func<GameRoot> getGame,
        Func<FleetSystem> getFleetSystem,
        Func<Planet, IReadOnlyList<Fleet>, BombardmentType, bool> canBombard,
        Func<Planet, IReadOnlyList<Fleet>, BombardmentType, BombardmentResult> executeBombardment,
        Func<Planet, IReadOnlyList<Fleet>, bool> canAssault,
        Func<Planet, IReadOnlyList<Fleet>, PlanetaryAssaultResult> executeAssault
    )
    {
        this.getGame = getGame ?? throw new ArgumentNullException(nameof(getGame));
        this.getFleetSystem =
            getFleetSystem ?? throw new ArgumentNullException(nameof(getFleetSystem));
        this.canBombard = canBombard ?? throw new ArgumentNullException(nameof(canBombard));
        this.executeBombardment =
            executeBombardment ?? throw new ArgumentNullException(nameof(executeBombardment));
        this.canAssault = canAssault ?? throw new ArgumentNullException(nameof(canAssault));
        this.executeAssault =
            executeAssault ?? throw new ArgumentNullException(nameof(executeAssault));
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
            && getFleetSystem().CreateFromCapitalShips(ships, playerFactionId) != null;
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
            return canBombard(liveTarget, fleets, type);

        return action == StrategyMenuAction.PlanetaryAssault && canAssault(liveTarget, fleets);
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
            return executeBombardment(liveTarget, fleets, type);

        return action == StrategyMenuAction.PlanetaryAssault
            ? executeAssault(liveTarget, fleets)
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
