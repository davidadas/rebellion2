using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    [PersistableObject(Name = "PlanetResourceValue")]
    public sealed class PlanetResourceValueConditional : GameConditional
    {
        [PersistableAttribute]
        public PlanetResource Resource { get; set; }

        [PersistableAttribute]
        public EventVariableComparison Comparison { get; set; }

        [PersistableAttribute]
        public int Value { get; set; }

        public override bool IsMet(GameConditionContext context)
        {
            Planet planet = context.Activation?.GetScopeTarget<Planet>();
            if (planet == null)
                return false;
            int current =
                Resource == PlanetResource.RawMaterials
                    ? planet.NumRawResourceNodes
                    : planet.EnergyCapacity;
            return Comparison switch
            {
                EventVariableComparison.Equal => current == Value,
                EventVariableComparison.NotEqual => current != Value,
                EventVariableComparison.GreaterThan => current > Value,
                EventVariableComparison.GreaterThanOrEqual => current >= Value,
                EventVariableComparison.LessThan => current < Value,
                EventVariableComparison.LessThanOrEqual => current <= Value,
                _ => false,
            };
        }
    }

    [PersistableObject(Name = "HasBuildingType")]
    public sealed class HasBuildingTypeConditional : GameConditional
    {
        [PersistableAttribute]
        public BuildingType Type { get; set; }

        public override bool IsMet(GameConditionContext context) =>
            context
                .Activation?.GetScopeTarget<Planet>()
                ?.Buildings.Any(building =>
                    building.BuildingType == Type
                    && building.ManufacturingStatus == ManufacturingStatus.Complete
                ) == true;
    }

    /// <summary>
    /// Tests whether an authored planet has any owner or one specific faction owner.
    /// </summary>
    [PersistableObject(Name = "IsOwned")]
    public sealed class IsOwnedConditional : GameConditional
    {
        [PersistableAttribute(Name = "PlanetInstanceID")]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        [PersistableAttribute(Name = "FactionInstanceID")]
        public string FactionInstanceID { get; set; }

        public override bool IsMet(GameConditionContext context)
        {
            GameRoot game = context.Game;
            Planet planet = string.IsNullOrWhiteSpace(PlanetBinding)
                ? game.GetSceneNodeByInstanceID<Planet>(PlanetInstanceID)
                : context.Activation?.GetBinding<Planet>(PlanetBinding);
            if (planet?.IsDestroyed != false)
                return false;

            Faction owner = game.GetFactions()
                .FirstOrDefault(faction => faction.InstanceID == planet.OwnerInstanceID);
            return owner != null
                && (
                    string.IsNullOrWhiteSpace(FactionInstanceID)
                    || owner.InstanceID == FactionInstanceID
                );
        }
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when all specified units are located on the same planet.
    /// </summary>
    [PersistableObject(Name = "AreOnSamePlanet")]
    public sealed class AreOnSamePlanetConditional : GameConditional
    {
        [PersistableMember(Name = "UnitInstanceIDs")]
        public List<string> UnitInstanceIDs { get; set; }

        public AreOnSamePlanetConditional()
            : base() { }

        /// <summary>
        /// Checks whether every referenced unit is parented to the same planet.
        /// </summary>
        /// <param name="context">The context used to resolve unit references.</param>
        /// <returns>True if all referenced units share a planet parent; false if any are missing or on a different planet.</returns>
        public override bool IsMet(GameConditionContext context)
        {
            GameRoot game = context.Game;
            List<ISceneNode> sceneNodes = game.GetSceneNodesByInstanceIDs(UnitInstanceIDs);
            if (sceneNodes.Count != UnitInstanceIDs.Count)
                return false;

            Planet comparator = null;

            // Check if all units are on the same planet.
            foreach (ISceneNode node in sceneNodes)
            {
                if (node == null)
                {
                    return false;
                }

                Planet planet = node.GetParentOfType<Planet>();
                comparator ??= planet;

                if (comparator != planet)
                {
                    return false;
                }
            }

            return comparator != null;
        }
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when exactly two units belong to different factions.
    /// </summary>
    [PersistableObject(Name = "AreOnOpposingFactions")]
    public sealed class AreOnOpposingFactionsConditional : GameConditional
    {
        [PersistableMember(Name = "UnitInstanceIDs")]
        public List<string> UnitInstanceIDs { get; set; } = new List<string>();

        public AreOnOpposingFactionsConditional()
            : base() { }

        /// <summary>
        /// Checks whether the two referenced units belong to different owners.
        /// </summary>
        /// <param name="context">The context used to resolve unit references.</param>
        /// <returns>True if exactly two units are referenced and their owner instance IDs differ.</returns>
        public override bool IsMet(GameConditionContext context)
        {
            GameRoot game = context.Game;
            // Get the scene nodes for the units.
            List<ISceneNode> sceneNodes = game.GetSceneNodesByInstanceIDs(UnitInstanceIDs);

            // Check if the units are on opposing factions.
            return sceneNodes.Count == 2
                && sceneNodes[0].OwnerInstanceID != sceneNodes[1].OwnerInstanceID;
        }
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when the specified unit is currently assigned to a mission.
    /// </summary>
    [PersistableObject(Name = "IsOnMission")]
    public sealed class IsOnMissionConditional : GameConditional
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        /// <summary>
        /// Checks whether the referenced unit is parented to a <see cref="Mission"/> node.
        /// </summary>
        /// <param name="context">The context used to resolve the unit.</param>
        /// <returns>True if the unit exists and its direct parent is a mission; otherwise false.</returns>
        public override bool IsMet(GameConditionContext context)
        {
            ISceneNode sceneNode = context.Game.GetSceneNodeByInstanceID<ISceneNode>(
                UnitInstanceID
            );
            // Check if the unit is on a mission.
            return sceneNode?.GetParent() is Mission;
        }
    }

    /// <summary>
    /// Tests whether a movable unit currently has an active movement state.
    /// </summary>
    [PersistableObject(Name = "IsInTransit")]
    public sealed class IsInTransitConditional : GameConditional
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        public override bool IsMet(GameConditionContext context) =>
            context.Game.GetSceneNodeByInstanceID<ISceneNode>(UnitInstanceID)
                is IMovable { Movement: not null };
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when all specified units are located on any planet.
    /// </summary>
    [PersistableObject(Name = "AreOnPlanet")]
    public sealed class AreOnPlanetConditional : GameConditional
    {
        public List<string> UnitInstanceIDs { get; set; }

        public AreOnPlanetConditional()
            : base() { }

        /// <summary>
        /// Checks whether every referenced unit has a planet somewhere in its ancestry.
        /// </summary>
        /// <param name="context">The context used to resolve unit references.</param>
        /// <returns>True if every referenced unit is on some planet; otherwise false.</returns>
        public override bool IsMet(GameConditionContext context)
        {
            GameRoot game = context.Game;
            // Get the instance IDs of the units to check.
            List<ISceneNode> sceneNodes = game.GetSceneNodesByInstanceIDs(UnitInstanceIDs);

            // Check if all units are on a planet.
            return sceneNodes.Count == UnitInstanceIDs.Count
                && sceneNodes.All(node => node.GetParentOfType<Planet>() != null);
        }
    }

    /// <summary>
    /// Tests whether a scene node is contained by a specific location node.
    /// </summary>
    [PersistableObject(Name = "IsAtLocation")]
    public sealed class IsAtLocationConditional : GameConditional
    {
        public string UnitInstanceID { get; set; }
        public string LocationInstanceID { get; set; }

        /// <inheritdoc />
        public override bool IsMet(GameConditionContext context)
        {
            GameRoot game = context.Game;
            ISceneNode unit = game.GetSceneNodeByInstanceID<ISceneNode>(UnitInstanceID);
            ISceneNode location = game.GetSceneNodeByInstanceID<ISceneNode>(LocationInstanceID);
            for (ISceneNode current = unit; current != null; current = current.GetParent())
            {
                if (current == location)
                    return true;
            }

            return false;
        }
    }
}
