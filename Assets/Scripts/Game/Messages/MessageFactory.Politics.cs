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
    public partial class MessageFactory
    {
        private void AddUprisingMessages(
            IEnumerable<PlanetNearUprisingResult> nearResults,
            IEnumerable<PlanetUprisingStartedResult> startedResults,
            IEnumerable<PlanetUprisingEndedResult> endedResults,
            GameRoot game,
            ICollection<MessageDelivery> deliveries
        )
        {
            foreach (PlanetNearUprisingResult result in nearResults)
            {
                Faction controller = GetPoliticalFaction(game, result.Planet?.OwnerInstanceID);
                AddPoliticalDelivery(
                    deliveries,
                    controller,
                    CreateNearUprising(controller, result),
                    result
                );
            }

            foreach (PlanetUprisingStartedResult result in startedResults)
            {
                Faction controller = GetPoliticalFaction(game, result.Planet?.OwnerInstanceID);
                AddPoliticalDelivery(
                    deliveries,
                    controller,
                    CreateUprisingStarted(controller, result, controller),
                    result
                );
                if (result.InstigatorFaction?.InstanceID != controller?.InstanceID)
                {
                    AddPoliticalDelivery(
                        deliveries,
                        result.InstigatorFaction,
                        CreateUprisingStarted(result.InstigatorFaction, result, controller),
                        result
                    );
                }
            }

            foreach (PlanetUprisingEndedResult result in endedResults)
            {
                Faction controller =
                    GetPoliticalFaction(game, result.Planet?.OwnerInstanceID) ?? result.Faction;
                AddPoliticalDelivery(
                    deliveries,
                    controller,
                    CreateUprisingEnded(controller, result, controller),
                    result
                );
            }
        }

        private void AddOwnershipMessages(
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
                    AddPoliticalDelivery(deliveries, recipient, message, result);
                }
            }
        }

        private Message CreateNearUprising(Faction faction, PlanetNearUprisingResult result)
        {
            if (result == null)
                return null;
            return BuildPoliticalMessage(
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
            return BuildPoliticalMessage(
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
            return BuildPoliticalMessage(
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
            return BuildPoliticalMessage(
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
            return BuildPoliticalMessage(
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
            return BuildPoliticalMessage(
                MessageResultType.PlanetDeclaredNeutralityBySupport,
                recipient,
                Values(result.PreviousOwner, result.Planet?.GetDisplayName()),
                result.Planet?.InstanceID,
                AdvisorNotificationType.NegativePopularSupport
            );
        }

        private Message BuildPoliticalMessage(
            MessageResultType resultType,
            Faction faction,
            Dictionary<string, string> values,
            string planetInstanceID,
            AdvisorNotificationType notification,
            Faction imageFaction = null
        )
        {
            MessageDefinition definition = _definitionResolver.GetDefinition(resultType);
            Message message = _templateBuilder.Build(definition, faction, values, imageFaction);
            if (message != null)
            {
                message.EventLocationInstanceID = planetInstanceID;
                message.NavigationTargetInstanceID = planetInstanceID;
            }
            return _deliveryBuilder.WithNotification(message, notification);
        }

        private void AddPoliticalDelivery(
            ICollection<MessageDelivery> deliveries,
            Faction faction,
            Message message,
            GameResult sourceResult
        ) => _deliveryBuilder.Add(deliveries, faction, message, sourceResult);

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

        private static Faction GetPoliticalFaction(GameRoot game, string instanceID) =>
            string.IsNullOrEmpty(instanceID)
                ? null
                : game.GetFactions().FirstOrDefault(faction => faction.InstanceID == instanceID);
    }
}
