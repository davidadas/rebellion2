using NUnit.Framework;

namespace Rebellion.Tests.UI.StrategyView
{
    [TestFixture]
    public sealed class CancelStackTests
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
            private readonly bool consume;

            public TestCancelable(bool consume)
            {
                this.consume = consume;
            }

            public int CancelCount { get; private set; }

            public bool TryCancel()
            {
                CancelCount++;
                return consume;
            }
        }
    }
}
