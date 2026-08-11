using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
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
                game.UnitLifecycle.AddToVoid(root);
            }
            foreach (IMovable unit in destroyed.OfType<IMovable>())
                game.UnitLifecycle.SetStatus(unit, VoidStatus.Destroyed);

            Planet planet = context.Activation?.GetTarget<Planet>();
            PlanetIncidentResult incident = context
                .Activation?.Results.OfType<PlanetIncidentResult>()
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

        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            ISceneNode unit = game.GetSceneNodeByInstanceID<ISceneNode>(UnitInstanceID);
            if (unit == null)
                throw new InvalidOperationException(
                    $"AddToVoid could not resolve unit '{UnitInstanceID}'."
                );
            game.UnitLifecycle.AddToVoid(unit);
            return new List<GameResult>();
        }
    }

    /// <summary>
    /// Records a participant's current attachment through the standard mission-return fields.
    /// </summary>
    [PersistableObject(Name = "SetMissionReturnDestination")]
    public sealed class SetMissionReturnDestinationAction : GameAction
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        public override List<GameResult> Execute(GameActionContext context)
        {
            IMissionParticipant participant =
                context.Game.GetSceneNodeByInstanceID<IMissionParticipant>(UnitInstanceID);
            if (participant == null)
                throw new InvalidOperationException(
                    $"SetMissionReturnDestination could not resolve participant '{UnitInstanceID}'."
                );
            return new List<GameResult>
            {
                new MissionReturnDestinationRequestedResult
                {
                    Participant = participant,
                    ReturnParent = participant.GetParent() as ContainerNode,
                    ReturnLocation = participant.GetParentOfType<Planet>(),
                    Tick = context.Game.CurrentTick,
                },
            };
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

        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            ISceneNode unit = game.GetSceneNodeByInstanceID<ISceneNode>(UnitInstanceID);
            if (unit == null)
                throw new InvalidOperationException(
                    $"SetStatus could not resolve unit '{UnitInstanceID}'."
                );
            game.UnitLifecycle.SetStatus(unit, Status, DisplayText);
            return new List<GameResult>();
        }
    }

    /// <summary>
    /// Requests activation of an off-map unit at an explicit destination.
    /// </summary>
    [PersistableObject(Name = "ActivateFromVoid")]
    public sealed class ActivateFromVoidAction : GameAction
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            ISceneNode unit = game.GetSceneNodeByInstanceID<ISceneNode>(UnitInstanceID);
            if (unit == null)
                throw new InvalidOperationException(
                    $"ActivateFromVoid could not resolve unit '{UnitInstanceID}'."
                );
            return new List<GameResult>
            {
                new UnitActivationRequestedResult
                {
                    Unit = unit,
                    UseMissionReturnDestination = true,
                    Tick = game.CurrentTick,
                },
            };
        }
    }
}
