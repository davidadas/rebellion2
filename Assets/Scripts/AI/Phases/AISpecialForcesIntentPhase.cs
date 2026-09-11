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

            IReadOnlyList<IMissionParticipant> availableParticipants =
                context.Assessment.AvailableMissionParticipants;
            List<SpecialForces> availableSpecialForces = availableParticipants
                .OfType<SpecialForces>()
                .ToList();
            AssignIntent(context, availableSpecialForces, SpecialForcesIntent.PrimaryAgent);

            List<Officer> availableOfficers = availableParticipants.OfType<Officer>().ToList();
            if (availableOfficers.Count == 0)
                return;

            AssignOfficerReplaceableUnitsAsDecoys(
                context,
                availableSpecialForces,
                availableOfficers
            );
        }

        /// <summary>
        /// Reserves every special-forces role whose mission capabilities are fully covered by
        /// available officers.
        /// </summary>
        private static void AssignOfficerReplaceableUnitsAsDecoys(
            AITurnContext context,
            IEnumerable<SpecialForces> specialForces,
            IReadOnlyCollection<Officer> officers
        )
        {
            foreach (
                IGrouping<string, SpecialForces> roleUnits in specialForces.GroupBy(
                    GetMissionCapabilitySetKey,
                    StringComparer.Ordinal
                )
            )
            {
                if (OfficersCoverEveryMissionCapability(officers, roleUnits.First()))
                    AssignIntent(context, roleUnits, SpecialForcesIntent.Decoy);
            }
        }

        /// <summary>Assigns one turn-scoped intent to each supplied special-forces unit.</summary>
        private static void AssignIntent(
            AITurnContext context,
            IEnumerable<SpecialForces> units,
            SpecialForcesIntent intent
        )
        {
            foreach (SpecialForces unit in units)
                context.SetSpecialForcesIntent(unit, intent);
        }

        /// <summary>
        /// Returns whether available officers can replace every capability in a special-forces role.
        /// </summary>
        /// <param name="officers">The officers available during this turn.</param>
        /// <param name="specialForces">A representative unit for the special-forces role.</param>
        /// <returns>True when every role mission can be performed by an officer.</returns>
        private static bool OfficersCoverEveryMissionCapability(
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
        private static string GetMissionCapabilitySetKey(SpecialForces unit)
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
