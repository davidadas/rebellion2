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
