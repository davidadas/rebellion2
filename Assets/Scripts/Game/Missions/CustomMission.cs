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
        public int MissionDefinitionRevision { get; set; }
        public string TargetInstanceID { get; set; }

        public CustomMission()
        {
            ConfigKey = "CustomMission";
            ParticipantRating = OfficerRating.None;
        }

        public CustomMission(
            CustomMissionDefinition definition,
            string targetInstanceId,
            string locationInstanceId,
            IEnumerable<string> participantInstanceIds,
            IEnumerable<string> decoyInstanceIds,
            string sourceEventInstanceId,
            GameRoot game
        )
            : base(
                definition?.InstanceID ?? throw new ArgumentNullException(nameof(definition)),
                ResolveOwner(definition, targetInstanceId, participantInstanceIds, game),
                ResolveLocation(definition, targetInstanceId, locationInstanceId, game).InstanceID,
                ResolveParticipants(participantInstanceIds, game),
                ResolveParticipants(decoyInstanceIds, game),
                OfficerRating.None,
                definition.DisplayName
            )
        {
            definition.EnsureValid();
            if (MainParticipants.Intersect(DecoyParticipants).Any())
                throw new InvalidOperationException(
                    "A custom mission participant cannot also be assigned as a decoy."
                );
            Definition = definition;
            MissionDefinitionID = definition.InstanceID;
            MissionDefinitionRevision = definition.Revision;
            TargetInstanceID = targetInstanceId;
            CanCancel = definition.CanCancel;
            SourceEventInstanceID = sourceEventInstanceId;
        }

        public void SetDefinition(CustomMissionDefinition definition)
        {
            if (
                definition?.InstanceID != MissionDefinitionID
                || definition.Revision != MissionDefinitionRevision
            )
                throw new InvalidOperationException(
                    $"Mission definition '{MissionDefinitionID}' revision {MissionDefinitionRevision} is unavailable."
                );
            definition.EnsureValid();
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
            where T : class
        {
            T target = game.GetSceneNodeByInstanceID<T>(TargetInstanceID);
            return target is ISceneNode node && node.GetParent() == null ? null : target;
        }

        private bool EvaluateSuccess(GameRoot game, IRandomNumberProvider provider)
        {
            MissionSuccessRule success = Definition.Success;
            if (success == null)
                throw new InvalidOperationException(
                    $"Mission definition '{MissionDefinitionID}' has no Success rule."
                );
            if (success.Automatic != null)
                return true;
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
        )
        {
            List<string> ids = instanceIds?.ToList() ?? new List<string>();
            if (ids.Count != ids.Distinct(StringComparer.Ordinal).Count())
                throw new InvalidOperationException(
                    "A custom mission cannot assign the same participant more than once."
                );
            List<IMissionParticipant> participants = ids.ConvertAll(
                game.GetSceneNodeByInstanceID<IMissionParticipant>
            );
            int missingIndex = participants.FindIndex(participant => participant == null);
            if (missingIndex >= 0)
                throw new InvalidOperationException(
                    $"Custom mission participant '{ids[missingIndex]}' is unavailable."
                );
            return participants;
        }

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
            string locationInstanceId,
            GameRoot game
        )
        {
            ISceneNode source = game.GetSceneNodeByInstanceID<ISceneNode>(
                string.IsNullOrWhiteSpace(locationInstanceId)
                    ? targetInstanceId
                    : locationInstanceId
            );
            return source as Planet
                ?? source?.GetParentOfType<Planet>()
                ?? throw new InvalidOperationException(
                    $"Mission '{definition.InstanceID}' could not resolve its location."
                );
        }
    }
}
