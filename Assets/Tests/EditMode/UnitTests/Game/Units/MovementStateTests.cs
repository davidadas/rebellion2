using System.Drawing;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Units
{
    [TestFixture]
    public sealed class MovementStateTests
    {
        [Test]
        public void CreateCopy_ObservedMovement_PreservesObservationTiming()
        {
            MovementState movement = new MovementState
            {
                TransitTicks = 20,
                TicksElapsed = 10,
                IsObservation = true,
                ObservedAtTick = 100,
            };

            MovementState copy = movement.CreateCopy();

            Assert.IsTrue(copy.IsObservation);
            Assert.AreEqual(100, copy.ObservedAtTick);
            Assert.AreEqual(10, copy.TicksRemaining());
        }

        [Test]
        public void Serialize_ObservedMovement_RoundTripsObservationTiming()
        {
            MovementState movement = new MovementState
            {
                IsObservation = true,
                ObservedAtTick = 100,
            };

            MovementState restored = SerializationHelper.Deserialize<MovementState>(
                SerializationHelper.Serialize(movement)
            );

            Assert.IsTrue(restored.IsObservation);
            Assert.AreEqual(100, restored.ObservedAtTick);
        }

        [Test]
        public void Deserialize_LegacyMovement_LeavesObservationTimeUnknown()
        {
            var xml = System.Xml.Linq.XDocument.Parse(
                SerializationHelper.Serialize(
                    new MovementState { TransitTicks = 20, TicksElapsed = 10 }
                )
            );
            foreach (
                var element in xml
                    .Root.Elements()
                    .Where(element => element.Name.LocalName is "ObservedAtTick" or "IsObservation")
                    .ToArray()
            )
                element.Remove();

            MovementState restored = SerializationHelper.Deserialize<MovementState>(xml.ToString());

            Assert.IsNull(restored.ObservedAtTick);
            Assert.IsFalse(restored.IsObservation);
            Assert.AreEqual(10, restored.TicksRemaining());
        }

        [Test]
        public void Serialize_TransitState_RoundTripsPositionsAndOrigin()
        {
            MovementState movement = new MovementState
            {
                TransitTicks = 9,
                TicksElapsed = 4,
                MovementGroupID = "group-1",
                SourceEventInstanceID = "SEND_OFFICER",
                ResolveEncounterOnArrival = true,
                OriginPosition = new Point(12, 34),
                CurrentPosition = new Point(56, 78),
            };

            string xml = SerializationHelper.Serialize(movement);
            MovementState restored = SerializationHelper.Deserialize<MovementState>(xml);

            Assert.AreEqual(9, restored.TransitTicks);
            Assert.AreEqual(4, restored.TicksElapsed);
            Assert.AreEqual("group-1", restored.MovementGroupID);
            Assert.AreEqual("SEND_OFFICER", restored.SourceEventInstanceID);
            Assert.IsTrue(restored.ResolveEncounterOnArrival);
            Assert.AreEqual(new Point(12, 34), restored.OriginPosition);
            Assert.AreEqual(new Point(56, 78), restored.CurrentPosition);
        }
    }
}
