using Rebellion.Game.Factions;

namespace Rebellion.Game.Messages
{
    public class MessageDelivery
    {
        public Faction Faction { get; }
        public Message Message { get; }

        public MessageDelivery(Faction faction, Message message)
        {
            Faction = faction;
            Message = message;
        }
    }
}
