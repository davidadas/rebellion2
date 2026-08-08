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
    /// A content-authored, guaranteed training journey for a single Force user.
    /// Story identity and presentation remain in game-events.xml; this class owns mission lifecycle invariants.
    /// </summary>
    [PersistableObject(Name = "ScriptedTrainingMission")]
    public sealed class ScriptedTrainingMission : Mission
    {
        public const string MissionTypeID = "ScriptedTraining";

        public string TraineeInstanceID { get; set; }
        public int DurationTicks { get; set; }
        public int CompletionBonusPercent { get; set; }
        public string CompletionVariableKey { get; set; }
        public int CompletionVariableValue { get; set; } = 1;

        public ScriptedTrainingMission()
        {
            ConfigKey = MissionTypeID;
            DisplayName = "Training";
        }

        public ScriptedTrainingMission(
            Officer trainee,
            int durationTicks,
            int completionBonusPercent,
            string completionVariableKey,
            int completionVariableValue,
            string displayName
        )
            : base(
                MissionTypeID,
                trainee?.OwnerInstanceID ?? throw new ArgumentNullException(nameof(trainee)),
                trainee.GetParentOfType<Planet>()?.InstanceID,
                new List<IMissionParticipant> { trainee },
                new List<IMissionParticipant>(),
                OfficerRating.None,
                displayName
            )
        {
            if (durationTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(durationTicks));
            if (completionBonusPercent < 0)
                throw new ArgumentOutOfRangeException(nameof(completionBonusPercent));
            if (string.IsNullOrWhiteSpace(completionVariableKey))
                throw new ArgumentException(
                    "Completion variable key is required.",
                    nameof(completionVariableKey)
                );

            TraineeInstanceID = trainee.InstanceID;
            DurationTicks = durationTicks;
            CompletionBonusPercent = completionBonusPercent;
            CompletionVariableKey = completionVariableKey;
            CompletionVariableValue = completionVariableValue;
        }

        public override bool ShouldRepeatAfterCompletion(GameRoot game) => false;

        internal override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            Officer trainee =
                MainParticipants.Find(participant => participant.InstanceID == TraineeInstanceID)
                as Officer;
            if (trainee == null)
                return new List<GameResult>
                {
                    BuildCompletedResult(
                        MissionOutcome.Failed,
                        MissionCompletionReason.TargetUnavailable,
                        game
                    ),
                };

            int previousRank = trainee.ForceRank;
            int bonus = previousRank * CompletionBonusPercent / 100;
            trainee.ForceValue += bonus;
            int previousVariable = game.GetEventVariable(CompletionVariableKey);
            game.SetEventVariable(CompletionVariableKey, CompletionVariableValue);

            return new List<GameResult>
            {
                new ForceExperienceResult
                {
                    Officer = trainee,
                    ExperienceGained = bonus,
                    PreviousForceRank = previousRank,
                    CurrentForceRank = trainee.ForceRank,
                    SuppressRankChangeMessage = true,
                    Tick = game.CurrentTick,
                },
                new EventVariableChangedResult
                {
                    Key = CompletionVariableKey,
                    PreviousValue = previousVariable,
                    CurrentValue = CompletionVariableValue,
                    Tick = game.CurrentTick,
                },
                BuildCompletedResult(MissionOutcome.Success, game),
            };
        }
    }
}
