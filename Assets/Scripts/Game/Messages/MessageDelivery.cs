using Rebellion.Game.Factions;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Carries a generated message with the faction that should receive it.
    /// </summary>
    public class MessageDelivery
    {
        public Faction Faction { get; }
        public Message Message { get; }

        /// <summary>
        /// Creates a message delivery for a faction.
        /// </summary>
        /// <param name="faction">The faction that should receive the message.</param>
        /// <param name="message">The message to add to the faction.</param>
        public MessageDelivery(Faction faction, Message message)
        {
            Faction = faction;
            Message = message;
        }
    }
}
