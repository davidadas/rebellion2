using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;
using Rebellion.Presentation.Advisor;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Owns transient recipient and advisor metadata while messages are translated.
    /// </summary>
    internal sealed class MessageDeliveryBuilder
    {
        private readonly Dictionary<Message, MessageDelivery> _deliveries = new();

        public MessageDelivery Get(Message message)
        {
            if (!_deliveries.TryGetValue(message, out MessageDelivery delivery))
            {
                delivery = new MessageDelivery { Message = message };
                _deliveries.Add(message, delivery);
            }
            return delivery;
        }

        public void Add(ICollection<MessageDelivery> deliveries, Faction recipient, Message message)
        {
            if (recipient == null || message == null)
                return;

            MessageDelivery delivery = Get(message);
            delivery.Recipient = recipient;
            deliveries.Add(delivery);
        }

        public Message WithNotification(Message message, AdvisorNotificationType notification)
        {
            if (message != null)
                Get(message).NotificationType = notification;
            return message;
        }

        public Message WithSubject(
            Message message,
            AdvisorSubjectNotification notification,
            Officer officer
        )
        {
            if (
                message == null
                || notification == AdvisorSubjectNotification.None
                || officer == null
            )
                return message;

            MessageDelivery delivery = Get(message);
            delivery.AdvisorSubjectNotification = notification;
            delivery.AdvisorSubjectTypeID = officer.TypeID;
            return message;
        }
    }
}
