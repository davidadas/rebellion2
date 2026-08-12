using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    public enum UnitCategory
    {
        Any,
        PlanetaryDefense,
        ManufacturingFacility,
        Regiment,
        Building,
        Officer,
        Fleet,
        CapitalShip,
        Starfighter,
        SpecialForces,
    }

    [PersistableObject]
    public abstract class GameEventSelector
    {
        internal abstract IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        );
    }

    [PersistableObject(Name = "SelectUnits")]
    public sealed class SelectUnits : GameEventSelector
    {
        [PersistableAttribute]
        public string InstanceID { get; set; }

        [PersistableAttribute]
        public string TypeID { get; set; }

        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        [PersistableAttribute]
        public string OwnerFactionInstanceID { get; set; }

        [PersistableAttribute]
        public UnitCategory UnitCategory { get; set; }

        [PersistableAttribute]
        public bool? IsCaptured { get; set; }

        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            string planetInstanceID = PlanetInstanceID;
            if (
                !string.IsNullOrWhiteSpace(PlanetBinding)
                && context?.GetBindingReference<Planet>(PlanetBinding) is Planet boundPlanet
            )
                planetInstanceID = boundPlanet.InstanceID;

            return game.GetRegisteredSceneNodesByType<ISceneNode>()
                .Where(IsUnit)
                .Where(node => node.GetParent() != null)
                .Where(node => !game.UnitLifecycle.IsInVoid(node))
                .Where(node =>
                    string.IsNullOrWhiteSpace(InstanceID) || node.InstanceID == InstanceID
                )
                .Where(node => string.IsNullOrWhiteSpace(TypeID) || node.TypeID == TypeID)
                .Where(node =>
                    string.IsNullOrWhiteSpace(planetInstanceID)
                    || node.GetParentOfType<Planet>()?.InstanceID == planetInstanceID
                )
                .Where(node =>
                    string.IsNullOrWhiteSpace(OwnerFactionInstanceID)
                    || node.OwnerInstanceID == OwnerFactionInstanceID
                )
                .Where(node =>
                    !IsCaptured.HasValue
                    || node is Officer officer && officer.IsCaptured == IsCaptured.Value
                )
                .Where(MatchesCategory);
        }

        private static bool IsUnit(ISceneNode node) =>
            node
                is Building
                    or Regiment
                    or Officer
                    or Fleet
                    or CapitalShip
                    or Starfighter
                    or SpecialForces;

        private bool MatchesCategory(ISceneNode node) =>
            UnitCategory switch
            {
                UnitCategory.Any => true,
                UnitCategory.PlanetaryDefense => node
                    is Building
                    {
                        BuildingType: BuildingType.Defense or BuildingType.Weapon,
                        ManufacturingStatus: ManufacturingStatus.Complete
                    },
                UnitCategory.ManufacturingFacility => node
                    is Building
                    {
                        BuildingType: BuildingType.Shipyard
                            or BuildingType.TrainingFacility
                            or BuildingType.ConstructionFacility,
                        ManufacturingStatus: ManufacturingStatus.Complete
                    },
                UnitCategory.Regiment => node is Regiment,
                UnitCategory.Building => node is Building,
                UnitCategory.Officer => node is Officer,
                UnitCategory.Fleet => node is Fleet,
                UnitCategory.CapitalShip => node is CapitalShip,
                UnitCategory.Starfighter => node is Starfighter,
                UnitCategory.SpecialForces => node is SpecialForces,
                _ => false,
            };
    }

    [PersistableObject(Name = "SelectRandomUnits")]
    public sealed class SelectRandomUnits : GameEventSelector
    {
        [PersistableAttribute]
        public int ChancePercent { get; set; } = 100;

        [PersistableAttribute]
        public int MinimumCount { get; set; }

        [PersistableAttribute]
        public int MaximumCount { get; set; } = int.MaxValue;

        [PersistableInlineCollection]
        public List<SelectUnits> Queries { get; set; } = new List<SelectUnits>();

        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            if (ChancePercent < 0 || ChancePercent > 100)
                throw new InvalidOperationException(
                    "SelectRandomUnits.ChancePercent must be between 0 and 100."
                );
            if (MinimumCount < 0)
                throw new InvalidOperationException(
                    "SelectRandomUnits.MinimumCount cannot be negative."
                );
            if (MaximumCount < MinimumCount)
                throw new InvalidOperationException(
                    "SelectRandomUnits.MaximumCount cannot be less than MinimumCount."
                );

            List<ISceneNode> candidates = Queries
                .SelectMany(query => query.Select(game, provider, context))
                .Distinct()
                .OrderBy(unit => unit.InstanceID, StringComparer.Ordinal)
                .ToList();
            List<ISceneNode> selected = candidates
                .Where(_ => provider.NextInt(0, 100) < ChancePercent)
                .ToList();
            List<ISceneNode> remaining = candidates.Except(selected).ToList();
            while (selected.Count < Math.Min(MinimumCount, candidates.Count))
            {
                int index = provider.NextInt(0, remaining.Count);
                selected.Add(remaining[index]);
                remaining.RemoveAt(index);
            }
            while (selected.Count > MaximumCount)
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
