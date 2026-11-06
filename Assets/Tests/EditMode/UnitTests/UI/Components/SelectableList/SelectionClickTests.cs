using NUnit.Framework;

namespace Rebellion.Tests.UI.Components.SelectableList
{
    [TestFixture]
    public sealed class SelectionClickTests
    {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void Complete_MatchingPress_ReturnsCapturedModifiers(bool control, bool shift)
        {
            SelectionClick click = new();
            click.Capture("unit", -1, new SelectionModifierState(control, shift));

            SelectionModifierState? result = click.Complete("unit", -1);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(control, result.Value.MultiSelect);
            Assert.AreEqual(shift, result.Value.RangeSelect);
        }

        [Test]
        public void Complete_NoPress_ReturnsNoSnapshot()
        {
            SelectionClick click = new();

            Assert.IsNull(click.Complete("unit", -1));
        }

        [TestCase("other", -1)]
        [TestCase("unit", 2)]
        public void Complete_DifferentItemOrPointer_DiscardsSnapshot(string itemId, int pointerId)
        {
            SelectionClick click = new();
            click.Capture("unit", -1, new SelectionModifierState(false, true));

            Assert.IsNull(click.Complete(itemId, pointerId));
            Assert.IsNull(click.Complete("unit", -1));
        }

        [Test]
        public void Complete_RepeatedRelease_DoesNotReuseSnapshot()
        {
            SelectionClick click = new();
            click.Capture("unit", -1, new SelectionModifierState(true, false));
            click.Complete("unit", -1);

            Assert.IsNull(click.Complete("unit", -1));
        }

        [Test]
        public void Capture_NewPress_ReplacesUnfinishedGesture()
        {
            SelectionClick click = new();
            click.Capture("first", -1, new SelectionModifierState(false, true));

            click.Capture("second", 2, new SelectionModifierState(true, false));
            SelectionModifierState? result = click.Complete("second", 2);

            Assert.IsTrue(result.HasValue);
            Assert.IsTrue(result.Value.MultiSelect);
            Assert.IsFalse(result.Value.RangeSelect);
        }

        [Test]
        public void Capture_SeparateWindows_KeepIndependentSnapshots()
        {
            SelectionClick first = new();
            SelectionClick second = new();
            first.Capture("unit", -1, new SelectionModifierState(false, true));

            second.Capture("unit", -1, new SelectionModifierState(true, false));

            Assert.IsTrue(first.Complete("unit", -1).Value.RangeSelect);
            Assert.IsTrue(second.Complete("unit", -1).Value.MultiSelect);
        }
    }
}
