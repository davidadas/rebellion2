using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Multiplayer;
using Rebellion.Multiplayer.Tactical;
using Rebellion.Tactical;
using Rebellion.Tactical.Synchronization;
using Rebellion.UI.SceneUI.TacticalCombat.Audio;
using Rebellion.Util.Random;
using TacticalCombatPlanner = global::Rebellion.AI.Tactical.TacticalCombatPlanner;

namespace Rebellion.Tests.Multiplayer.Tactical
{
    [TestFixture]
    public sealed class TacticalCombatReplicaPlaybackTests
    {
        private TacticalCombatSession authority;
        private TacticalCombatSession replica;
        private TacticalCombatReplicaPlayback playback;
        private SystemRandomProvider replicaRandom;

        /// <summary>
        /// Creates independent authority and replica encounters before each presentation assertion.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            authority = CreateSession(CombatSide.Attacker, new SystemRandomProvider(12));
            replicaRandom = new SystemRandomProvider(12);
            replica = CreateSession(CombatSide.Defender, replicaRandom);
            authority.ConfigureSynchronizedAuthority();
            replica.ConfigureSynchronizedReplica();
            playback = new TacticalCombatReplicaPlayback(replica);
        }

        /// <summary>
        /// Releases encounter references after each isolated presentation assertion.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            playback.Dispose();
            authority.Selection.Dispose();
            authority.Audio.Dispose();
            replica.Selection.Dispose();
            replica.Audio.Dispose();
        }

        [Test]
        public void Advance_Replica_InterpolatesWithoutChangingRawStateOrRandom()
        {
            replica.ApplySnapshot(Snapshot(1, 0, Vector3.Zero, Vector3.UnitZ));
            replica.ApplySnapshot(Snapshot(2, 100, new Vector3(10, 20, 30), Vector3.UnitX));
            TacticalCombatUnit unit = replica.GetUnit("empire");
            float hull = unit.Systems.Hull;
            int systemTime = replica.SystemTime;
            long randomCalls = replicaRandom.CallCount;

            playback.Advance(50);

            Assert.IsTrue(playback.TryGetPose("empire", out Vector3 position, out Vector3 heading));
            Assert.AreEqual(new Vector3(5, 10, 15), position);
            Assert.That(heading.Length(), Is.EqualTo(1).Within(.00001f));
            Assert.AreEqual(new Vector3(10, 20, 30), unit.Position);
            Assert.AreEqual(Vector3.UnitX, unit.Heading);
            Assert.AreEqual(hull, unit.Systems.Hull);
            Assert.AreEqual(systemTime, replica.SystemTime);
            Assert.AreEqual(randomCalls, replicaRandom.CallCount);
        }

        [Test]
        public void Advance_PausedCompleteSnapshot_DrainsFinalPoseWithoutAdvancingCombat()
        {
            replica.ApplySnapshot(Snapshot(1, 0, Vector3.Zero, Vector3.UnitZ));
            TacticalCombatSnapshot terminal = Snapshot(2, 100, Vector3.UnitX * 10, Vector3.UnitZ);
            terminal.Paused = true;
            terminal.Complete = true;
            replica.ApplySnapshot(terminal);
            long randomCalls = replicaRandom.CallCount;

            playback.Advance(50);
            Assert.IsTrue(playback.TryGetPose("empire", out Vector3 middle, out _));
            Assert.AreEqual(5, middle.X);
            playback.Advance(50);

            Assert.IsTrue(playback.TryGetPose("empire", out Vector3 final, out _));
            Assert.AreEqual(10, final.X);
            Assert.AreEqual(100, playback.TimestampMilliseconds);
            Assert.IsTrue(replica.IsComplete);
            Assert.IsTrue(replica.IsPaused);
            Assert.AreEqual(3, replica.SystemTime);
            Assert.AreEqual(randomCalls, replicaRandom.CallCount);
        }

        [Test]
        public void Advance_ReplicaDeparture_InterpolatesTimerWithoutChangingParticipant()
        {
            TacticalCombatSnapshot first = Snapshot(1, 0, Vector3.Zero, Vector3.UnitZ);
            TacticalCombatSnapshot second = Snapshot(2, 100, Vector3.Zero, Vector3.UnitZ);
            TacticalCombatUnitSnapshot start = first.Units.Single(unit => unit.UnitID == "empire");
            TacticalCombatUnitSnapshot end = second.Units.Single(unit => unit.UnitID == "empire");
            start.Status = end.Status = TacticalCombatUnitStatus.EnteringHyperspace;
            start.HyperspaceMilliseconds = 100;
            end.HyperspaceMilliseconds = 200;
            replica.ApplySnapshot(first);
            replica.ApplySnapshot(second);

            playback.Advance(50);

            Assert.IsTrue(playback.TryGetPose("empire", out _, out _, out float timer));
            Assert.AreEqual(150, timer);
            Assert.AreEqual(200, replica.GetUnit("empire").HyperspaceMilliseconds);
        }

        [Test]
        public void Advance_DestroyedParticipant_RetainsPriorLifecycleUntilDeathTimestamp()
        {
            replica.ApplySnapshot(Snapshot(1, 0, Vector3.Zero, Vector3.UnitZ));
            TacticalCombatSnapshot death = Snapshot(2, 100, Vector3.UnitX * 10, Vector3.UnitZ);
            TacticalCombatUnitSnapshot state = death.Units.Single(unit => unit.UnitID == "empire");
            state.Hull = 0;
            state.Status = TacticalCombatUnitStatus.Destroyed;
            replica.ApplySnapshot(death);
            TacticalCombatUnit raw = replica.GetUnit("empire");
            Assert.IsFalse(raw.Systems.IsAlive);
            Assert.AreEqual(TacticalCombatUnitStatus.Destroyed, raw.Status);
            long randomCalls = replicaRandom.CallCount;

            playback.Advance(99);

            Assert.IsTrue(
                playback.TryGetLifecycle(
                    "empire",
                    out TacticalCombatUnitStatus before,
                    out bool alive
                )
            );
            Assert.AreEqual(TacticalCombatUnitStatus.Active, before);
            Assert.IsTrue(alive);
            Assert.IsFalse(raw.Systems.IsAlive);
            playback.Advance(1);
            Assert.IsTrue(
                playback.TryGetLifecycle(
                    "empire",
                    out TacticalCombatUnitStatus after,
                    out bool dead
                )
            );
            Assert.AreEqual(TacticalCombatUnitStatus.Destroyed, after);
            Assert.IsFalse(dead);
            playback.Advance(100);
            Assert.IsTrue(playback.TryGetLifecycle("empire", out _, out bool remainsAlive));
            Assert.IsFalse(remainsAlive);
            Assert.AreEqual(randomCalls, replicaRandom.CallCount);
        }

        [Test]
        public void Advance_EnteringHyperspace_ChangesDepartureBranchAtSampledTimestamp()
        {
            replica.ApplySnapshot(Snapshot(1, 0, Vector3.Zero, Vector3.UnitZ));
            TacticalCombatSnapshot departure = Snapshot(2, 100, Vector3.UnitX * 10, Vector3.UnitZ);
            TacticalCombatUnitSnapshot state = departure.Units.Single(unit =>
                unit.UnitID == "empire"
            );
            state.Status = TacticalCombatUnitStatus.EnteringHyperspace;
            state.HyperspaceMilliseconds = 1200;
            replica.ApplySnapshot(departure);

            playback.Advance(99);

            Assert.IsTrue(
                playback.TryGetPose(
                    "empire",
                    out Vector3 position,
                    out _,
                    out float beforeTimer,
                    out TacticalCombatUnitStatus before,
                    out _
                )
            );
            Assert.AreEqual(TacticalCombatUnitStatus.Active, before);
            Assert.AreEqual(0, beforeTimer);
            Assert.AreEqual(Vector3.Zero, position);
            playback.Advance(1);
            Assert.IsTrue(
                playback.TryGetPose(
                    "empire",
                    out _,
                    out _,
                    out float afterTimer,
                    out TacticalCombatUnitStatus after,
                    out _
                )
            );
            Assert.AreEqual(TacticalCombatUnitStatus.EnteringHyperspace, after);
            Assert.AreEqual(1200, afterTimer);
        }

        [Test]
        public void Constructor_Authority_RejectsWithoutChangingCombatState()
        {
            Vector3 position = authority.GetUnit("empire").Position;

            Assert.Throws<InvalidOperationException>(() =>
                new TacticalCombatReplicaPlayback(authority)
            );

            Assert.AreEqual(position, authority.GetUnit("empire").Position);
        }

        [Test]
        public void Dispose_LaterProjection_LeavesDisplayedPoseFrozen()
        {
            replica.ApplySnapshot(Snapshot(1, 0, Vector3.Zero, Vector3.UnitZ));
            playback.Dispose();

            replica.ApplySnapshot(Snapshot(2, 100, Vector3.UnitX * 10, Vector3.UnitZ));
            playback.Advance(100);

            Assert.IsTrue(playback.TryGetPose("empire", out Vector3 position, out _));
            Assert.AreEqual(Vector3.Zero, position);
            Assert.AreEqual(0, playback.TimestampMilliseconds);
            Assert.AreEqual(Vector3.UnitX * 10, replica.GetUnit("empire").Position);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ApplySnapshot_InvalidPresentationData_RejectsBeforeRawReplicaMutation(
            bool timestampInvalid
        )
        {
            replica.ApplySnapshot(Snapshot(1, 0, Vector3.Zero, Vector3.UnitZ));
            TacticalCombatSnapshot invalid = Snapshot(2, 100, Vector3.UnitX * 10, Vector3.UnitZ);
            invalid.Sides[0].Withdrawing = true;
            if (timestampInvalid)
                invalid.TimestampMilliseconds = double.NaN;
            else
                invalid.Units.Single(unit => unit.UnitID == "empire").Position = new(
                    new Vector3(float.NaN, 0, 0)
                );
            long randomCalls = replicaRandom.CallCount;

            Assert.Throws<ArgumentException>(() => replica.ApplySnapshot(invalid));

            Assert.AreEqual(Vector3.Zero, replica.GetUnit("empire").Position);
            Assert.IsFalse(replica.GetSide(CombatSide.Attacker).IsWithdrawing);
            Assert.AreEqual(randomCalls, replicaRandom.CallCount);
            Assert.IsTrue(playback.TryGetPose("empire", out Vector3 pose, out _));
            Assert.AreEqual(Vector3.Zero, pose);
            replica.ApplySnapshot(Snapshot(3, 100, Vector3.UnitX * 10, Vector3.UnitZ));
        }

        /// <summary>
        /// Captures a complete projection while assigning one participant's canonical test pose.
        /// </summary>
        /// <param name="sequence">The next complete state sequence.</param>
        /// <param name="timestamp">The authority presentation timestamp.</param>
        /// <param name="position">The test participant's canonical position.</param>
        /// <param name="heading">The test participant's canonical heading.</param>
        /// <returns>The complete authority snapshot with no presentation effects.</returns>
        private TacticalCombatSnapshot Snapshot(
            long sequence,
            double timestamp,
            Vector3 position,
            Vector3 heading
        )
        {
            TacticalCombatUnit unit = authority.GetUnit("empire");
            unit.Position = position;
            unit.Heading = heading;
            TacticalCombatSnapshot snapshot = authority.CaptureSnapshot(sequence);
            snapshot.TimestampMilliseconds = timestamp;
            snapshot.Effects = Array.Empty<TacticalCombatEvent>();
            return snapshot;
        }

        /// <summary>
        /// Captures a two-faction encounter without arrival or simulation advancement.
        /// </summary>
        /// <param name="player">The local view's faction.</param>
        /// <param name="random">The local stream whose calls are observed.</param>
        /// <returns>The unadvanced combat session.</returns>
        private static TacticalCombatSession CreateSession(
            CombatSide player,
            IRandomNumberProvider random
        )
        {
            TacticalCombatUnit empire = CreateUnit("empire", CombatSide.Attacker, 1000001);
            TacticalCombatUnit alliance = CreateUnit("alliance", CombatSide.Defender, 1);
            return new TacticalCombatSession(
                new[] { alliance, empire },
                new[]
                {
                    new TacticalCombatGroup(10, new[] { alliance }, Vector3.UnitZ),
                    new TacticalCombatGroup(1000010, new[] { empire }, -Vector3.UnitZ),
                },
                new TacticalCombatSide(
                    CombatSide.Attacker,
                    1,
                    -Vector3.UnitZ,
                    true,
                    0,
                    0,
                    null,
                    null,
                    0,
                    0,
                    false
                ),
                new TacticalCombatSide(
                    CombatSide.Defender,
                    0,
                    Vector3.UnitZ,
                    true,
                    0,
                    0,
                    null,
                    null,
                    0,
                    0,
                    false
                ),
                player,
                100,
                random,
                TacticalCombatAudio.Create,
                TacticalCombatPlanner.Attach
            );
        }

        /// <summary>
        /// Captures a complete unarmed participant with observable raw position and systems.
        /// </summary>
        /// <param name="id">The stable participant identity.</param>
        /// <param name="side">The participant's faction.</param>
        /// <param name="index">The tactical registration index.</param>
        /// <returns>The captured capital ship.</returns>
        private static TacticalCombatUnit CreateUnit(string id, CombatSide side, int index) =>
            new(
                CombatUnit.Create(
                    new CapitalShip
                    {
                        InstanceID = id,
                        OwnerInstanceID = side == CombatSide.Attacker ? "FNEMP1" : "FNALL1",
                        MaxHullStrength = 1000,
                        CurrentHullStrength = 1000,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                        Hyperdrive = 1,
                        SublightSpeed = 4,
                    }
                ),
                side,
                index,
                new TacticalCombatUnitDefinition { PresentationIndex = 1 }
            )
            {
                Heading = Vector3.UnitZ,
                DesiredHeading = Vector3.UnitZ,
            };
    }
}
