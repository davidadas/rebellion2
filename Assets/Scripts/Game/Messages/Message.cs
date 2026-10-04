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
        [PersistableMember(Name = nameof(Type))]
        private MessageType _type;

        [PersistableMember(Name = nameof(ResultType))]
        private MessageResultType _resultType;

        [PersistableMember(Name = nameof(Title))]
        private string _title;

        [PersistableMember(Name = nameof(Body))]
        private string _body;

        [PersistableMember(Name = nameof(BackgroundImageKey))]
        private string _backgroundImageKey;

        [PersistableMember(Name = nameof(OverlayImagePath))]
        private string _overlayImagePath;

        [PersistableMember(Name = nameof(BackgroundAudioPath))]
        private string _backgroundAudioPath;

        [PersistableMember(Name = nameof(OfficerVoicePath))]
        private string _officerVoicePath;

        [PersistableMember(Name = nameof(EventLocationInstanceID))]
        private string _eventLocationInstanceId;

        [PersistableMember(Name = nameof(NavigationTargetInstanceID))]
        private string _navigationTargetInstanceId;

        [PersistableMember(Name = nameof(NavigationSecondaryTargetInstanceID))]
        private string _navigationSecondaryTargetInstanceId;

        [PersistableMember(Name = nameof(MissionInstanceID))]
        private string _missionInstanceId;

        [PersistableMember(Name = nameof(CreatedTick))]
        private int _createdTick;

        [PersistableMember(Name = nameof(Read))]
        private bool _read;

        [PersistableIgnore]
        public MessageType Type
        {
            get => _type;
            set => _type = value;
        }

        [PersistableIgnore]
        public MessageResultType ResultType
        {
            get => _resultType;
            set => _resultType = value;
        }

        [PersistableIgnore]
        public string Title
        {
            get => _title;
            set => _title = value;
        }

        [PersistableIgnore]
        public string Body
        {
            get => _body;
            set => _body = value;
        }

        [PersistableIgnore]
        public string BackgroundImageKey
        {
            get => _backgroundImageKey;
            set => _backgroundImageKey = value;
        }

        [PersistableIgnore]
        public string OverlayImagePath
        {
            get => _overlayImagePath;
            set => _overlayImagePath = value;
        }

        [PersistableIgnore]
        public string BackgroundAudioPath
        {
            get => _backgroundAudioPath;
            set => _backgroundAudioPath = value;
        }

        [PersistableIgnore]
        public string OfficerVoicePath
        {
            get => _officerVoicePath;
            set => _officerVoicePath = value;
        }

        [PersistableIgnore]
        public string EventLocationInstanceID
        {
            get => _eventLocationInstanceId;
            set => _eventLocationInstanceId = value;
        }

        [PersistableIgnore]
        public string NavigationTargetInstanceID
        {
            get => _navigationTargetInstanceId;
            set => _navigationTargetInstanceId = value;
        }

        [PersistableIgnore]
        public string NavigationSecondaryTargetInstanceID
        {
            get => _navigationSecondaryTargetInstanceId;
            set => _navigationSecondaryTargetInstanceId = value;
        }

        [PersistableIgnore]
        public string MissionInstanceID
        {
            get => _missionInstanceId;
            set => _missionInstanceId = value;
        }

        [PersistableIgnore]
        public int CreatedTick
        {
            get => _createdTick;
            set => _createdTick = value;
        }

        [PersistableIgnore]
        public bool Read
        {
            get => _read;
            set => _read = value;
        }

        /// <summary>
        /// Initializes shared message state during deserialization.
        /// </summary>
        protected Message() { }

        /// <summary>
        /// Initializes a new instance of the Message class.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="title">The title.</param>
        /// <param name="body">The body.</param>
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
        /// Creates a status message with separate title and body text.
        /// </summary>
        /// <param name="type">The message category.</param>
        /// <param name="title">The message title.</param>
        /// <param name="body">The message body.</param>
        public StatusMessage(MessageType type, string title, string body)
            : base(type, title, body) { }
    }
}
