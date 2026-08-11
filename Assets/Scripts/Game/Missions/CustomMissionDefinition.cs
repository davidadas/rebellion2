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
        public string OwnerFactionInstanceID { get; set; }
        public string AuthorityUnitInstanceID { get; set; }
        public string FollowUpMissionDefinitionID { get; set; }

        public string CaptorFactionInstanceID { get; set; }
        public bool TargetCanEscape { get; set; }
        public int AttackRating { get; set; }
        public OfficerRating ResistanceRating { get; set; } = OfficerRating.Combat;
        public string ProbabilityTableKey { get; set; }

        public int RatingDivisor { get; set; } = 1;
        public int SuccessCombatBonus { get; set; }
        public int SuccessEspionageBonus { get; set; }
        public bool CaptureRescuerOnFailure { get; set; }
        public bool FailedRescuerCanEscape { get; set; }

        public string CaptiveFactionInstanceID { get; set; }
        public bool CaptivesCanEscapeAfterPickup { get; set; }

        public int VictoryForceRank { get; set; }
        public int MinimumFailureInjury { get; set; }
        public int MaximumFailureInjury { get; set; }
        public bool CaptivesCanEscapeOnVictory { get; set; }
    }

    /// <summary>
    /// References one concrete unit assigned to a content-authored mission.
    /// </summary>
    [PersistableObject(Name = "Participant")]
    public sealed class MissionUnitReference
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }
    }
}
