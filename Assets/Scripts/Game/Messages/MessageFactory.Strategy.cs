using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Presentation.Advisor;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Translates strategic objectives and planet incidents into faction reports.
    /// </summary>
    public partial class MessageFactory
    {
        private void AddObjectiveMessages(
            IEnumerable<PlanetOwnershipChangedResult> ownershipResults,
            IEnumerable<HeadquartersDestroyedResult> headquartersResults,
            GameRoot game,
            ICollection<MessageDelivery> deliveries
        )
        {
            foreach (PlanetOwnershipChangedResult result in ownershipResults)
            {
                MessageDefinition definition = Find(
                    MessageResultType.PlanetCaptured,
                    result.Planet?.InstanceID,
                    result.PreviousOwner?.InstanceID,
                    result.NewOwner?.InstanceID,
                    null
                );
                if (definition == null)
                    continue;

                foreach (Faction recipient in GetOwnershipRecipients(result, game))
                {
                    Message message = BuildStrategicMessage(
                        definition,
                        recipient,
                        new Dictionary<string, string>
                        {
                            { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                            {
                                "previousFaction",
                                result.PreviousOwner?.GetDisplayName() ?? string.Empty
                            },
                            { "newFaction", result.NewOwner?.GetDisplayName() ?? string.Empty },
                        },
                        result.NewOwner
                    );
                    SetStrategicLocation(message, result.Planet, result.Planet);
                    _deliveryBuilder.Add(deliveries, recipient, message, result);
                }
            }

            foreach (HeadquartersDestroyedResult result in headquartersResults)
            {
                MessageDefinition definition = Find(
                    MessageResultType.HeadquartersDestroyed,
                    result.Planet?.InstanceID,
                    null,
                    null,
                    result.Defender?.InstanceID
                );
                if (definition == null)
                    continue;

                foreach (
                    Faction recipient in new[] { result.Attacker, result.Defender }
                        .Where(faction => faction != null)
                        .Distinct()
                )
                {
                    Message message = BuildStrategicMessage(
                        definition,
                        recipient,
                        new Dictionary<string, string>
                        {
                            { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                            { "attacker", result.Attacker?.GetDisplayName() ?? string.Empty },
                            { "defender", result.Defender?.GetDisplayName() ?? string.Empty },
                        },
                        result.Attacker
                    );
                    SetStrategicLocation(message, result.Planet, result.Headquarters);
                    _deliveryBuilder.Add(deliveries, recipient, message, result);
                }
            }
        }

        private void AddIncidentMessages(
            IEnumerable<PlanetIncidentResult> results,
            GameRoot game,
            ICollection<MessageDelivery> deliveries
        )
        {
            foreach (PlanetIncidentResult result in results)
            {
                Faction recipient = GetStrategicFaction(game, result.Planet?.OwnerInstanceID);
                if (recipient == null)
                    continue;
                MessageResultType resultType = result.IncidentType switch
                {
                    IncidentType.Disaster => MessageResultType.NaturalDisaster,
                    IncidentType.Resource when result.NewValue > result.OldValue =>
                        MessageResultType.NewResources,
                    IncidentType.Resource => MessageResultType.ResourcesDepleted,
                    _ => MessageResultType.None,
                };
                if (resultType == MessageResultType.None)
                    continue;

                bool hasDestroyedObjects = result.DestroyedObjects.Count > 0;
                MessageDefinition definition = _definitions.FirstOrDefault(candidate =>
                    candidate.ResultType == resultType
                    && (
                        resultType == MessageResultType.NaturalDisaster
                            ? candidate.HasDestroyedObjects == hasDestroyedObjects
                            : candidate.PlanetStat == result.ChangedStat
                    )
                );
                if (definition == null)
                    continue;

                Message message = BuildStrategicMessage(
                    definition,
                    recipient,
                    new Dictionary<string, string>
                    {
                        { "system", result.Planet.GetDisplayName() },
                        {
                            "destroyedObjects",
                            string.Join(
                                Environment.NewLine,
                                result.DestroyedObjects.Select(entity => entity.GetDisplayName())
                            )
                        },
                    },
                    recipient
                );
                SetStrategicLocation(
                    message,
                    result.Planet,
                    result.DestroyedObjects.OfType<ISceneNode>().FirstOrDefault()
                );
                _deliveryBuilder.Add(deliveries, recipient, message, result);
            }
        }

        private MessageDefinition Find(
            MessageResultType resultType,
            string planetInstanceID,
            string previousOwnerInstanceID,
            string newOwnerInstanceID,
            string factionInstanceID
        ) =>
            _definitions.FirstOrDefault(definition =>
                definition.ResultType == resultType
                && Matches(definition.PlanetInstanceID, planetInstanceID)
                && Matches(definition.PreviousOwnerInstanceID, previousOwnerInstanceID)
                && Matches(definition.NewOwnerInstanceID, newOwnerInstanceID)
                && Matches(definition.FactionInstanceID, factionInstanceID)
            );

        private Message BuildStrategicMessage(
            MessageDefinition definition,
            Faction recipient,
            Dictionary<string, string> values,
            Faction imageFaction
        )
        {
            Message message = _templateBuilder.Build(definition, recipient, values, imageFaction);
            return _deliveryBuilder.WithNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
        }

        private static void SetStrategicLocation(
            Message message,
            ISceneNode planet,
            ISceneNode target
        )
        {
            if (message == null)
                return;
            message.EventLocationInstanceID = planet?.InstanceID;
            message.NavigationTargetInstanceID = (target ?? planet)?.InstanceID;
        }

        private static IEnumerable<Faction> GetOwnershipRecipients(
            PlanetOwnershipChangedResult result,
            GameRoot game
        )
        {
            HashSet<string> recipientIDs = new HashSet<string>(
                result.ObserverFactionInstanceIDs ?? Enumerable.Empty<string>()
            );
            if (result.PreviousOwner != null)
                recipientIDs.Add(result.PreviousOwner.InstanceID);
            if (result.NewOwner != null)
                recipientIDs.Add(result.NewOwner.InstanceID);
            return game.GetFactions().Where(faction => recipientIDs.Contains(faction.InstanceID));
        }

        private static bool Matches(string selector, string value) =>
            string.IsNullOrWhiteSpace(selector)
            || string.Equals(selector, value, StringComparison.Ordinal);

        private static Faction GetStrategicFaction(GameRoot game, string instanceID) =>
            string.IsNullOrEmpty(instanceID)
                ? null
                : game.GetFactions().FirstOrDefault(faction => faction.InstanceID == instanceID);
    }
}
