using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Extensions;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Proposal to start a mission for a participant team.
    /// </summary>
    public sealed class AIMissionProposal : AIProposal
    {
        // Participants.
        public IReadOnlyList<IMissionParticipant> Participants { get; }

        public IReadOnlyList<IMissionParticipant> MainParticipants { get; }

        public IReadOnlyList<IMissionParticipant> DecoyParticipants { get; }

        public IMissionParticipant Participant => MainParticipants.FirstOrDefault();

        // Mission Definition.
        public string MissionTypeID { get; }
        public Planet TargetPlanet { get; }
        public Officer TargetOfficer { get; }

        public ISceneNode SelectedTarget { get; }

        public ResearchDiscipline? Discipline { get; }

        /// <summary>
        /// Creates a mission proposal.
        /// </summary>
        /// <param name="mainParticipants">Participants assigned to execute the mission.</param>
        /// <param name="missionTypeId">Mission type ID to start.</param>
        /// <param name="targetPlanet">Planet targeted by the mission.</param>
        /// <param name="selectedTarget">Object selected inside the mission planet.</param>
        /// <param name="targetOfficer">Officer targeted by the mission.</param>
        /// <param name="discipline">Research discipline advanced by the mission.</param>
        /// <param name="decoyParticipants">Participants assigned to distract mission defenders.</param>
        public AIMissionProposal(
            IEnumerable<IMissionParticipant> mainParticipants,
            string missionTypeId,
            Planet targetPlanet,
            ISceneNode selectedTarget = null,
            Officer targetOfficer = null,
            ResearchDiscipline? discipline = null,
            IEnumerable<IMissionParticipant> decoyParticipants = null
        )
        {
            MainParticipants =
                mainParticipants?.Where(participant => participant != null).ToList()
                ?? new List<IMissionParticipant>();
            DecoyParticipants =
                decoyParticipants?.Where(participant => participant != null).ToList()
                ?? new List<IMissionParticipant>();
            Participants = MainParticipants.Concat(DecoyParticipants).ToList();
            MissionTypeID = missionTypeId;
            TargetPlanet = targetPlanet;
            SelectedTarget = selectedTarget;
            TargetOfficer = targetOfficer;
            Discipline = discipline;
        }

        /// <summary>
        /// Returns claims used to avoid selecting incompatible mission proposals.
        /// </summary>
        /// <returns>Claim keys for this proposal.</returns>
        public override IReadOnlyList<string> GetClaimKeys()
        {
            List<string> claimKeys = Participants
                .Select(participant => AIClaimKeys.MissionActor(participant.InstanceID))
                .ToList();

            AddMissionSpecificClaims(claimKeys);

            return claimKeys;
        }

        /// <summary>
        /// Adds claims that are specific to this mission target.
        /// </summary>
        /// <param name="claimKeys">The claim list to update.</param>
        private void AddMissionSpecificClaims(List<string> claimKeys)
        {
            if (MissionTypeID == MissionTypeIDs.Recruitment)
            {
                claimKeys.Add(AIClaimKeys.MissionRecruitment(Participant.OwnerInstanceID));
                return;
            }

            if (MissionTypeID == MissionTypeIDs.Research && Discipline.HasValue)
            {
                claimKeys.Add(
                    AIClaimKeys.MissionResearch(Participant.OwnerInstanceID, Discipline.Value)
                );
                return;
            }

            if (TargetOfficer != null)
            {
                claimKeys.Add(AIClaimKeys.MissionOfficer(TargetOfficer.InstanceID));
                return;
            }

            if (SelectedTarget != null)
            {
                claimKeys.Add(AIClaimKeys.MissionTarget(SelectedTarget.InstanceID));
                return;
            }

            claimKeys.Add(AIClaimKeys.MissionAtPlanet(MissionTypeID, TargetPlanet.InstanceID));
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
                SelectedTarget?.InstanceID,
                Discipline?.ToString(),
                string.Join(",", MainParticipants.Select(participant => participant.InstanceID)),
                string.Join(",", DecoyParticipants.Select(participant => participant.InstanceID))
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

            return context.Missions.CanCreateMission(CreateRequest());
        }

        /// <summary>
        /// Starts the mission if it still passes validation.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context))
                return;

            context.Missions.InitiateMission(CreateRequest());
        }

        /// <summary>
        /// Returns whether the proposal's actors and targets are still usable.
        /// </summary>
        /// <returns>True if the proposal is still valid.</returns>
        private bool IsStillValid()
        {
            if (MainParticipants.Count == 0 || TargetPlanet == null)
                return false;

            foreach (IMissionParticipant participant in Participants)
            {
                if (!IsParticipantAvailable(participant))
                    return false;
            }

            if (MissionTypeID == MissionTypeIDs.Research && !Discipline.HasValue)
                return false;

            if (RequiresTargetOfficer() && TargetOfficer == null)
                return false;

            if (MissionTypeID == MissionTypeIDs.Sabotage && SelectedTarget == null)
                return false;

            return IsTargetOfficerAvailable();
        }

        /// <summary>
        /// Returns whether this proposal requires an officer target.
        /// </summary>
        /// <returns>True if this mission requires an officer target.</returns>
        private bool RequiresTargetOfficer()
        {
            return MissionTypeID == MissionTypeIDs.Abduction
                || MissionTypeID == MissionTypeIDs.Assassination
                || MissionTypeID == MissionTypeIDs.Rescue;
        }

        /// <summary>Creates the mission-start request represented by the proposal.</summary>
        /// <returns>The mission-start request.</returns>
        internal MissionStartRequest CreateRequest()
        {
            return new MissionStartRequest
            {
                MissionTypeID = MissionTypeID,
                Location = TargetPlanet,
                SelectedTarget = SelectedTarget,
                Discipline = Discipline,
                MainParticipants = MainParticipants.ToList(),
                DecoyParticipants = DecoyParticipants.ToList(),
            };
        }

        /// <summary>
        /// Returns whether participant available.
        /// </summary>
        /// <param name="participant">The mission participant.</param>
        /// <returns>True when the condition is satisfied.</returns>
        private bool IsParticipantAvailable(IMissionParticipant participant)
        {
            if (
                participant?.IsMovable() != true
                || participant.IsOnMission()
                || participant.GetTransitMovement() != null
            )
                return false;

            if (participant is Officer officer && (officer.IsCaptured || officer.IsKilled))
                return false;

            if (
                participant is SpecialForces specialForces
                && specialForces.ManufacturingStatus != ManufacturingStatus.Complete
            )
                return false;

            return participant.CanPerformMission(MissionTypeID);
        }

        /// <summary>
        /// Returns whether target officer available.
        /// </summary>
        /// <returns>True when the condition is satisfied.</returns>
        private bool IsTargetOfficerAvailable()
        {
            if (TargetOfficer == null)
                return true;

            if (TargetOfficer.IsKilled)
                return false;

            return MissionTypeID == MissionTypeIDs.Rescue
                ? TargetOfficer.IsCaptured
                : !TargetOfficer.IsCaptured;
        }
    }
}
