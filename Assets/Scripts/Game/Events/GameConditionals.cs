using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    #region CompositeConditions
    /// <summary>
    /// A <see cref="GameConditional"/> that is met when all child conditions are met.
    /// </summary>
    [PersistableObject(Name = "All")]
    public sealed class AllConditional : GameConditional
    {
        [PersistableInlineCollection]
        public List<GameConditional> Conditionals = new List<GameConditional>();

        public AllConditional()
            : base() { }

        /// <summary>
        /// Evaluates the AND composition: all child conditions must be met.
        /// </summary>
        /// <param name="context">The current condition-evaluation context.</param>
        /// <returns>True if every child condition is met; otherwise false.</returns>
        public override bool IsMet(GameConditionContext context) =>
            Conditionals.All(conditional => conditional.IsMet(context));
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when any child condition is met.
    /// </summary>
    [PersistableObject(Name = "Any")]
    public sealed class AnyConditional : GameConditional
    {
        [PersistableInlineCollection]
        public List<GameConditional> Conditionals = new List<GameConditional>();

        public AnyConditional()
            : base() { }

        /// <summary>
        /// Evaluates the OR composition: at least one child condition must be met.
        /// </summary>
        /// <param name="context">The current condition-evaluation context.</param>
        /// <returns>True if any child condition is met; otherwise false.</returns>
        public override bool IsMet(GameConditionContext context) =>
            Conditionals.Any(conditional => conditional.IsMet(context));
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when none of the child conditions are met.
    /// </summary>
    [PersistableObject(Name = "Not")]
    public sealed class NotConditional : GameConditional
    {
        [PersistableInlineCollection]
        public List<GameConditional> Conditionals = new List<GameConditional>();

        public NotConditional()
            : base() { }

        /// <summary>
        /// Evaluates the NOT composition: no child condition may be met.
        /// </summary>
        /// <param name="context">The current condition-evaluation context.</param>
        /// <returns>True if every child condition is unmet; otherwise false.</returns>
        public override bool IsMet(GameConditionContext context) =>
            Conditionals.All(conditional => !conditional.IsMet(context));
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when exactly one child condition is met.
    /// </summary>
    [PersistableObject(Name = "Xor")]
    public sealed class XorConditional : GameConditional
    {
        [PersistableInlineCollection]
        public List<GameConditional> Conditionals = new List<GameConditional>();

        public XorConditional()
            : base() { }

        /// <summary>
        /// Evaluates the XOR composition: exactly one child condition must be met.
        /// </summary>
        /// <param name="context">The current condition-evaluation context.</param>
        /// <returns>True if precisely one child condition is met; otherwise false.</returns>
        public override bool IsMet(GameConditionContext context) =>
            Conditionals.Count(conditional => conditional.IsMet(context)) == 1;
    }
    #endregion

    #region EventStateConditions
    /// <summary>
    /// Selects the comparison applied to two authored scalar values.
    /// </summary>
    public enum ComparisonOperator
    {
        Equal,
        NotEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
    }

    /// <summary>
    /// Applies the shared authored comparison vocabulary to integer values.
    /// </summary>
    internal static class IntegerComparison
    {
        /// <summary>
        /// Compares an actual integer with an expected integer using the selected operator.
        /// </summary>
        internal static bool Evaluate(int actual, ComparisonOperator operation, int expected) =>
            operation switch
            {
                ComparisonOperator.Equal => actual == expected,
                ComparisonOperator.NotEqual => actual != expected,
                ComparisonOperator.GreaterThan => actual > expected,
                ComparisonOperator.GreaterThanOrEqual => actual >= expected,
                ComparisonOperator.LessThan => actual < expected,
                ComparisonOperator.LessThanOrEqual => actual <= expected,
                _ => throw new InvalidOperationException(
                    $"Unsupported comparison operator '{operation}'."
                ),
            };
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when the current tick count satisfies a comparison against a target value.
    /// </summary>
    [PersistableObject(Name = "TickCount")]
    public sealed class TickCountConditional : GameConditional
    {
        [PersistableAttribute]
        public ComparisonOperator Comparison { get; set; }

        [PersistableAttribute]
        public int Ticks { get; set; }

        /// <summary>
        /// Compares the current tick against the authored tick count.
        /// </summary>
        /// <param name="context">The context providing the current game state.</param>
        /// <returns>True when the tick comparison holds; otherwise false.</returns>
        public override bool IsMet(GameConditionContext context)
        {
            GameRoot game = context.Game;
            return IntegerComparison.Evaluate(game.CurrentTick, Comparison, Ticks);
        }
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met after the specified event has activated.
    /// </summary>
    [PersistableObject(Name = "HasEventActivated")]
    public sealed class HasEventActivatedConditional : GameConditional
    {
        [PersistableAttribute]
        public string EventInstanceID { get; set; }

        /// <summary>
        /// Checks whether the event with the configured instance ID has activated at least once.
        /// </summary>
        /// <param name="context">The context providing event runtime state.</param>
        /// <returns>True if the event has activated; otherwise false.</returns>
        public override bool IsMet(GameConditionContext context)
        {
            return context.Game.EventRuntime.GetState(EventInstanceID).ActivationCount > 0;
        }
    }

    /// <summary>
    /// Tests whether the specified event can no longer activate.
    /// </summary>
    [PersistableObject(Name = "IsEventComplete")]
    public sealed class IsEventCompleteConditional : GameConditional
    {
        [PersistableAttribute]
        public string EventInstanceID { get; set; }

        /// <summary>
        /// Checks the persisted completion state for the referenced event.
        /// </summary>
        /// <param name="context">The context providing event runtime state.</param>
        /// <returns>True when the referenced event is permanently complete.</returns>
        public override bool IsMet(GameConditionContext context)
        {
            return context.Game.EventRuntime.GetState(EventInstanceID).IsComplete;
        }
    }

    /// <summary>
    /// Compares a persistent, data-defined event variable with an authored value.
    /// </summary>
    [PersistableObject(Name = "EvaluateEventVariable")]
    public sealed class EvaluateEventVariableConditional : GameConditional
    {
        [PersistableAttribute]
        public string Key { get; set; }

        [PersistableAttribute]
        public ComparisonOperator Comparison { get; set; }

        [PersistableAttribute]
        public int CompareTo { get; set; }

        /// <summary>
        /// Compares the current event variable value with the authored integer.
        /// </summary>
        /// <param name="context">The context providing event runtime state.</param>
        /// <returns>True when the variable comparison succeeds.</returns>
        public override bool IsMet(GameConditionContext context)
        {
            int current = context.Game.EventRuntime.GetVariable(Key);
            return IntegerComparison.Evaluate(current, Comparison, CompareTo);
        }
    }

    /// <summary>
    /// Compares one scalar binding with an authored scalar or another scalar binding.
    /// </summary>
    [PersistableObject(Name = "EvaluateBinding")]
    public sealed class EvaluateBindingConditional : GameConditional
    {
        [PersistableAttribute]
        public string Binding { get; set; }

        [PersistableAttribute]
        public ComparisonOperator Comparison { get; set; }

        [PersistableAttribute]
        public string CompareTo { get; set; }

        [PersistableAttribute]
        public string CompareToBinding { get; set; }

        /// <summary>
        /// Evaluates the configured scalar comparison against the current bindings.
        /// </summary>
        /// <param name="context">The context providing the game and event bindings.</param>
        /// <returns>True when the configured comparison succeeds.</returns>
        public override bool IsMet(GameConditionContext context)
        {
            bool hasLiteral = CompareTo != null;
            bool hasBinding = !string.IsNullOrWhiteSpace(CompareToBinding);
            if (hasLiteral == hasBinding)
                throw new InvalidOperationException(
                    "EvaluateBinding requires exactly one CompareTo or CompareToBinding."
                );
            if (
                context.Evaluation == null
                || !context.Evaluation.TryGetBindingReference(Binding, out object actual)
            )
                return false;

            object expected;
            if (hasBinding)
            {
                if (!context.Evaluation.TryGetBindingReference(CompareToBinding, out expected))
                    return false;
            }
            else
            {
                if (actual == null)
                    return Comparison == ComparisonOperator.NotEqual;
                expected = ConvertLiteral(actual, CompareTo);
            }

            int comparison = Compare(actual, expected);
            return Comparison switch
            {
                ComparisonOperator.Equal => comparison == 0,
                ComparisonOperator.NotEqual => comparison != 0,
                ComparisonOperator.GreaterThan => comparison > 0,
                ComparisonOperator.GreaterThanOrEqual => comparison >= 0,
                ComparisonOperator.LessThan => comparison < 0,
                ComparisonOperator.LessThanOrEqual => comparison <= 0,
                _ => throw new InvalidOperationException(
                    $"Unsupported binding comparison '{Comparison}'."
                ),
            };
        }

        /// <summary>
        /// Converts an authored literal to the runtime type supplied by the compared binding.
        /// </summary>
        /// <param name="actual">The runtime value that establishes the required type.</param>
        /// <param name="literal">The authored scalar text.</param>
        /// <returns>The converted scalar value.</returns>
        private static object ConvertLiteral(object actual, string literal)
        {
            if (actual == null)
                return null;
            if (actual is bool && bool.TryParse(literal, out bool boolean))
                return boolean;
            if (actual is bool)
                throw new InvalidOperationException($"'{literal}' is not a Boolean value.");
            if (actual is int)
            {
                if (
                    !int.TryParse(
                        literal,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int integer
                    )
                )
                    throw new InvalidOperationException($"'{literal}' is not an integer value.");
                return integer;
            }
            if (actual is double)
            {
                if (
                    !double.TryParse(
                        literal,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double number
                    )
                )
                    throw new InvalidOperationException($"'{literal}' is not a double value.");
                return number;
            }
            if (actual is Enum)
            {
                Type enumType = actual.GetType();
                if (!Enum.GetNames(enumType).Contains(literal))
                    throw new InvalidOperationException(
                        $"'{literal}' is not a valid {enumType.Name} value."
                    );
                return Enum.Parse(enumType, literal, false);
            }
            if (actual is string)
                return literal;
            throw new InvalidOperationException(
                $"Binding values of type '{actual?.GetType().Name ?? "null"}' cannot be compared."
            );
        }

        /// <summary>
        /// Compares two compatible runtime scalar values.
        /// </summary>
        /// <param name="actual">The value exposed by the primary binding.</param>
        /// <param name="expected">The authored or bound comparison value.</param>
        /// <returns>A negative, zero, or positive comparison result.</returns>
        private int Compare(object actual, object expected)
        {
            if (actual == null || expected == null)
            {
                if (IsOrderedComparison())
                    throw new InvalidOperationException(
                        "Null bindings cannot participate in ordered comparisons."
                    );
                return actual == null && expected == null ? 0 : 1;
            }

            if (actual.GetType() != expected.GetType())
                throw new InvalidOperationException(
                    $"Bindings '{Binding}' and '{CompareToBinding}' have incompatible value types '{actual.GetType().Name}' and '{expected.GetType().Name}'."
                );
            if (actual is int integer)
                return integer.CompareTo((int)expected);
            if (actual is double number)
                return number.CompareTo((double)expected);
            if (IsOrderedComparison())
                throw new InvalidOperationException(
                    $"Binding '{Binding}' supports ordered comparisons only for numeric values."
                );
            if (actual is bool boolean)
                return boolean.CompareTo((bool)expected);
            if (actual is string text)
                return string.Compare(text, (string)expected, StringComparison.Ordinal);
            if (actual is Enum)
                return Equals(actual, expected) ? 0 : 1;
            throw new InvalidOperationException(
                $"Binding values of type '{actual.GetType().Name}' cannot be compared."
            );
        }

        /// <summary>
        /// Returns whether the authored operator requires ordered scalar values.
        /// </summary>
        /// <returns>True when the operator compares relative ordering.</returns>
        private bool IsOrderedComparison() =>
            Comparison
                is ComparisonOperator.GreaterThan
                    or ComparisonOperator.GreaterThanOrEqual
                    or ComparisonOperator.LessThan
                    or ComparisonOperator.LessThanOrEqual;
    }

    /// <summary>
    /// Tests whether a bound scene-node collection contains one canonical unit.
    /// </summary>
    [PersistableObject(Name = "BindingIncludesUnit")]
    public sealed class BindingIncludesUnitConditional : GameConditional
    {
        [PersistableAttribute]
        public string Binding { get; set; }

        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        public override bool IsMet(GameConditionContext context)
        {
            if (
                context.Evaluation == null
                || !context.Evaluation.TryGetBindingReference(Binding, out object actual)
                || actual is not IEnumerable values
            )
                return false;

            foreach (object value in values)
            {
                if (
                    value is IGameEntity entity
                    && string.Equals(entity.InstanceID, UnitInstanceID, StringComparison.Ordinal)
                )
                    return true;
            }
            return false;
        }
    }
    #endregion

    #region OfficerConditions
    /// <summary>
    /// Selects one boolean officer state for a data-defined condition.
    /// </summary>
    public abstract class OfficerBooleanConditional : GameConditional
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        public override bool IsMet(GameConditionContext context)
        {
            Officer officer = context.Game.GetSceneNodeByInstanceID<Officer>(
                OfficerInstanceID,
                includeDisabled: true
            );
            return officer != null && Evaluate(officer);
        }

        protected abstract bool Evaluate(Officer officer);
    }

    [PersistableObject(Name = "IsCaptured")]
    public sealed class IsCapturedConditional : OfficerBooleanConditional
    {
        [PersistableAttribute]
        public string CaptorFactionInstanceID { get; set; }

        protected override bool Evaluate(Officer officer) =>
            officer.IsCaptured
            && (
                string.IsNullOrWhiteSpace(CaptorFactionInstanceID)
                || officer.CaptorInstanceID == CaptorFactionInstanceID
            );
    }

    [PersistableObject(Name = "IsKilled")]
    public sealed class IsKilledConditional : OfficerBooleanConditional
    {
        protected override bool Evaluate(Officer officer) => officer.IsKilled;
    }

    [PersistableObject(Name = "IsInjured")]
    public sealed class IsInjuredConditional : OfficerBooleanConditional
    {
        protected override bool Evaluate(Officer officer) => officer.InjuryPoints > 0;
    }

    [PersistableObject(Name = "IsForceEligible")]
    public sealed class IsForceEligibleConditional : OfficerBooleanConditional
    {
        protected override bool Evaluate(Officer officer) => officer.IsForceEligible;
    }

    /// <summary>
    /// Compares one officer's effective Force rank with an authored threshold.
    /// </summary>
    [PersistableObject(Name = "HasForceRank")]
    public sealed class HasForceRankConditional : GameConditional
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public ComparisonOperator Comparison { get; set; }

        [PersistableAttribute]
        public ForceRankLabel Rank { get; set; }

        /// <summary>
        /// Compares the officer's Force rank with the configured rank threshold.
        /// </summary>
        /// <param name="context">The context providing the current game state.</param>
        /// <returns>True when the Force-rank comparison succeeds.</returns>
        public override bool IsMet(GameConditionContext context)
        {
            Officer officer = context.Game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                return false;

            int current = officer.ForceRank;
            int expected = context.Game.GetConfig().Jedi.GetMinimumRank(Rank);
            if (expected == int.MaxValue)
                throw new InvalidOperationException($"Force rank '{Rank}' is not configured.");
            return IntegerComparison.Evaluate(current, Comparison, expected);
        }
    }

    #endregion

    #region SceneConditions
    [PersistableObject(Name = "HasBuildingType")]
    public sealed class HasBuildingTypeConditional : GameConditional
    {
        [PersistableAttribute]
        public BuildingType Type { get; set; }

        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        public override bool IsMet(GameConditionContext context)
        {
            Planet planet = !string.IsNullOrWhiteSpace(PlanetBinding)
                ? context.Evaluation?.GetBindingReference<Planet>(PlanetBinding)
                : context.Game.GetSceneNodeByInstanceID<Planet>(PlanetInstanceID);
            return planet
                    ?.GetChildren<Building>()
                    .Any(building =>
                        building.BuildingType == Type
                        && building.ManufacturingStatus == ManufacturingStatus.Complete
                    ) == true;
        }
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
                : context.Evaluation?.GetBindingReference<Planet>(PlanetBinding);
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
                ? context.Evaluation?.GetBindingReference<Planet>(PlanetBinding)
                : context.Game.GetSceneNodeByInstanceID<Planet>(PlanetInstanceID);
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
        PlanetSector,
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
                SceneAncestorType.PlanetSector => node.GetParentOfType<PlanetSector>(),
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
    /// Tests whether a retained scene node participates in normal gameplay queries.
    /// </summary>
    [PersistableObject(Name = "IsActive")]
    public sealed class IsActiveConditional : GameConditional
    {
        [PersistableAttribute]
        public string NodeInstanceID { get; set; }

        /// <summary>
        /// Checks whether the referenced node is active in its hierarchy.
        /// </summary>
        /// <param name="context">The context providing the current game state.</param>
        /// <returns>True when the node and its ancestors are active.</returns>
        public override bool IsMet(GameConditionContext context)
        {
            ISceneNode node = context.Game.GetSceneNodeByInstanceID<ISceneNode>(
                NodeInstanceID,
                includeDisabled: true
            );
            return node?.IsActive() == true;
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

        /// <summary>
        /// Checks whether the configured unit is contained by the configured location.
        /// </summary>
        /// <param name="context">The context providing the current game state.</param>
        /// <returns>True when the unit is contained by the location.</returns>
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
    #endregion
}
