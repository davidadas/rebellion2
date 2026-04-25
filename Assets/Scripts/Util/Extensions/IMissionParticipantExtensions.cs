using Rebellion.Game;

namespace Rebellion.Util.Extensions
{
    public static class IMissionParticipantExtensions
    {
        /// <summary>
        /// Reads a skill value from a mission participant's skill map.
        /// </summary>
        /// <param name="participant">The participant whose skills to query.</param>
        /// <param name="skill">The skill to look up.</param>
        /// <returns>The stored skill value.</returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">
        /// Thrown when the participant has no entry for the requested skill.
        /// </exception>
        public static int GetMissionSkillValue(
            this IMissionParticipant participant,
            MissionParticipantSkill skill
        ) => participant.Skills[skill];
    }
}
