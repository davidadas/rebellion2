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
    internal sealed class StrategicMessageFactory
    {
        private readonly MessageDefinition[] _definitions;
        private readonly MessageTemplateBuilder _templates;
        private readonly MessageDeliveryBuilder _deliveries;

        public StrategicMessageFactory(
            IEnumerable<MessageDefinition> definitions,
            MessageTemplateBuilder templates,
            MessageDeliveryBuilder deliveries
        )
        {
            _definitions = definitions?.ToArray() ?? Array.Empty<MessageDefinition>();
            _templates = templates;
            _deliveries = deliveries;
        }

        public void AddObjectiveMessages(
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
                    Message message = Build(
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
                    SetLocation(message, result.Planet, result.Planet);
                    _deliveries.Add(deliveries, recipient, message);
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
                    Message message = Build(
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
                    SetLocation(message, result.Planet, result.Headquarters);
                    _deliveries.Add(deliveries, recipient, message);
                }
            }
        }

        public void AddIncidentMessages(
            IEnumerable<PlanetIncidentResult> results,
            GameRoot game,
            ICollection<MessageDelivery> deliveries
        )
        {
            foreach (PlanetIncidentResult result in results)
            {
                Faction recipient = GetFaction(game, result.Planet?.OwnerInstanceID);
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

                Message message = Build(
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
                SetLocation(
                    message,
                    result.Planet,
                    result.DestroyedObjects.OfType<ISceneNode>().FirstOrDefault()
                );
                _deliveries.Add(deliveries, recipient, message);
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

        private Message Build(
            MessageDefinition definition,
            Faction recipient,
            Dictionary<string, string> values,
            Faction imageFaction
        )
        {
            Message message = _templates.Build(definition, recipient, values, imageFaction);
            return _deliveries.WithNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
        }

        private static void SetLocation(Message message, ISceneNode planet, ISceneNode target)
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

        private static Faction GetFaction(GameRoot game, string instanceID) =>
            string.IsNullOrEmpty(instanceID)
                ? null
                : game.GetFactions().FirstOrDefault(faction => faction.InstanceID == instanceID);
    }
}
