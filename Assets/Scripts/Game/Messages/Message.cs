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

    /// <summary>
    /// Selects the advisor announcement produced for a data-authored narrative message.
    /// Subject cues combine with the message subject; all other cues select a generic category.
    /// </summary>
    public enum AdvisorCue
    {
        None = AdvisorNotificationCode.None,
        PositivePopularSupport = AdvisorNotificationCode.PositivePopularSupport,
        NegativePopularSupport = AdvisorNotificationCode.NegativePopularSupport,
        Manufacturing = AdvisorNotificationCode.Manufacturing,
        Research = AdvisorNotificationCode.Research,
        FleetArrived = AdvisorNotificationCode.FleetArrived,
        UnitsArrived = AdvisorNotificationCode.UnitsArrived,
        CapitalShipRepaired = AdvisorNotificationCode.CapitalShipRepaired,
        StarfighterRepaired = AdvisorNotificationCode.StarfighterRepaired,
        Maintenance = AdvisorNotificationCode.Maintenance,
        BlockadeInitiated = AdvisorNotificationCode.BlockadeInitiated,
        BlockadeDetected = AdvisorNotificationCode.BlockadeDetected,
        FieldPersonnel = AdvisorNotificationCode.FieldPersonnel,
        AgentReport = AdvisorNotificationCode.AgentReport,
        PlanetaryStatus = AdvisorNotificationCode.PlanetaryStatus,
        PrisonerEscaped = AdvisorNotificationCode.PrisonerEscaped,
        InterceptedCommunication = AdvisorNotificationCode.InterceptedCommunication,
        Bombardment = AdvisorNotificationCode.Bombardment,
        PlanetaryAssault = AdvisorNotificationCode.PlanetaryAssault,
        SubjectReport = 1000,
        SubjectCaptured,
        SubjectReleased,
    }

    public class Message : BaseGameEntity
    {
        public MessageType Type;
        public MessageResultType ResultType;
        public string Title;
        public string Text;
        public string Body;
        public string BackgroundImageKey;
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
