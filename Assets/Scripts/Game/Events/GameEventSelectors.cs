using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    #region Base selectors

    /// <summary>
    /// Filters scene nodes by stable instance identity and faction ownership.
    /// </summary>
    public abstract class OwnedSceneNodeSelector<T> : GameEventSelector
        where T : class, ISceneNode
    {
        [PersistableAttribute]
        public bool IncludeInactive { get; set; }

        [PersistableAttribute]
        public string InstanceID { get; set; }

        [PersistableAttribute]
        public string OwnerFactionInstanceID { get; set; }
    }

    /// <summary>
    /// Additionally filters owned nodes by an explicit or bound planet.
    /// </summary>
    public abstract class LocatedSceneNodeSelector<T> : OwnedSceneNodeSelector<T>
        where T : class, ISceneNode
    {
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }
    }

    /// <summary>
    /// Additionally filters manufacturable nodes by type and production state.
    /// </summary>
    public abstract class ManufacturableSelector<T> : LocatedSceneNodeSelector<T>
        where T : class, ISceneNode, IManufacturable
    {
        [PersistableAttribute]
        public string TypeID { get; set; }

        [PersistableAttribute]
        public ManufacturingStatus? ManufacturingStatus { get; set; }
    }

    #endregion

    #region Galaxy selectors

    /// <summary>
    /// Selects active, non-destroyed planets.
    /// </summary>
    [PersistableObject]
    public sealed class SelectPlanets : OwnedSceneNodeSelector<Planet>
    {
        [PersistableAttribute]
        public PlanetSectorType? SectorType { get; set; }
    }

    /// <summary>
    /// Selects active planet sectors.
    /// </summary>
    [PersistableObject]
    public sealed class SelectPlanetSectors : GameEventSelector
    {
        [PersistableAttribute]
        public bool IncludeInactive { get; set; }

        [PersistableAttribute]
        public string InstanceID { get; set; }

        [PersistableAttribute]
        public PlanetSectorType? SectorType { get; set; }
    }

    #endregion

    #region Unit selectors

    /// <summary>
    /// Selects officers, optionally including inactive nodes.
    /// </summary>
    [PersistableObject]
    public sealed class SelectOfficers : LocatedSceneNodeSelector<Officer>
    {
        [PersistableAttribute]
        public bool? IsCaptured { get; set; }
    }

    /// <summary>
    /// Selects active special-forces units.
    /// </summary>
    [PersistableObject]
    public sealed class SelectSpecialForces : LocatedSceneNodeSelector<SpecialForces> { }

    /// <summary>
    /// Selects active fleets.
    /// </summary>
    [PersistableObject]
    public sealed class SelectFleets : LocatedSceneNodeSelector<Fleet> { }

    /// <summary>
    /// Selects active missions.
    /// </summary>
    [PersistableObject]
    public sealed class SelectMissions : LocatedSceneNodeSelector<Mission> { }

    /// <summary>
    /// Selects active capital ships.
    /// </summary>
    [PersistableObject]
    public sealed class SelectCapitalShips : ManufacturableSelector<CapitalShip> { }

    /// <summary>
    /// Selects active starfighter units.
    /// </summary>
    [PersistableObject]
    public sealed class SelectStarfighters : ManufacturableSelector<Starfighter> { }

    /// <summary>
    /// Selects active regiment units.
    /// </summary>
    [PersistableObject]
    public sealed class SelectRegiments : ManufacturableSelector<Regiment> { }

    /// <summary>
    /// Groups buildings by their strategic purpose for authored selection.
    /// </summary>
    public enum BuildingSelectionCategory
    {
        Any,
        PlanetaryDefense,
        ManufacturingFacility,
    }

    /// <summary>
    /// Selects active buildings.
    /// </summary>
    [PersistableObject]
    public sealed class SelectBuildings : ManufacturableSelector<Building>
    {
        [PersistableAttribute]
        public BuildingSelectionCategory Category { get; set; }
    }

    /// <summary>
    /// Selects manufacturing orders queued at matching planets.
    /// </summary>
    [PersistableObject]
    public sealed class SelectManufacturingOrders : GameEventSelector
    {
        [PersistableAttribute]
        public bool IncludeInactive { get; set; }

        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        [PersistableAttribute]
        public string OwnerFactionInstanceID { get; set; }

        [PersistableAttribute]
        public ManufacturingType? ManufacturingType { get; set; }
    }

    #endregion

    #region Composite selectors

    /// <summary>
    /// Randomly samples the union of its candidate selectors.
    /// </summary>
    [PersistableObject]
    public sealed class SelectRandom : GameEventSelector
    {
        [PersistableAttribute]
        public int ChancePercent { get; set; } = 100;

        [PersistableAttribute]
        public int? Count { get; set; }

        [PersistableAttribute]
        public int MinimumCount { get; set; }

        [PersistableAttribute]
        public int? MaximumCount { get; set; }

        [PersistableMember(Name = "From")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();
    }

    /// <summary>
    /// Selects the first node from an ordered candidate union.
    /// </summary>
    [PersistableObject]
    public sealed class SelectFirst : GameEventSelector
    {
        [PersistableMember(Name = "From")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();
    }

    /// <summary>
    /// Selects canonical scene nodes held in an event binding.
    /// </summary>
    [PersistableObject]
    public sealed class SelectBinding : GameEventSelector
    {
        [PersistableAttribute]
        public string Binding { get; set; }
    }

    /// <summary>
    /// Selects the nearest parent of a requested type for each candidate node.
    /// </summary>
    [PersistableObject]
    public sealed class SelectNearestParent : GameEventSelector
    {
        [PersistableAttribute]
        public SceneAncestorType Type { get; set; }

        [PersistableMember(Name = "From")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();
    }

    /// <summary>
    /// Selects the remembered previous parent of one unit.
    /// </summary>
    [PersistableObject]
    public sealed class SelectPreviousLocation : GameEventSelector
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        [PersistableAttribute]
        public string UnitBinding { get; set; }
    }

    #endregion
}
