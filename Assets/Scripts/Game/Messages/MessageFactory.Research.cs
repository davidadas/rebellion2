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
    public partial class MessageFactory
    {
        private void AddResearchMessages(
            IEnumerable<ResearchOrderedResult> completedResults,
            IEnumerable<ResearchExhaustedResult> exhaustedResults,
            ICollection<MessageDelivery> deliveries
        )
        {
            foreach (ResearchOrderedResult result in completedResults)
            {
                if (result?.Technology == null)
                    continue;

                Message message = BuildResearchMessage(
                    _definitionResolver.GetDefinition(
                        MessageResultType.ResearchComplete,
                        discipline: result.Discipline
                    ),
                    result.Faction,
                    new Dictionary<string, string>
                    {
                        { "item", GetResearchDisplayName(result.Technology.GetReference()) },
                    }
                );
                _deliveryBuilder.Add(deliveries, result.Faction, message, result);
            }

            foreach (ResearchExhaustedResult result in exhaustedResults)
            {
                if (result == null)
                    continue;

                Message message = BuildResearchMessage(
                    _definitionResolver.GetDefinition(
                        MessageResultType.ResearchExhausted,
                        discipline: result.Discipline
                    ),
                    result.Faction,
                    new Dictionary<string, string>()
                );
                _deliveryBuilder.Add(deliveries, result.Faction, message, result);
            }
        }

        private Message BuildResearchMessage(
            MessageDefinition definition,
            Faction faction,
            Dictionary<string, string> values
        )
        {
            Message message = _templateBuilder.Build(definition, faction, values);
            return _deliveryBuilder.WithNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
        }

        private static string GetResearchDisplayName(IGameEntity entity) =>
            entity?.GetDisplayName() ?? string.Empty;
    }
}
