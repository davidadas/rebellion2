using System;
using Rebellion.Game.Scripting;

namespace Rebellion.Gameplay.Scripting
{
    /// <summary>
    /// Resolves the mutually exclusive numeric value forms supported by event actions.
    /// </summary>
    internal static class GameActionNumericValue
    {
        /// <summary>
        /// Resolves exactly one fixed, bound, or rolled integer.
        /// </summary>
        /// <param name="value">The fixed value, when authored.</param>
        /// <param name="binding">The event binding reference, when authored.</param>
        /// <param name="roll">The integer roll, when authored.</param>
        /// <param name="context">The current action execution context.</param>
        /// <param name="actionName">The XML action name used in validation errors.</param>
        /// <param name="valueName">The XML value name used in validation errors.</param>
        /// <returns>The resolved integer.</returns>
        internal static int ResolveInteger(
            int? value,
            string binding,
            RollInteger roll,
            GameActionContext context,
            string actionName,
            string valueName
        )
        {
            int modeCount =
                (value.HasValue ? 1 : 0)
                + (!string.IsNullOrWhiteSpace(binding) ? 1 : 0)
                + (roll != null ? 1 : 0);
            if (modeCount != 1)
                throw new InvalidOperationException(
                    $"{actionName} requires exactly one {valueName}, {valueName}Binding, or RollInteger."
                );
            if (!string.IsNullOrWhiteSpace(binding))
            {
                if (context.Evaluation?.TryGetBindingReference(binding, out int boundValue) != true)
                    throw new InvalidOperationException(
                        $"{actionName} could not resolve integer binding '{binding}'."
                    );
                return boundValue;
            }
            return value ?? GameBindingResolver.Roll(roll, context.Random);
        }

        /// <summary>
        /// Resolves exactly one fixed, bound, or rolled double.
        /// </summary>
        /// <param name="value">The fixed value, when authored.</param>
        /// <param name="binding">The event binding reference, when authored.</param>
        /// <param name="roll">The double roll, when authored.</param>
        /// <param name="context">The current action execution context.</param>
        /// <param name="actionName">The XML action name used in validation errors.</param>
        /// <param name="valueName">The XML value name used in validation errors.</param>
        /// <returns>The resolved double.</returns>
        internal static double ResolveDouble(
            double? value,
            string binding,
            RollDouble roll,
            GameActionContext context,
            string actionName,
            string valueName
        )
        {
            int modeCount =
                (value.HasValue ? 1 : 0)
                + (!string.IsNullOrWhiteSpace(binding) ? 1 : 0)
                + (roll != null ? 1 : 0);
            if (modeCount != 1)
                throw new InvalidOperationException(
                    $"{actionName} requires exactly one {valueName}, {valueName}Binding, or RollDouble."
                );
            if (!string.IsNullOrWhiteSpace(binding))
            {
                if (
                    context.Evaluation?.TryGetBindingReference(binding, out double boundValue)
                    != true
                )
                    throw new InvalidOperationException(
                        $"{actionName} could not resolve double binding '{binding}'."
                    );
                return boundValue;
            }
            return value ?? GameBindingResolver.Roll(roll, context.Random);
        }
    }
}
