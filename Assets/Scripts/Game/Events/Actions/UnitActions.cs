using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
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
    public abstract class UnitSelector
    {
        internal abstract IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        );
    }

    [PersistableObject(Name = "SelectUnits")]
    public sealed class SelectUnits : UnitSelector
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
                && context?.GetBinding<Planet>(PlanetBinding) is Planet boundPlanet
            )
                planetInstanceID = boundPlanet.InstanceID;

            return game.GetRegisteredSceneNodesByType<ISceneNode>()
                .Where(IsUnit)
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
    public sealed class SelectRandomUnits : UnitSelector
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
            List<ISceneNode> candidates = Queries
                .SelectMany(query => query.Select(game, provider, context))
                .Distinct()
                .OrderBy(unit => unit.InstanceID, StringComparer.Ordinal)
                .ToList();
            List<ISceneNode> selected = candidates
                .Where(_ => provider.NextInt(0, 100) < Math.Clamp(ChancePercent, 0, 100))
                .ToList();
            List<ISceneNode> remaining = candidates.Except(selected).ToList();
            while (selected.Count < Math.Min(Math.Max(0, MinimumCount), candidates.Count))
            {
                int index = provider.NextInt(0, remaining.Count);
                selected.Add(remaining[index]);
                remaining.RemoveAt(index);
            }
            while (selected.Count > Math.Max(0, MaximumCount))
                selected.RemoveAt(provider.NextInt(0, selected.Count));
            return selected;
        }
    }

    [PersistableObject(Name = "SelectBinding")]
    public sealed class SelectBinding : UnitSelector
    {
        [PersistableAttribute]
        public string Name { get; set; }

        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            if (context?.TryGetBinding(Name, out object value) != true)
                return Enumerable.Empty<ISceneNode>();
            if (value is ISceneNode node)
                return new[] { node };
            return value is IEnumerable<ISceneNode> nodes ? nodes : Enumerable.Empty<ISceneNode>();
        }
    }

    [PersistableObject(Name = "DestroyUnits")]
    public sealed class DestroyUnitsAction : GameAction
    {
        [PersistableInlineCollection]
        public List<UnitSelector> Selectors { get; set; } = new List<UnitSelector>();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game) => Execute(game, game.Random, null);

        /// <inheritdoc />
        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            if (Selectors.Count == 0)
                throw new InvalidOperationException("DestroyUnits requires at least one selector.");
            List<ISceneNode> destroyed = Selectors
                .SelectMany(selector => selector.Select(game, provider, context))
                .Distinct()
                .ToList();

            foreach (ISceneNode unit in destroyed)
                game.DetachNode(unit);

            Planet planet = context?.GetScopeTarget<Planet>();
            PlanetIncidentResult incident = context
                ?.Results.OfType<PlanetIncidentResult>()
                .LastOrDefault(result =>
                    result.Planet == planet && result.IncidentType == IncidentType.Disaster
                );
            if (incident != null)
            {
                incident.DestroyedObjects.AddRange(destroyed.Cast<IGameEntity>());
                incident.Severity += destroyed.Count;
            }

            return destroyed.ConvertAll<GameResult>(unit => new GameObjectDestroyedResult
            {
                DestroyedObject = unit,
                Context = planet,
                Tick = game.CurrentTick,
            });
        }
    }

    /// <summary>
    /// References one unit in a scripted movement group.
    /// </summary>
    [PersistableObject(Name = "Unit")]
    public sealed class MovementUnitReference
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }
    }

    /// <summary>
    /// Requests authoritative movement for one or more movable scene nodes.
    /// </summary>
    [PersistableObject(Name = "RequestMovement")]
    public class RequestMovementAction : GameAction
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        [PersistableAttribute]
        public string DestinationInstanceID { get; set; }

        [PersistableAttribute]
        public string DestinationUnitInstanceID { get; set; }

        [PersistableAttribute]
        public string DestinationBinding { get; set; }

        public List<MovementUnitReference> Units { get; set; } = new List<MovementUnitReference>();

        [PersistableInlineCollection]
        public List<UnitSelector> Selectors { get; set; } = new List<UnitSelector>();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game) => Execute(game, game.Random, null);

        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            List<IMovable> units = ResolveUnits(game, provider, context);
            ContainerNode destination = ResolveDestination(game, context);
            if (destination == null)
                throw new InvalidOperationException(
                    "RequestMovement could not resolve its destination."
                );

            return new List<GameResult>
            {
                new UnitMovementRequestedResult
                {
                    Unit = units.Count == 1 ? units[0] : null,
                    Units = units.Count > 1 ? units : new List<IMovable>(),
                    Destination = destination,
                    Tick = game.CurrentTick,
                },
            };
        }

        private List<IMovable> ResolveUnits(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            IEnumerable<string> instanceIDs = Units.Select(reference => reference.UnitInstanceID);
            if (!string.IsNullOrWhiteSpace(UnitInstanceID))
                instanceIDs = new[] { UnitInstanceID }.Concat(instanceIDs);

            List<IMovable> units = instanceIDs
                .Distinct(StringComparer.Ordinal)
                .Select(game.GetSceneNodeByInstanceID<IMovable>)
                .Concat(
                    Selectors
                        .SelectMany(selector => selector.Select(game, provider, context))
                        .Cast<IMovable>()
                )
                .Distinct()
                .ToList();
            if (units.Count == 0 || units.Any(unit => unit == null))
                throw new InvalidOperationException(
                    "RequestMovement could not resolve every requested movable unit."
                );
            return units;
        }

        private ContainerNode ResolveDestination(GameRoot game, GameEventExecutionContext context)
        {
            if (!string.IsNullOrWhiteSpace(DestinationInstanceID))
                return game.GetSceneNodeByInstanceID<ContainerNode>(DestinationInstanceID);

            if (
                !string.IsNullOrWhiteSpace(DestinationBinding)
                && context?.TryGetBinding(DestinationBinding, out object bound) == true
            )
            {
                if (bound is ContainerNode container)
                    return container;
                if (bound is ISceneNode node)
                    return node.GetParentOfType<Planet>();
            }

            ISceneNode destinationUnit = game.GetSceneNodeByInstanceID<ISceneNode>(
                DestinationUnitInstanceID
            );
            return destinationUnit as ContainerNode ?? destinationUnit?.GetParentOfType<Planet>();
        }
    }

    /// <summary>
    /// Removes one active unit from the scene graph while retaining it in faction storage.
    /// </summary>
    [PersistableObject(Name = "AddToVoid")]
    public sealed class AddToVoidAction : GameAction
    {
        [PersistableAttribute(Name = "UnitInstanceID")]
        public string UnitInstanceID { get; set; }

        public override List<GameResult> Execute(GameRoot game)
        {
            ISceneNode unit = game.GetSceneNodeByInstanceID<ISceneNode>(UnitInstanceID);
            if (unit == null)
                throw new InvalidOperationException(
                    $"AddToVoid could not resolve unit '{UnitInstanceID}'."
                );
            game.AddToVoid(unit);
            return new List<GameResult>();
        }
    }

    /// <summary>
    /// Sets the reason an off-map unit is unavailable.
    /// </summary>
    [PersistableObject(Name = "SetStatus")]
    public sealed class SetStatusAction : GameAction
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        [PersistableAttribute]
        public VoidStatus? Status { get; set; }

        [PersistableAttribute]
        public string DisplayText { get; set; }

        public override List<GameResult> Execute(GameRoot game)
        {
            ISceneNode unit = game.GetSceneNodeByInstanceID<ISceneNode>(UnitInstanceID);
            if (unit == null)
                throw new InvalidOperationException(
                    $"SetStatus could not resolve unit '{UnitInstanceID}'."
                );
            game.SetVoidStatus(unit, Status, DisplayText);
            return new List<GameResult>();
        }
    }

    /// <summary>
    /// Returns an off-map unit to its last valid attachment or a friendly fallback planet.
    /// </summary>
    [PersistableObject(Name = "ReturnFromVoid")]
    public sealed class ReturnFromVoidAction : GameAction
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        public override List<GameResult> Execute(GameRoot game)
        {
            ISceneNode unit = game.GetSceneNodeByInstanceID<ISceneNode>(UnitInstanceID);
            if (unit == null)
                throw new InvalidOperationException(
                    $"ReturnFromVoid could not resolve unit '{UnitInstanceID}'."
                );
            game.ReturnFromVoid(unit);
            return new List<GameResult>();
        }
    }
}
