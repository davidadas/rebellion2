using NUnit.Framework;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Hud
{
    [TestFixture]
    public class AutosaveIndicatorTimingTests
    {
        private AutosaveIndicatorTiming timing;

        /// <summary>
        /// Creates a hidden indicator for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            timing = new AutosaveIndicatorTiming();
        }

        [Test]
        public void Refresh_AutosaveStarts_ShowsLabel()
        {
            Assert.IsTrue(timing.Refresh(true, 10f));
            Assert.IsTrue(timing.IsVisible);
        }

        [Test]
        public void Refresh_StateUnchanged_ReportsNoChange()
        {
            Assert.IsFalse(timing.Refresh(false, 10f));
            timing.Refresh(true, 10f);

            Assert.IsFalse(timing.Refresh(true, 30f));
            Assert.IsTrue(timing.IsVisible);
        }

        [Test]
        public void Refresh_AutosaveFinishesBeforeMinimum_KeepsLabelUntilMinimumElapses()
        {
            timing.Refresh(true, 10f);

            Assert.IsFalse(timing.Refresh(false, 10.2f));
            Assert.IsTrue(timing.IsVisible);
            Assert.IsFalse(
                timing.Refresh(false, 10f + AutosaveIndicatorTiming.MinimumVisibleSeconds - 0.01f)
            );
            Assert.IsTrue(
                timing.Refresh(false, 10f + AutosaveIndicatorTiming.MinimumVisibleSeconds)
            );
            Assert.IsFalse(timing.IsVisible);
        }

        [Test]
        public void Refresh_AutosaveFinishesAfterMinimum_HidesLabelImmediately()
        {
            timing.Refresh(true, 10f);

            Assert.IsTrue(timing.Refresh(false, 15f));
            Assert.IsFalse(timing.IsVisible);
        }

        [Test]
        public void Refresh_AutosaveRestartsAfterHiding_RestartsMinimum()
        {
            timing.Refresh(true, 10f);
            timing.Refresh(false, 15f);

            timing.Refresh(true, 20f);

            Assert.IsFalse(timing.Refresh(false, 20.5f));
            Assert.IsTrue(timing.IsVisible);
        }
    }
}
