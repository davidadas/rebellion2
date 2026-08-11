using System.Collections.Generic;
using Rebellion.Game.Results;
using Rebellion.Presentation.Advisor;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Translates authored message requests into concrete faction deliveries.
    /// </summary>
    internal sealed class AuthoredMessageDeliveryFactory
    {
        private readonly MessageTemplateBuilder _templateBuilder;

        public AuthoredMessageDeliveryFactory(MessageTemplateBuilder templateBuilder)
        {
            _templateBuilder = templateBuilder;
        }

        /// <summary>
        /// Creates deliveries for valid authored message requests.
        /// </summary>
        public IEnumerable<MessageDelivery> CreateDeliveries(
            IEnumerable<MessageRequestedResult> results
        )
        {
            foreach (MessageRequestedResult result in results)
            {
                MessageDelivery delivery = CreateDelivery(result);
                if (delivery != null)
                    yield return delivery;
            }
        }

        private MessageDelivery CreateDelivery(MessageRequestedResult result)
        {
            if (result?.Recipient == null)
                return null;

            MessageDefinition definition = new MessageDefinition
            {
                MessageType = result.MessageType,
                Subject = result.Subject,
                Body = result.Body,
                BackgroundImage = CreateBackground(result),
                AmbientAudioPath = result.AmbientAudioPath,
            };
            Message message = _templateBuilder.Build(
                definition,
                result.Recipient,
                new Dictionary<string, string>
                {
                    { "subject", result.SubjectNode?.GetDisplayName() ?? string.Empty },
                    {
                        "relatedSubject",
                        result.RelatedSubjectNode?.GetDisplayName() ?? string.Empty
                    },
                    { "location", result.Location?.GetDisplayName() ?? string.Empty },
                    { "faction", result.Recipient.GetDisplayName() },
                },
                overlayImagePath: result.OverlayImagePath,
                officerVoicePath: result.OfficerVoicePath
            );
            if (message == null)
                return null;

            message.EventLocationInstanceID = result.Location?.InstanceID;
            message.NavigationTargetInstanceID = result.SubjectNode?.InstanceID;

            MessageDelivery delivery = new MessageDelivery
            {
                Recipient = result.Recipient,
                Message = message,
                AdvisorNotification = result.AdvisorNotification,
                AdvisorSubjectTypeID = result.SubjectNode?.TypeID,
            };
            ApplyAdvisorPreset(delivery);
            return delivery;
        }

        private static MessageBackgroundImage CreateBackground(MessageRequestedResult result)
        {
            if (
                string.IsNullOrWhiteSpace(result.BackgroundImageKey)
                && string.IsNullOrWhiteSpace(result.BackgroundImagePath)
            )
                return null;

            return new MessageBackgroundImage
            {
                Key = result.BackgroundImageKey,
                Path = result.BackgroundImagePath,
            };
        }

        private static void ApplyAdvisorPreset(MessageDelivery delivery)
        {
            AdvisorNotification notification = delivery.AdvisorNotification;
            if (notification?.Preset.HasValue != true)
                return;

            switch (notification.Preset.Value)
            {
                case AdvisorNotificationPreset.SubjectReport:
                    delivery.AdvisorSubjectNotification = AdvisorSubjectNotification.Report;
                    break;
                case AdvisorNotificationPreset.SubjectCaptured:
                    delivery.AdvisorSubjectNotification = AdvisorSubjectNotification.Captured;
                    break;
                case AdvisorNotificationPreset.SubjectReleased:
                    delivery.AdvisorSubjectNotification = AdvisorSubjectNotification.Released;
                    break;
                default:
                    delivery.NotificationType = notification.Preset.Value.ToNotificationType();
                    break;
            }
        }
    }
}
