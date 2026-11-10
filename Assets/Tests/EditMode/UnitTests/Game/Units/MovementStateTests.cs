using System;
using System.Drawing;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Game.Units
{
    [TestFixture]
    public sealed class MovementStateTests
    {
        [Test]
        public void Advance_PartialLeg_InterpolatesPositionAndRemainingTime()
        {
            MovementState movement = new MovementState(
                new MovementLeg(10, new Point(10, 20), "destination"),
                "home",
                "group"
            );

            movement.Advance(4, new Point(110, 70));

            Assert.AreEqual(new Point(50, 40), movement.CurrentPosition);
            Assert.AreEqual(6, movement.TicksRemaining());
            Assert.IsFalse(movement.IsComplete());
        }

        [Test]
        public void Advance_ContinuedJourney_PreservesContextAcrossLegBoundary()
        {
            MovementState movement = CreateContinuedJourney();
            movement.MarkObserved(100);

            movement.Advance(6, new Point(200, 20));

            Assert.AreEqual(new Point(100, 20), movement.CurrentPosition);
            Assert.AreEqual(0, movement.TicksElapsed);
            Assert.AreEqual("final-stop", movement.DestinationPlanetInstanceID);
            Assert.IsNull(movement.NextLeg);
            Assert.AreEqual("origin", movement.PreviousLocationInstanceID);
            Assert.AreEqual("group", movement.MovementGroupID);
            Assert.AreEqual("event", movement.SourceEventInstanceID);
            Assert.IsTrue(movement.ResolveEncounterOnArrival);
            Assert.IsTrue(movement.IsObservation);
            Assert.AreEqual(100, movement.ObservedAtTick);
            Assert.IsFalse(movement.IsComplete());
            Assert.AreEqual(10, movement.TicksRemaining());

            movement.Advance(10, new Point(200, 20));

            Assert.IsTrue(movement.IsComplete());
            Assert.AreEqual(0, movement.TicksRemaining());
            Assert.AreEqual(new Point(200, 20), movement.CurrentPosition);
        }

        [Test]
        public void Advance_AcrossLegBoundary_CarriesElapsedTicksIntoContinuation()
        {
            MovementState movement = CreateContinuedJourney();

            movement.Advance(11, new Point(200, 20));

            Assert.AreEqual(5, movement.TicksElapsed);
            Assert.AreEqual(5, movement.TicksRemaining());
            Assert.AreEqual(new Point(150, 20), movement.CurrentPosition);
        }

        [Test]
        public void Advance_NegativeTicks_RejectsClockRewind()
        {
            MovementState movement = CreateContinuedJourney();

            Assert.Throws<ArgumentOutOfRangeException>(() => movement.Advance(-1, Point.Empty));
            Assert.AreEqual(4, movement.TicksElapsed);
            Assert.AreEqual(16, movement.TicksRemaining());
        }

        [Test]
        public void Redirect_FinishedLeg_StartsContinuationWithoutReplacingContext()
        {
            MovementState movement = new MovementState(
                new MovementLeg(4, Point.Empty, "first-stop"),
                "origin",
                "group",
                "event",
                true
            );
            movement.Advance(4, new Point(100, 20));

            movement.Redirect(
                new MovementLeg(6, new Point(100, 20), "final-stop"),
                "first-stop",
                "origin"
            );

            Assert.AreEqual(0, movement.TicksElapsed);
            Assert.AreEqual(6, movement.TicksRemaining());
            Assert.AreEqual("final-stop", movement.DestinationPlanetInstanceID);
            Assert.AreEqual("event", movement.SourceEventInstanceID);
            Assert.IsNull(movement.NextLeg);
        }

        [Test]
        public void CreateCopy_ObservedMovement_PreservesObservationTiming()
        {
            MovementState movement = new MovementState(
                new MovementLeg(20, Point.Empty, "destination"),
                "origin",
                "group"
            );
            movement.Advance(10, new Point(100, 0));
            movement.MarkObserved(100);

            MovementState copy = movement.CreateCopy();

            Assert.IsTrue(copy.IsObservation);
            Assert.AreEqual(100, copy.ObservedAtTick);
            Assert.AreEqual(10, copy.TicksRemaining());
        }

        [Test]
        public void MarkObserved_ExistingObservation_RetainsKnownTime()
        {
            MovementState movement = new MovementState(
                new MovementLeg(20, Point.Empty, "destination"),
                "origin",
                "group"
            );
            movement.MarkObserved(100);

            movement.MarkObserved();

            Assert.IsTrue(movement.IsObservation);
            Assert.AreEqual(100, movement.ObservedAtTick);
        }

        [Test]
        public void CreateCopy_ContinuedJourney_CopiesBothLegsIndependently()
        {
            MovementState movement = CreateContinuedJourney();

            MovementState copy = movement.CreateCopy();
            copy.Advance(9, new Point(200, 20));

            Assert.AreNotSame(movement, copy);
            Assert.AreEqual("origin", copy.PreviousLocationInstanceID);
            Assert.AreEqual("final-stop", copy.DestinationPlanetInstanceID);
            Assert.AreEqual("first-stop", movement.DestinationPlanetInstanceID);
            Assert.AreEqual("final-stop", movement.NextLeg.DestinationPlanetInstanceID);
            Assert.AreEqual(16, movement.TicksRemaining());
            Assert.AreEqual(7, copy.TicksRemaining());
        }

        [Test]
        public void Serialize_ContinuedJourney_RoundTripsTimingAndContextWithoutDuplicatingMetadata()
        {
            MovementState movement = CreateContinuedJourney();
            movement.MarkObserved(100);

            string xml = SerializationHelper.Serialize(movement);
            MovementState restored = SerializationHelper.Deserialize<MovementState>(xml);

            Assert.AreEqual("origin", restored.PreviousLocationInstanceID);
            Assert.AreEqual("first-stop", restored.DestinationPlanetInstanceID);
            Assert.AreEqual("final-stop", restored.NextLeg.DestinationPlanetInstanceID);
            Assert.AreEqual(new Point(100, 20), restored.NextLeg.OriginPosition);
            Assert.AreEqual(16, restored.TicksRemaining());
            XElement continuation = XDocument.Parse(xml).Root.Element("NextLeg");
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "TransitTicks",
                    "OriginPositionX",
                    "OriginPositionY",
                    "DestinationPlanetInstanceID",
                },
                continuation.Elements().Select(element => element.Name.LocalName)
            );
            Assert.IsEmpty(continuation.Attributes());

            restored.Advance(6, new Point(200, 20));

            Assert.AreEqual("event", restored.SourceEventInstanceID);
            Assert.AreEqual("group", restored.MovementGroupID);
            Assert.AreEqual(100, restored.ObservedAtTick);
            Assert.IsTrue(restored.ResolveEncounterOnArrival);
        }

        [Test]
        public void Deserialize_LegacyFlatMovement_PreservesTimingAndLeavesUnknownMetadataUnset()
        {
            MovementState restored = SerializationHelper.Deserialize<MovementState>(
                "<MovementState><TransitTicks>20</TransitTicks><TicksElapsed>10</TicksElapsed>"
                    + "<OriginPositionX>12</OriginPositionX><OriginPositionY>34</OriginPositionY>"
                    + "<CurrentPositionX>56</CurrentPositionX><CurrentPositionY>78</CurrentPositionY></MovementState>"
            );

            Assert.IsNull(restored.ObservedAtTick);
            Assert.IsFalse(restored.IsObservation);
            Assert.IsNull(restored.PreviousLocationInstanceID);
            Assert.IsNull(restored.DestinationPlanetInstanceID);
            Assert.IsNull(restored.NextLeg);
            Assert.AreEqual(10, restored.TicksRemaining());
            Assert.AreEqual(new Point(12, 34), restored.OriginPosition);
            Assert.AreEqual(new Point(56, 78), restored.CurrentPosition);
        }

        [Test]
        public void Deserialize_LegacyContinuation_UsesRootMetadataAfterLegTransition()
        {
            MovementState restored = SerializationHelper.Deserialize<MovementState>(
                "<MovementState PreviousLocationInstanceID=\"origin\"><TransitTicks>10</TransitTicks><TicksElapsed>4</TicksElapsed>"
                    + "<MovementGroupID>group</MovementGroupID><SourceEventInstanceID>event</SourceEventInstanceID>"
                    + "<ResolveEncounterOnArrival>true</ResolveEncounterOnArrival><IsObservation>true</IsObservation><ObservedAtTick>100</ObservedAtTick>"
                    + "<DestinationPlanetInstanceID>first-stop</DestinationPlanetInstanceID>"
                    + "<NextLeg PreviousLocationInstanceID=\"origin\"><TransitTicks>10</TransitTicks><TicksElapsed>0</TicksElapsed>"
                    + "<MovementGroupID>group</MovementGroupID><SourceEventInstanceID>event</SourceEventInstanceID>"
                    + "<ResolveEncounterOnArrival>true</ResolveEncounterOnArrival><OriginPositionX>100</OriginPositionX><OriginPositionY>20</OriginPositionY>"
                    + "<CurrentPositionX>100</CurrentPositionX><CurrentPositionY>20</CurrentPositionY><IsObservation>false</IsObservation>"
                    + "<DestinationPlanetInstanceID>final-stop</DestinationPlanetInstanceID></NextLeg></MovementState>",
                new GameSerializerSettings { IgnoreUnknownElements = true }
            );

            restored.Advance(6, new Point(200, 20));

            Assert.AreEqual("final-stop", restored.DestinationPlanetInstanceID);
            Assert.AreEqual("origin", restored.PreviousLocationInstanceID);
            Assert.AreEqual("group", restored.MovementGroupID);
            Assert.AreEqual("event", restored.SourceEventInstanceID);
            Assert.IsTrue(restored.ResolveEncounterOnArrival);
            Assert.IsTrue(restored.IsObservation);
            Assert.AreEqual(100, restored.ObservedAtTick);
            Assert.AreEqual(10, restored.TicksRemaining());
        }

        [Test]
        public void Serialize_ClearedPreviousLocation_RetainsKnownEmptyValue()
        {
            MovementState movement = CreateContinuedJourney();
            movement.RecordPreviousLocation(string.Empty);

            MovementState restored = SerializationHelper.Deserialize<MovementState>(
                SerializationHelper.Serialize(movement)
            );

            Assert.AreEqual(string.Empty, restored.PreviousLocationInstanceID);
        }

        [Test]
        public void Serialize_TransitState_RoundTripsPositionsAndContext()
        {
            MovementState movement = new MovementState(
                new MovementLeg(9, new Point(12, 34), "destination"),
                "origin",
                "group-1",
                "SEND_OFFICER",
                true
            );
            movement.Advance(4, new Point(111, 133));
            movement.SetPosition(new Point(56, 78));

            MovementState restored = SerializationHelper.Deserialize<MovementState>(
                SerializationHelper.Serialize(movement)
            );

            Assert.AreEqual(9, restored.TransitTicks);
            Assert.AreEqual(4, restored.TicksElapsed);
            Assert.AreEqual("group-1", restored.MovementGroupID);
            Assert.AreEqual("SEND_OFFICER", restored.SourceEventInstanceID);
            Assert.IsTrue(restored.ResolveEncounterOnArrival);
            Assert.AreEqual(new Point(12, 34), restored.OriginPosition);
            Assert.AreEqual(new Point(56, 78), restored.CurrentPosition);
        }

        /// <summary>
        /// Starts a journey partway through its outbound leg with a planned continuation.
        /// </summary>
        /// <returns>The continuing journey.</returns>
        private static MovementState CreateContinuedJourney()
        {
            MovementState movement = new MovementState(
                new MovementLeg(10, Point.Empty, "first-stop"),
                "origin",
                "group",
                "event",
                true
            );
            movement.Advance(4, new Point(100, 20));
            movement.Redirect(
                new MovementLeg(10, new Point(100, 20), "final-stop"),
                "first-stop",
                "origin"
            );
            return movement;
        }
    }
}
