using NUnit.Framework;
using Rebellion.Game.Advisor;

namespace Rebellion.Tests.UI.Runtime.Themes
{
    [TestFixture]
    public class StrategyAdvisorThemeTests
    {
        [Test]
        public void GetNotification_GeneralNotification_ReturnsSemanticPresentation()
        {
            StrategyAdvisorNotificationTheme expected = new StrategyAdvisorNotificationTheme
            {
                NotificationType = AdvisorNotificationType.Maintenance,
            };
            StrategyAdvisorTheme theme = new StrategyAdvisorTheme();
            theme.Notifications.Add(expected);

            StrategyAdvisorNotificationTheme notification = theme.GetNotification(
                AdvisorNotificationType.Maintenance,
                null,
                AdvisorSubjectNotification.None
            );

            Assert.AreSame(expected, notification);
        }

        [Test]
        public void GetNotification_KnownSubject_ReturnsSubjectPresentation()
        {
            StrategyAdvisorNotificationTheme expected = new StrategyAdvisorNotificationTheme
            {
                SubjectTypeID = "subject-type",
                SubjectNotification = AdvisorSubjectNotification.Captured,
            };
            StrategyAdvisorTheme theme = new StrategyAdvisorTheme();
            theme.Notifications.Add(expected);

            StrategyAdvisorNotificationTheme notification = theme.GetNotification(
                AdvisorNotificationType.None,
                "subject-type",
                AdvisorSubjectNotification.Captured
            );

            Assert.AreSame(expected, notification);
        }

        [Test]
        public void GetNotification_UnconfiguredSubject_ReturnsDefaultSubjectPresentation()
        {
            StrategyAdvisorNotificationTheme expected = new StrategyAdvisorNotificationTheme
            {
                SubjectNotification = AdvisorSubjectNotification.Report,
            };
            StrategyAdvisorTheme theme = new StrategyAdvisorTheme();
            theme.Notifications.Add(expected);

            StrategyAdvisorNotificationTheme notification = theme.GetNotification(
                AdvisorNotificationType.None,
                "unconfigured-subject-type",
                AdvisorSubjectNotification.Report
            );

            Assert.AreSame(expected, notification);
        }

        [Test]
        public void GetNotificationKey_SharedQueueGroup_ReturnsSameSemanticKey()
        {
            StrategyAdvisorNotificationTheme general = new StrategyAdvisorNotificationTheme
            {
                NotificationType = AdvisorNotificationType.FieldPersonnel,
                QueueGroup = AdvisorNotificationType.FieldPersonnel,
            };
            StrategyAdvisorNotificationTheme subject = new StrategyAdvisorNotificationTheme
            {
                SubjectTypeID = "subject-type",
                SubjectNotification = AdvisorSubjectNotification.Report,
                QueueGroup = AdvisorNotificationType.FieldPersonnel,
            };

            string generalKey = StrategyAdvisorTheme.GetNotificationKey(general);
            string subjectKey = StrategyAdvisorTheme.GetNotificationKey(subject);

            Assert.AreEqual("Group:FieldPersonnel", generalKey);
            Assert.AreEqual(generalKey, subjectKey);
        }

        [Test]
        public void GetFramePath_AdvisorTheme_ReturnsRoleResourceAndFramePath()
        {
            StrategyAdvisorTheme theme = new StrategyAdvisorTheme
            {
                AnimationImageRoot =
                    "Pack/Factions/Example/Strategy/Advisor/Animations/Notifications",
            };

            string path = theme.GetFramePath("Standard", 4, true);

            Assert.AreEqual(
                "Pack/Factions/Example/Strategy/Advisor/Animations/Notifications/Alert/Standard/frame-004",
                path
            );
        }

        [Test]
        public void GetFramePath_BriefingTheme_ReturnsResourceAndFramePath()
        {
            StrategyBriefingTheme theme = new StrategyBriefingTheme
            {
                AnimationImageRoot = "Pack/Factions/Example/Strategy/Advisor/Animations/Briefings",
            };

            string path = theme.GetFramePath("Introduction", 12);

            Assert.AreEqual(
                "Pack/Factions/Example/Strategy/Advisor/Animations/Briefings/Introduction/frame-012",
                path
            );
        }

        [Test]
        public void GetAudioPath_AdvisorTheme_ReturnsNamedAudioPath()
        {
            StrategyAdvisorTheme theme = new StrategyAdvisorTheme
            {
                AudioRoot = "Pack/Factions/Example/Strategy/Advisor/Audio/Notifications",
            };

            string path = theme.GetAudioPath("FleetArrived");

            Assert.AreEqual(
                "Pack/Factions/Example/Strategy/Advisor/Audio/Notifications/FleetArrived",
                path
            );
        }

        /// <summary>
        /// Verifies opening preload data excludes briefing segments that are not yet visible.
        /// </summary>
        [Test]
        public void CreateOpeningPreloadManifest_Briefing_ReturnsOpeningAndSkipMedia()
        {
            StrategyBriefingTheme theme = new StrategyBriefingTheme
            {
                AnimationImageRoot = "Pack/Factions/Example/Strategy/Advisor/Animations/Briefings",
                AudioRoot = "Pack/Factions/Example/Strategy/Advisor/Audio/Briefings",
                Skip = new StrategyBriefingSegmentTheme { Animation = "Skip", Audio = "Skip" },
            };
            theme.Segments.Add(
                new StrategyBriefingSegmentTheme
                {
                    Animation = "Introduction",
                    Audio = "Introduction",
                }
            );
            theme.Segments.Add(
                new StrategyBriefingSegmentTheme
                {
                    Animation = "Introduction",
                    Audio = "Introduction",
                }
            );

            ContentPreloadManifest manifest = theme.CreateOpeningPreloadManifest();

            Assert.AreEqual(64, manifest.TexturesPerFrame);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Pack/Factions/Example/Strategy/Advisor/Animations/Briefings/Introduction",
                    "Pack/Factions/Example/Strategy/Advisor/Animations/Briefings/Skip",
                },
                manifest.TextureDirectories
            );
            CollectionAssert.AreEqual(
                new[]
                {
                    "Pack/Factions/Example/Strategy/Advisor/Audio/Briefings/Introduction",
                    "Pack/Factions/Example/Strategy/Advisor/Audio/Briefings/Skip",
                },
                manifest.Audio
            );
        }

        /// <summary>
        /// Verifies an incremental preload manifest contains only its requested segment.
        /// </summary>
        [Test]
        public void CreateSegmentPreloadManifest_Segment_ReturnsOnlyRequestedMedia()
        {
            StrategyBriefingTheme theme = new StrategyBriefingTheme
            {
                AnimationImageRoot = "Pack/Factions/Example/Strategy/Advisor/Animations/Briefings",
                AudioRoot = "Pack/Factions/Example/Strategy/Advisor/Audio/Briefings",
            };
            StrategyBriefingSegmentTheme segment = new StrategyBriefingSegmentTheme
            {
                Animation = "PopularSupport",
                Audio = "PopularSupport",
            };

            ContentPreloadManifest manifest = theme.CreateSegmentPreloadManifest(segment);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Pack/Factions/Example/Strategy/Advisor/Animations/Briefings/PopularSupport",
                },
                manifest.TextureDirectories
            );
            CollectionAssert.AreEqual(
                new[] { "Pack/Factions/Example/Strategy/Advisor/Audio/Briefings/PopularSupport" },
                manifest.Audio
            );
        }
    }
}
