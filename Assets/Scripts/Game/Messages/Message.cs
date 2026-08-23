using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

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

    /// <summary>
    /// Provides the shared durable state of an item delivered to a faction's Messages window.
    /// </summary>
    [PersistableObject]
    public abstract class Message : BaseGameEntity
    {
        public MessageType Type;
        public MessageResultType ResultType;
        public string Title;
        public string Body;
        public string BackgroundImageKey;
        public string OverlayImagePath;
        public string BackgroundAudioPath;
        public string OfficerVoicePath;
        public string EventLocationInstanceID;
        public string NavigationTargetInstanceID;
        public string NavigationSecondaryTargetInstanceID;
        public string MissionInstanceID;

        public int CreatedTick;
        public bool Read;

        /// <summary>
        /// Initializes shared message state during deserialization.
        /// </summary>
        protected Message() { }

        /// <summary>
        /// Constructor for creating a new message.
        /// </summary>
        /// <param name="type">The type of message.</param>
        /// <param name="text">The text of the message.</param>
        protected Message(MessageType type, string text)
        {
            Type = type;
            Title = text;
            Body = text;
        }

        protected Message(MessageType type, string title, string body)
        {
            Type = type;
            Title = title;
            Body = body;
        }
    }

    /// <summary>
    /// Represents a normal status message displayed in the Messages detail view.
    /// </summary>
    [PersistableObject(Name = "Message")]
    public sealed class StatusMessage : Message
    {
        /// <summary>
        /// Initializes a status message during deserialization.
        /// </summary>
        public StatusMessage() { }

        /// <summary>
        /// Creates a status message whose title and body use the same text.
        /// </summary>
        /// <param name="type">The message category.</param>
        /// <param name="text">The message title and body.</param>
        public StatusMessage(MessageType type, string text)
            : base(type, text) { }

        /// <summary>
        /// Creates a status message with separate title and body text.
        /// </summary>
        /// <param name="type">The message category.</param>
        /// <param name="title">The message title.</param>
        /// <param name="body">The message body.</param>
        public StatusMessage(MessageType type, string title, string body)
            : base(type, title, body) { }
    }
}
