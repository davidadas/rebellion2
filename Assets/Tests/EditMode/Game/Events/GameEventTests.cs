using System;
using NUnit.Framework;
using Rebellion.Game.Events;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public sealed class GameEventTests
    {
        [Test]
        public void ValidateRunLimits_MinimumExceedsMaximum_ThrowsInvalidOperationException()
        {
            GameEvent gameEvent = new GameEvent { MinimumRuns = 3, MaximumRuns = 2 };

            TestDelegate validate = gameEvent.ValidateRunLimits;

            Assert.Throws<InvalidOperationException>(validate);
        }

        [Test]
        public void ValidateRunLimits_UnlimitedWithMaximum_ThrowsInvalidOperationException()
        {
            GameEvent gameEvent = new GameEvent { MaximumRuns = 2, UnlimitedRuns = true };

            TestDelegate validate = gameEvent.ValidateRunLimits;

            Assert.Throws<InvalidOperationException>(validate);
        }
    }
}
