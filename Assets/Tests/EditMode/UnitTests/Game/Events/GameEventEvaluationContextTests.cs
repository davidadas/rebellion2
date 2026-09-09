using System;
using NUnit.Framework;
using Rebellion.Game.Events;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class GameEventEvaluationContextTests
    {
        [Test]
        public void Bind_BlankName_ThrowsArgumentException()
        {
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState(),
                null
            );

            TestDelegate bind = () => context.Bind(" ", new object());

            Assert.Throws<ArgumentException>(bind);
        }

        [Test]
        public void Bind_DuplicateName_ThrowsInvalidOperationException()
        {
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState(),
                null
            );
            context.Bind("binding", 1);

            TestDelegate bind = () => context.Bind("binding", 2);

            Assert.Throws<InvalidOperationException>(bind);
        }

        [Test]
        public void GetBindingReference_ExactOpaqueName_ReturnsValue()
        {
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState(),
                null
            );
            context.Bind("#@$#@binding", 42);

            int value = context.GetBindingReference<int>("#@$#@binding");

            Assert.AreEqual(42, value);
        }

        [Test]
        public void AddResult_NullResult_DoesNotRecordResult()
        {
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState(),
                null
            );

            context.AddResult(null);

            Assert.IsEmpty(context.Results);
        }
    }
}
