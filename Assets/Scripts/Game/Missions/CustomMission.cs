using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// A persisted mission instance whose duration and success check are supplied by content.
    /// Result-triggered game events apply any domain effects after it resolves.
    /// </summary>
    [PersistableObject(Name = "CustomMission")]
    public sealed class CustomMission : Mission
    {
        [PersistableIgnore]
        public CustomMissionDefinition Definition { get; private set; }

        public string MissionDefinitionID { get; set; }
        public string TargetInstanceID { get; set; }

        public CustomMission()
        {
            ConfigKey = "CustomMission";
            ParticipantRating = OfficerRating.None;
        }

        public CustomMission(
            CustomMissionDefinition definition,
            string targetInstanceId,
            IEnumerable<string> participantInstanceIds,
            IEnumerable<string> decoyInstanceIds,
            string sourceEventInstanceId,
            GameRoot game
        )
            : base(
                definition?.InstanceID ?? throw new ArgumentNullException(nameof(definition)),
                ResolveOwner(definition, targetInstanceId, participantInstanceIds, game),
                ResolveLocation(definition, targetInstanceId, game).InstanceID,
                ResolveParticipants(participantInstanceIds, game),
                ResolveParticipants(decoyInstanceIds, game),
                OfficerRating.None,
                definition.DisplayName
            )
        {
            Definition = definition;
            MissionDefinitionID = definition.InstanceID;
            TargetInstanceID = targetInstanceId;
            CanCancel = definition.CanCancel;
            SourceEventInstanceID = sourceEventInstanceId;
        }

        public void SetDefinition(CustomMissionDefinition definition)
        {
            if (definition?.InstanceID != MissionDefinitionID)
                throw new InvalidOperationException(
                    $"Mission definition '{MissionDefinitionID}' is unavailable."
                );
            Definition = definition;
        }

        public int RollDuration(IRandomNumberProvider provider)
        {
            EnsureDefinition();
            if (Definition.Duration?.Fixed != null)
                return Definition.Duration.Fixed.Ticks;
            if (Definition.Duration?.Random != null)
                return provider.NextInt(
                    Definition.Duration.Random.MinimumTicks,
                    checked(Definition.Duration.Random.MaximumTicks + 1)
                );
            throw new InvalidOperationException(
                $"Mission definition '{MissionDefinitionID}' has no Duration."
            );
        }

        public override bool ShouldRepeatAfterCompletion(GameRoot game) => false;

        internal override bool AppliesFoiledParticipantConsequences => false;

        public override MissionCompletionReason? GetAbortReason(GameRoot game)
        {
            MissionCompletionReason? baseReason = base.GetAbortReason(game);
            if (baseReason.HasValue)
                return baseReason;
            return GetTarget<ISceneNode>(game) == null
                ? MissionCompletionReason.TargetUnavailable
                : null;
        }

        internal override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            EnsureDefinition();
            bool succeeded = EvaluateSuccess(game, provider);
            return new List<GameResult>
            {
                BuildCompletedResult(
                    succeeded ? MissionOutcome.Success : MissionOutcome.Failed,
                    game
                ),
            };
        }

        internal T GetTarget<T>(GameRoot game)
            where T : class => game.GetSceneNodeByInstanceID<T>(TargetInstanceID);

        private bool EvaluateSuccess(GameRoot game, IRandomNumberProvider provider)
        {
            MissionSuccessRule success = Definition.Success;
            if (success == null)
                throw new InvalidOperationException(
                    $"Mission definition '{MissionDefinitionID}' has no Success rule."
                );
            if (success.Automatic != null)
                return true;
            if (success.Chance != null)
                return provider.NextDouble() * 100 < GetSuccessChance(success.Chance);

            Officer target = GetTarget<Officer>(game);
            if (target == null)
                throw new InvalidOperationException(
                    $"Mission '{MissionDefinitionID}' requires an officer target for its opposed check."
                );
            OpposedMissionSuccess opposed = success.Opposed;
            double probability = LookupSuccessProbability(
                game,
                opposed.AttackRating - target.GetEffectiveRating(opposed.TargetRating),
                opposed.ProbabilityTableKey
            );
            return IsSuccessfulProbabilityRoll(provider.NextDouble() * 100, probability);
        }

        private int GetSuccessChance(ChanceMissionSuccess chance)
        {
            int result = chance.BasePercent;
            foreach (MissionRatingContribution contribution in chance.Ratings)
            {
                if (contribution.Divisor <= 0)
                    throw new InvalidOperationException(
                        $"Mission '{MissionDefinitionID}' rating divisors must be positive."
                    );
                Officer participant = GetMainParticipant<Officer>(contribution.ParticipantIndex);
                if (participant == null)
                    throw new InvalidOperationException(
                        $"Mission '{MissionDefinitionID}' could not resolve participant {contribution.ParticipantIndex} as an officer."
                    );
                result +=
                    participant.GetEffectiveRating(contribution.Rating) / contribution.Divisor;
            }
            return Math.Clamp(result, 0, 100);
        }

        private T GetMainParticipant<T>(int index)
            where T : class =>
            index >= 0 && index < MainParticipants.Count ? MainParticipants[index] as T : null;

        private void EnsureDefinition()
        {
            if (Definition == null)
                throw new InvalidOperationException(
                    $"Mission definition '{MissionDefinitionID}' has not been attached."
                );
        }

        private static List<IMissionParticipant> ResolveParticipants(
            IEnumerable<string> instanceIds,
            GameRoot game
        ) =>
            instanceIds
                ?.Select(game.GetSceneNodeByInstanceID<IMissionParticipant>)
                .Where(participant => participant != null)
                .ToList()
            ?? new List<IMissionParticipant>();

        private static string ResolveOwner(
            CustomMissionDefinition definition,
            string targetInstanceId,
            IEnumerable<string> participantInstanceIds,
            GameRoot game
        )
        {
            if (!string.IsNullOrWhiteSpace(definition.OwnerFactionInstanceID))
                return definition.OwnerFactionInstanceID;
            IMissionParticipant participant = ResolveParticipants(participantInstanceIds, game)
                .FirstOrDefault();
            ISceneNode target = game.GetSceneNodeByInstanceID<ISceneNode>(targetInstanceId);
            return participant?.OwnerInstanceID
                ?? target?.OwnerInstanceID
                ?? throw new InvalidOperationException(
                    $"Mission '{definition.InstanceID}' could not resolve an owner."
                );
        }

        private static Planet ResolveLocation(
            CustomMissionDefinition definition,
            string targetInstanceId,
            GameRoot game
        )
        {
            ISceneNode source = game.GetSceneNodeByInstanceID<ISceneNode>(targetInstanceId);
            return source as Planet
                ?? source?.GetParentOfType<Planet>()
                ?? throw new InvalidOperationException(
                    $"Mission '{definition.InstanceID}' could not resolve its location."
                );
        }
    }
}
