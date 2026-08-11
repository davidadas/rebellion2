using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Presentation.Advisor;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Translates research results into faction message deliveries.
    /// </summary>
    internal sealed class ResearchMessageFactory
    {
        private readonly MessageDefinitionResolver _definitions;
        private readonly MessageTemplateBuilder _templates;
        private readonly MessageDeliveryBuilder _deliveries;

        public ResearchMessageFactory(
            MessageDefinitionResolver definitions,
            MessageTemplateBuilder templates,
            MessageDeliveryBuilder deliveries
        )
        {
            _definitions = definitions;
            _templates = templates;
            _deliveries = deliveries;
        }

        public void AddMessages(
            IEnumerable<ResearchOrderedResult> completedResults,
            IEnumerable<ResearchExhaustedResult> exhaustedResults,
            ICollection<MessageDelivery> deliveries
        )
        {
            foreach (ResearchOrderedResult result in completedResults)
            {
                if (result?.Technology == null)
                    continue;

                Message message = Build(
                    _definitions.GetDefinition(
                        MessageResultType.ResearchComplete,
                        discipline: result.Discipline
                    ),
                    result.Faction,
                    new Dictionary<string, string>
                    {
                        { "item", GetDisplayName(result.Technology.GetReference()) },
                    }
                );
                _deliveries.Add(deliveries, result.Faction, message);
            }

            foreach (ResearchExhaustedResult result in exhaustedResults)
            {
                if (result == null)
                    continue;

                Message message = Build(
                    _definitions.GetDefinition(
                        MessageResultType.ResearchExhausted,
                        discipline: result.Discipline
                    ),
                    result.Faction,
                    new Dictionary<string, string>()
                );
                _deliveries.Add(deliveries, result.Faction, message);
            }
        }

        private Message Build(
            MessageDefinition definition,
            Faction faction,
            Dictionary<string, string> values
        )
        {
            Message message = _templates.Build(definition, faction, values);
            return _deliveries.WithNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
        }

        private static string GetDisplayName(IGameEntity entity) =>
            entity?.GetDisplayName() ?? string.Empty;
    }
}
