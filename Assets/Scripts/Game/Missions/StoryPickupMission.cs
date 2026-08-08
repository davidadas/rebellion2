using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// A content-authored collector mission that transfers matching prisoners to its owner.
    /// </summary>
    [PersistableObject(Name = "StoryPickupMission")]
    public sealed class StoryPickupMission : Mission
    {
        public const string MissionTypeID = "StoryPickup";

        public string CollectorOfficerInstanceID { get; set; }
        public string CaptiveFactionInstanceID { get; set; }
        public int DurationTicks { get; set; }
        public bool CaptivesCanEscapeAfterPickup { get; set; }

        [PersistableIgnore]
        private readonly List<string> _prisonerInstanceIDs = new List<string>();

        public StoryPickupMission()
        {
            ConfigKey = MissionTypeID;
            DisplayName = "Prisoner Pickup";
            ParticipantRating = OfficerRating.None;
        }

        public StoryPickupMission(
            Officer collector,
            Planet location,
            string captiveFactionInstanceId,
            int durationTicks,
            bool captivesCanEscapeAfterPickup,
            string displayName,
            string sourceEventInstanceId
        )
            : base(
                MissionTypeID,
                collector?.OwnerInstanceID ?? throw new ArgumentNullException(nameof(collector)),
                location?.InstanceID ?? throw new ArgumentNullException(nameof(location)),
                new List<IMissionParticipant> { collector },
                new List<IMissionParticipant>(),
                OfficerRating.None,
                displayName
            )
        {
            if (string.IsNullOrWhiteSpace(captiveFactionInstanceId))
                throw new ArgumentException(
                    "Captive faction instance ID is required.",
                    nameof(captiveFactionInstanceId)
                );
            if (durationTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(durationTicks));

            CollectorOfficerInstanceID = collector.InstanceID;
            CaptiveFactionInstanceID = captiveFactionInstanceId;
            DurationTicks = durationTicks;
            CaptivesCanEscapeAfterPickup = captivesCanEscapeAfterPickup;
            SourceEventInstanceID = sourceEventInstanceId;
        }

        public override bool ShouldRepeatAfterCompletion(GameRoot game) => false;

        internal override bool AppliesFoiledParticipantConsequences => false;

        protected override double GetFoilProbability(double defenseScore, GameRoot game) => 0;

        public override MissionCompletionReason? GetAbortReason(GameRoot game)
        {
            MissionCompletionReason? reason = base.GetAbortReason(game);
            if (reason.HasValue)
                return reason;

            return GetEligiblePrisoners().Any() ? null : MissionCompletionReason.TargetUnavailable;
        }

        internal override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            Officer collector = game.GetSceneNodeByInstanceID<Officer>(CollectorOfficerInstanceID);
            List<Officer> prisoners = GetEligiblePrisoners().ToList();
            if (collector == null || prisoners.Count == 0)
            {
                return new List<GameResult>
                {
                    Stamp(
                        BuildCompletedResult(
                            MissionOutcome.Failed,
                            MissionCompletionReason.TargetUnavailable,
                            game
                        )
                    ),
                };
            }

            foreach (Officer prisoner in prisoners)
            {
                prisoner.CaptorInstanceID = OwnerInstanceID;
                prisoner.CanEscape = CaptivesCanEscapeAfterPickup;
                _prisonerInstanceIDs.Add(prisoner.InstanceID);
            }

            Planet location = GetParent() as Planet;
            return new List<GameResult>
            {
                Stamp(
                    new OfficerPickupResult
                    {
                        Officer = collector,
                        InProgress = false,
                        Tick = game.CurrentTick,
                    }
                ),
                Stamp(
                    new StoryPickupCompletedResult
                    {
                        Collector = collector,
                        Location = location,
                        Prisoners = prisoners,
                        Tick = game.CurrentTick,
                    }
                ),
                Stamp(BuildCompletedResult(MissionOutcome.Success, game)),
            };
        }

        internal override IEnumerable<IMovable> GetSuccessfulReturnPassengers(GameRoot game)
        {
            foreach (string prisonerId in _prisonerInstanceIDs)
            {
                Officer prisoner = game.GetSceneNodeByInstanceID<Officer>(prisonerId);
                if (prisoner?.IsCaptured == true && prisoner.CaptorInstanceID == OwnerInstanceID)
                    yield return prisoner;
            }
        }

        private IEnumerable<Officer> GetEligiblePrisoners()
        {
            Planet location = GetParent() as Planet;
            return location
                    ?.GetAllOfficers()
                    .Where(officer =>
                        officer.IsCaptured
                        && !officer.IsKilled
                        && officer.OwnerInstanceID == CaptiveFactionInstanceID
                    )
                ?? Enumerable.Empty<Officer>();
        }

        private T Stamp<T>(T result)
            where T : GameResult
        {
            result.SourceEventInstanceID = SourceEventInstanceID;
            return result;
        }
    }
}
