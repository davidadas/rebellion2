using System;
using NUnit.Framework;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatRenderSettingsTests
    {
        [Test]
        public void Constructor_NullSettings_RejectsMissingInput() =>
            Assert.Throws<ArgumentNullException>(() => new TacticalCombatRenderSettings(null));

        [Test]
        public void Constructor_AuthoredDefaults_CapturesEnabledFeatures()
        {
            TacticalCombatRenderSettings snapshot = new(new UserVideoSettings());

            Assert.IsTrue(snapshot.ShowStarfield);
            Assert.IsTrue(snapshot.ShowPlanet);
            Assert.IsTrue(snapshot.ShowPyro);
            Assert.IsTrue(snapshot.HighDetail);
            Assert.IsTrue(snapshot.ShowHolocube);
        }

        [Test]
        public void Constructor_SettingsChangedAfterCapture_PreservesEntryOptions()
        {
            UserVideoSettings settings = new()
            {
                ShowStarfield = false,
                ShowPlanet = true,
                ShowPyro = false,
                HighDetail = true,
                ShowHolocube = false,
            };
            TacticalCombatRenderSettings snapshot = new(settings);

            settings.ShowStarfield = true;
            settings.ShowPlanet = false;
            settings.ShowPyro = true;
            settings.HighDetail = false;
            settings.ShowHolocube = true;

            Assert.IsFalse(snapshot.ShowStarfield);
            Assert.IsTrue(snapshot.ShowPlanet);
            Assert.IsFalse(snapshot.ShowPyro);
            Assert.IsTrue(snapshot.HighDetail);
            Assert.IsFalse(snapshot.ShowHolocube);
        }
    }
}
