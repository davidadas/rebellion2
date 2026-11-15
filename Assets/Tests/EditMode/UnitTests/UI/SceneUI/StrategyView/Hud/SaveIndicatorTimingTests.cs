using NUnit.Framework;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Hud
{
    [TestFixture]
    public class SaveIndicatorTimingTests
    {
        private SaveIndicatorTiming timing;

        /// <summary>
        /// Creates a hidden indicator for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            timing = new SaveIndicatorTiming();
        }

        [TestCase(SaveGameKind.Automatic)]
        [TestCase(SaveGameKind.Quick)]
        [TestCase(SaveGameKind.Manual)]
        public void Refresh_SaveStarts_ShowsMatchingKind(SaveGameKind kind)
        {
            Assert.IsTrue(timing.Refresh(kind, 10f));
            Assert.IsTrue(timing.IsVisible);
            Assert.AreEqual(kind, timing.SaveKind);
        }

        [Test]
        public void Refresh_StateUnchanged_ReportsNoChange()
        {
            Assert.IsFalse(timing.Refresh(null, 10f));
            timing.Refresh(SaveGameKind.Automatic, 10f);

            Assert.IsFalse(timing.Refresh(SaveGameKind.Automatic, 30f));
            Assert.IsTrue(timing.IsVisible);
        }

        [Test]
        public void Refresh_AutosaveFinishesBeforeMinimum_KeepsLabelUntilMinimumElapses()
        {
            timing.Refresh(SaveGameKind.Automatic, 10f);

            Assert.IsFalse(timing.Refresh(null, 10.2f));
            Assert.IsTrue(timing.IsVisible);
            Assert.IsFalse(
                timing.Refresh(null, 10f + SaveIndicatorTiming.MinimumVisibleSeconds - 0.01f)
            );
            Assert.IsTrue(timing.Refresh(null, 10f + SaveIndicatorTiming.MinimumVisibleSeconds));
            Assert.IsFalse(timing.IsVisible);
        }

        [Test]
        public void Refresh_AutosaveFinishesAfterMinimum_HidesLabelImmediately()
        {
            timing.Refresh(SaveGameKind.Automatic, 10f);

            Assert.IsTrue(timing.Refresh(null, 15f));
            Assert.IsFalse(timing.IsVisible);
        }

        [Test]
        public void Refresh_AutosaveRestartsAfterHiding_RestartsMinimum()
        {
            timing.Refresh(SaveGameKind.Automatic, 10f);
            timing.Refresh(null, 15f);

            timing.Refresh(SaveGameKind.Automatic, 20f);

            Assert.IsFalse(timing.Refresh(null, 20.5f));
            Assert.IsTrue(timing.IsVisible);
        }

        [Test]
        public void Refresh_SaveKindChanges_ChangesLabelAndRestartsMinimum()
        {
            timing.Refresh(SaveGameKind.Automatic, 10f);
            Assert.IsTrue(timing.Refresh(SaveGameKind.Quick, 10.8f));
            Assert.AreEqual(SaveGameKind.Quick, timing.SaveKind);
            Assert.IsFalse(timing.Refresh(null, 11.1f));
            Assert.IsTrue(timing.IsVisible);
            Assert.IsTrue(timing.Refresh(null, 12f));
        }

        [Test]
        public void Refresh_SameKindRestartsDuringMinimum_RestartsMinimumForNewSave()
        {
            timing.Refresh(SaveGameKind.Manual, 10f);
            timing.Refresh(null, 10.2f);
            timing.Refresh(SaveGameKind.Manual, 10.8f);
            Assert.IsFalse(timing.Refresh(null, 11.1f));
            Assert.IsTrue(timing.IsVisible);
            Assert.IsTrue(timing.Refresh(null, 12f));
        }
    }
}
