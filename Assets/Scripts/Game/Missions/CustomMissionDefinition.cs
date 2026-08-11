using System;
using System.Collections.Generic;
using System.Linq;
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
        public int Revision { get; set; } = 1;
        public bool CanCancel { get; set; }
        public MissionDuration Duration { get; set; }
        public string OwnerFactionInstanceID { get; set; }
        public MissionSuccessRule Success { get; set; }

        public void EnsureValid()
        {
            int durationModes =
                (Duration?.Fixed == null ? 0 : 1) + (Duration?.Random == null ? 0 : 1);
            if (durationModes != 1)
                throw new InvalidOperationException(
                    $"Mission definition '{InstanceID}' requires exactly one duration."
                );
            if (Duration?.Fixed?.Ticks < 0)
                throw new InvalidOperationException(
                    $"Mission definition '{InstanceID}' has a negative fixed duration."
                );
            if (
                Duration.Random != null
                && (
                    Duration.Random.MinimumTicks < 0
                    || Duration.Random.MaximumTicks < Duration.Random.MinimumTicks
                )
            )
                throw new InvalidOperationException(
                    $"Mission definition '{InstanceID}' has an invalid random duration."
                );

            int successModes =
                (Success?.Automatic == null ? 0 : 1)
                + (Success?.Chance == null ? 0 : 1)
                + (Success?.Opposed == null ? 0 : 1);
            if (successModes != 1)
                throw new InvalidOperationException(
                    $"Mission definition '{InstanceID}' requires exactly one success rule."
                );
            if (Success.Chance != null)
            {
                if (Success.Chance.BasePercent < 0 || Success.Chance.BasePercent > 100)
                    throw new InvalidOperationException(
                        $"Mission definition '{InstanceID}' has an invalid base chance."
                    );
                if (
                    Success.Chance.Ratings.Any(rating =>
                        rating.Rating == OfficerRating.None
                        || rating.Divisor <= 0
                        || rating.ParticipantIndex < 0
                    )
                )
                    throw new InvalidOperationException(
                        $"Mission definition '{InstanceID}' has an invalid rating contribution."
                    );
            }
            if (
                Success.Opposed != null
                && (
                    Success.Opposed.TargetRating == OfficerRating.None
                    || string.IsNullOrWhiteSpace(Success.Opposed.ProbabilityTableKey)
                )
            )
                throw new InvalidOperationException(
                    $"Mission definition '{InstanceID}' has an invalid opposed rule."
                );
        }
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
