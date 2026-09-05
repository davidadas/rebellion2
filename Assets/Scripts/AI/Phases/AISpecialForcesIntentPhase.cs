using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Assigns turn-scoped primary and decoy roles to available special-forces units.
    /// </summary>
    public sealed class AISpecialForcesIntentPhase : IAITurnPhase
    {
        /// <summary>
        /// Reserves configured specialists as decoys when officers can perform the same work.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public void Execute(AITurnContext context)
        {
            if (context?.Assessment == null)
                return;

            IReadOnlyList<IMissionParticipant> participants = context
                .Assessment
                .AvailableMissionParticipants;
            List<Officer> officers = participants.OfType<Officer>().ToList();
            List<SpecialForces> specialForces = participants.OfType<SpecialForces>().ToList();
            foreach (SpecialForces unit in specialForces)
                context.SetSpecialForcesIntent(unit, SpecialForcesIntent.PrimaryAgent);

            if (officers.Count == 0)
                return;

            foreach (
                IGrouping<string, SpecialForces> role in specialForces.GroupBy(
                    GetRoleId,
                    StringComparer.Ordinal
                )
            )
            {
                if (!CanOfficersPerformRole(officers, role.First()))
                    continue;

                foreach (
                    SpecialForces decoy in role.OrderByDescending(unit =>
                            unit.GetEffectiveRating(OfficerRating.Espionage)
                        )
                        .ThenBy(unit => unit.InstanceID, StringComparer.Ordinal)
                )
                {
                    context.SetSpecialForcesIntent(decoy, SpecialForcesIntent.Decoy);
                }
            }
        }

        /// <summary>
        /// Returns whether available officers can replace every capability in a special-forces role.
        /// </summary>
        /// <param name="officers">The officers available during this turn.</param>
        /// <param name="specialForces">A representative unit for the special-forces role.</param>
        /// <returns>True when every role mission can be performed by an officer.</returns>
        private static bool CanOfficersPerformRole(
            IEnumerable<Officer> officers,
            SpecialForces specialForces
        )
        {
            return specialForces.AllowedMissionTypeIDs.Count > 0
                && specialForces.AllowedMissionTypeIDs.All(missionTypeId =>
                    officers.Any(officer => officer.CanPerformMission(missionTypeId))
                );
        }

        /// <summary>
        /// Returns a stable identifier for a special-forces mission role.
        /// </summary>
        /// <param name="unit">The special-forces unit to inspect.</param>
        /// <returns>The ordered mission-capability identifier.</returns>
        private static string GetRoleId(SpecialForces unit)
        {
            return string.Join(
                "|",
                unit.AllowedMissionTypeIDs.OrderBy(
                    missionTypeId => missionTypeId,
                    StringComparer.Ordinal
                )
            );
        }
    }
}
