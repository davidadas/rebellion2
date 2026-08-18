using System.Drawing;
using NUnit.Framework;
using Rebellion.Game.Movement;

namespace Rebellion.Tests.Game.Movement
{
    [TestFixture]
    public sealed class MovementStateTests
    {
        [Test]
        public void Serialize_TransitState_RoundTripsPositionsAndOrigin()
        {
            MovementState movement = new MovementState
            {
                TransitTicks = 9,
                TicksElapsed = 4,
                MovementGroupID = "group-1",
                SourceEventInstanceID = "SEND_OFFICER",
                OriginPosition = new Point(12, 34),
                CurrentPosition = new Point(56, 78),
            };

            string xml = SerializationHelper.Serialize(movement);
            MovementState restored = SerializationHelper.Deserialize<MovementState>(xml);

            Assert.AreEqual(9, restored.TransitTicks);
            Assert.AreEqual(4, restored.TicksElapsed);
            Assert.AreEqual("group-1", restored.MovementGroupID);
            Assert.AreEqual("SEND_OFFICER", restored.SourceEventInstanceID);
            Assert.AreEqual(new Point(12, 34), restored.OriginPosition);
            Assert.AreEqual(new Point(56, 78), restored.CurrentPosition);
        }
    }
}
