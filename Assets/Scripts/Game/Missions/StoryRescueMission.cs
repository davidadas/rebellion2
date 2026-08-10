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
    /// A content-authored rescue whose success and failure consequences remain save-game state.
    /// </summary>
    [PersistableObject(Name = "StoryRescueMission")]
    public sealed class StoryRescueMission : Mission
    {
        public const string MissionTypeID = "StoryRescue";

        [PersistableIgnore]
        private readonly List<string> _releasedOfficerInstanceIDs = new List<string>();

        // Story State.
        public string CaptiveOfficerInstanceID { get; set; }
        public string RescuerOfficerInstanceID { get; set; }
        public int DurationTicks { get; set; }
        public int DurationRandomTicks { get; set; }
        public int RatingDivisor { get; set; } = 1;
        public int SuccessCombatBonus { get; set; }
        public int SuccessEspionageBonus { get; set; }
        public bool CaptureRescuerOnFailure { get; set; }
        public bool FailedRescuerCanEscape { get; set; }

        /// <summary>
        /// Creates an empty story-rescue mission for deserialization.
        /// </summary>
        public StoryRescueMission()
        {
            ConfigKey = MissionTypeID;
            DisplayName = "Rescue";
            ParticipantRating = OfficerRating.None;
        }

        /// <summary>
        /// Creates a rescue mission with content-authored timing and consequences.
        /// </summary>
        /// <param name="captive">The officer whose location anchors the rescue.</param>
        /// <param name="rescuer">The officer performing the rescue.</param>
        /// <param name="durationTicks">The fixed mission duration.</param>
        /// <param name="durationRandomTicks">The exclusive upper bound of added random duration.</param>
        /// <param name="ratingDivisor">The divisor applied to each contributing rating.</param>
        /// <param name="successCombatBonus">The combat increase awarded on success.</param>
        /// <param name="successEspionageBonus">The espionage increase awarded on success.</param>
        /// <param name="captureRescuerOnFailure">Whether failure captures the rescuer.</param>
        /// <param name="failedRescuerCanEscape">Whether a captured rescuer may escape.</param>
        /// <param name="displayName">The player-facing mission name.</param>
        /// <param name="sourceEventInstanceId">The event that started this mission.</param>
        public StoryRescueMission(
            Officer captive,
            Officer rescuer,
            int durationTicks,
            int durationRandomTicks,
            int ratingDivisor,
            int successCombatBonus,
            int successEspionageBonus,
            bool captureRescuerOnFailure,
            bool failedRescuerCanEscape,
            string displayName,
            string sourceEventInstanceId
        )
            : base(
                MissionTypeID,
                rescuer?.OwnerInstanceID ?? throw new ArgumentNullException(nameof(rescuer)),
                captive?.GetParentOfType<Planet>()?.InstanceID
                    ?? throw new ArgumentNullException(nameof(captive)),
                new List<IMissionParticipant> { rescuer },
                new List<IMissionParticipant>(),
                OfficerRating.None,
                displayName
            )
        {
            if (durationTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(durationTicks));
            if (durationRandomTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(durationRandomTicks));
            if (ratingDivisor < 1)
                throw new ArgumentOutOfRangeException(nameof(ratingDivisor));
            if (successCombatBonus < 0)
                throw new ArgumentOutOfRangeException(nameof(successCombatBonus));
            if (successEspionageBonus < 0)
                throw new ArgumentOutOfRangeException(nameof(successEspionageBonus));

            CaptiveOfficerInstanceID = captive.InstanceID;
            RescuerOfficerInstanceID = rescuer.InstanceID;
            DurationTicks = durationTicks;
            DurationRandomTicks = durationRandomTicks;
            RatingDivisor = ratingDivisor;
            SuccessCombatBonus = successCombatBonus;
            SuccessEspionageBonus = successEspionageBonus;
            CaptureRescuerOnFailure = captureRescuerOnFailure;
            FailedRescuerCanEscape = failedRescuerCanEscape;
            SourceEventInstanceID = sourceEventInstanceId;
        }

        /// <inheritdoc />
        public override bool ShouldRepeatAfterCompletion(GameRoot game) => false;

        /// <inheritdoc />
        internal override bool AppliesFoiledParticipantConsequences => false;

        /// <inheritdoc />
        protected override double GetFoilProbability(double defenseScore, GameRoot game) => 0;

        /// <inheritdoc />
        public override MissionCompletionReason? GetAbortReason(GameRoot game)
        {
            MissionCompletionReason? reason = base.GetAbortReason(game);
            if (reason.HasValue)
                return reason;

            Officer captive = game.GetSceneNodeByInstanceID<Officer>(CaptiveOfficerInstanceID);
            return captive?.IsCaptured == true && captive.GetParentOfType<Planet>() == GetParent()
                ? null
                : MissionCompletionReason.TargetUnavailable;
        }

        /// <inheritdoc />
        internal override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            Officer rescuer = game.GetSceneNodeByInstanceID<Officer>(RescuerOfficerInstanceID);
            Officer captive = game.GetSceneNodeByInstanceID<Officer>(CaptiveOfficerInstanceID);
            if (rescuer == null || captive?.IsCaptured != true)
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

            int successPercent =
                rescuer.GetEffectiveRating(OfficerRating.Combat) / RatingDivisor
                + rescuer.GetEffectiveRating(OfficerRating.Espionage) / RatingDivisor;
            bool success = provider.NextDouble() * 100 < successPercent;
            List<GameResult> results = success
                ? ResolveSuccess(game, rescuer)
                : ResolveFailure(game, rescuer);
            results.Add(
                Stamp(
                    BuildCompletedResult(
                        success ? MissionOutcome.Success : MissionOutcome.Failed,
                        game
                    )
                )
            );
            return results;
        }

        /// <inheritdoc />
        internal override IEnumerable<IMovable> GetSuccessfulReturnPassengers(GameRoot game)
        {
            foreach (string officerId in _releasedOfficerInstanceIDs)
            {
                Officer officer = game.GetSceneNodeByInstanceID<Officer>(officerId);
                if (officer?.IsCaptured == false)
                    yield return officer;
            }
        }

        /// <summary>
        /// Applies rescue rewards and releases every matching prisoner at the location.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="rescuer">The successful rescuer.</param>
        /// <returns>The capture-state and rescue results.</returns>
        private List<GameResult> ResolveSuccess(GameRoot game, Officer rescuer)
        {
            rescuer.IncrementBaseRating(OfficerRating.Combat, SuccessCombatBonus);
            rescuer.IncrementBaseRating(OfficerRating.Espionage, SuccessEspionageBonus);

            Planet location = GetParent() as Planet;
            List<GameResult> results = new List<GameResult>();
            foreach (
                Officer officer in location
                    ?.GetAllOfficers()
                    .Where(officer =>
                        officer.IsCaptured && officer.OwnerInstanceID == OwnerInstanceID
                    )
                    .ToList()
                    ?? new List<Officer>()
            )
            {
                officer.IsCaptured = false;
                officer.CaptorInstanceID = null;
                officer.CanEscape = false;
                _releasedOfficerInstanceIDs.Add(officer.InstanceID);
                results.Add(
                    Stamp(
                        new OfficerCaptureStateResult
                        {
                            TargetOfficer = officer,
                            IsCaptured = false,
                            Context = location,
                            Tick = game.CurrentTick,
                        }
                    )
                );
                results.Add(
                    Stamp(
                        new OfficerRescuedResult
                        {
                            Officer = officer,
                            RescuingFaction = game.GetFactionByOwnerInstanceID(OwnerInstanceID),
                            Location = location,
                            Tick = game.CurrentTick,
                        }
                    )
                );
            }
            return results;
        }

        /// <summary>
        /// Applies the authored failure consequence to the rescuer.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="rescuer">The unsuccessful rescuer.</param>
        /// <returns>The capture result, or an empty collection.</returns>
        private List<GameResult> ResolveFailure(GameRoot game, Officer rescuer)
        {
            if (!CaptureRescuerOnFailure)
                return new List<GameResult>();

            rescuer.IsCaptured = true;
            rescuer.CaptorInstanceID = null;
            rescuer.CanEscape = FailedRescuerCanEscape;
            return new List<GameResult>
            {
                Stamp(
                    new OfficerCaptureStateResult
                    {
                        TargetOfficer = rescuer,
                        IsCaptured = true,
                        Context = GetParent() as Planet,
                        Tick = game.CurrentTick,
                    }
                ),
            };
        }

        /// <summary>
        /// Copies this mission's event provenance to a result.
        /// </summary>
        /// <typeparam name="T">The emitted result type.</typeparam>
        /// <param name="result">The result to stamp.</param>
        /// <returns>The stamped result.</returns>
        private T Stamp<T>(T result)
            where T : GameResult
        {
            result.SourceEventInstanceID = SourceEventInstanceID;
            return result;
        }
    }
}
