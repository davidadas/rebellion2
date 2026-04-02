using Rebellion.Game;

namespace Rebellion.Util.Extensions
{
    public static class IMissionParticipantExtensions
    {
        public static int GetMissionSkillValue(
            this IMissionParticipant participant,
            MissionParticipantSkill skill
        ) => participant.Skills[skill];
    }
}
