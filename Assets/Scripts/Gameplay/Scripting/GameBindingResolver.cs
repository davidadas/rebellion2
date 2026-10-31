using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Scripting;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay.Scripting
{
    /// <summary>
    /// Resolves authored values and result arguments into execution bindings.
    /// </summary>
    public static class GameBindingResolver
    {
        /// <summary>
        /// Validates one trigger's argument aliases and returns their declared value types.
        /// </summary>
        /// <param name="trigger">The trigger whose result arguments are exposed.</param>
        /// <param name="sourceDescription">The authored source label used in diagnostics.</param>
        /// <returns>The unique argument aliases and their declared value types.</returns>
        internal static Dictionary<string, Type> ValidateTriggerBindings(
            GameTrigger trigger,
            string sourceDescription
        )
        {
            _ = GameTriggerMatcher.GetResultType(trigger);
            Dictionary<string, Type> triggerBindings = new(StringComparer.Ordinal);
            foreach (GameBinding binding in trigger.Bindings)
            {
                if (
                    string.IsNullOrWhiteSpace(binding.Argument)
                    || string.IsNullOrWhiteSpace(binding.As)
                    || !triggerBindings.TryAdd(
                        binding.As,
                        GameTriggerMatcher.GetBindingType(trigger, binding.Argument)
                    )
                )
                    throw new InvalidOperationException(
                        $"{sourceDescription} has an invalid or duplicate trigger binding alias '{binding.As}'."
                    );
            }
            return triggerBindings;
        }

        /// <summary>
        /// Validates authored value bindings against the aliases already available to this execution.
        /// </summary>
        /// <param name="bindings">The authored bindings in evaluation order.</param>
        /// <param name="aliases">The alias types already supplied by the activation source.</param>
        /// <param name="sourceDescription">The authored source label used in diagnostics.</param>
        internal static void ValidateBindings(
            IEnumerable<GameBinding> bindings,
            Dictionary<string, Type> aliases,
            string sourceDescription
        )
        {
            foreach (GameBinding binding in bindings)
            {
                int bindingModeCount =
                    (binding.Selectors.Count > 0 ? 1 : 0)
                    + (binding.RollInteger != null ? 1 : 0)
                    + (binding.RollDouble != null ? 1 : 0)
                    + (binding.Sources.Count > 0 ? 1 : 0);
                if (bindingModeCount != 1)
                    throw new InvalidOperationException(
                        $"{sourceDescription} binding '{binding.As}' requires exactly one source: From, RollInteger, RollDouble, or a typed value source."
                    );
                if (binding.Sources.Count > 1)
                    throw new InvalidOperationException(
                        $"{sourceDescription} binding '{binding.As}' has multiple typed value sources."
                    );
                if (string.IsNullOrWhiteSpace(binding.As))
                    throw new InvalidOperationException(
                        $"{sourceDescription} has a binding with a missing alias."
                    );

                Type bindingType = GetValueType(binding);
                if (!aliases.TryAdd(binding.As, bindingType))
                    throw new InvalidOperationException(
                        $"{sourceDescription} has duplicate binding alias '{binding.As}'."
                    );
            }
        }

        /// <summary>
        /// Adds the authored result arguments to the rule evaluation context.
        /// </summary>
        /// <param name="definition">The authored trigger or participant filter.</param>
        /// <param name="context">The rule evaluation context receiving the bindings.</param>
        /// <param name="result">The matched simulation result.</param>
        internal static void Bind(
            GameTrigger definition,
            GameEvaluationContext context,
            GameResult result
        )
        {
            foreach (GameBinding binding in definition.Bindings)
            {
                GameTriggerArgument argument = GameTriggerArguments.Get(
                    GameTriggerMatcher.GetResultType(definition),
                    binding.Argument
                );
                context.Bind(binding.As, argument.Resolve(result));
            }
        }

        /// <summary>
        /// Resolves the requested operation.
        /// </summary>
        /// <param name="source">The authored binding definition.</param>
        /// <param name="game">The game.</param>
        /// <param name="context">The context.</param>
        /// <returns>The resolved value.</returns>
        private static object ResolveSource(
            SkillRatingBindingSource source,
            GameRoot game,
            GameEvaluationContext context
        )
        {
            bool hasInstanceID = !string.IsNullOrWhiteSpace(source.OfficerInstanceID);
            bool hasBinding = !string.IsNullOrWhiteSpace(source.OfficerBinding);
            if (hasInstanceID == hasBinding)
                throw new InvalidOperationException(
                    "SkillRating requires exactly one OfficerInstanceID or OfficerBinding."
                );
            Officer officer = hasBinding
                ? context?.GetBindingReference<Officer>(source.OfficerBinding)
                : game.GetSceneNodeByInstanceID<Officer>(
                    source.OfficerInstanceID,
                    includeDisabled: true
                );
            if (officer == null)
                throw new InvalidOperationException("SkillRating could not resolve its officer.");
            return officer.GetEffectiveRating(source.Rating);
        }

        /// <summary>
        /// Resolves the requested operation.
        /// </summary>
        /// <param name="source">The authored binding definition.</param>
        /// <param name="game">The game.</param>
        /// <param name="context">The context.</param>
        /// <returns>The resolved value.</returns>
        private static object ResolveSource(
            OfficerForceBindingSource source,
            GameRoot game,
            GameEvaluationContext context
        )
        {
            bool hasInstanceID = !string.IsNullOrWhiteSpace(source.OfficerInstanceID);
            bool hasBinding = !string.IsNullOrWhiteSpace(source.OfficerBinding);
            if (hasInstanceID == hasBinding)
                throw new InvalidOperationException(
                    "OfficerForce requires exactly one OfficerInstanceID or OfficerBinding."
                );
            Officer officer = hasBinding
                ? context?.GetBindingReference<Officer>(source.OfficerBinding)
                : game.GetSceneNodeByInstanceID<Officer>(
                    source.OfficerInstanceID,
                    includeDisabled: true
                );
            if (officer == null)
                throw new InvalidOperationException("OfficerForce could not resolve its officer.");
            return officer.ForceRank;
        }

        /// <summary>
        /// Resolves the requested operation.
        /// </summary>
        /// <param name="source">The authored binding definition.</param>
        /// <param name="game">The game.</param>
        /// <param name="context">The context.</param>
        /// <returns>The resolved value.</returns>
        private static object ResolveSource(
            PlanetStatBindingSource source,
            GameRoot game,
            GameEvaluationContext context
        )
        {
            bool hasInstanceID = !string.IsNullOrWhiteSpace(source.PlanetInstanceID);
            bool hasBinding = !string.IsNullOrWhiteSpace(source.PlanetBinding);
            if (hasInstanceID == hasBinding)
                throw new InvalidOperationException(
                    "PlanetStat requires exactly one PlanetInstanceID or PlanetBinding."
                );
            Planet planet = hasBinding
                ? context?.GetBindingReference<Planet>(source.PlanetBinding)
                : game.GetSceneNodeByInstanceID<Planet>(
                    source.PlanetInstanceID,
                    includeDisabled: true
                );
            if (planet == null)
                throw new InvalidOperationException("PlanetStat could not resolve its planet.");
            return planet.GetStatValue(source.Stat);
        }

        /// <summary>
        /// Resolves the requested operation.
        /// </summary>
        /// <param name="source">The authored binding definition.</param>
        /// <param name="game">The game.</param>
        /// <param name="provider">The provider.</param>
        /// <param name="context">The context.</param>
        /// <returns>The resolved value.</returns>
        private static object ResolveSource(
            SelectionCountBindingSource source,
            GameRoot game,
            IRandomNumberProvider provider,
            GameEvaluationContext context
        )
        {
            if (source.Selectors.Count == 0)
                throw new InvalidOperationException(
                    "SelectionCount requires at least one selector."
                );
            return source
                .Selectors.SelectMany(selector =>
                    GameSelectorResolver.Select(selector, game, provider, context)
                )
                .Distinct()
                .Count();
        }

        /// <summary>
        /// Gets the value type exposed by the configured binding source.
        /// </summary>
        /// <param name="binding">The authored binding definition.</param>
        /// <returns>The bound value type.</returns>
        internal static Type GetValueType(GameBinding binding)
        {
            if (binding.RollInteger != null)
                return typeof(int);
            if (binding.RollDouble != null)
                return typeof(double);
            if (binding.Sources.Count == 1)
                return GetSourceValueType(binding.Sources[0]);
            return typeof(ISceneNode);
        }

        /// <summary>
        /// Resolves the authored source and stores its value in the evaluation context.
        /// </summary>
        /// <param name="binding">The authored binding definition.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">The random number provider used by selectors and rolls.</param>
        /// <param name="context">The rule evaluation context that receives the binding.</param>
        internal static void Bind(
            GameBinding binding,
            GameRoot game,
            IRandomNumberProvider provider,
            GameEvaluationContext context
        )
        {
            int modeCount =
                (binding.Selectors.Count > 0 ? 1 : 0)
                + (binding.RollInteger != null ? 1 : 0)
                + (binding.RollDouble != null ? 1 : 0)
                + (binding.Sources.Count > 0 ? 1 : 0);
            if (modeCount != 1)
                throw new InvalidOperationException(
                    $"Binding '{binding.As}' requires exactly one From, RollInteger, RollDouble, or typed value source."
                );

            if (binding.RollInteger != null)
            {
                context.Bind(binding.As, Roll(binding.RollInteger, provider));
                return;
            }
            if (binding.RollDouble != null)
            {
                context.Bind(binding.As, Roll(binding.RollDouble, provider));
                return;
            }
            if (binding.Sources.Count > 0)
            {
                if (binding.Sources.Count != 1)
                    throw new InvalidOperationException(
                        $"Binding '{binding.As}' requires exactly one typed value source."
                    );
                context.Bind(
                    binding.As,
                    ResolveSource(binding.Sources[0], game, provider, context)
                );
                return;
            }

            if (binding.Selectors.Count != 1)
                throw new InvalidOperationException(
                    $"Selection binding '{binding.As}' requires exactly one selector."
                );

            ISceneNode[] values = GameSelectorResolver
                .Select(binding.Selectors[0], game, provider, context)
                .Distinct()
                .ToArray();
            if (values.Length != 1)
                throw new InvalidOperationException(
                    $"Selection binding '{binding.As}' must resolve exactly one object but resolved {values.Length}."
                );
            context.Bind(binding.As, values[0]);
        }

        /// <summary>
        /// Rolls one integer inside the authored inclusive range.
        /// </summary>
        /// <param name="roll">The authored range.</param>
        /// <param name="provider">The random-number provider used for the roll.</param>
        /// <returns>An integer from <see cref="RollInteger.Minimum"/> through <see cref="RollInteger.Maximum"/>.</returns>
        internal static int Roll(RollInteger roll, IRandomNumberProvider provider)
        {
            if (roll.Minimum > roll.Maximum)
                throw new InvalidOperationException("RollInteger Minimum cannot exceed Maximum.");

            long valueCount = (long)roll.Maximum - roll.Minimum + 1;
            long offset = (long)Math.Floor(provider.NextDouble() * valueCount);
            return checked((int)(roll.Minimum + offset));
        }

        /// <summary>
        /// Rolls one double inside the authored range.
        /// </summary>
        /// <param name="roll">The authored range.</param>
        /// <param name="provider">The random-number provider used for the roll.</param>
        /// <returns>A double no less than <see cref="RollDouble.Minimum"/> and less than <see cref="RollDouble.Maximum"/>.</returns>
        internal static double Roll(RollDouble roll, IRandomNumberProvider provider)
        {
            if (
                double.IsNaN(roll.Minimum)
                || double.IsInfinity(roll.Minimum)
                || double.IsNaN(roll.Maximum)
                || double.IsInfinity(roll.Maximum)
                || roll.Minimum >= roll.Maximum
            )
                throw new InvalidOperationException(
                    "RollDouble requires finite bounds with Minimum less than Maximum."
                );

            double sample = provider.NextDouble();
            return roll.Minimum * (1 - sample) + roll.Maximum * sample;
        }

        /// <summary>
        /// Gets the scalar type exposed by an authored source.
        /// </summary>
        /// <param name="source">The authored source.</param>
        /// <returns>The source's scalar value type.</returns>
        private static Type GetSourceValueType(GameBindingSource source) =>
            source switch
            {
                SkillRatingBindingSource => typeof(int),
                OfficerForceBindingSource => typeof(int),
                PlanetStatBindingSource => typeof(int),
                SelectionCountBindingSource => typeof(int),
                null => throw new NullReferenceException(),
                _ => throw new InvalidOperationException(
                    $"Unsupported binding source '{source.GetType().Name}'."
                ),
            };

        /// <summary>
        /// Resolves an authored scalar source against the current evaluation.
        /// </summary>
        /// <param name="source">The authored scalar source.</param>
        /// <param name="game">The active game state.</param>
        /// <param name="provider">The random source supplied to selectors.</param>
        /// <param name="context">The current activation bindings.</param>
        /// <returns>The resolved scalar value.</returns>
        private static object ResolveSource(
            GameBindingSource source,
            GameRoot game,
            IRandomNumberProvider provider,
            GameEvaluationContext context
        ) =>
            source switch
            {
                SkillRatingBindingSource definition => ResolveSource(definition, game, context),
                OfficerForceBindingSource definition => ResolveSource(definition, game, context),
                PlanetStatBindingSource definition => ResolveSource(definition, game, context),
                SelectionCountBindingSource definition => ResolveSource(
                    definition,
                    game,
                    provider,
                    context
                ),
                null => throw new NullReferenceException(),
                _ => throw new InvalidOperationException(
                    $"Unsupported binding source '{source.GetType().Name}'."
                ),
            };
    }
}
