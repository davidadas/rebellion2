using Rebellion.SceneGraph;

namespace Rebellion.Game.Factions
{
    public enum MessageType
    {
        PopularSupport = 1,
        Fleet = 2,
        Mission = 3,
        Resource = 4,
        Manufacturing = 5,
        Defense = 6,
        Conflict = 7,
        Chat = 8,
        Advice = 9,
    }

    public class Message : BaseGameEntity
    {
        public MessageType Type;
        public string Text;
        public bool Read;

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public Message() { }

        /// <summary>
        /// Constructor for creating a new message.
        /// </summary>
        /// <param name="type">The type of message.</param>
        /// <param name="text">The text of the message.</param>
        public Message(MessageType type, string text)
        {
            Type = type;
            Text = text;
        }

        /// <summary>
        /// Returns the message text and marks it as read.
        /// </summary>
        /// <returns>The text of the message.</returns>
        public string GetText()
        {
            Read = true;
            return Text;
        }
    }
}
