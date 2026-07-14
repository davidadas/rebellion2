using Rebellion.SceneGraph;

namespace Rebellion.Game.Messages
{
    public enum MessageType
    {
        PopularSupport,
        Fleet,
        Mission,
        Resource,
        Manufacturing,
        Defense,
        Conflict,
        Chat,
        Advice,
    }

    public class Message : BaseGameEntity
    {
        public MessageType Type;
        public string Title;
        public string Text;
        public string Body;
        public string DisplayImageKey;
        public string OverlayImagePath;
        public string MessageVoicePath;
        public string OfficerVoicePath;
        public string EventLocationInstanceID;
        public string MissionInstanceID;
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
            Title = text;
            Text = text;
            Body = text;
        }

        public Message(MessageType type, string title, string body)
        {
            Type = type;
            Title = title;
            Text = body;
            Body = body;
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
