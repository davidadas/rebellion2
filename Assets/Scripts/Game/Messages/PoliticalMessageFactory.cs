using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Presentation.Advisor;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Translates uprising and popular-support ownership results into faction messages.
    /// </summary>
    internal sealed class PoliticalMessageFactory
    {
        private readonly MessageDefinitionResolver _definitions;
        private readonly MessageTemplateBuilder _templates;
        private readonly MessageDeliveryBuilder _deliveries;

        public PoliticalMessageFactory(
            MessageDefinitionResolver definitions,
            MessageTemplateBuilder templates,
            MessageDeliveryBuilder deliveries
        )
        {
            _definitions = definitions;
            _templates = templates;
            _deliveries = deliveries;
        }

        public void AddUprisingMessages(
            IEnumerable<PlanetNearUprisingResult> nearResults,
            IEnumerable<PlanetUprisingStartedResult> startedResults,
            IEnumerable<PlanetUprisingEndedResult> endedResults,
            GameRoot game,
            ICollection<MessageDelivery> deliveries
        )
        {
            foreach (PlanetNearUprisingResult result in nearResults)
            {
                Faction controller = GetFaction(game, result.Planet?.OwnerInstanceID);
                Add(deliveries, controller, CreateNearUprising(controller, result));
            }

            foreach (PlanetUprisingStartedResult result in startedResults)
            {
                Faction controller = GetFaction(game, result.Planet?.OwnerInstanceID);
                Add(deliveries, controller, CreateUprisingStarted(controller, result, controller));
                if (result.InstigatorFaction?.InstanceID != controller?.InstanceID)
                {
                    Add(
                        deliveries,
                        result.InstigatorFaction,
                        CreateUprisingStarted(result.InstigatorFaction, result, controller)
                    );
                }
            }

            foreach (PlanetUprisingEndedResult result in endedResults)
            {
                Faction controller =
                    GetFaction(game, result.Planet?.OwnerInstanceID) ?? result.Faction;
                Add(deliveries, controller, CreateUprisingEnded(controller, result, controller));
            }
        }

        public void AddOwnershipMessages(
            IEnumerable<PlanetOwnershipChangedResult> results,
            GameRoot game,
            ICollection<MessageDelivery> deliveries
        )
        {
            foreach (PlanetOwnershipChangedResult result in results)
            {
                if (result.Reason != PlanetOwnershipChangeReason.PopularSupport)
                    continue;

                foreach (Faction recipient in GetRecipients(result, game))
                {
                    Message message =
                        recipient == result.NewOwner ? CreateJoined(result)
                        : result.NewOwner != null ? CreateJoinedEnemy(result, recipient)
                        : CreateNeutrality(result, recipient);
                    Add(deliveries, recipient, message);
                }
            }
        }

        private Message CreateNearUprising(Faction faction, PlanetNearUprisingResult result)
        {
            if (result == null)
                return null;
            return Build(
                MessageResultType.NearUprising,
                faction,
                new Dictionary<string, string>
                {
                    { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                },
                result.Planet?.InstanceID,
                AdvisorNotificationType.NegativePopularSupport
            );
        }

        private Message CreateUprisingStarted(
            Faction faction,
            PlanetUprisingStartedResult result,
            Faction controller
        )
        {
            if (result == null)
                return null;
            AdvisorNotificationType notification =
                faction?.InstanceID == controller?.InstanceID
                    ? AdvisorNotificationType.NegativePopularSupport
                    : AdvisorNotificationType.PositivePopularSupport;
            return Build(
                MessageResultType.UprisingStarted,
                faction,
                new Dictionary<string, string>
                {
                    { "faction", controller?.GetDisplayName() ?? string.Empty },
                    { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                },
                result.Planet?.InstanceID,
                notification
            );
        }

        private Message CreateUprisingEnded(
            Faction faction,
            PlanetUprisingEndedResult result,
            Faction controller
        )
        {
            if (result == null)
                return null;
            return Build(
                MessageResultType.UprisingEnded,
                faction,
                new Dictionary<string, string>
                {
                    { "faction", controller?.GetDisplayName() ?? string.Empty },
                    { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                },
                result.Planet?.InstanceID,
                AdvisorNotificationType.PositivePopularSupport,
                controller
            );
        }

        private Message CreateJoined(PlanetOwnershipChangedResult result)
        {
            if (result?.NewOwner == null)
                return null;
            return Build(
                MessageResultType.PlanetJoinedBySupport,
                result.NewOwner,
                Values(result.NewOwner, result.Planet?.GetDisplayName()),
                result.Planet?.InstanceID,
                AdvisorNotificationType.PositivePopularSupport
            );
        }

        private Message CreateJoinedEnemy(PlanetOwnershipChangedResult result, Faction recipient)
        {
            if (
                result?.NewOwner == null
                || recipient == null
                || recipient.InstanceID == result.NewOwner.InstanceID
            )
                return null;
            return Build(
                MessageResultType.PlanetJoinedEnemyBySupport,
                recipient,
                Values(result.NewOwner, result.Planet?.GetDisplayName()),
                result.Planet?.InstanceID,
                AdvisorNotificationType.NegativePopularSupport,
                result.NewOwner
            );
        }

        private Message CreateNeutrality(PlanetOwnershipChangedResult result, Faction recipient)
        {
            if (result?.PreviousOwner == null || result.NewOwner != null || recipient == null)
                return null;
            return Build(
                MessageResultType.PlanetDeclaredNeutralityBySupport,
                recipient,
                Values(result.PreviousOwner, result.Planet?.GetDisplayName()),
                result.Planet?.InstanceID,
                AdvisorNotificationType.NegativePopularSupport
            );
        }

        private Message Build(
            MessageResultType resultType,
            Faction faction,
            Dictionary<string, string> values,
            string planetInstanceID,
            AdvisorNotificationType notification,
            Faction imageFaction = null
        )
        {
            MessageDefinition definition = _definitions.GetDefinition(resultType);
            Message message = _templates.Build(definition, faction, values, imageFaction);
            if (message != null)
            {
                message.EventLocationInstanceID = planetInstanceID;
                message.NavigationTargetInstanceID = planetInstanceID;
            }
            return _deliveries.WithNotification(message, notification);
        }

        private void Add(
            ICollection<MessageDelivery> deliveries,
            Faction faction,
            Message message
        ) => _deliveries.Add(deliveries, faction, message);

        private static Dictionary<string, string> Values(Faction faction, string system) =>
            new Dictionary<string, string>
            {
                { "faction", faction?.GetDisplayName() ?? string.Empty },
                { "system", system ?? string.Empty },
            };

        private static IEnumerable<Faction> GetRecipients(
            PlanetOwnershipChangedResult result,
            GameRoot game
        )
        {
            HashSet<string> recipientIds = new HashSet<string>(
                result.ObserverFactionInstanceIDs ?? Enumerable.Empty<string>()
            );
            if (result.PreviousOwner != null)
                recipientIds.Add(result.PreviousOwner.InstanceID);
            if (result.NewOwner != null)
                recipientIds.Add(result.NewOwner.InstanceID);
            return game.GetFactions().Where(faction => recipientIds.Contains(faction.InstanceID));
        }

        private static Faction GetFaction(GameRoot game, string instanceID) =>
            string.IsNullOrEmpty(instanceID)
                ? null
                : game.GetFactions().FirstOrDefault(faction => faction.InstanceID == instanceID);
    }
}
