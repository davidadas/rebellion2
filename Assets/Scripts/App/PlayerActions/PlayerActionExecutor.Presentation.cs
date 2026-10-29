using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.App.PlayerActions
{
    public sealed partial class PlayerActionExecutor
    {
        /// <summary>
        /// Toggles one persisted advisor setting and runs newly enabled delegated work.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The setting to toggle.</param>
        /// <returns>The completed setting-change outcome.</returns>
        private PlayerActionResult ToggleAdvisorOption(
            Faction actor,
            ToggleAdvisorOptionPlayerAction request
        )
        {
            bool processAutomation = false;
            switch (request.Option)
            {
                case PlayerAdvisorOption.ManageGarrisons:
                    actor.ManageGarrisons = !actor.ManageGarrisons;
                    processAutomation = actor.ManageGarrisons;
                    break;
                case PlayerAdvisorOption.ManageProduction:
                    actor.ManageProduction = !actor.ManageProduction;
                    processAutomation = actor.ManageProduction;
                    break;
                case PlayerAdvisorOption.ManageNaming:
                    actor.ManageNaming = !actor.ManageNaming;
                    processAutomation = actor.ManageNaming;
                    break;
                case PlayerAdvisorOption.TranslateCounterpart:
                    actor.TranslateCounterpart = !actor.TranslateCounterpart;
                    break;
                case PlayerAdvisorOption.AgentAdvice:
                    actor.AgentAdvice = !actor.AgentAdvice;
                    break;
                default:
                    return Reject("The advisor option is unsupported.");
            }
            if (processAutomation)
            {
                _session.GetService<FactionAutomationCommands>().ProcessFaction(actor);
                _session.GetService<NamingCommands>().ProcessFaction(actor);
            }
            return new PlayerActionResult(true);
        }

        /// <summary>
        /// Resolves selected messages only within the issuing faction's delivered buckets.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="messageIDs">The selected message identities.</param>
        /// <returns>The matching delivered messages.</returns>
        private static List<Message> FindMessages(Faction actor, IReadOnlyList<string> messageIDs)
        {
            HashSet<string> selected = new HashSet<string>(
                (messageIDs ?? Array.Empty<string>()).Where(id => !string.IsNullOrEmpty(id)),
                StringComparer.Ordinal
            );
            return actor
                    .Messages?.Values.Where(bucket => bucket != null)
                    .SelectMany(bucket => bucket)
                    .Where(message => message != null && selected.Contains(message.InstanceID))
                    .ToList()
                ?? new List<Message>();
        }

        /// <summary>
        /// Records read state on matching messages without altering notification acknowledgments.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The messages that were presented.</param>
        /// <returns>The completed read-state outcome.</returns>
        private static PlayerActionResult ReadMessages(
            Faction actor,
            ReadMessagesPlayerAction request
        )
        {
            List<Message> messages = FindMessages(actor, request.MessageIDs);
            foreach (Message message in messages)
                message.Read = true;
            return Complete(messages.Count > 0);
        }

        /// <summary>
        /// Marks all messages delivered to this actor as read.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <returns>The completed read-state outcome.</returns>
        private static PlayerActionResult MarkAllMessagesRead(Faction actor)
        {
            actor.MarkAllMessagesRead();
            return new PlayerActionResult(true);
        }

        /// <summary>
        /// Removes matching selected messages across their delivered category buckets.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The message identities to remove.</param>
        /// <returns>The completed removal outcome.</returns>
        private static PlayerActionResult RemoveMessages(
            Faction actor,
            RemoveMessagesPlayerAction request
        )
        {
            List<Message> messages = FindMessages(actor, request.MessageIDs);
            foreach (Message message in messages)
                actor.RemoveMessage(message);
            return Complete(messages.Count > 0);
        }

        /// <summary>
        /// Acknowledges matching presented notifications without marking messages as read.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The messages whose notifications were presented.</param>
        /// <returns>The completed acknowledgment outcome.</returns>
        private static PlayerActionResult AcknowledgeMessages(
            Faction actor,
            AcknowledgeMessagesPlayerAction request
        )
        {
            bool changed = false;
            foreach (Message message in FindMessages(actor, request.MessageIDs))
            {
                if (message.NotificationAcknowledged)
                    continue;
                message.NotificationAcknowledged = true;
                changed = true;
            }
            return Complete(changed);
        }

        /// <summary>
        /// Toggles a category or the default notification state when every category is selected.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The category, or null to toggle all categories.</param>
        /// <returns>The completed notification-setting outcome.</returns>
        private static PlayerActionResult ToggleMessageNotification(
            Faction actor,
            ToggleMessageNotificationPlayerAction request
        )
        {
            if (!request.MessageType.HasValue)
                actor.ToggleAllAdvisorMessageNotifications();
            else if (Enum.IsDefined(typeof(MessageType), request.MessageType.Value))
                actor.ToggleAdvisorMessageNotification(request.MessageType.Value);
            else
                return Reject("The message category is unsupported.");
            return new PlayerActionResult(true);
        }

        /// <summary>
        /// Consumes a matching report's pending continuation choice without stopping its mission.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The delivered report carrying the choice.</param>
        /// <returns>The completed continuation-choice outcome.</returns>
        private static PlayerActionResult ConsumeMissionContinuation(
            Faction actor,
            ConsumeMissionContinuationPlayerAction request
        )
        {
            Message message = FindMessages(actor, new[] { request.MessageID }).FirstOrDefault();
            if (message?.MissionContinuationPending != true)
                return Reject("The report has no pending mission choice.");
            message.MissionContinuationPending = false;
            return new PlayerActionResult(true);
        }

        /// <summary>
        /// Executes controlled fleet bombardment and captures the actor-visible report once.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The selected fleets, visible target, and bombardment profile.</param>
        /// <returns>The completed combat report or an explicit rejection.</returns>
        private PlayerActionResult Bombard(Faction actor, BombardPlayerAction request)
        {
            if (
                !Enum.IsDefined(typeof(BombardmentType), request.Type)
                || !TryResolveSelection(request.ItemIDs, out List<Fleet> fleets)
                || !OwnsSelection(actor, fleets)
            )
                return Reject("The selected fleets cannot execute this bombardment.");
            CombatReport report = _session
                .GetService<BombardmentCommands>()
                .TryExecuteReport(fleets, request.ObservedTarget, request.Type, actor.InstanceID);
            return report == null
                ? Reject("The bombardment is unavailable.")
                : new PlayerActionResult(true, combatReport: report);
        }

        /// <summary>
        /// Executes controlled fleet assault and captures the actor-visible report once.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The selected fleets and observed target state.</param>
        /// <returns>The completed combat report or an explicit rejection.</returns>
        private PlayerActionResult Assault(Faction actor, AssaultPlayerAction request)
        {
            if (
                !TryResolveSelection(request.ItemIDs, out List<Fleet> fleets)
                || !OwnsSelection(actor, fleets)
            )
                return Reject("The selected fleets cannot execute this assault.");
            CombatReport report = _session
                .GetService<PlanetaryAssaultCommands>()
                .TryExecuteReport(fleets, request.ObservedTarget, actor.InstanceID);
            return report == null
                ? Reject("The assault is unavailable.")
                : new PlayerActionResult(true, combatReport: report);
        }
    }
}
