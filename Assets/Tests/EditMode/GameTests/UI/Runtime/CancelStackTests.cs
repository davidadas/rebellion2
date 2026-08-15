using NUnit.Framework;

namespace Rebellion.Tests.UI.Runtime
{
    [TestFixture]
    public class CancelStackTests
    {
        [Test]
        public void TryCancel_UsesMostRecentlyRegisteredCancelableFirst()
        {
            CancelStack stack = new CancelStack();
            TestCancelable first = new TestCancelable(true);
            TestCancelable second = new TestCancelable(true);

            stack.Register(first);
            stack.Register(second);

            Assert.IsTrue(stack.TryCancel());
            Assert.AreEqual(0, first.CancelCount);
            Assert.AreEqual(1, second.CancelCount);
        }

        [Test]
        public void TryCancel_FallsThroughWhenCancelableDoesNotConsume()
        {
            CancelStack stack = new CancelStack();
            TestCancelable first = new TestCancelable(true);
            TestCancelable second = new TestCancelable(false);

            stack.Register(first);
            stack.Register(second);

            Assert.IsTrue(stack.TryCancel());
            Assert.AreEqual(1, first.CancelCount);
            Assert.AreEqual(1, second.CancelCount);
        }

        /// <summary>
        /// Verifies reopening a cancelable promotes it ahead of previously registered handlers.
        /// </summary>
        [Test]
        public void Register_ExistingCancelable_PromotesItToTop()
        {
            CancelStack stack = new CancelStack();
            TestCancelable menu = new TestCancelable(true);
            TestCancelable windowManager = new TestCancelable(true);

            stack.Register(menu);
            stack.Register(windowManager);
            stack.Register(menu);

            Assert.IsTrue(stack.TryCancel());
            Assert.AreEqual(1, menu.CancelCount);
            Assert.AreEqual(0, windowManager.CancelCount);
        }

        [Test]
        public void Unregister_RemovesCancelableFromStack()
        {
            CancelStack stack = new CancelStack();
            TestCancelable first = new TestCancelable(true);
            TestCancelable second = new TestCancelable(true);

            stack.Register(first);
            stack.Register(second);
            stack.Unregister(second);

            Assert.IsTrue(stack.TryCancel());
            Assert.AreEqual(1, first.CancelCount);
            Assert.AreEqual(0, second.CancelCount);
        }

        private sealed class TestCancelable : ICancelable
        {
            private readonly bool _consume;

            public TestCancelable(bool consume)
            {
                _consume = consume;
            }

            public int CancelCount { get; private set; }

            public bool TryCancel()
            {
                CancelCount++;
                return _consume;
            }
        }
    }
}
