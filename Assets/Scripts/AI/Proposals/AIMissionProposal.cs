using System;
using System.Collections.Generic;
using Rebellion.AI.Director;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Proposal to start a mission for one participant.
    /// </summary>
    public sealed class AIMissionProposal : AIProposal
    {
        /// <summary>
        /// Participant assigned to the mission.
        /// </summary>
        public IMissionParticipant Participant { get; }

        /// <summary>
        /// Mission type ID to start.
        /// </summary>
        public string MissionTypeID { get; }

        /// <summary>
        /// Planet targeted by the mission.
        /// </summary>
        public Planet TargetPlanet { get; }

        /// <summary>
        /// Officer targeted by the mission.
        /// </summary>
        public Officer TargetOfficer { get; }

        /// <summary>
        /// Research discipline advanced by the mission.
        /// </summary>
        public ResearchDiscipline? Discipline { get; }

        /// <summary>
        /// Creates a mission proposal.
        /// </summary>
        /// <param name="participant">Participant assigned to the mission.</param>
        /// <param name="missionTypeId">Mission type ID to start.</param>
        /// <param name="targetPlanet">Planet targeted by the mission.</param>
        public AIMissionProposal(
            IMissionParticipant participant,
            string missionTypeId,
            Planet targetPlanet
        )
            : this(participant, missionTypeId, targetPlanet, null, null) { }

        /// <summary>
        /// Creates a mission proposal with an officer target.
        /// </summary>
        /// <param name="participant">Participant assigned to the mission.</param>
        /// <param name="missionTypeId">Mission type ID to start.</param>
        /// <param name="targetPlanet">Planet targeted by the mission.</param>
        /// <param name="targetOfficer">Officer targeted by the mission.</param>
        public AIMissionProposal(
            IMissionParticipant participant,
            string missionTypeId,
            Planet targetPlanet,
            Officer targetOfficer
        )
            : this(participant, missionTypeId, targetPlanet, targetOfficer, null) { }

        /// <summary>
        /// Creates a research mission proposal.
        /// </summary>
        /// <param name="officer">Officer assigned to the research mission.</param>
        /// <param name="missionTypeId">Mission type ID to start.</param>
        /// <param name="targetPlanet">Planet targeted by the mission.</param>
        /// <param name="discipline">Research discipline advanced by the mission.</param>
        public AIMissionProposal(
            Officer officer,
            string missionTypeId,
            Planet targetPlanet,
            ResearchDiscipline discipline
        )
            : this(officer, missionTypeId, targetPlanet, null, discipline) { }

        /// <summary>
        /// Creates a mission proposal.
        /// </summary>
        /// <param name="participant">Participant assigned to the mission.</param>
        /// <param name="missionTypeId">Mission type ID to start.</param>
        /// <param name="targetPlanet">Planet targeted by the mission.</param>
        /// <param name="targetOfficer">Officer targeted by the mission.</param>
        /// <param name="discipline">Research discipline advanced by the mission.</param>
        private AIMissionProposal(
            IMissionParticipant participant,
            string missionTypeId,
            Planet targetPlanet,
            Officer targetOfficer,
            ResearchDiscipline? discipline
        )
        {
            Participant = participant;
            MissionTypeID = missionTypeId;
            TargetPlanet = targetPlanet;
            TargetOfficer = targetOfficer;
            Discipline = discipline;
        }

        /// <summary>
        /// Returns claims used to avoid selecting incompatible mission proposals.
        /// </summary>
        /// <returns>Claim keys for this proposal.</returns>
        public override IReadOnlyList<string> GetClaimKeys()
        {
            List<string> claimKeys = new List<string> { $"mission:actor:{Participant.InstanceID}" };

            AddMissionSpecificClaims(claimKeys);

            return claimKeys;
        }

        /// <summary>
        /// Adds claims that are specific to this mission target.
        /// </summary>
        /// <param name="claimKeys">The claim list to update.</param>
        private void AddMissionSpecificClaims(List<string> claimKeys)
        {
            if (MissionTypeID == RecruitmentMission.MissionTypeID)
            {
                claimKeys.Add($"mission:recruitment:{Participant.OwnerInstanceID}");
                return;
            }

            if (MissionTypeID == ResearchMission.MissionTypeID && Discipline.HasValue)
            {
                claimKeys.Add($"mission:research:{Participant.OwnerInstanceID}:{Discipline.Value}");
                return;
            }

            if (TargetOfficer != null)
            {
                claimKeys.Add($"mission:officer:{TargetOfficer.InstanceID}");
                return;
            }

            claimKeys.Add($"mission:{MissionTypeID}:planet:{TargetPlanet.InstanceID}");
        }

        /// <summary>
        /// Returns a stable sort key for mission selection.
        /// </summary>
        /// <returns>A stable sort key.</returns>
        public override string GetSortKey()
        {
            return string.Join(
                ":",
                "mission",
                MissionTypeID,
                TargetPlanet?.InstanceID,
                TargetOfficer?.InstanceID,
                Discipline?.ToString(),
                Participant?.InstanceID
            );
        }

        /// <summary>
        /// Returns whether this mission proposal may be selected.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if this mission proposal may be selected.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            return IsStillValid();
        }

        /// <summary>
        /// Returns whether this mission can still be created.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if this mission can still be created.</returns>
        public override bool CanExecute(AITurnContext context)
        {
            if (context?.Missions == null || !IsStillValid())
                return false;

            return context.Missions.CanCreateMission(
                new MissionStartRequest
                {
                    MissionTypeID = MissionTypeID,
                    Target = TargetPlanet,
                    TargetOfficer = TargetOfficer,
                    Discipline = Discipline,
                    MainParticipants = new List<IMissionParticipant> { Participant },
                }
            );
        }

        /// <summary>
        /// Starts the mission if it still passes validation.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context))
                return;

            try
            {
                context.Missions.InitiateMission(
                    new MissionStartRequest
                    {
                        MissionTypeID = MissionTypeID,
                        Target = TargetPlanet,
                        TargetOfficer = TargetOfficer,
                        Discipline = Discipline,
                        MainParticipants = new List<IMissionParticipant> { Participant },
                    }
                );
            }
            catch (InvalidOperationException)
            {
                return;
            }
        }

        /// <summary>
        /// Returns whether the proposal's actors and targets are still usable.
        /// </summary>
        /// <returns>True if the proposal is still valid.</returns>
        private bool IsStillValid()
        {
            if (Participant == null || TargetPlanet == null)
                return false;

            if (!Participant.IsMovable() || Participant.IsOnMission())
                return false;

            if (Participant is Officer officer && (officer.IsCaptured || officer.IsKilled))
                return false;

            if (
                Participant is SpecialForces specialForces
                && specialForces.ManufacturingStatus != ManufacturingStatus.Complete
            )
                return false;

            if (!Participant.CanPerformMission(MissionTypeID))
                return false;

            if (MissionTypeID == ResearchMission.MissionTypeID && !Discipline.HasValue)
                return false;

            if (RequiresTargetOfficer() && TargetOfficer == null)
                return false;

            return TargetOfficer == null || !TargetOfficer.IsCaptured && !TargetOfficer.IsKilled;
        }

        /// <summary>
        /// Returns whether this proposal requires an officer target.
        /// </summary>
        /// <returns>True if this mission requires an officer target.</returns>
        private bool RequiresTargetOfficer()
        {
            return MissionTypeID == AbductionMission.MissionTypeID
                || MissionTypeID == AssassinationMission.MissionTypeID;
        }
    }
}
