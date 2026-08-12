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
    [PersistableObject(Name = "DestroyUnits")]
    public sealed class DestroyUnitsAction : GameAction
    {
        [PersistableInlineCollection]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            if (Selectors.Count == 0)
                throw new InvalidOperationException("DestroyUnits requires at least one selector.");
            HashSet<ISceneNode> selected = Selectors
                .SelectMany(selector => selector.Select(game, context.Random, context.Activation))
                .ToHashSet();
            List<ISceneNode> destroyedRoots = selected
                .Where(unit => !HasSelectedAncestor(unit, selected))
                .ToList();
            List<ISceneNode> destroyed = new List<ISceneNode>();

            foreach (ISceneNode root in destroyedRoots)
            {
                root.Traverse(unit => destroyed.Add(unit));
                game.DeleteNode(root);
            }

            Planet planet = context.Activation?.GetTarget<Planet>();

            return destroyed.ConvertAll<GameResult>(unit => new GameObjectDestroyedResult
            {
                DestroyedObject = unit,
                Context = planet,
                Tick = game.CurrentTick,
            });
        }

        private static bool HasSelectedAncestor(ISceneNode unit, HashSet<ISceneNode> selected)
        {
            for (ISceneNode parent = unit.GetParent(); parent != null; parent = parent.GetParent())
            {
                if (selected.Contains(parent))
                    return true;
            }
            return false;
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
    public sealed class RequestMovementAction : GameAction
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
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            List<IMovable> units = ResolveUnits(game, context.Random, context.Activation);
            ContainerNode destination = ResolveDestination(game, context.Activation);
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

            List<ISceneNode> selected = instanceIDs
                .Distinct(StringComparer.Ordinal)
                .Select(game.GetSceneNodeByInstanceID<ISceneNode>)
                .Concat(Selectors.SelectMany(selector => selector.Select(game, provider, context)))
                .Distinct()
                .ToList();
            if (selected.Count == 0 || selected.Any(unit => unit == null))
                throw new InvalidOperationException(
                    "RequestMovement could not resolve every requested movable unit."
                );
            if (selected.Any(unit => unit is not IMovable))
                throw new InvalidOperationException(
                    "RequestMovement selectors may return only movable units."
                );
            return selected.Cast<IMovable>().ToList();
        }

        private ContainerNode ResolveDestination(GameRoot game, GameEventExecutionContext context)
        {
            if (!string.IsNullOrWhiteSpace(DestinationInstanceID))
                return game.GetSceneNodeByInstanceID<ContainerNode>(DestinationInstanceID);

            if (
                !string.IsNullOrWhiteSpace(DestinationBinding)
                && context?.TryGetBindingReference(DestinationBinding, out object bound) == true
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

        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
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
    /// Requests activation of an off-map unit at an explicit destination.
    /// </summary>
    [PersistableObject(Name = "RemoveFromVoid")]
    public sealed class RemoveFromVoidAction : GameAction
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            ISceneNode unit = game.GetSceneNodeByInstanceID<ISceneNode>(UnitInstanceID);
            if (unit == null)
                throw new InvalidOperationException(
                    $"RemoveFromVoid could not resolve unit '{UnitInstanceID}'."
                );
            if (!game.RemoveFromVoid(unit))
                throw new InvalidOperationException(
                    $"RemoveFromVoid could not restore '{UnitInstanceID}' to a previous location."
                );
            return new List<GameResult>();
        }
    }
}
