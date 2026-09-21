using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    public sealed partial class GameEventExecutor
    {
        /// <summary>Evaluates an authored condition against its activation context.</summary>
        /// <param name="definition">The authored condition to evaluate.</param>
        /// <param name="context">The existing game and activation context.</param>
        /// <returns>Whether the condition matches.</returns>
        public static bool IsMet(GameConditional definition, GameConditionContext context) =>
            definition switch
            {
                AllConditional value => IsMet(value, context),
                AnyConditional value => IsMet(value, context),
                NotConditional value => IsMet(value, context),
                XorConditional value => IsMet(value, context),
                TickCountConditional value => IsMet(value, context),
                HasEventActivatedConditional value => IsMet(value, context),
                IsEventCompleteConditional value => IsMet(value, context),
                EvaluateEventVariableConditional value => IsMet(value, context),
                EvaluateBindingConditional value => IsMet(value, context),
                BindingIncludesUnitConditional value => IsMet(value, context),
                OfficerBooleanConditional value => IsMet(value, context),
                HasForceRankConditional value => IsMet(value, context),
                HasBuildingTypeConditional value => IsMet(value, context),
                IsOwnedConditional value => IsMet(value, context),
                RollAgainstPopularSupportConditional value => IsMet(value, context),
                ShareParentConditional value => IsMet(value, context),
                ShareAncestorConditional value => IsMet(value, context),
                AreOnOpposingFactionsConditional value => IsMet(value, context),
                IsOnMissionConditional value => IsMet(value, context),
                IsActiveConditional value => IsMet(value, context),
                IsInTransitConditional value => IsMet(value, context),
                IsAtLocationConditional value => IsMet(value, context),
                null => throw new NullReferenceException(),
                _ => throw new InvalidOperationException(
                    $"Unsupported condition '{definition.GetType().Name}'."
                ),
            };

        /// <summary>Evaluates an authored condition against the current game.</summary>
        /// <param name="definition">The authored condition to evaluate.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>Whether the condition matches.</returns>
        public static bool IsMet(GameConditional definition, GameRoot game) =>
            IsMet(definition, new GameConditionContext(game));

        /// <summary>Evaluates an authored condition against a triggering result.</summary>
        /// <param name="definition">The authored condition to evaluate.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="triggerResult">The result triggering this evaluation.</param>
        /// <returns>Whether the condition matches.</returns>
        public static bool IsMet(
            GameConditional definition,
            GameRoot game,
            GameResult triggerResult
        ) => IsMet(definition, new GameConditionContext(game, triggerResult));

        /// <summary>Evaluates an authored condition against existing activation bindings.</summary>
        /// <param name="definition">The authored condition to evaluate.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="evaluation">The existing activation bindings.</param>
        /// <returns>Whether the condition matches.</returns>
        internal static bool IsMet(
            GameConditional definition,
            GameRoot game,
            GameEventEvaluationContext evaluation
        ) => IsMet(definition, new GameConditionContext(game, evaluation));

        /// <summary>Reads the officer state selected by an authored condition.</summary>
        /// <param name="definition">The authored officer condition.</param>
        /// <param name="officer">The officer being evaluated.</param>
        /// <returns>Whether the selected state matches.</returns>
        private static bool Evaluate(OfficerBooleanConditional definition, Officer officer) =>
            definition switch
            {
                IsCapturedConditional value => Evaluate(value, officer),
                IsKilledConditional => officer.IsKilled,
                IsInjuredConditional => officer.InjuryPoints > 0,
                IsForceEligibleConditional => officer.IsForceEligible,
                _ => throw new InvalidOperationException(
                    $"Unsupported officer condition '{definition.GetType().Name}'."
                ),
            };

        /// <summary>
        /// Evaluates the AND composition: all child conditions must be met.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The current condition-evaluation context.</param>
        /// <returns>True if every child condition is met; otherwise false.</returns>
        private static bool IsMet(AllConditional definition, GameConditionContext context) =>
            definition.Conditionals.All(conditional => IsMet(conditional, context));

        /// <summary>
        /// Evaluates the OR composition: at least one child condition must be met.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The current condition-evaluation context.</param>
        /// <returns>True if any child condition is met; otherwise false.</returns>
        private static bool IsMet(AnyConditional definition, GameConditionContext context) =>
            definition.Conditionals.Any(conditional => IsMet(conditional, context));

        /// <summary>
        /// Evaluates the NOT composition: no child condition may be met.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The current condition-evaluation context.</param>
        /// <returns>True if every child condition is unmet; otherwise false.</returns>
        private static bool IsMet(NotConditional definition, GameConditionContext context) =>
            definition.Conditionals.All(conditional => !IsMet(conditional, context));

        /// <summary>
        /// Evaluates the XOR composition: exactly one child condition must be met.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The current condition-evaluation context.</param>
        /// <returns>True if precisely one child condition is met; otherwise false.</returns>
        private static bool IsMet(XorConditional definition, GameConditionContext context) =>
            definition.Conditionals.Count(conditional => IsMet(conditional, context)) == 1;

        /// <summary>
        /// Compares the current tick against the authored tick count.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The context providing the current game state.</param>
        /// <returns>True when the tick comparison holds; otherwise false.</returns>
        private static bool IsMet(TickCountConditional definition, GameConditionContext context)
        {
            GameRoot game = context.Game;
            return IntegerComparison.Evaluate(
                game.CurrentTick,
                definition.Comparison,
                definition.Ticks
            );
        }

        /// <summary>
        /// Checks whether the event with the configured instance ID has activated at least once.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The context providing event runtime state.</param>
        /// <returns>True if the event has activated; otherwise false.</returns>
        private static bool IsMet(
            HasEventActivatedConditional definition,
            GameConditionContext context
        )
        {
            return context.Game.EventRuntime.GetState(definition.EventInstanceID).ActivationCount
                > 0;
        }

        /// <summary>
        /// Checks the persisted completion state for the referenced event.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The context providing event runtime state.</param>
        /// <returns>True when the referenced event is permanently complete.</returns>
        private static bool IsMet(
            IsEventCompleteConditional definition,
            GameConditionContext context
        )
        {
            return context.Game.EventRuntime.GetState(definition.EventInstanceID).IsComplete;
        }

        /// <summary>
        /// Compares the current event variable value with the authored integer.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The context providing event runtime state.</param>
        /// <returns>True when the variable comparison succeeds.</returns>
        private static bool IsMet(
            EvaluateEventVariableConditional definition,
            GameConditionContext context
        )
        {
            int current = context.Game.EventRuntime.GetVariable(definition.Key);
            return IntegerComparison.Evaluate(current, definition.Comparison, definition.CompareTo);
        }

        /// <summary>
        /// Checks whether the condition is met.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The context.</param>
        /// <returns>True when the condition is met; otherwise false.</returns>
        private static bool IsMet(
            EvaluateBindingConditional definition,
            GameConditionContext context
        )
        {
            bool hasLiteral = definition.CompareTo != null;
            bool hasBinding = !string.IsNullOrWhiteSpace(definition.CompareToBinding);
            if (hasLiteral == hasBinding)
                throw new InvalidOperationException(
                    "EvaluateBinding requires exactly one CompareTo or CompareToBinding."
                );
            if (
                context.Evaluation == null
                || !context.Evaluation.TryGetBindingReference(definition.Binding, out object actual)
            )
                return false;

            object expected;
            if (hasBinding)
            {
                if (
                    !context.Evaluation.TryGetBindingReference(
                        definition.CompareToBinding,
                        out expected
                    )
                )
                    return false;
            }
            else
            {
                if (actual == null)
                    return definition.Comparison == ComparisonOperator.NotEqual;
                expected = ConvertLiteral(actual, definition.CompareTo);
            }

            int comparison = Compare(definition, actual, expected);
            return definition.Comparison switch
            {
                ComparisonOperator.Equal => comparison == 0,
                ComparisonOperator.NotEqual => comparison != 0,
                ComparisonOperator.GreaterThan => comparison > 0,
                ComparisonOperator.GreaterThanOrEqual => comparison >= 0,
                ComparisonOperator.LessThan => comparison < 0,
                ComparisonOperator.LessThanOrEqual => comparison <= 0,
                _ => throw new InvalidOperationException(
                    $"Unsupported binding comparison '{definition.Comparison}'."
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
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="actual">The value exposed by the primary binding.</param>
        /// <param name="expected">The authored or bound comparison value.</param>
        /// <returns>A negative, zero, or positive comparison result.</returns>
        private static int Compare(
            EvaluateBindingConditional definition,
            object actual,
            object expected
        )
        {
            if (actual == null || expected == null)
            {
                if (IsOrderedComparison(definition))
                    throw new InvalidOperationException(
                        "Null bindings cannot participate in ordered comparisons."
                    );
                return actual == null && expected == null ? 0 : 1;
            }

            if (actual.GetType() != expected.GetType())
                throw new InvalidOperationException(
                    $"Bindings '{definition.Binding}' and '{definition.CompareToBinding}' have incompatible value types '{actual.GetType().Name}' and '{expected.GetType().Name}'."
                );
            if (actual is int integer)
                return integer.CompareTo((int)expected);
            if (actual is double number)
                return number.CompareTo((double)expected);
            if (IsOrderedComparison(definition))
                throw new InvalidOperationException(
                    $"Binding '{definition.Binding}' supports ordered comparisons only for numeric values."
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
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <returns>True when the ordered comparison condition is met; otherwise false.</returns>
        private static bool IsOrderedComparison(EvaluateBindingConditional definition) =>
            definition.Comparison
                is ComparisonOperator.GreaterThan
                    or ComparisonOperator.GreaterThanOrEqual
                    or ComparisonOperator.LessThan
                    or ComparisonOperator.LessThanOrEqual;

        /// <summary>
        /// Checks whether the condition is met.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The context.</param>
        /// <returns>True when the condition is met; otherwise false.</returns>
        private static bool IsMet(
            BindingIncludesUnitConditional definition,
            GameConditionContext context
        )
        {
            if (
                context.Evaluation == null
                || !context.Evaluation.TryGetBindingReference(definition.Binding, out object actual)
                || actual is not IEnumerable values
            )
                return false;

            foreach (object value in values)
            {
                if (
                    value is IGameEntity entity
                    && string.Equals(
                        entity.InstanceID,
                        definition.UnitInstanceID,
                        StringComparison.Ordinal
                    )
                )
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Checks whether the condition is met.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The context.</param>
        /// <returns>True when the condition is met; otherwise false.</returns>
        private static bool IsMet(
            OfficerBooleanConditional definition,
            GameConditionContext context
        )
        {
            Officer officer = context.Game.GetSceneNodeByInstanceID<Officer>(
                definition.OfficerInstanceID,
                includeDisabled: true
            );
            return officer != null && Evaluate(definition, officer);
        }

        /// <summary>
        /// Evaluates the requested operation.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="officer">The officer.</param>
        /// <returns>True when the officer is captured by the configured faction; otherwise false.</returns>
        private static bool Evaluate(IsCapturedConditional definition, Officer officer) =>
            officer.IsCaptured
            && (
                string.IsNullOrWhiteSpace(definition.CaptorFactionInstanceID)
                || officer.CaptorInstanceID == definition.CaptorFactionInstanceID
            );

        /// <summary>
        /// Compares the officer's Force rank with the configured rank threshold.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The context providing the current game state.</param>
        /// <returns>True when the Force-rank comparison succeeds.</returns>
        private static bool IsMet(HasForceRankConditional definition, GameConditionContext context)
        {
            Officer officer = context.Game.GetSceneNodeByInstanceID<Officer>(
                definition.OfficerInstanceID,
                includeDisabled: true
            );
            if (officer == null)
                return false;

            int current = officer.ForceRank;
            int expected = context.Game.GetConfig().Jedi.GetMinimumRank(definition.Rank);
            if (expected == int.MaxValue)
                throw new InvalidOperationException(
                    $"Force rank '{definition.Rank}' is not configured."
                );
            return IntegerComparison.Evaluate(current, definition.Comparison, expected);
        }

        /// <summary>
        /// Checks whether the condition is met.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The context.</param>
        /// <returns>True when the condition is met; otherwise false.</returns>
        private static bool IsMet(
            HasBuildingTypeConditional definition,
            GameConditionContext context
        )
        {
            Planet planet = !string.IsNullOrWhiteSpace(definition.PlanetBinding)
                ? context.Evaluation?.GetBindingReference<Planet>(definition.PlanetBinding)
                : context.Game.GetSceneNodeByInstanceID<Planet>(
                    definition.PlanetInstanceID,
                    includeDisabled: true
                );
            return planet
                    ?.GetChildren<Building>(includeDisabled: true)
                    .Any(building =>
                        building.IsEnabled
                        && building.BuildingType == definition.Type
                        && building.ManufacturingStatus == ManufacturingStatus.Complete
                    ) == true;
        }

        /// <summary>
        /// Checks whether the condition is met.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The context.</param>
        /// <returns>True when the condition is met; otherwise false.</returns>
        private static bool IsMet(IsOwnedConditional definition, GameConditionContext context)
        {
            GameRoot game = context.Game;
            Planet planet = string.IsNullOrWhiteSpace(definition.PlanetBinding)
                ? game.GetSceneNodeByInstanceID<Planet>(
                    definition.PlanetInstanceID,
                    includeDisabled: true
                )
                : context.Evaluation?.GetBindingReference<Planet>(definition.PlanetBinding);
            if (planet?.IsDestroyed != false)
                return false;

            Faction owner = game.GetFactions()
                .FirstOrDefault(faction => faction.InstanceID == planet.OwnerInstanceID);
            return owner != null
                && (
                    string.IsNullOrWhiteSpace(definition.FactionInstanceID)
                    || owner.InstanceID == definition.FactionInstanceID
                );
        }

        /// <summary>
        /// Checks whether the condition is met.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The context.</param>
        /// <returns>True when the condition is met; otherwise false.</returns>
        private static bool IsMet(
            RollAgainstPopularSupportConditional definition,
            GameConditionContext context
        )
        {
            Planet planet = !string.IsNullOrWhiteSpace(definition.PlanetBinding)
                ? context.Evaluation?.GetBindingReference<Planet>(definition.PlanetBinding)
                : context.Game.GetSceneNodeByInstanceID<Planet>(
                    definition.PlanetInstanceID,
                    includeDisabled: true
                );
            if (planet == null || string.IsNullOrWhiteSpace(definition.FactionInstanceID))
                return false;

            int support = planet.GetPopularSupport(definition.FactionInstanceID);
            return context.Random.NextInt(0, 100) < support;
        }

        /// <summary>
        /// Checks whether the condition is met.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The context.</param>
        /// <returns>True when the condition is met; otherwise false.</returns>
        private static bool IsMet(ShareParentConditional definition, GameConditionContext context)
        {
            List<ISceneNode> nodes = ResolveDistinctUnits(definition, context);
            if (nodes == null)
                return false;
            ISceneNode parent = nodes[0].GetParent();
            return parent != null && nodes.All(node => ReferenceEquals(node.GetParent(), parent));
        }

        /// <summary>
        /// Resolves distinct units.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The context.</param>
        /// <returns>The resolved distinct units.</returns>
        private static List<ISceneNode> ResolveDistinctUnits(
            ShareParentConditional definition,
            GameConditionContext context
        ) => SceneConditionUnits.ResolveDistinct(context.Game, definition.Units);

        /// <summary>
        /// Checks whether the condition is met.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The context.</param>
        /// <returns>True when the condition is met; otherwise false.</returns>
        private static bool IsMet(ShareAncestorConditional definition, GameConditionContext context)
        {
            List<ISceneNode> nodes = SceneConditionUnits.ResolveDistinct(
                context.Game,
                definition.Units
            );
            if (nodes == null)
                return false;
            List<ISceneNode> ancestors = nodes.ConvertAll(node =>
                SceneAncestors.Resolve(node, definition.Type)
            );
            return ancestors[0] != null
                && ancestors.All(ancestor => ReferenceEquals(ancestor, ancestors[0]));
        }

        /// <summary>
        /// Checks whether the two referenced units belong to different owners.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The context used to resolve unit references.</param>
        /// <returns>True if exactly two units are referenced and their owner instance IDs differ.</returns>
        private static bool IsMet(
            AreOnOpposingFactionsConditional definition,
            GameConditionContext context
        )
        {
            GameRoot game = context.Game;
            // Get the scene nodes for the units.
            List<ISceneNode> sceneNodes = definition
                .UnitInstanceIDs.Select(id =>
                    game.GetSceneNodeByInstanceID<ISceneNode>(id, includeDisabled: true)
                )
                .Where(node => node != null)
                .ToList();

            // Check if the units are on opposing factions.
            return sceneNodes.Count == 2
                && sceneNodes[0].OwnerInstanceID != sceneNodes[1].OwnerInstanceID;
        }

        /// <summary>
        /// Checks whether the referenced unit is parented to a <see cref="Mission"/> node.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The context used to resolve the unit.</param>
        /// <returns>True if the unit exists and its direct parent is a mission; otherwise false.</returns>
        private static bool IsMet(IsOnMissionConditional definition, GameConditionContext context)
        {
            ISceneNode sceneNode = context.Game.GetSceneNodeByInstanceID<ISceneNode>(
                definition.UnitInstanceID,
                includeDisabled: true
            );
            // Check if the unit is on a mission.
            return sceneNode?.GetParent() is Mission;
        }

        /// <summary>
        /// Checks whether the referenced node is active in its hierarchy.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The context providing the current game state.</param>
        /// <returns>True when the node and its ancestors are active.</returns>
        private static bool IsMet(IsActiveConditional definition, GameConditionContext context)
        {
            ISceneNode node = context.Game.GetSceneNodeByInstanceID<ISceneNode>(
                definition.NodeInstanceID,
                includeDisabled: true
            );
            return node?.IsActive() == true;
        }

        /// <summary>
        /// Checks whether the condition is met.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The context.</param>
        /// <returns>True when the condition is met; otherwise false.</returns>
        private static bool IsMet(
            IsInTransitConditional definition,
            GameConditionContext context
        ) =>
            context.Game.GetSceneNodeByInstanceID<ISceneNode>(
                definition.UnitInstanceID,
                includeDisabled: true
            )
                is IMovable { Movement: not null };

        /// <summary>
        /// Checks whether the configured unit is contained by the configured location.
        /// </summary>
        /// <param name="definition">The authored condition being evaluated.</param>
        /// <param name="context">The context providing the current game state.</param>
        /// <returns>True when the unit is contained by the location.</returns>
        private static bool IsMet(IsAtLocationConditional definition, GameConditionContext context)
        {
            GameRoot game = context.Game;
            ISceneNode unit = game.GetSceneNodeByInstanceID<ISceneNode>(
                definition.UnitInstanceID,
                includeDisabled: true
            );
            ISceneNode location = game.GetSceneNodeByInstanceID<ISceneNode>(
                definition.LocationInstanceID,
                includeDisabled: true
            );
            for (ISceneNode current = unit; current != null; current = current.GetParent())
            {
                if (current == location)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Applies the shared authored comparison vocabulary to integer values.
    /// </summary>
    internal static class IntegerComparison
    {
        /// <summary>
        /// Compares an actual integer with an expected integer using the selected operator.
        /// </summary>
        /// <param name="actual">The actual.</param>
        /// <param name="operation">The operation.</param>
        /// <param name="expected">The expected.</param>
        /// <returns>True when the comparison between the actual and expected values holds.</returns>
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

    internal static class SceneConditionUnits
    {
        /// <summary>
        /// Resolves distinct.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="references">The references.</param>
        /// <returns>The resolved distinct.</returns>
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
            List<ISceneNode> nodes = ids.Select(id =>
                    game.GetSceneNodeByInstanceID<ISceneNode>(id, includeDisabled: true)
                )
                .Where(node => node != null)
                .ToList();
            return nodes.Count == ids.Count ? nodes : null;
        }
    }
}
