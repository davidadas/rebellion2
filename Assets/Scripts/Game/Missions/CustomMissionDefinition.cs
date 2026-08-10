using System.Collections.Generic;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Missions
{
    public enum CustomMissionResolution
    {
        OfficerCapture,
        OfficerRescue,
        PrisonerPickup,
        ForceConfrontation,
    }

    public enum CustomMissionPhase
    {
        Resolve,
        GatherTarget,
        EscortToDestination,
    }

    /// <summary>
    /// Defines one reusable, content-authored mission lifecycle.
    /// </summary>
    [PersistableObject(Name = "MissionDefinition")]
    public sealed class CustomMissionDefinition : BaseGameEntity
    {
        public bool CanAbort { get; set; }
        public int DurationTicks { get; set; }
        public int DurationRandomTicks { get; set; }
        public CustomMissionResolution Resolution { get; set; }
        public CustomMissionPhase Phase { get; set; }
        public string OwnerRole { get; set; }
        public string LocationRole { get; set; }
        public string TrackedLocationRole { get; set; }
        public string FollowUpMissionDefinitionID { get; set; }
        public List<string> ParticipantRoles { get; set; } = new List<string>();

        public string TargetRole { get; set; }
        public string CaptorFactionInstanceID { get; set; }
        public bool TargetCanEscape { get; set; }
        public int AttackRating { get; set; }
        public OfficerRating ResistanceRating { get; set; } = OfficerRating.Combat;
        public string ProbabilityTableKey { get; set; }

        public string CaptiveRole { get; set; }
        public string RescuerRole { get; set; }
        public int RatingDivisor { get; set; } = 1;
        public int SuccessCombatBonus { get; set; }
        public int SuccessEspionageBonus { get; set; }
        public bool CaptureRescuerOnFailure { get; set; }
        public bool FailedRescuerCanEscape { get; set; }

        public string CollectorRole { get; set; }
        public string CaptiveFactionInstanceID { get; set; }
        public bool CaptivesCanEscapeAfterPickup { get; set; }

        public string SubjectRole { get; set; }
        public string OpponentRole { get; set; }
        public string AuthorityRole { get; set; }
        public int VictoryForceRank { get; set; }
        public int MinimumFailureInjury { get; set; }
        public int MaximumFailureInjury { get; set; }
        public bool CaptivesCanEscapeOnVictory { get; set; }
    }

    /// <summary>
    /// Binds a semantic mission role to one concrete unit instance.
    /// </summary>
    [PersistableObject(Name = "Role")]
    public sealed class MissionRoleAssignment
    {
        [PersistableAttribute]
        public string Name { get; set; }

        [PersistableAttribute]
        public string UnitInstanceID { get; set; }
    }
}
