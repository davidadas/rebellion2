using System;
using System.Collections.Generic;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Changes personnel lifecycle state after validating the requested operation.
    /// </summary>
    public sealed class PersonnelCommands
    {
        private readonly PersonnelQueries _queries;

        /// <summary>
        /// Creates personnel commands using the active game's eligibility rules.
        /// </summary>
        /// <param name="queries">The personnel queries for the active game.</param>
        public PersonnelCommands(PersonnelQueries queries)
        {
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        }

        /// <summary>
        /// Marks an officer as killed and disables it while preserving historical references.
        /// </summary>
        /// <param name="officer">The officer to kill and retain.</param>
        public void KillOfficer(Officer officer)
        {
            if (officer == null)
                throw new ArgumentNullException(nameof(officer));

            officer.IsEnabled = false;
            officer.IsKilled = true;
            officer.Movement = null;
        }

        /// <summary>
        /// Retires an entire validated personnel selection.
        /// </summary>
        /// <param name="personnel">The personnel or their snapshots.</param>
        /// <param name="ownerInstanceId">The faction authorized to retire the personnel.</param>
        /// <returns>True when every selected person was retired.</returns>
        public bool Retire(IReadOnlyList<ISceneNode> personnel, string ownerInstanceId)
        {
            if (
                !_queries.TryResolveRetirementSelection(
                    personnel,
                    ownerInstanceId,
                    out List<ISceneNode> live
                )
            )
                return false;

            foreach (ISceneNode person in live)
            {
                person.IsEnabled = false;
                if (person is Officer officer)
                    officer.IsRetired = true;
                else if (person is SpecialForces specialForces)
                    specialForces.IsRetired = true;
            }

            return true;
        }
    }
}
