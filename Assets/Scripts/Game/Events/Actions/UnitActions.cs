using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
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

    internal static class UnitActionTargets
    {
        internal static List<IMovable> ResolveUnits(
            string unitInstanceID,
            IEnumerable<GameEventSelector> selectors,
            GameActionContext context,
            string actionName
        )
        {
            GameRoot game = context.Game;
            IEnumerable<ISceneNode> selected = (
                selectors ?? Enumerable.Empty<GameEventSelector>()
            ).SelectMany(selector => selector.Select(game, context.Random, context.Activation));
            if (!string.IsNullOrWhiteSpace(unitInstanceID))
            {
                ISceneNode direct = game.GetSceneNodeByInstanceID<ISceneNode>(unitInstanceID);
                if (direct == null)
                    throw new InvalidOperationException(
                        $"{actionName} could not resolve unit '{unitInstanceID}'."
                    );
                selected = new[] { direct }.Concat(selected);
            }

            List<ISceneNode> resolved = selected
                .Where(node => node != null)
                .Select(node => game.GetSceneNodeByInstanceID<ISceneNode>(node.InstanceID))
                .Where(node => node != null)
                .GroupBy(node => node.InstanceID, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
            if (resolved.Count == 0)
                throw new InvalidOperationException(
                    $"{actionName} requires at least one resolvable unit."
                );
            if (resolved.Any(unit => unit is not IMovable))
                throw new InvalidOperationException(
                    $"{actionName} unit selectors may return only movable units."
                );
            return resolved.Cast<IMovable>().ToList();
        }

        internal static List<ContainerNode> ResolveDestinations(
            string destinationInstanceID,
            IEnumerable<GameEventSelector> selectors,
            GameActionContext context,
            string actionName
        )
        {
            GameRoot game = context.Game;
            List<GameEventSelector> destinationSelectors = (
                selectors ?? Enumerable.Empty<GameEventSelector>()
            ).ToList();
            bool selectFirstAccepted =
                destinationSelectors.Count == 1 && destinationSelectors[0] is SelectFirst;
            IEnumerable<ISceneNode> selected = selectFirstAccepted
                ? ((SelectFirst)destinationSelectors[0]).SelectCandidates(
                    game,
                    context.Random,
                    context.Activation
                )
                : destinationSelectors.SelectMany(selector =>
                    selector.Select(game, context.Random, context.Activation)
                );
            if (!string.IsNullOrWhiteSpace(destinationInstanceID))
            {
                ISceneNode direct = game.GetSceneNodeByInstanceID<ISceneNode>(
                    destinationInstanceID
                );
                if (direct == null)
                    throw new InvalidOperationException(
                        $"{actionName} could not resolve destination '{destinationInstanceID}'."
                    );
                selected = new[] { direct }.Concat(selected);
            }

            List<ContainerNode> destinations = selected
                .Where(node => node != null)
                .Select(node => game.GetSceneNodeByInstanceID<ISceneNode>(node.InstanceID))
                .OfType<ContainerNode>()
                .GroupBy(node => node.InstanceID, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
            if (destinations.Count == 0 || (!selectFirstAccepted && destinations.Count != 1))
                throw new InvalidOperationException(
                    $"{actionName} requires exactly one destination or an explicit SelectFirst; resolved {destinations.Count}."
                );
            return destinations;
        }
    }

    public abstract class UnitTransferAction : GameAction
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        [PersistableAttribute]
        public string DestinationInstanceID { get; set; }

        public List<GameEventSelector> Units { get; set; } = new List<GameEventSelector>();

        public List<GameEventSelector> Destination { get; set; } = new List<GameEventSelector>();

        protected (List<IMovable> Units, List<ContainerNode> Destinations) Resolve(
            GameActionContext context,
            string actionName
        ) =>
            (
                UnitActionTargets.ResolveUnits(UnitInstanceID, Units, context, actionName),
                UnitActionTargets.ResolveDestinations(
                    DestinationInstanceID,
                    Destination,
                    context,
                    actionName
                )
            );
    }

    /// <summary>
    /// Places one or more units at a destination without transit time.
    /// </summary>
    [PersistableObject(Name = "PlaceUnits")]
    public sealed class PlaceUnitsAction : UnitTransferAction
    {
        public override List<GameResult> Execute(GameActionContext context)
        {
            (List<IMovable> units, List<ContainerNode> destinations) = Resolve(
                context,
                "PlaceUnits"
            );
            return new List<GameResult>
            {
                new UnitPlacementRequestedResult
                {
                    Units = units,
                    Destination = destinations[0],
                    DestinationCandidates = destinations,
                    Tick = context.Game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Sends one or more units through normal movement and transit.
    /// </summary>
    [PersistableObject(Name = "SendUnits")]
    public sealed class SendUnitsAction : UnitTransferAction
    {
        public override List<GameResult> Execute(GameActionContext context)
        {
            (List<IMovable> units, List<ContainerNode> destinations) = Resolve(
                context,
                "SendUnits"
            );
            if (
                units.Any(unit =>
                    unit is not ISceneNode node
                    || node.GetParent() == null
                    || context.Game.IsInVoid(node)
                )
            )
                throw new InvalidOperationException(
                    "SendUnits requires active units at a valid scene location."
                );
            return new List<GameResult>
            {
                new UnitMovementRequestedResult
                {
                    Units = units,
                    Destination = destinations[0],
                    DestinationCandidates = destinations,
                    Tick = context.Game.CurrentTick,
                },
            };
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

        [PersistableInlineCollection]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            List<IMovable> units = UnitActionTargets.ResolveUnits(
                UnitInstanceID,
                Selectors,
                context,
                "AddToVoid"
            );
            foreach (IMovable movable in units)
            {
                ISceneNode unit = (ISceneNode)movable;
                if (unit.GetParent() == null || game.IsInVoid(unit))
                    throw new InvalidOperationException(
                        $"AddToVoid requires an active unit; '{unit.GetDisplayName()}' is not active."
                    );
            }
            foreach (IMovable unit in units)
                game.AddToVoid((ISceneNode)unit);
            return new List<GameResult>();
        }
    }

    /// <summary>
    /// Detaches one retained unit from faction void storage.
    /// </summary>
    [PersistableObject(Name = "RemoveFromVoid")]
    public sealed class RemoveFromVoidAction : GameAction
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        [PersistableInlineCollection]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            List<IMovable> units = UnitActionTargets.ResolveUnits(
                UnitInstanceID,
                Selectors,
                context,
                "RemoveFromVoid"
            );
            foreach (IMovable movable in units)
            {
                ISceneNode unit = (ISceneNode)movable;
                if (!game.IsInVoid(unit))
                    throw new InvalidOperationException(
                        $"RemoveFromVoid requires a retained unit; '{unit.GetDisplayName()}' is not retained."
                    );
            }
            foreach (IMovable unit in units)
                game.RemoveFromVoid((ISceneNode)unit);
            return new List<GameResult>();
        }
    }
}
