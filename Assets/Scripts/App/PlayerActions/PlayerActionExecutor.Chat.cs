using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using Rebellion.Simulation;

namespace Rebellion.App.PlayerActions
{
    /// <summary>
    /// Delivers ordered peer chat through the same durable message and notification pipeline.
    /// </summary>
    public sealed partial class PlayerActionExecutor
    {
        /// <summary>
        /// Delivers raw text only to the opposite human participant, without adding sender history.
        /// </summary>
        /// <param name="actor">The authenticated sending faction.</param>
        /// <param name="request">The unchanged text submitted by its composer.</param>
        /// <returns>The completed delivery outcome.</returns>
        private PlayerActionResult SendPeerChat(Faction actor, SendPeerChatPlayerAction request)
        {
            if (string.IsNullOrEmpty(request.Text))
                return Reject("Enter a message to send.");
            Player sender = _session.Game.GetFactionPlayer(actor.InstanceID);
            Player[] recipients = _session
                .Game.GetPlayers()
                .Where(player =>
                    player.ControllerType == PlayerControllerType.Human
                    && player.FactionID != actor.InstanceID
                )
                .ToArray();
            if (sender?.ControllerType != PlayerControllerType.Human || recipients.Length != 1)
                return Reject("Peer chat requires the other human participant.");
            Faction recipient = _session
                .Game.GetFactions()
                .Single(faction => faction.InstanceID == recipients[0].FactionID);
            string name = sender.DisplayName ?? actor.DisplayName ?? actor.InstanceID;
            _session.Pipeline.ProcessResults(
                _session
                    .GetService<MessageCommands>()
                    .Deliver(
                        new[]
                        {
                            new MessageDelivery
                            {
                                Recipient = recipient,
                                NotificationType = AdvisorNotificationType.ChatReceived,
                                Message = new StatusMessage(
                                    MessageType.Chat,
                                    "Message from " + name,
                                    request.Text
                                ),
                            },
                        }
                    )
            );
            return new PlayerActionResult(true);
        }
    }
}
