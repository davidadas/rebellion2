using NUnit.Framework;

namespace Rebellion.Tests.UI.SceneUI.LoadingScreen
{
    [TestFixture]
    public sealed class LimitedYawDragTests
    {
        [Test]
        public void Move_LargeHorizontalDrag_ClampsToConfiguredArc()
        {
            LimitedYawDrag drag = new LimitedYawDrag(45f, 110f);
            drag.Begin(500f, 0f);

            drag.Move(5000f, 1000f);

            Assert.AreEqual(45f, drag.TargetYaw);
            drag.Begin(500f, 0f);
            drag.Move(-5000f, 1000f);
            Assert.AreEqual(-45f, drag.TargetYaw);
        }

        [Test]
        public void End_ActiveGesture_RequestsCenteredView()
        {
            LimitedYawDrag drag = new LimitedYawDrag(45f, 110f);
            drag.Begin(100f, 0f);
            drag.Move(300f, 1000f);
            drag.End();

            drag.Move(900f, 1000f);

            Assert.AreEqual(0f, drag.TargetYaw);
            Assert.IsFalse(drag.IsActive);
        }

        [Test]
        public void Begin_ReturningCamera_ContinuesFromDisplayedYaw()
        {
            LimitedYawDrag drag = new LimitedYawDrag(45f, 110f);

            drag.Begin(200f, 12f);
            drag.Move(300f, 1000f);

            Assert.AreEqual(23f, drag.TargetYaw);
        }

        [Test]
        public void Reset_SelectedYaw_RecentersAndEndsGesture()
        {
            LimitedYawDrag drag = new LimitedYawDrag(45f, 110f);
            drag.Begin(100f, 0f);
            drag.Move(300f, 1000f);

            drag.Reset();

            Assert.AreEqual(0f, drag.TargetYaw);
            Assert.IsFalse(drag.IsActive);
        }
    }
}
