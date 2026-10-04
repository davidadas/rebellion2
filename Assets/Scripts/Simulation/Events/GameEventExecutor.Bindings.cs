using System;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;

namespace Rebellion.Simulation
{
    public partial class GameEventExecutor
    {
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
            GameEventEvaluationContext context
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
            GameEventEvaluationContext context
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
            GameEventEvaluationContext context
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
            GameEventEvaluationContext context
        )
        {
            if (source.Selectors.Count == 0)
                throw new InvalidOperationException(
                    "SelectionCount requires at least one selector."
                );
            return source
                .Selectors.SelectMany(selector =>
                    GameEventExecutor.Select(selector, game, provider, context)
                )
                .Distinct()
                .Count();
        }

        /// <summary>
        /// Gets the value type exposed by the configured binding source.
        /// </summary>
        /// <param name="binding">The authored binding definition.</param>
        /// <returns>The bound value type.</returns>
        private static Type GetValueType(GameEventBinding binding)
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
        /// <param name="context">The event evaluation context that receives the binding.</param>
        internal static void Bind(
            GameEventBinding binding,
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventEvaluationContext context
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

            ISceneNode[] values = GameEventExecutor
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

        /// <summary>Gets the scalar type exposed by an authored source.</summary>
        /// <param name="source">The authored source.</param>
        /// <returns>The source's scalar value type.</returns>
        private static Type GetSourceValueType(GameEventBindingSource source) =>
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

        /// <summary>Resolves an authored scalar source against the current evaluation.</summary>
        /// <param name="source">The authored scalar source.</param>
        /// <param name="game">The active game state.</param>
        /// <param name="provider">The random source supplied to selectors.</param>
        /// <param name="context">The current activation bindings.</param>
        /// <returns>The resolved scalar value.</returns>
        private static object ResolveSource(
            GameEventBindingSource source,
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventEvaluationContext context
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
