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

        // Story State.
        public string TargetOfficerInstanceID { get; set; }
        public int DurationTicks { get; set; }
        public string CaptorFactionInstanceID { get; set; }
        public bool TargetCanEscape { get; set; }
        public int AttackRating { get; set; }
        public OfficerRating ResistanceRating { get; set; } = OfficerRating.Combat;
        public string ProbabilityTableKey { get; set; } = AbductionMission.MissionTypeID;

        /// <summary>
        /// Creates an empty story-capture mission for deserialization.
        /// </summary>
        public StoryCaptureMission()
        {
            ConfigKey = MissionTypeID;
            DisplayName = "Capture";
            ParticipantRating = OfficerRating.None;
        }

        /// <summary>
        /// Creates a content-authored attempt to capture an officer.
        /// </summary>
        /// <param name="target">The officer targeted for capture.</param>
        /// <param name="durationTicks">The mission duration.</param>
        /// <param name="captorFactionInstanceId">The faction credited with a successful capture.</param>
        /// <param name="targetCanEscape">Whether the captive may escape.</param>
        /// <param name="attackRating">The authored attack strength.</param>
        /// <param name="resistanceRating">The target rating used for resistance.</param>
        /// <param name="probabilityTableKey">The probability table used to resolve the attempt.</param>
        /// <param name="displayName">The player-facing mission name.</param>
        /// <param name="sourceEventInstanceId">The event that started this mission.</param>
        public StoryCaptureMission(
            Officer target,
            int durationTicks,
            string captorFactionInstanceId,
            bool targetCanEscape,
            int attackRating,
            OfficerRating resistanceRating,
            string probabilityTableKey,
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
            if (string.IsNullOrWhiteSpace(probabilityTableKey))
                throw new ArgumentException(
                    "A probability-table key is required.",
                    nameof(probabilityTableKey)
                );

            TargetOfficerInstanceID = target.InstanceID;
            DurationTicks = durationTicks;
            CaptorFactionInstanceID = captorFactionInstanceId;
            TargetCanEscape = targetCanEscape;
            AttackRating = attackRating;
            ResistanceRating = resistanceRating;
            ProbabilityTableKey = probabilityTableKey;
            SourceEventInstanceID = sourceEventInstanceId;
        }

        /// <inheritdoc />
        public override bool ShouldRepeatAfterCompletion(GameRoot game) => false;

        /// <inheritdoc />
        internal override bool AppliesFoiledParticipantConsequences => false;

        /// <summary>
        /// Story captures are authored attacks, not player missions that local defenses can foil.
        /// </summary>
        /// <param name="defenseScore">The local mission-defense score.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>Always zero.</returns>
        protected override double GetFoilProbability(double defenseScore, GameRoot game) => 0;

        /// <inheritdoc />
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

            int resistance = target.GetEffectiveRating(ResistanceRating);
            double captureProbability = LookupSuccessProbability(
                game,
                AttackRating - resistance,
                ProbabilityTableKey
            );
            if (!IsSuccessfulProbabilityRoll(provider.NextDouble() * 100, captureProbability))
            {
                MissionCompletedResult failed = BuildCompletedResult(MissionOutcome.Failed, game);
                failed.SourceEventInstanceID = SourceEventInstanceID;
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
                    failed,
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
