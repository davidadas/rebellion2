using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

public sealed class StrategyMissionTarget : ITargetable
{
    public StrategyMissionTarget(GalaxyMapPlanet planet, ISceneNode item)
    {
        Planet = planet;
        Item = item;
    }

    public GalaxyMapPlanet Planet { get; }
    public ISceneNode Item { get; }
    public object Target => this;

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

    public ISceneNode GetSpecificMissionTarget(MissionType missionType)
    {
        if (
            Item != null
            && (
                missionType == MissionType.Sabotage
                || missionType == MissionType.Abduction
                || missionType == MissionType.Assassination
                || missionType == MissionType.Rescue
            )
        )
            return Item;

        return null;
    }

    public Officer GetMissionTargetOfficer(MissionType missionType)
    {
        if (
            missionType == MissionType.Abduction
            || missionType == MissionType.Assassination
            || missionType == MissionType.Rescue
        )
            return Item as Officer;

        return null;
    }
}
