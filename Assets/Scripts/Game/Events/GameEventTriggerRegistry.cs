using System;
using System.Collections.Generic;
using Rebellion.Game.Results;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Defines stable content-facing identifiers for simulation result types.
    /// </summary>
    public static class GameEventTriggerRegistry
    {
        private static readonly Dictionary<string, Type> TypesById = new Dictionary<string, Type>(
            StringComparer.Ordinal
        )
        {
            ["core:dagobah.completed"] = typeof(DagobahCompletedResult),
            ["core:force.discovered"] = typeof(ForceDiscoveryResult),
            ["core:mission.completed"] = typeof(MissionCompletedResult),
            ["core:officer.capture-changed"] = typeof(OfficerCaptureStateResult),
            ["core:officer.encountered"] = typeof(OfficerEncounterResult),
            ["core:officer.capture-attempted"] = typeof(OfficerCaptureAttemptResult),
            ["core:force-confrontation.completed"] = typeof(ForceConfrontationCompletedResult),
            ["core:prisoner-pickup.completed"] = typeof(PrisonerPickupCompletedResult),
            ["core:unit.arrived"] = typeof(UnitArrivedResult),
        };

        /// <summary>
        /// Returns whether a stable trigger identifier is registered.
        /// </summary>
        /// <param name="triggerId">The content-facing trigger identifier.</param>
        /// <returns>True when the identifier maps to a result type.</returns>
        public static bool IsKnown(string triggerId) =>
            !string.IsNullOrWhiteSpace(triggerId) && TypesById.ContainsKey(triggerId);

        /// <summary>
        /// Returns whether a result satisfies a stable trigger identifier.
        /// </summary>
        /// <param name="triggerId">The content-facing trigger identifier.</param>
        /// <param name="result">The result to inspect.</param>
        /// <returns>True when the result has the registered type.</returns>
        public static bool Matches(string triggerId, GameResult result) =>
            result != null
            && TypesById.TryGetValue(triggerId, out Type resultType)
            && resultType.IsInstanceOfType(result);

        /// <summary>
        /// Matches the legacy CLR result-type trigger retained for save compatibility.
        /// </summary>
        /// <param name="typeName">The authored CLR type name.</param>
        /// <param name="result">The result to inspect.</param>
        /// <returns>True when the result type name matches exactly.</returns>
        public static bool MatchesLegacyTypeName(string typeName, GameResult result) =>
            result != null
            && string.Equals(typeName, result.GetType().Name, StringComparison.Ordinal);
    }
}
