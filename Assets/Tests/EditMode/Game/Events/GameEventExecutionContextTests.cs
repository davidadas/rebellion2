using System;
using NUnit.Framework;
using Rebellion.Game.Events;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class GameEventExecutionContextTests
    {
        [Test]
        public void Constructor_UnitArrival_BindsUnitDestinationAndPlanet()
        {
            Officer officer = new Officer { InstanceID = "officer" };
            Planet destination = new Planet { InstanceID = "planet" };
            UnitArrivedResult arrival = new UnitArrivedResult
            {
                Unit = officer,
                Destination = destination,
            };

            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                new GameEventState(),
                null,
                arrival
            );

            Assert.AreSame(officer, context.GetBinding<Officer>("unit"));
            Assert.AreSame(destination, context.GetBinding<Planet>("destination"));
            Assert.AreSame(destination, context.GetBinding<Planet>("planet"));
        }

        [Test]
        public void Bind_BlankName_ThrowsArgumentException()
        {
            GameEventExecutionContext context = new GameEventExecutionContext(
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
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                new GameEventState(),
                null
            );

            context.AddResult(null);

            Assert.IsEmpty(context.Results);
        }
    }
}
