using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Resolves the best configured message definition for simulation-result selectors.
    /// </summary>
    internal sealed class MessageDefinitionResolver
    {
        private readonly MessageDefinition[] _definitions;

        public MessageDefinitionResolver(IEnumerable<MessageDefinition> definitions)
        {
            _definitions = definitions?.ToArray() ?? Array.Empty<MessageDefinition>();
        }

        public MessageDefinition GetDefinition(
            MessageResultType resultType,
            MessageResultOutcome outcome = MessageResultOutcome.None,
            MessagePlanetOwnership planetOwnership = MessagePlanetOwnership.None,
            BuildingType buildingType = BuildingType.None,
            ManufacturingType manufacturingType = ManufacturingType.None,
            ResearchDiscipline? discipline = null,
            string gameObjectTypeId = null,
            bool planetDestroyed = false,
            string factionInstanceId = null
        )
        {
            return _definitions
                .Where(definition =>
                    definition.ResultType == resultType
                    && definition.Outcome == outcome
                    && definition.PlanetOwnership == planetOwnership
                    && definition.BuildingType == buildingType
                    && definition.ManufacturingType == manufacturingType
                    && string.IsNullOrEmpty(definition.MissionTypeID)
                    && definition.MissionCompletionReason == MissionCompletionReason.None
                    && (!discipline.HasValue || definition.ResearchDiscipline == discipline.Value)
                    && MatchesOptionalSelector(definition.GameObjectTypeID, gameObjectTypeId)
                    && definition.PlanetDestroyed == planetDestroyed
                    && MatchesOptionalSelector(definition.FactionInstanceID, factionInstanceId)
                )
                .OrderByDescending(definition =>
                    !string.IsNullOrWhiteSpace(definition.GameObjectTypeID)
                )
                .ThenByDescending(definition =>
                    !string.IsNullOrWhiteSpace(definition.FactionInstanceID)
                )
                .FirstOrDefault();
        }

        public MessageDefinition GetMissionDefinition(
            MessageResultType resultType,
            MessageResultOutcome outcome,
            string missionTypeID,
            MissionCompletionReason completionReason = MissionCompletionReason.None
        )
        {
            MessageDefinition definition = FindMissionDefinition(
                resultType,
                outcome,
                missionTypeID,
                completionReason
            );
            if (definition != null)
                return definition;

            bool canUseGenericDefinition = CanUseGenericMissionDefinition(completionReason);
            if (completionReason != MissionCompletionReason.None && canUseGenericDefinition)
            {
                definition = FindMissionDefinition(
                    resultType,
                    outcome,
                    missionTypeID,
                    MissionCompletionReason.None
                );
            }
            if (definition != null || string.IsNullOrEmpty(missionTypeID))
                return definition;

            definition = FindMissionDefinition(resultType, outcome, null, completionReason);
            if (definition != null || completionReason == MissionCompletionReason.None)
                return definition;

            return canUseGenericDefinition
                ? FindMissionDefinition(resultType, outcome, null, MissionCompletionReason.None)
                : null;
        }

        private MessageDefinition FindMissionDefinition(
            MessageResultType resultType,
            MessageResultOutcome outcome,
            string missionTypeID,
            MissionCompletionReason completionReason
        )
        {
            return _definitions.FirstOrDefault(candidate =>
                candidate.ResultType == resultType
                && candidate.Outcome == outcome
                && candidate.PlanetOwnership == MessagePlanetOwnership.None
                && candidate.BuildingType == BuildingType.None
                && candidate.ManufacturingType == ManufacturingType.None
                && string.Equals(
                    candidate.MissionTypeID ?? string.Empty,
                    missionTypeID ?? string.Empty,
                    StringComparison.Ordinal
                )
                && candidate.MissionCompletionReason == completionReason
            );
        }

        private static bool CanUseGenericMissionDefinition(
            MissionCompletionReason completionReason
        ) =>
            completionReason
                is MissionCompletionReason.None
                    or MissionCompletionReason.Success
                    or MissionCompletionReason.Failure
                    or MissionCompletionReason.Foiled
                    or MissionCompletionReason.ResearchBreakthrough;

        private static bool MatchesOptionalSelector(string selector, string value) =>
            string.IsNullOrWhiteSpace(selector)
            || string.Equals(selector, value, StringComparison.Ordinal);
    }
}
