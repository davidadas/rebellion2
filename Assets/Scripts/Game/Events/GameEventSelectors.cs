using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
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
        public string InstanceID { get; set; }

        [PersistableAttribute]
        public string OwnerFactionInstanceID { get; set; }

        /// <summary>
        /// Returns active nodes that match the authored identity filters.
        /// </summary>
        protected IEnumerable<T> SelectOwned(GameRoot game)
        {
            return SelectOwned(Active<T>(game));
        }

        /// <summary>
        /// Filters a supplied node sequence by authored identity and ownership.
        /// </summary>
        protected IEnumerable<T> SelectOwned(IEnumerable<T> nodes)
        {
            return nodes
                .Where(node =>
                    string.IsNullOrWhiteSpace(InstanceID) || node.InstanceID == InstanceID
                )
                .Where(node =>
                    string.IsNullOrWhiteSpace(OwnerFactionInstanceID)
                    || node.OwnerInstanceID == OwnerFactionInstanceID
                );
        }
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

        /// <summary>
        /// Returns owned nodes located at the selected planet.
        /// </summary>
        protected IEnumerable<T> SelectLocated(GameRoot game, GameEventExecutionContext context) =>
            SelectOwned(game)
                .Where(node => MatchesLocation(node, context, PlanetInstanceID, PlanetBinding));
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

        /// <summary>
        /// Returns located units matching the authored manufacturing filters.
        /// </summary>
        protected IEnumerable<T> SelectManufacturable(
            GameRoot game,
            GameEventExecutionContext context
        ) =>
            SelectLocated(game, context)
                .Where(unit => string.IsNullOrWhiteSpace(TypeID) || unit.TypeID == TypeID)
                .Where(unit =>
                    !ManufacturingStatus.HasValue
                    || unit.ManufacturingStatus == ManufacturingStatus.Value
                );
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
        public PlanetSystemType? SystemType { get; set; }

        /// <summary>
        /// Returns active planets that match the authored ownership and system filters.
        /// </summary>
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) =>
            SelectOwned(game)
                .Where(planet => !planet.IsDestroyed)
                .Where(planet =>
                    !SystemType.HasValue
                    || planet.GetParentOfType<PlanetSystem>()?.SystemType == SystemType.Value
                );
    }

    /// <summary>
    /// Selects active planet systems.
    /// </summary>
    [PersistableObject]
    public sealed class SelectPlanetSystems : GameEventSelector
    {
        [PersistableAttribute]
        public string InstanceID { get; set; }

        [PersistableAttribute]
        public PlanetSystemType? SystemType { get; set; }

        /// <summary>
        /// Returns active planet systems that match the authored filters.
        /// </summary>
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) =>
            Active<PlanetSystem>(game)
                .Where(system =>
                    string.IsNullOrWhiteSpace(InstanceID) || system.InstanceID == InstanceID
                )
                .Where(system => !SystemType.HasValue || system.SystemType == SystemType.Value);
    }

    #endregion

    #region Unit selectors

    /// <summary>
    /// Selects active or retained officers.
    /// </summary>
    [PersistableObject]
    public sealed class SelectOfficers : LocatedSceneNodeSelector<Officer>
    {
        [PersistableAttribute]
        public bool IncludeRetained { get; set; }

        [PersistableAttribute]
        public bool? IsCaptured { get; set; }

        /// <summary>
        /// Returns officers that match the authored location and captivity filters.
        /// </summary>
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            IEnumerable<Officer> officers = IncludeRetained
                ? game.GetRegisteredSceneNodesByType<Officer>(includeDisabled: true)
                : Active<Officer>(game);
            return SelectOwned(officers)
                .Where(node =>
                    MatchesActiveOrRecordedLocation(
                        game,
                        node,
                        context,
                        PlanetInstanceID,
                        PlanetBinding
                    )
                )
                .Where(officer => !IsCaptured.HasValue || officer.IsCaptured == IsCaptured.Value);
        }
    }

    /// <summary>
    /// Selects active special-forces units.
    /// </summary>
    [PersistableObject]
    public sealed class SelectSpecialForces : LocatedSceneNodeSelector<SpecialForces>
    {
        /// <summary>
        /// Returns special-forces units that match the authored location filters.
        /// </summary>
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) => SelectLocated(game, context);
    }

    /// <summary>
    /// Selects active fleets.
    /// </summary>
    [PersistableObject]
    public sealed class SelectFleets : LocatedSceneNodeSelector<Fleet>
    {
        /// <summary>
        /// Returns fleets that match the authored location filters.
        /// </summary>
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) => SelectLocated(game, context);
    }

    /// <summary>
    /// Selects active missions.
    /// </summary>
    [PersistableObject]
    public sealed class SelectMissions : LocatedSceneNodeSelector<Mission>
    {
        /// <summary>
        /// Returns missions that match the authored location filters.
        /// </summary>
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) => SelectLocated(game, context);
    }

    /// <summary>
    /// Selects active capital ships.
    /// </summary>
    [PersistableObject]
    public sealed class SelectCapitalShips : ManufacturableSelector<CapitalShip>
    {
        /// <summary>
        /// Returns capital ships that match the authored unit filters.
        /// </summary>
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) => SelectManufacturable(game, context);
    }

    /// <summary>
    /// Selects active starfighter units.
    /// </summary>
    [PersistableObject]
    public sealed class SelectStarfighters : ManufacturableSelector<Starfighter>
    {
        /// <summary>
        /// Returns starfighters that match the authored unit filters.
        /// </summary>
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) => SelectManufacturable(game, context);
    }

    /// <summary>
    /// Selects active regiment units.
    /// </summary>
    [PersistableObject]
    public sealed class SelectRegiments : ManufacturableSelector<Regiment>
    {
        /// <summary>
        /// Returns regiments that match the authored unit filters.
        /// </summary>
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) => SelectManufacturable(game, context);
    }

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

        /// <summary>
        /// Returns buildings that match the authored unit and strategic-category filters.
        /// </summary>
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) => SelectManufacturable(game, context).Where(MatchesCategory);

        /// <summary>
        /// Returns whether a building belongs to the authored strategic category.
        /// </summary>
        private bool MatchesCategory(Building building) =>
            Category switch
            {
                BuildingSelectionCategory.Any => true,
                BuildingSelectionCategory.PlanetaryDefense => building.BuildingType
                    is BuildingType.Defense
                        or BuildingType.Weapon,
                BuildingSelectionCategory.ManufacturingFacility => building.BuildingType
                    is BuildingType.Shipyard
                        or BuildingType.TrainingFacility
                        or BuildingType.ConstructionFacility,
                _ => false,
            };
    }

    /// <summary>
    /// Selects manufacturing orders queued at matching planets.
    /// </summary>
    [PersistableObject]
    public sealed class SelectManufacturingOrders : GameEventSelector
    {
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        [PersistableAttribute]
        public string OwnerFactionInstanceID { get; set; }

        [PersistableAttribute]
        public ManufacturingType? ManufacturingType { get; set; }

        /// <summary>
        /// Returns manufacturing orders that match the authored planet, owner, and type filters.
        /// </summary>
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            Planet boundPlanet = !string.IsNullOrWhiteSpace(PlanetBinding)
                ? context?.GetBindingReference<Planet>(PlanetBinding)
                : null;
            string planetID = boundPlanet?.InstanceID ?? PlanetInstanceID;
            IEnumerable<Planet> planets = Active<Planet>(game)
                .Where(planet =>
                    string.IsNullOrWhiteSpace(planetID) || planet.InstanceID == planetID
                )
                .Where(planet =>
                    string.IsNullOrWhiteSpace(OwnerFactionInstanceID)
                    || planet.OwnerInstanceID == OwnerFactionInstanceID
                );
            return planets
                .SelectMany(planet => planet.ManufacturingQueue)
                .Where(entry => !ManufacturingType.HasValue || entry.Key == ManufacturingType.Value)
                .SelectMany(entry => entry.Value)
                .Cast<ISceneNode>();
        }
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

        /// <summary>
        /// Randomly samples the authored candidate selectors within the configured limits.
        /// </summary>
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            if (ChancePercent < 0 || ChancePercent > 100)
                throw new InvalidOperationException(
                    "SelectRandom ChancePercent must be between 0 and 100."
                );
            if (Count is < 0 || MinimumCount < 0 || MaximumCount is < 0)
                throw new InvalidOperationException("SelectRandom counts cannot be negative.");
            if (Count.HasValue && (MinimumCount != 0 || MaximumCount.HasValue))
                throw new InvalidOperationException(
                    "SelectRandom Count cannot be combined with MinimumCount or MaximumCount."
                );
            if (MaximumCount.HasValue && MaximumCount.Value < MinimumCount)
                throw new InvalidOperationException(
                    "SelectRandom MaximumCount cannot be less than MinimumCount."
                );

            List<ISceneNode> remaining = Selectors
                .SelectMany(selector => selector.Select(game, provider, context))
                .Distinct()
                .OrderBy(node => node.InstanceID, StringComparer.Ordinal)
                .ToList();
            List<ISceneNode> selected = new List<ISceneNode>();
            int minimum = Count ?? MinimumCount;
            int maximum = Count ?? MaximumCount ?? remaining.Count;
            foreach (ISceneNode candidate in remaining.ToList())
            {
                if (provider.NextInt(0, 100) >= ChancePercent)
                    continue;
                selected.Add(candidate);
                remaining.Remove(candidate);
            }
            while (selected.Count < Math.Min(minimum, selected.Count + remaining.Count))
            {
                int index = provider.NextInt(0, remaining.Count);
                selected.Add(remaining[index]);
                remaining.RemoveAt(index);
            }
            while (selected.Count > maximum)
                selected.RemoveAt(provider.NextInt(0, selected.Count));
            return selected;
        }
    }

    /// <summary>
    /// Selects the first node from an ordered candidate union.
    /// </summary>
    [PersistableObject]
    public sealed class SelectFirst : GameEventSelector
    {
        [PersistableMember(Name = "From")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Returns the first distinct node produced by the authored candidate selectors.
        /// </summary>
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) => SelectCandidates(game, provider, context).Take(1);

        /// <summary>
        /// Returns the distinct candidate sequence before taking its first node.
        /// </summary>
        internal IEnumerable<ISceneNode> SelectCandidates(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) => Selectors.SelectMany(selector => selector.Select(game, provider, context)).Distinct();
    }

    /// <summary>
    /// Selects canonical scene nodes held in an event binding.
    /// </summary>
    [PersistableObject]
    public sealed class SelectBinding : GameEventSelector
    {
        [PersistableAttribute]
        public string Binding { get; set; }

        /// <summary>
        /// Returns the scene node or nodes held by the authored event binding.
        /// </summary>
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            if (context?.TryGetBindingReference(Binding, out object value) != true)
                throw new InvalidOperationException(
                    $"SelectBinding could not resolve binding '{Binding}'."
                );

            IEnumerable<ISceneNode> nodes = value switch
            {
                ISceneNode node => new[] { node },
                IEnumerable<ISceneNode> collection => collection,
                _ => throw new InvalidOperationException(
                    $"SelectBinding '{Binding}' does not contain scene nodes."
                ),
            };
            List<ISceneNode> selected = new List<ISceneNode>();
            foreach (ISceneNode node in nodes)
            {
                ISceneNode canonical =
                    node == null
                        ? null
                        : game.GetSceneNodeByInstanceID<ISceneNode>(node.InstanceID);
                if (canonical == null)
                    throw new InvalidOperationException(
                        $"SelectBinding '{Binding}' contains an unregistered scene node."
                    );
                if (selected.All(existing => existing.InstanceID != canonical.InstanceID))
                    selected.Add(canonical);
            }
            return selected;
        }
    }

    /// <summary>
    /// Selects a requested ancestor for each candidate node.
    /// </summary>
    [PersistableObject]
    public sealed class SelectAncestors : GameEventSelector
    {
        [PersistableAttribute]
        public SceneAncestorType Type { get; set; }

        [PersistableMember(Name = "From")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Returns the requested ancestor of each authored candidate node.
        /// </summary>
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) =>
            Selectors
                .SelectMany(selector => selector.Select(game, provider, context))
                .Select(node => SceneAncestors.Resolve(node, Type))
                .Where(node => node != null)
                .Distinct();
    }

    /// <summary>
    /// Selects the remembered previous parent of one active or retained unit.
    /// </summary>
    [PersistableObject]
    public sealed class SelectPreviousLocation : GameEventSelector
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        [PersistableAttribute]
        public string UnitBinding { get; set; }

        /// <summary>
        /// Returns the remembered previous location of the authored unit.
        /// </summary>
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            bool hasInstanceID = !string.IsNullOrWhiteSpace(UnitInstanceID);
            bool hasBinding = !string.IsNullOrWhiteSpace(UnitBinding);
            if (hasInstanceID == hasBinding)
                throw new InvalidOperationException(
                    "SelectPreviousLocation requires exactly one of UnitInstanceID or UnitBinding."
                );
            ISceneNode unit = hasBinding
                ? context?.GetBindingReference<ISceneNode>(UnitBinding)
                : game.GetSceneNodeByInstanceID<ISceneNode>(UnitInstanceID);
            if (unit == null)
                return Enumerable.Empty<ISceneNode>();
            ISceneNode parent = game.GetSceneNodeByInstanceID<ISceneNode>(
                unit.LastParentInstanceID
            );
            return parent == null ? Enumerable.Empty<ISceneNode>() : new[] { parent };
        }
    }

    #endregion
}
