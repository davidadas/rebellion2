using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UserSettings
{
    [TestFixture]
    public sealed class UserVideoSettingsTests
    {
        [Test]
        public void Normalize_VersionOneSettings_RestoresTacticalDefaultsAndUpgradesVersion()
        {
            global::UserSettings settings = new global::UserSettings
            {
                Version = 1,
                Video = new UserVideoSettings
                {
                    ShowStarfield = false,
                    ShowPlanet = false,
                    ShowPyro = false,
                    HighDetail = false,
                    ShowHolocube = false,
                },
            };

            settings.Normalize();

            Assert.AreEqual(global::UserSettings.CurrentVersion, settings.Version);
            Assert.IsTrue(settings.Video.ShowStarfield);
            Assert.IsTrue(settings.Video.ShowPlanet);
            Assert.IsTrue(settings.Video.ShowPyro);
            Assert.IsTrue(settings.Video.HighDetail);
            Assert.IsTrue(settings.Video.ShowHolocube);
        }

        [TestCase(UserTacticalOption.Starfield)]
        [TestCase(UserTacticalOption.Planet)]
        [TestCase(UserTacticalOption.Pyro)]
        [TestCase(UserTacticalOption.HighDetail)]
        [TestCase(UserTacticalOption.Holocube)]
        public void SetEnabled_OptionDisabled_UpdatesRequestedOption(UserTacticalOption option)
        {
            UserVideoSettings settings = new UserVideoSettings();

            settings.SetEnabled(option, false);

            Assert.IsFalse(settings.IsEnabled(option));
        }

        [Test]
        public void JsonUtility_ExplicitTacticalOptions_RoundTripsState()
        {
            global::UserSettings settings = new global::UserSettings();
            settings.Video.SetEnabled(UserTacticalOption.Starfield, false);
            settings.Video.SetEnabled(UserTacticalOption.HighDetail, false);

            string json = JsonUtility.ToJson(settings);
            global::UserSettings restored = JsonUtility.FromJson<global::UserSettings>(json);
            restored.Normalize();

            Assert.IsFalse(restored.Video.ShowStarfield);
            Assert.IsTrue(restored.Video.ShowPlanet);
            Assert.IsTrue(restored.Video.ShowPyro);
            Assert.IsFalse(restored.Video.HighDetail);
            Assert.IsTrue(restored.Video.ShowHolocube);
        }
    }
}
