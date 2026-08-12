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
    [PersistableObject]
    public abstract class GameEventSelector
    {
        internal abstract IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        );

        protected static IEnumerable<T> Active<T>(GameRoot game)
            where T : class, ISceneNode
        {
            return game.GetRegisteredSceneNodesByType<T>()
                .Where(node => node.GetParent() != null && !game.IsInVoid(node));
        }

        protected static bool MatchesLocation(
            ISceneNode node,
            GameEventExecutionContext context,
            string planetInstanceID,
            string planetBinding
        )
        {
            Planet planet = !string.IsNullOrWhiteSpace(planetBinding)
                ? context?.GetBindingReference<Planet>(planetBinding)
                : null;
            string expected = planet?.InstanceID ?? planetInstanceID;
            return string.IsNullOrWhiteSpace(expected)
                || node is Planet selectedPlanet && selectedPlanet.InstanceID == expected
                || node.GetParentOfType<Planet>()?.InstanceID == expected;
        }
    }

    public abstract class OwnedSceneNodeSelector<T> : GameEventSelector
        where T : class, ISceneNode
    {
        [PersistableAttribute]
        public string InstanceID { get; set; }

        [PersistableAttribute]
        public string OwnerFactionInstanceID { get; set; }

        protected IEnumerable<T> SelectOwned(GameRoot game)
        {
            return Active<T>(game)
                .Where(node =>
                    string.IsNullOrWhiteSpace(InstanceID) || node.InstanceID == InstanceID
                )
                .Where(node =>
                    string.IsNullOrWhiteSpace(OwnerFactionInstanceID)
                    || node.OwnerInstanceID == OwnerFactionInstanceID
                );
        }
    }

    public abstract class LocatedSceneNodeSelector<T> : OwnedSceneNodeSelector<T>
        where T : class, ISceneNode
    {
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        protected IEnumerable<T> SelectLocated(GameRoot game, GameEventExecutionContext context) =>
            SelectOwned(game)
                .Where(node => MatchesLocation(node, context, PlanetInstanceID, PlanetBinding));
    }

    [PersistableObject(Name = "SelectPlanets")]
    public sealed class SelectPlanets : OwnedSceneNodeSelector<Planet>
    {
        [PersistableAttribute]
        public PlanetSystemType? SystemType { get; set; }

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

    [PersistableObject(Name = "SelectPlanetSystems")]
    public sealed class SelectPlanetSystems : GameEventSelector
    {
        [PersistableAttribute]
        public string InstanceID { get; set; }

        [PersistableAttribute]
        public PlanetSystemType? SystemType { get; set; }

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

    [PersistableObject(Name = "SelectOfficers")]
    public sealed class SelectOfficers : LocatedSceneNodeSelector<Officer>
    {
        [PersistableAttribute]
        public bool? IsCaptured { get; set; }

        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) =>
            SelectLocated(game, context)
                .Where(officer => !IsCaptured.HasValue || officer.IsCaptured == IsCaptured.Value);
    }

    [PersistableObject(Name = "SelectSpecialForces")]
    public sealed class SelectSpecialForces : LocatedSceneNodeSelector<SpecialForces>
    {
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) => SelectLocated(game, context);
    }

    [PersistableObject(Name = "SelectFleets")]
    public sealed class SelectFleets : LocatedSceneNodeSelector<Fleet>
    {
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) => SelectLocated(game, context);
    }

    [PersistableObject(Name = "SelectMissions")]
    public sealed class SelectMissions : LocatedSceneNodeSelector<Mission>
    {
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) => SelectLocated(game, context);
    }

    public abstract class ManufacturableSelector<T> : LocatedSceneNodeSelector<T>
        where T : class, ISceneNode, IManufacturable
    {
        [PersistableAttribute]
        public string TypeID { get; set; }

        [PersistableAttribute]
        public ManufacturingStatus? ManufacturingStatus { get; set; }

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

    [PersistableObject(Name = "SelectCapitalShips")]
    public sealed class SelectCapitalShips : ManufacturableSelector<CapitalShip>
    {
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) => SelectManufacturable(game, context);
    }

    [PersistableObject(Name = "SelectStarfighters")]
    public sealed class SelectStarfighters : ManufacturableSelector<Starfighter>
    {
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) => SelectManufacturable(game, context);
    }

    [PersistableObject(Name = "SelectRegiments")]
    public sealed class SelectRegiments : ManufacturableSelector<Regiment>
    {
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) => SelectManufacturable(game, context);
    }

    public enum BuildingSelectionCategory
    {
        Any,
        PlanetaryDefense,
        ManufacturingFacility,
    }

    [PersistableObject(Name = "SelectBuildings")]
    public sealed class SelectBuildings : ManufacturableSelector<Building>
    {
        [PersistableAttribute]
        public BuildingSelectionCategory Category { get; set; }

        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) => SelectManufacturable(game, context).Where(MatchesCategory);

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

    [PersistableObject(Name = "SelectManufacturingOrders")]
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

    [PersistableObject(Name = "SelectRandom")]
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

        [PersistableInlineCollection]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

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

    [PersistableObject(Name = "SelectBinding")]
    public sealed class SelectBinding : GameEventSelector
    {
        [PersistableAttribute]
        public string Binding { get; set; }

        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            if (context?.TryGetBindingReference(Binding, out object value) != true)
                return Enumerable.Empty<ISceneNode>();
            if (value is ISceneNode node)
                return new[] { node };
            return value is IEnumerable<ISceneNode> nodes ? nodes : Enumerable.Empty<ISceneNode>();
        }
    }
}
