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
    [PersistableObject(Name = "ComparePlanetStat")]
    public sealed class ComparePlanetStatConditional : GameConditional
    {
        [PersistableAttribute]
        public PlanetStat Stat { get; set; }

        [PersistableAttribute]
        public EventVariableComparison Comparison { get; set; }

        [PersistableAttribute]
        public int Value { get; set; }

        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        public override bool IsMet(GameConditionContext context)
        {
            Planet planet = !string.IsNullOrWhiteSpace(PlanetBinding)
                ? context.Activation?.GetBindingReference<Planet>(PlanetBinding)
                : context.Game.GetSceneNodeByInstanceID<Planet>(PlanetInstanceID);
            planet ??= context.Activation?.GetTarget<Planet>();
            if (planet == null)
                return false;
            int current = AdjustPlanetStatAction.GetValue(planet, Stat);
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
                .Activation?.GetTarget<Planet>()
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
                : context.Activation?.GetBindingReference<Planet>(PlanetBinding);
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
    /// Rolls against one faction's current popular support at a planet.
    /// </summary>
    [PersistableObject(Name = "RollAgainstPopularSupport")]
    public sealed class RollAgainstPopularSupportConditional : GameConditional
    {
        [PersistableAttribute]
        public string FactionInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        public override bool IsMet(GameConditionContext context)
        {
            Planet planet = !string.IsNullOrWhiteSpace(PlanetBinding)
                ? context.Activation?.GetBindingReference<Planet>(PlanetBinding)
                : context.Game.GetSceneNodeByInstanceID<Planet>(PlanetInstanceID);
            planet ??= context.Activation?.GetTarget<Planet>();
            if (planet == null || string.IsNullOrWhiteSpace(FactionInstanceID))
                return false;

            int support = planet.GetPopularSupport(FactionInstanceID);
            return context.Random.NextInt(0, 100) < support;
        }
    }

    [PersistableObject(Name = "Unit")]
    public sealed class EventUnitReference
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }
    }

    public enum SceneAncestorType
    {
        Galaxy,
        PlanetSystem,
        Planet,
        Fleet,
        Mission,
        CapitalShip,
    }

    internal static class SceneAncestors
    {
        internal static ISceneNode Resolve(ISceneNode node, SceneAncestorType type) =>
            type switch
            {
                SceneAncestorType.Galaxy => node.GetParentOfType<GalaxyMap>(),
                SceneAncestorType.PlanetSystem => node.GetParentOfType<PlanetSystem>(),
                SceneAncestorType.Planet => node.GetParentOfType<Planet>(),
                SceneAncestorType.Fleet => node.GetParentOfType<Fleet>(),
                SceneAncestorType.Mission => node.GetParentOfType<Mission>(),
                SceneAncestorType.CapitalShip => node.GetParentOfType<CapitalShip>(),
                _ => null,
            };
    }

    [PersistableObject(Name = "ShareParent")]
    public sealed class ShareParentConditional : GameConditional
    {
        [PersistableInlineCollection]
        public List<EventUnitReference> Units { get; set; } = new List<EventUnitReference>();

        public override bool IsMet(GameConditionContext context)
        {
            List<ISceneNode> nodes = ResolveDistinctUnits(context);
            if (nodes == null)
                return false;
            ISceneNode parent = nodes[0].GetParent();
            return parent != null && nodes.All(node => ReferenceEquals(node.GetParent(), parent));
        }

        private List<ISceneNode> ResolveDistinctUnits(GameConditionContext context) =>
            SceneConditionUnits.ResolveDistinct(context.Game, Units);
    }

    [PersistableObject(Name = "ShareAncestor")]
    public sealed class ShareAncestorConditional : GameConditional
    {
        [PersistableAttribute]
        public SceneAncestorType Type { get; set; }

        [PersistableInlineCollection]
        public List<EventUnitReference> Units { get; set; } = new List<EventUnitReference>();

        public override bool IsMet(GameConditionContext context)
        {
            List<ISceneNode> nodes = SceneConditionUnits.ResolveDistinct(context.Game, Units);
            if (nodes == null)
                return false;
            List<ISceneNode> ancestors = nodes.ConvertAll(node =>
                SceneAncestors.Resolve(node, Type)
            );
            return ancestors[0] != null
                && ancestors.All(ancestor => ReferenceEquals(ancestor, ancestors[0]));
        }
    }

    internal static class SceneConditionUnits
    {
        public static List<ISceneNode> ResolveDistinct(
            GameRoot game,
            IReadOnlyCollection<EventUnitReference> references
        )
        {
            if (references == null || references.Count < 2)
                return null;
            List<string> ids = references.Select(reference => reference.UnitInstanceID).ToList();
            if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct().Count() != ids.Count)
                return null;
            List<ISceneNode> nodes = game.GetSceneNodesByInstanceIDs(ids);
            return nodes.Count == ids.Count ? nodes : null;
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
