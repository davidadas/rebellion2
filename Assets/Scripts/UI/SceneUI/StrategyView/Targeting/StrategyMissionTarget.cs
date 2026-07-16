using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

/// <summary>
/// Identifies a strategy planet and optional scene node selected by targeting.
/// </summary>
public sealed class StrategyMissionTarget : ITargetable
{
    /// <summary>
    /// Creates a strategy target for one planet and optional contained item.
    /// </summary>
    /// <param name="planet">The selected galaxy-map planet.</param>
    /// <param name="item">The optional selected scene node.</param>
    public StrategyMissionTarget(GalaxyMapPlanet planet, ISceneNode item)
    {
        Planet = planet;
        Item = item;
    }

    /// <summary>
    /// Gets the selected galaxy-map planet.
    /// </summary>
    public GalaxyMapPlanet Planet { get; }

    /// <summary>
    /// Gets the optional selected scene node.
    /// </summary>
    public ISceneNode Item { get; }

    /// <summary>
    /// Gets this semantic target for the targeting contract.
    /// </summary>
    public object Target => this;

    /// <summary>
    /// Resolves the concrete fleet, ship, or planet that can receive movement.
    /// </summary>
    /// <returns>The movement destination, or null when the planet is unavailable.</returns>
    public ISceneNode GetMoveDestination()
    {
        if (Planet?.Planet == null)
            return null;

        if (Item == null)
            return Planet.Planet;

        if (Item is Fleet or CapitalShip)
            return Item;

        ISceneNode parent = Item.GetParent();
        if (parent is Fleet or CapitalShip)
            return parent;

        return Planet.Planet;
    }

    /// <summary>
    /// Resolves the explicit scene-node target used by one mission type.
    /// </summary>
    /// <param name="missionTypeID">The requested mission type identifier.</param>
    /// <returns>The specific mission target, or null for location-only missions.</returns>
    public ISceneNode GetSpecificMissionTarget(string missionTypeID)
    {
        if (
            Item != null
            && (
                missionTypeID == MissionTypeIDs.Sabotage
                || missionTypeID == MissionTypeIDs.Abduction
                || missionTypeID == MissionTypeIDs.Assassination
                || missionTypeID == MissionTypeIDs.Rescue
            )
        )
            return Item;

        return null;
    }

    /// <summary>
    /// Resolves the officer target used by one officer-directed mission type.
    /// </summary>
    /// <param name="missionTypeID">The requested mission type identifier.</param>
    /// <returns>The selected officer, or null when the mission is not officer-directed.</returns>
    public Officer GetMissionTargetOfficer(string missionTypeID)
    {
        if (
            missionTypeID == MissionTypeIDs.Abduction
            || missionTypeID == MissionTypeIDs.Assassination
            || missionTypeID == MissionTypeIDs.Rescue
        )
            return Item as Officer;

        return null;
    }
}
