using System;
using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// A content-authored capture attempt whose lifecycle is persisted as a normal mission.
    /// </summary>
    [PersistableObject(Name = "StoryCaptureMission")]
    public sealed class StoryCaptureMission : Mission
    {
        public const string MissionTypeID = "StoryCapture";

        public string TargetOfficerInstanceID { get; set; }
        public int DurationTicks { get; set; }
        public string CaptorFactionInstanceID { get; set; }
        public bool TargetCanEscape { get; set; }
        public string SourceEventInstanceID { get; set; }

        public StoryCaptureMission()
        {
            ConfigKey = MissionTypeID;
            DisplayName = "Capture";
            ParticipantRating = OfficerRating.None;
        }

        public StoryCaptureMission(
            Officer target,
            int durationTicks,
            string captorFactionInstanceId,
            bool targetCanEscape,
            string displayName,
            string sourceEventInstanceId
        )
            : base(
                MissionTypeID,
                target?.OwnerInstanceID ?? throw new ArgumentNullException(nameof(target)),
                target.GetParentOfType<Planet>()?.InstanceID,
                new List<IMissionParticipant> { target },
                new List<IMissionParticipant>(),
                OfficerRating.None,
                displayName
            )
        {
            if (durationTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(durationTicks));

            TargetOfficerInstanceID = target.InstanceID;
            DurationTicks = durationTicks;
            CaptorFactionInstanceID = captorFactionInstanceId;
            TargetCanEscape = targetCanEscape;
            SourceEventInstanceID = sourceEventInstanceId;
        }

        public override bool ShouldRepeatAfterCompletion(GameRoot game) => false;

        internal override bool AppliesFoiledParticipantConsequences => false;

        /// <summary>
        /// Story captures are authored attacks, not player missions that local defenses can foil.
        /// </summary>
        protected override double GetFoilProbability(double defenseScore, GameRoot game) => 0;

        internal override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            Planet location = GetParent() as Planet;
            if (target?.IsKilled != false || target?.IsCaptured != false)
            {
                return new List<GameResult>
                {
                    new StoryCaptureResolvedResult
                    {
                        Target = target,
                        Location = location,
                        WasCaptured = false,
                        Tick = game.CurrentTick,
                        SourceEventInstanceID = SourceEventInstanceID,
                    },
                    BuildCompletedResult(
                        MissionOutcome.Failed,
                        MissionCompletionReason.TargetUnavailable,
                        game
                    ),
                };
            }

            target.IsCaptured = true;
            target.CaptorInstanceID = CaptorFactionInstanceID;
            target.CanEscape = TargetCanEscape;

            OfficerCaptureStateResult captureResult = new OfficerCaptureStateResult
            {
                TargetOfficer = target,
                IsCaptured = true,
                Context = location,
                Tick = game.CurrentTick,
                SourceEventInstanceID = SourceEventInstanceID,
            };
            MissionCompletedResult completed = BuildCompletedResult(MissionOutcome.Success, game);
            completed.SourceEventInstanceID = SourceEventInstanceID;
            return new List<GameResult>
            {
                captureResult,
                new StoryCaptureResolvedResult
                {
                    Target = target,
                    Location = location,
                    WasCaptured = true,
                    Tick = game.CurrentTick,
                    SourceEventInstanceID = SourceEventInstanceID,
                },
                completed,
            };
        }
    }
}
