using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Multiplayer;
using Rebellion.Multiplayer.Tactical;
using Rebellion.Tactical.Synchronization;

namespace Rebellion.Tests.Multiplayer.Tactical
{
    [TestFixture]
    public sealed class MultiplayerTacticalSnapshotDeliveryTests
    {
        [TestCase(0)]
        [TestCase(5)]
        [TestCase(25)]
        public void Receive_CompressedFullFramesWithFragmentLossAndJitter_ReportsDeliveredCadence(
            int lossPercent
        )
        {
            const int updates = 400;
            double now = 0;
            Random random = new(734);
            List<(double Due, byte[] Data)> fragments = new();
            List<(double Due, MultiplayerPacket Packet)> reliable = new();
            List<double> receivedAt = new();
            HashSet<long> checkpoints = new();
            Queue<MultiplayerPacket> toHost = new();
            int sentDatagrams = 0;
            int droppedDatagrams = 0;
            int deliveredSnapshots = 0;
            int peakAssemblies = 0;
            int peakBytes = 0;
            int payloadBytes = 0;
            string initialization = Guid.NewGuid().ToString("N");
            string battle = Guid.NewGuid().ToString("N");
            TacticalCombatUnitSnapshot[] units = CreateFullFrameUnits();
            MultiplayerSnapshotDatagrams sender = new(
                1170,
                datagram =>
                {
                    sentDatagrams++;
                    if (random.Next(100) < lossPercent)
                    {
                        droppedDatagrams++;
                        return true;
                    }
                    byte[] copy = datagram.ToArray();
                    fragments.Add((now + 40 + random.Next(111), copy));
                    if (random.Next(10) == 0)
                        fragments.Add((now + 155 + random.Next(30), copy));
                    return true;
                },
                () => now
            );
            MultiplayerSnapshotDatagrams receiver = new(1170, _ => true, () => now);
            using MultiplayerTacticalProtocol host = new(
                MultiplayerRole.Host,
                initialization,
                battle,
                CombatSide.Attacker,
                (_, _) => true,
                sequence => new TacticalCombatSnapshot { Sequence = sequence, Units = units },
                _ => Assert.Fail("The authority cannot import a replica state."),
                _ => Assert.Fail("The authority cannot import replica effects."),
                () => 0,
                _ => false,
                packet =>
                {
                    if (packet.Type == MultiplayerMessageType.TacticalSnapshot)
                    {
                        payloadBytes = packet.Payload.Length;
                        Assert.IsTrue(sender.TrySend(packet));
                    }
                    else
                    {
                        if (packet.Type == MultiplayerMessageType.TacticalCheckpoint)
                            checkpoints.Add(
                                MultiplayerTacticalSnapshotCodec.Decode(packet.Payload).Sequence
                            );
                        reliable.Add((now + 100, packet));
                    }
                },
                () => now
            );
            using MultiplayerTacticalProtocol guest = new(
                MultiplayerRole.Guest,
                initialization,
                battle,
                CombatSide.Attacker,
                (_, _) => throw new AssertionException("The replica cannot execute commands."),
                _ => throw new AssertionException("The replica cannot capture simulation state."),
                frame =>
                {
                    Assert.AreEqual(units.Length, frame.Units.Length);
                    Assert.AreEqual(units[0].Hull, frame.Units[0].Hull);
                    if (!checkpoints.Contains(frame.Sequence))
                    {
                        deliveredSnapshots++;
                        receivedAt.Add(now);
                    }
                },
                _ => Assert.Fail("This fixture does not emit effects."),
                () => 0,
                _ => false,
                toHost.Enqueue,
                () => now
            );
            host.Ready();
            guest.Ready();
            host.Receive(toHost.Dequeue());
            guest.Receive(reliable.Single().Packet);
            reliable.Clear();

            for (int step = 0; step < updates * 5 + 30; step++)
            {
                now = step * 10;
                if (step < updates * 5 && step % 5 == 0)
                    host.PublishSnapshot();
                foreach (var item in reliable.Where(item => item.Due <= now).ToArray())
                    guest.Receive(item.Packet);
                reliable.RemoveAll(item => item.Due <= now);
                foreach (
                    var item in fragments
                        .Where(item => item.Due <= now)
                        .OrderBy(item => item.Due)
                        .ToArray()
                )
                {
                    MultiplayerPacket complete = receiver.Receive(new(item.Data));
                    if (complete != null)
                        guest.Receive(complete);
                    peakAssemblies = Math.Max(peakAssemblies, receiver.BufferedAssemblies);
                    peakBytes = Math.Max(peakBytes, receiver.BufferedBytes);
                }
                fragments.RemoveAll(item => item.Due <= now);
                guest.Pump();
                while (toHost.Count != 0)
                    host.Receive(toHost.Dequeue());
                host.Pump();
                Assert.IsNull(host.Failure, host.Failure?.ToString());
                Assert.IsNull(guest.Failure, guest.Failure?.ToString());
            }

            double largestGap = receivedAt
                .Zip(receivedAt.Skip(1), (before, after) => after - before)
                .DefaultIfEmpty(0)
                .Max();
            TestContext.WriteLine(
                $"Full snapshot {payloadBytes} bytes; 1170-byte datagrams with one XOR parity per four data fragments, {lossPercent}% fragment loss; sent={sentDatagrams}, dropped={droppedDatagrams}, delivered={deliveredSnapshots}/{updates}, cadence={deliveredSnapshots / 20d:F2} Hz, largest gap={largestGap:F0} ms, peak assemblies={peakAssemblies}, peak bytes={peakBytes}."
            );
            Assert.Greater(payloadBytes, 8192);
            Assert.Less(payloadBytes, 20 * 1024);
            Assert.LessOrEqual(
                peakAssemblies,
                MultiplayerSnapshotDatagrams.MaximumConcurrentAssemblies
            );
            Assert.LessOrEqual(peakBytes, MultiplayerSnapshotDatagrams.MaximumBufferedBytes);
            Assert.GreaterOrEqual(
                deliveredSnapshots,
                lossPercent == 0 ? 300
                    : lossPercent == 5 ? 300
                    : 5
            );
            if (lossPercent == 0)
                Assert.LessOrEqual(largestGap, 200);
        }

        /// <summary>
        /// Creates a complete fleet projection with independently varying lossless fields.
        /// </summary>
        /// <returns>The detached synthetic full unit states.</returns>
        private static TacticalCombatUnitSnapshot[] CreateFullFrameUnits()
        {
            Random random = new(741);
            TacticalCombatUnitSnapshot[] units = new TacticalCombatUnitSnapshot[96];
            for (int index = 0; index < units.Length; index++)
                units[index] = new TacticalCombatUnitSnapshot
                {
                    UnitID = $"unit-{index:D4}",
                    Position = CreateVector(random),
                    Heading = CreateVector(random),
                    Velocity = CreateVector(random),
                    DesiredHeading = CreateVector(random),
                    DesiredMovement = CreateVector(random),
                    Hull = (float)random.NextDouble() * 10000,
                    Shields = (float)random.NextDouble() * 10000,
                    ShieldLimit = (float)random.NextDouble() * 10000,
                    AttackDelay = (float)random.NextDouble() * 10000,
                    MovementDelay = (float)random.NextDouble() * 10000,
                    WeaponRechargeSample = (float)random.NextDouble() * 10000,
                    ShieldRechargeSample = (float)random.NextDouble() * 10000,
                    ComponentDamage = Enumerable
                        .Range(0, 5)
                        .Select(_ => (uint)random.Next())
                        .ToArray(),
                    Charge = Enumerable
                        .Range(0, 4)
                        .Select(_ => (float)random.NextDouble() * 10000)
                        .ToArray(),
                    Ready = new bool[4],
                    HyperspaceMilliseconds = (float)random.NextDouble() * 10000,
                    TractorRestraint = (float)random.NextDouble() * 10000,
                    CommandBudget = (float)random.NextDouble() * 10000,
                    TurnRate = (float)random.NextDouble() * 10000,
                };
            return units;
        }

        /// <summary>
        /// Creates one finite independently varying projected vector.
        /// </summary>
        /// <param name="random">The deterministic fixture random source.</param>
        /// <returns>The detached vector value.</returns>
        private static TacticalCombatSnapshotVector CreateVector(Random random) =>
            new()
            {
                X = (float)random.NextDouble() * 10000,
                Y = (float)random.NextDouble() * 10000,
                Z = (float)random.NextDouble() * 10000,
            };
    }
}
