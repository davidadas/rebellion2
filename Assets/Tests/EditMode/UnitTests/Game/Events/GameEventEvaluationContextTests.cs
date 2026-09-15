using System;
using NUnit.Framework;
using Rebellion.Game.Events;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class GameEventEvaluationContextTests
    {
        /// <summary>
        /// Verifies bind blank name throws argument exception.
        /// </summary>
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

        /// <summary>
        /// Verifies bind duplicate name throws invalid operation exception.
        /// </summary>
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

        /// <summary>
        /// Verifies get binding reference exact opaque name returns value.
        /// </summary>
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

        /// <summary>
        /// Verifies add result null result does not record result.
        /// </summary>
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
