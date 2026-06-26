using System;
using Rebellion.Game.Research;

namespace Rebellion.Game.Missions
{
    public sealed class MissionOption
    {
        /// <summary>
        /// Creates a selectable mission option.
        /// </summary>
        /// <param name="missionTypeID">The mission type ID used to start the mission.</param>
        /// <param name="displayName">The display name for this option.</param>
        /// <param name="participantRating">The rating primary participants use for the mission.</param>
        /// <param name="decoyParticipantRating">The rating decoy participants use for the mission.</param>
        /// <param name="discipline">The research discipline used by research options.</param>
        public MissionOption(
            string missionTypeID,
            string displayName,
            OfficerRating participantRating,
            OfficerRating decoyParticipantRating = OfficerRating.None,
            ResearchDiscipline? discipline = null
        )
        {
            if (string.IsNullOrEmpty(missionTypeID))
                throw new ArgumentException("Mission type ID is required.", nameof(missionTypeID));

            MissionTypeID = missionTypeID;
            DisplayName = displayName ?? missionTypeID;
            ParticipantRating = participantRating;
            DecoyParticipantRating = decoyParticipantRating;
            Discipline = discipline;
        }

        public string MissionTypeID { get; }
        public string DisplayName { get; }
        public OfficerRating ParticipantRating { get; }
        public OfficerRating DecoyParticipantRating { get; }
        public ResearchDiscipline? Discipline { get; }
    }
}
