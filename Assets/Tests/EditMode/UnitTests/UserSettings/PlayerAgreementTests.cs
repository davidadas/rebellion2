using System;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UserSettings
{
    [TestFixture]
    public sealed class PlayerAgreementTests
    {
        [Test]
        public void IsAccepted_NewSettings_ReturnsFalse()
        {
            PlayerAgreement agreement = new PlayerAgreement("Agreement", "Privacy");

            Assert.IsFalse(agreement.IsAccepted(new global::UserSettings()));
        }

        [Test]
        public void IsAccepted_AcceptanceSurvivesSerialization_ReturnsTrue()
        {
            PlayerAgreement agreement = new PlayerAgreement("Agreement", "Privacy");
            global::UserSettings settings = new global::UserSettings
            {
                AcceptedAgreementVersion = agreement.Version,
            };

            global::UserSettings restored = JsonUtility.FromJson<global::UserSettings>(
                JsonUtility.ToJson(settings)
            );

            Assert.IsTrue(agreement.IsAccepted(restored));
            Assert.AreEqual(UserAnalyticsConsent.Undecided, restored.Analytics.Consent);
        }

        [Test]
        public void IsAccepted_ChangedAgreement_RequiresNewAcceptance()
        {
            PlayerAgreement previous = new PlayerAgreement("Previous agreement", "Privacy");
            PlayerAgreement current = new PlayerAgreement("Updated agreement", "Privacy");
            global::UserSettings settings = new global::UserSettings
            {
                AcceptedAgreementVersion = previous.Version,
            };

            Assert.IsFalse(current.IsAccepted(settings));
        }

        [Test]
        public void IsAccepted_ChangedPrivacyNotice_RequiresNewAcceptance()
        {
            PlayerAgreement previous = new PlayerAgreement("Agreement", "Previous privacy notice");
            PlayerAgreement current = new PlayerAgreement("Agreement", "Updated privacy notice");
            global::UserSettings settings = new global::UserSettings
            {
                AcceptedAgreementVersion = previous.Version,
            };

            Assert.IsFalse(current.IsAccepted(settings));
        }

        [Test]
        public void IsAccepted_PlatformLineEndingsDiffer_PreservesAcceptance()
        {
            PlayerAgreement windows = new PlayerAgreement(
                "Agreement\r\nTerms",
                "Privacy\r\nNotice"
            );
            PlayerAgreement unix = new PlayerAgreement("Agreement\nTerms", "Privacy\nNotice");

            Assert.AreEqual(windows.Version, unix.Version);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void Constructor_MissingAgreement_RejectsDocument(string text)
        {
            Assert.Throws<ArgumentException>(() => new PlayerAgreement(text, "Privacy"));
        }

        [Test]
        public void Constructor_MissingPrivacyNotice_RejectsDocument()
        {
            Assert.Throws<ArgumentException>(() => new PlayerAgreement("Agreement", ""));
        }

        [Test]
        public void IsAccepted_LegacySettings_RequiresExplicitAcceptance()
        {
            global::UserSettings settings = JsonUtility.FromJson<global::UserSettings>("{}");
            settings.Normalize();

            Assert.IsFalse(new PlayerAgreement("Agreement", "Privacy").IsAccepted(settings));
        }
    }
}
