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

    public enum AdvisorNotificationCode
    {
        None = 0,
        PositivePopularSupport = 1,
        NegativePopularSupport = 2,
        Manufacturing = 3,
        Research = 4,
        FleetArrived = 5,
        UnitsArrived = 6,
        CapitalShipRepaired = 8,
        StarfighterRepaired = 9,
        Maintenance = 12,
        BlockadeInitiated = 13,
        BlockadeDetected = 14,
        FieldPersonnel = 20,
        AgentReport = 21,
        PlanetaryStatus = 28,
        PrisonerEscaped = 36,
        InterceptedCommunication = 41,
        Bombardment = 46,
        PlanetaryAssault = 47,
    }

    public enum AdvisorSubjectNotification
    {
        None,
        Report,
        Captured,
        Released,
    }

    public class Message : BaseGameEntity
    {
        public MessageType Type;
        public MessageResultType ResultType;
        public string Title;
        public string Text;
        public string Body;
        public string DisplayImageKey;
        public string OverlayImagePath;
        public string MessageVoicePath;
        public string OfficerVoicePath;
        public string EventLocationInstanceID;
        public string NavigationTargetInstanceID;
        public string NavigationSecondaryTargetInstanceID;
        public int AdvisorNotificationCode;
        public AdvisorSubjectNotification AdvisorSubjectNotification;
        public string AdvisorSubjectTypeID;
        public string MissionInstanceID;
        public int CreatedTick;
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
