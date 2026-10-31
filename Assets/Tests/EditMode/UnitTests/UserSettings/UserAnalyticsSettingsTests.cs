using NUnit.Framework;

namespace Rebellion.Tests.UserSettings
{
    [TestFixture]
    public sealed class UserAnalyticsSettingsTests
    {
        [Test]
        public void Normalize_UnsupportedConsent_RestoresUndecidedChoice()
        {
            UserAnalyticsSettings settings = new UserAnalyticsSettings
            {
                Consent = (UserAnalyticsConsent)99,
            };

            settings.Normalize();

            Assert.AreEqual(UserAnalyticsConsent.Undecided, settings.Consent);
            Assert.IsFalse(settings.IsEnabled);
        }

        [Test]
        public void IsEnabled_GrantedConsent_ReturnsTrue()
        {
            UserAnalyticsSettings settings = new UserAnalyticsSettings
            {
                Consent = UserAnalyticsConsent.Granted,
            };

            Assert.IsTrue(settings.IsEnabled);
        }
    }
}
