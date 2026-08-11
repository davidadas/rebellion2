using Rebellion.Game.Factions;
using Rebellion.Presentation.Advisor;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Carries one message and its transient presentation request to a recipient.
    /// </summary>
    public sealed class MessageDelivery
    {
        public Faction Recipient { get; set; }
        public Message Message { get; set; }
        public AdvisorNotificationType NotificationType { get; set; }
        public AdvisorSubjectNotification AdvisorSubjectNotification { get; set; }
        public string AdvisorSubjectTypeID { get; set; }
        public AdvisorNotification AdvisorNotification { get; set; }
    }
}
