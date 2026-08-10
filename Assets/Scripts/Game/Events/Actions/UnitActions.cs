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
    /// <summary>
    /// Selects candidate buildings by their general gameplay type.
    /// </summary>
    [PersistableObject]
    public sealed class BuildingCandidates
    {
        public List<BuildingType> BuildingTypes { get; set; } = new List<BuildingType>();
    }

    /// <summary>
    /// Includes all eligible regiments on the selected planet.
    /// </summary>
    [PersistableObject]
    public sealed class RegimentCandidates { }

    /// <summary>
    /// Combines the unit categories eligible for a destructive incident.
    /// </summary>
    [PersistableObject]
    public sealed class DestroyUnitCandidates
    {
        public BuildingCandidates Buildings { get; set; }
        public RegimentCandidates Regiments { get; set; }
    }

    /// <summary>
    /// Destroys a bounded random subset of eligible units on the selected planet.
    /// </summary>
    [PersistableObject(Name = "DestroyUnits")]
    public sealed class DestroyUnitsAction : GameAction
    {
        [PersistableAttribute(Name = "ChancePerUnit")]
        public double ChancePerUnit { get; set; } = 0.1;

        [PersistableAttribute(Name = "MinimumCount")]
        public int MinimumCount { get; set; }

        [PersistableAttribute(Name = "MaximumCount")]
        public int MaximumCount { get; set; } = int.MaxValue;

        public DestroyUnitCandidates Candidates { get; set; } = new DestroyUnitCandidates();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game) =>
            throw new InvalidOperationException("DestroyUnits requires a planet target.");

        /// <inheritdoc />
        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            Planet planet = context?.GetScopeTarget<Planet>();
            if (planet == null)
                return Execute(game);

            List<ISceneNode> eligible = new List<ISceneNode>();
            if (Candidates?.Buildings != null)
            {
                eligible.AddRange(
                    planet.Buildings.Where(building =>
                        building.ManufacturingStatus == ManufacturingStatus.Complete
                        && building.OwnerInstanceID == planet.OwnerInstanceID
                        && Candidates.Buildings.BuildingTypes.Contains(building.BuildingType)
                    )
                );
            }
            if (Candidates?.Regiments != null)
                eligible.AddRange(
                    planet.Regiments.Where(regiment =>
                        regiment.OwnerInstanceID == planet.OwnerInstanceID
                    )
                );

            eligible = eligible.OrderBy(unit => unit.InstanceID, StringComparer.Ordinal).ToList();
            List<ISceneNode> destroyed = eligible
                .Where(_ => provider.NextDouble() < Math.Clamp(ChancePerUnit, 0.0, 1.0))
                .ToList();
            List<ISceneNode> remaining = eligible.Except(destroyed).ToList();
            while (destroyed.Count < Math.Min(MinimumCount, eligible.Count))
            {
                int index = provider.NextInt(0, remaining.Count);
                destroyed.Add(remaining[index]);
                remaining.RemoveAt(index);
            }
            while (destroyed.Count > Math.Max(0, MaximumCount))
                destroyed.RemoveAt(provider.NextInt(0, destroyed.Count));

            foreach (ISceneNode unit in destroyed)
                game.DetachNode(unit);

            PlanetIncidentResult incident = context
                .Results.OfType<PlanetIncidentResult>()
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
    /// Requests authoritative movement for one movable scene node.
    /// </summary>
    [PersistableObject(Name = "RequestMovement")]
    public class RequestMovementAction : GameAction
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }
        public string DestinationInstanceID { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            IMovable unit = game.GetSceneNodeByInstanceID<IMovable>(UnitInstanceID);
            ContainerNode destination = game.GetSceneNodeByInstanceID<ContainerNode>(
                DestinationInstanceID
            );
            if (unit == null)
                throw new InvalidOperationException(
                    $"RequestMovement could not resolve movable unit '{UnitInstanceID}'."
                );
            if (destination == null)
                throw new InvalidOperationException(
                    $"RequestMovement could not resolve destination '{DestinationInstanceID}'."
                );

            return new List<GameResult>
            {
                new UnitMovementRequestedResult
                {
                    Unit = unit,
                    Destination = destination,
                    Tick = game.CurrentTick,
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
        public VoidStatus Status { get; set; }
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
