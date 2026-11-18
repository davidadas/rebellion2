using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Gameplay;
using Rebellion.Multiplayer;
using Rebellion.Tactical;
using Pose = Rebellion.Multiplayer.TacticalCombatPresentationBuffer.Pose;

namespace Rebellion.Tests.Multiplayer
{
    [TestFixture]
    public sealed class TacticalCombatPresentationBufferTests
    {
        [Test]
        public void TryGetPose_EmptyHistory_HasFiniteZeroCursorAndNoPose()
        {
            TacticalCombatPresentationBuffer buffer = new();

            buffer.Advance(100);

            Assert.AreEqual(0, buffer.TimestampMilliseconds);
            Assert.AreEqual(0, buffer.BufferedFrameCount);
            Assert.IsFalse(buffer.TryGetPose("unit", out _));
        }

        [Test]
        public void Advance_ContinuousSnapshots_InterpolatesPositionAndNormalizedHeading()
        {
            TacticalCombatPresentationBuffer buffer = new();
            buffer.Add(1, 0, Poses(new Pose(Vector3.Zero, Vector3.UnitZ)));
            buffer.Add(2, 100, Poses(new Pose(new Vector3(10, 20, 30), Vector3.UnitX)));

            buffer.Advance(50);

            Assert.AreEqual(50, buffer.TimestampMilliseconds);
            Assert.IsTrue(buffer.TryGetPose("unit", out Pose pose));
            Assert.AreEqual(new Vector3(5, 10, 15), pose.Position);
            Assert.That(
                Vector3.Distance(Vector3.Normalize(Vector3.UnitX + Vector3.UnitZ), pose.Heading),
                Is.LessThan(.00001f)
            );
        }

        [Test]
        public void Advance_PacketsStop_ReachesLastPoseWithoutExtrapolation()
        {
            TacticalCombatPresentationBuffer buffer = new();
            buffer.Add(1, 0, Poses(At(0)));
            buffer.Add(2, 100, Poses(At(10)));

            buffer.Advance(100);

            Assert.AreEqual(100, buffer.TimestampMilliseconds);
            AssertPoseX(buffer, 10);
            buffer.Advance(float.MaxValue);
            Assert.AreEqual(100, buffer.TimestampMilliseconds);
            AssertPoseX(buffer, 10);
        }

        [Test]
        public void Add_JitteredReceipt_UsesTimestampBracketWithoutRewindingCursor()
        {
            TacticalCombatPresentationBuffer buffer = new();
            buffer.Add(1, 0, Poses(At(0)));
            buffer.Add(2, 100, Poses(At(10)));
            buffer.Advance(80);
            AssertPoseX(buffer, 8);

            buffer.Add(3, 120, Poses(At(12)));

            Assert.AreEqual(80, buffer.TimestampMilliseconds);
            AssertPoseX(buffer, 8);
            buffer.Advance(30);
            Assert.AreEqual(110, buffer.TimestampMilliseconds);
            AssertPoseX(buffer, 11);
        }

        [Test]
        public void Add_HistoryExceedsCapacity_RetainsBoundedRecentSnapshots()
        {
            TacticalCombatPresentationBuffer buffer = new(capacity: 4);
            for (int i = 0; i < 80; i++)
            {
                Assert.IsTrue(buffer.Add(i, i * 10, Poses(At(i))));
                Assert.LessOrEqual(buffer.BufferedFrameCount, 4);
            }

            buffer.Advance(100);

            Assert.AreEqual(4, buffer.BufferedFrameCount);
            Assert.AreEqual(790, buffer.TimestampMilliseconds);
            AssertPoseX(buffer, 79);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void Add_StaleSequence_DoesNotReplaceNewerPose(int sequence)
        {
            TacticalCombatPresentationBuffer buffer = new();
            buffer.Add(2, 100, Poses(At(10)));

            Assert.IsFalse(buffer.Add(sequence, 200, Poses(At(99))));

            Assert.AreEqual(1, buffer.BufferedFrameCount);
            Assert.AreEqual(100, buffer.TimestampMilliseconds);
            AssertPoseX(buffer, 10);
        }

        [TestCase(100)]
        [TestCase(50)]
        public void Add_NewerSequenceWithNonIncreasingTimestamp_SnapsAndRebases(double timestamp)
        {
            TacticalCombatPresentationBuffer buffer = new();
            buffer.Add(1, 0, Poses(At(0)));
            buffer.Add(2, 100, Poses(At(10)));

            Assert.IsTrue(buffer.Add(3, timestamp, Poses(At(20))));

            Assert.AreEqual(1, buffer.BufferedFrameCount);
            Assert.AreEqual(timestamp, buffer.TimestampMilliseconds);
            AssertPoseX(buffer, 20);
            buffer.Add(4, timestamp + 100, Poses(At(30)));
            buffer.Advance(50);
            AssertPoseX(buffer, 25);
        }

        [Test]
        public void Add_LargeTimestampGap_SnapsInsteadOfTraversingMissingMovement()
        {
            TacticalCombatPresentationBuffer buffer = new();
            buffer.Add(1, 0, Poses(At(0)));
            buffer.Add(2, 100, Poses(At(10)));

            buffer.Add(3, 351, Poses(At(100)));

            Assert.AreEqual(1, buffer.BufferedFrameCount);
            Assert.AreEqual(351, buffer.TimestampMilliseconds);
            AssertPoseX(buffer, 100);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-1)]
        public void Add_InvalidTimestamp_RejectsAtomically(double timestamp)
        {
            TacticalCombatPresentationBuffer buffer = new();
            buffer.Add(1, 100, Poses(At(10)));

            Assert.IsFalse(buffer.Add(2, timestamp, Poses(At(99))));

            Assert.AreEqual(1, buffer.BufferedFrameCount);
            Assert.AreEqual(100, buffer.TimestampMilliseconds);
            AssertPoseX(buffer, 10);
            Assert.IsTrue(buffer.Add(2, 200, Poses(At(20))));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Add_NonfinitePoseComponent_RejectsEntireSnapshot(float value)
        {
            TacticalCombatPresentationBuffer buffer = new();
            buffer.Add(1, 0, Poses(At(0)));
            Pose[] invalid =
            {
                new(new Vector3(value, 0, 0), Vector3.UnitZ),
                new(Vector3.Zero, new Vector3(0, value, 0)),
                new(Vector3.Zero, Vector3.UnitZ, hyperspaceMilliseconds: value),
            };
            foreach (Pose pose in invalid)
            {
                Assert.IsFalse(
                    buffer.Add(
                        2,
                        100,
                        new Dictionary<string, Pose> { ["unit"] = At(99), ["invalid"] = pose }
                    )
                );
                Assert.AreEqual(1, buffer.BufferedFrameCount);
                AssertPoseX(buffer, 0);
            }
            Assert.IsTrue(buffer.Add(2, 100, Poses(At(10))));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Advance_NonpositiveOrNonfiniteElapsed_PreservesCursor(float elapsed)
        {
            TacticalCombatPresentationBuffer buffer = new();
            buffer.Add(1, 0, Poses(At(0)));
            buffer.Add(2, 100, Poses(At(10)));
            buffer.Advance(50);

            buffer.Advance(elapsed);

            Assert.AreEqual(50, buffer.TimestampMilliseconds);
            AssertPoseX(buffer, 5);
        }

        [Test]
        public void Add_MutableCallerDictionary_RetainsDetachedCompleteSnapshot()
        {
            TacticalCombatPresentationBuffer buffer = new();
            Dictionary<string, Pose> poses = Poses(At(10));
            buffer.Add(1, 100, poses);

            poses["unit"] = At(99);
            poses.Clear();

            AssertPoseX(buffer, 10);
        }

        [TestCase(TacticalCombatUnitStatus.Destroyed)]
        [TestCase(TacticalCombatUnitStatus.Withdrawn)]
        [TestCase(TacticalCombatUnitStatus.Recovered)]
        [TestCase(TacticalCombatUnitStatus.Docked)]
        public void TryGetPose_RemovedVisibility_SnapsOnlyAtSampledTransitionTimestamp(
            TacticalCombatUnitStatus status
        )
        {
            TacticalCombatPresentationBuffer buffer = new();
            buffer.Add(1, 0, Poses(At(0)));
            buffer.Add(2, 100, Poses(new Pose(new Vector3(100, 0, 0), Vector3.UnitZ, status)));

            buffer.Advance(99);
            Assert.IsTrue(buffer.TryGetPose("unit", out Pose before));
            Assert.AreEqual(TacticalCombatUnitStatus.Active, before.Status);
            Assert.AreEqual(Vector3.Zero, before.Position);

            buffer.Advance(1);
            Assert.IsTrue(buffer.TryGetPose("unit", out Pose pose));

            Assert.AreEqual(status, pose.Status);
            Assert.AreEqual(100, pose.Position.X);
        }

        [Test]
        public void TryGetPose_NewlyActiveParticipant_RemainsDockedUntilSampledActivation()
        {
            TacticalCombatPresentationBuffer buffer = new();
            buffer.Add(
                1,
                0,
                Poses(new Pose(Vector3.Zero, Vector3.UnitZ, TacticalCombatUnitStatus.Docked))
            );
            buffer.Add(2, 100, Poses(At(100)));
            AssertPoseX(buffer, 0);
            Assert.IsTrue(buffer.TryGetPose("unit", out Pose docked));
            Assert.AreEqual(TacticalCombatUnitStatus.Docked, docked.Status);

            buffer.Add(3, 150, Poses(At(150)));
            buffer.Advance(25);
            AssertPoseX(buffer, 0);
            buffer.Advance(25);
            AssertPoseX(buffer, 100);
            Assert.IsTrue(buffer.TryGetPose("unit", out Pose active));
            Assert.AreEqual(TacticalCombatUnitStatus.Active, active.Status);
            buffer.Advance(25);

            AssertPoseX(buffer, 125);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TryGetPose_CarrierOrAppliedGroupChanges_SnapsAtSampledOwnershipBoundary(
            bool carrierChanges
        )
        {
            TacticalCombatPresentationBuffer buffer = new();
            buffer.Add(
                1,
                0,
                Poses(new Pose(Vector3.Zero, Vector3.UnitZ, carrierID: "carrier", groupID: 1))
            );
            buffer.Add(
                2,
                100,
                Poses(
                    new Pose(
                        new Vector3(100, 0, 0),
                        Vector3.UnitZ,
                        carrierID: carrierChanges ? null : "carrier",
                        groupID: carrierChanges ? 1 : 2
                    )
                )
            );

            AssertPoseX(buffer, 0);
            buffer.Advance(99);
            AssertPoseX(buffer, 0);
            buffer.Advance(1);
            AssertPoseX(buffer, 100);
        }

        [Test]
        public void TryGetPose_ContinuousDeparture_InterpolatesTimeAndSnapsOnTimerReversal()
        {
            TacticalCombatPresentationBuffer buffer = new();
            buffer.Add(1, 0, Poses(Departure(100)));
            buffer.Add(2, 100, Poses(Departure(200)));
            buffer.Advance(50);
            Assert.IsTrue(buffer.TryGetPose("unit", out Pose middle));
            Assert.AreEqual(150, middle.HyperspaceMilliseconds);

            buffer.Add(3, 150, Poses(Departure(20)));

            buffer.Advance(99);
            Assert.IsTrue(buffer.TryGetPose("unit", out Pose before));
            Assert.AreEqual(200, before.HyperspaceMilliseconds);
            buffer.Advance(1);
            Assert.IsTrue(buffer.TryGetPose("unit", out Pose reset));
            Assert.AreEqual(20, reset.HyperspaceMilliseconds);
        }

        [Test]
        public void TryGetPose_AliveFlagChangesWithoutStatus_DelaysDeathAndNeverRevivesFromOlderSnapshots()
        {
            TacticalCombatPresentationBuffer buffer = new();
            buffer.Add(1, 0, Poses(At(0)));
            buffer.Add(
                2,
                100,
                Poses(new Pose(new Vector3(100, 0, 0), Vector3.UnitZ, isAlive: false))
            );
            buffer.Advance(99);
            Assert.IsTrue(buffer.TryGetPose("unit", out Pose alive));
            Assert.IsTrue(alive.IsAlive);
            Assert.AreEqual(Vector3.Zero, alive.Position);

            buffer.Advance(1);
            Assert.IsTrue(buffer.TryGetPose("unit", out Pose dead));
            Assert.IsFalse(dead.IsAlive);
            Assert.AreEqual(100, dead.Position.X);
            Assert.IsFalse(buffer.Add(1, 50, Poses(At(50))));
            buffer.Advance(100);
            Assert.IsTrue(buffer.TryGetPose("unit", out Pose stillDead));
            Assert.IsFalse(stillDead.IsAlive);
        }

        [Test]
        public void TryGetPose_OppositeHeadingsCancel_RetainsLatestAuthoredHeading()
        {
            TacticalCombatPresentationBuffer buffer = new();
            buffer.Add(1, 0, Poses(new Pose(Vector3.Zero, Vector3.UnitZ)));
            buffer.Add(2, 100, Poses(new Pose(Vector3.Zero, -Vector3.UnitZ)));
            buffer.Advance(50);

            Assert.IsTrue(buffer.TryGetPose("unit", out Pose pose));

            Assert.AreEqual(-Vector3.UnitZ, pose.Heading);
        }

        [Test]
        public void TryGetPose_LargeFiniteHeadings_NormalizesWithoutOverflow()
        {
            TacticalCombatPresentationBuffer buffer = new();
            buffer.Add(1, 0, Poses(new Pose(Vector3.Zero, Vector3.UnitZ * float.MaxValue)));
            buffer.Add(2, 100, Poses(new Pose(Vector3.Zero, Vector3.UnitX * float.MaxValue)));
            buffer.Advance(50);

            Assert.IsTrue(buffer.TryGetPose("unit", out Pose pose));

            Assert.That(pose.Heading.Length(), Is.EqualTo(1).Within(.00001f));
            Assert.That(pose.Heading.X, Is.EqualTo(pose.Heading.Z).Within(.00001f));
        }

        /// <summary>
        /// Captures one continuously active participant on the horizontal axis.
        /// </summary>
        /// <param name="x">The horizontal position.</param>
        /// <returns>The active pose.</returns>
        private static Pose At(float x) => new(new Vector3(x, 0, 0), Vector3.UnitZ);

        /// <summary>
        /// Captures an unchanged canonical departure pose with an advancing animation timer.
        /// </summary>
        /// <param name="milliseconds">The departure timer.</param>
        /// <returns>The departing pose.</returns>
        private static Pose Departure(float milliseconds) =>
            new(
                Vector3.Zero,
                Vector3.UnitZ,
                TacticalCombatUnitStatus.EnteringHyperspace,
                hyperspaceMilliseconds: milliseconds
            );

        /// <summary>
        /// Provides a complete one-participant pose snapshot.
        /// </summary>
        /// <param name="pose">The participant's captured pose.</param>
        /// <returns>The detached input snapshot.</returns>
        private static Dictionary<string, Pose> Poses(Pose pose) => new() { ["unit"] = pose };

        /// <summary>
        /// Checks the sampled horizontal position through the public pose query.
        /// </summary>
        /// <param name="buffer">The presentation history.</param>
        /// <param name="expected">The expected horizontal position.</param>
        private static void AssertPoseX(TacticalCombatPresentationBuffer buffer, float expected)
        {
            Assert.IsTrue(buffer.TryGetPose("unit", out Pose pose));
            Assert.That(pose.Position.X, Is.EqualTo(expected).Within(.00001f));
        }
    }
}
