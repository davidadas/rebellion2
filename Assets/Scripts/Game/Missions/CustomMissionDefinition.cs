using System.Collections.Generic;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Missions
{
    [PersistableObject]
    public sealed class FixedMissionDuration
    {
        [PersistableAttribute]
        public int Ticks { get; set; }
    }

    [PersistableObject]
    public sealed class RandomMissionDuration
    {
        [PersistableAttribute]
        public int MinimumTicks { get; set; }

        [PersistableAttribute]
        public int MaximumTicks { get; set; }
    }

    [PersistableObject]
    public sealed class MissionDuration
    {
        public FixedMissionDuration Fixed { get; set; }
        public RandomMissionDuration Random { get; set; }
    }

    [PersistableObject]
    public sealed class AutomaticMissionSuccess { }

    [PersistableObject(Name = "Rating")]
    public sealed class MissionRatingContribution
    {
        [PersistableAttribute]
        public OfficerRating Rating { get; set; }

        [PersistableAttribute]
        public int Divisor { get; set; } = 1;

        [PersistableAttribute]
        public int ParticipantIndex { get; set; }
    }

    [PersistableObject]
    public sealed class ChanceMissionSuccess
    {
        [PersistableAttribute]
        public int BasePercent { get; set; }

        public List<MissionRatingContribution> Ratings { get; set; } =
            new List<MissionRatingContribution>();
    }

    [PersistableObject]
    public sealed class OpposedMissionSuccess
    {
        [PersistableAttribute]
        public int AttackRating { get; set; }

        [PersistableAttribute]
        public OfficerRating TargetRating { get; set; }

        [PersistableAttribute]
        public string ProbabilityTableKey { get; set; }
    }

    [PersistableObject]
    public sealed class MissionSuccessRule
    {
        public AutomaticMissionSuccess Automatic { get; set; }
        public ChanceMissionSuccess Chance { get; set; }
        public OpposedMissionSuccess Opposed { get; set; }
    }

    /// <summary>
    /// Defines one reusable, content-authored mission lifecycle.
    /// </summary>
    [PersistableObject(Name = "MissionDefinition")]
    public sealed class CustomMissionDefinition : BaseGameEntity
    {
        public bool CanCancel { get; set; }
        public MissionDuration Duration { get; set; }
        public string OwnerFactionInstanceID { get; set; }
        public MissionSuccessRule Success { get; set; }
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
