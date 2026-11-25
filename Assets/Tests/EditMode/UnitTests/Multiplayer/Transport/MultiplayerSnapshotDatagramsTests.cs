using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rebellion.Multiplayer;
using Encoding = global::System.Text.Encoding;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerSnapshotDatagramsTests
    {
        [Test]
        public void Receive_ReorderedAndDuplicatedFragments_RestoresOneExactCompletePacket()
        {
            List<byte[]> datagrams = new();
            MultiplayerSnapshotDatagrams sender = CreateSender(datagrams);
            MultiplayerSnapshotDatagrams receiver = CreateSender(new List<byte[]>());
            MultiplayerPacket original = Packet(3200, 12);
            Assert.IsTrue(sender.TrySend(original));
            MultiplayerPacket restored = null;
            foreach (byte[] datagram in datagrams.AsEnumerable().Reverse())
            {
                restored = receiver.Receive(new(datagram)) ?? restored;
                Assert.IsNull(receiver.Receive(new(datagram)));
            }
            Assert.IsNotNull(restored);
            Assert.AreEqual(original.Metadata, restored.Metadata);
            CollectionAssert.AreEqual(original.Payload, restored.Payload);
            Assert.AreEqual(MultiplayerMessageType.TacticalSnapshot, restored.Type);
            Assert.AreEqual(0, receiver.BufferedBytes);
            Assert.IsTrue(datagrams.All(datagram => datagram.Length <= 128));
        }

        [Test]
        public void Receive_NewerPartialSnapshot_PreservesOlderAssemblyUntilEitherSnapshotCompletes()
        {
            List<byte[]> datagrams = new();
            MultiplayerSnapshotDatagrams sender = CreateSender(datagrams);
            sender.TrySend(Packet(2000, 1));
            byte[][] older = datagrams.ToArray();
            datagrams.Clear();
            MultiplayerPacket expected = Packet(900, 2);
            sender.TrySend(expected);
            MultiplayerSnapshotDatagrams receiver = CreateSender(new List<byte[]>());
            receiver.Receive(new(older[0]));
            receiver.Receive(new(datagrams[0]));
            Assert.AreEqual(2, receiver.BufferedAssemblies);
            MultiplayerPacket completedOlder = null;
            foreach (byte[] old in older.Skip(1))
                completedOlder = receiver.Receive(new(old)) ?? completedOlder;
            CollectionAssert.AreEqual(Packet(2000, 1).Payload, completedOlder.Payload);
            Assert.AreEqual(1, receiver.BufferedAssemblies);
            MultiplayerPacket latest = null;
            foreach (byte[] fragment in datagrams.Skip(1).Reverse())
                latest = receiver.Receive(new(fragment)) ?? latest;
            CollectionAssert.AreEqual(expected.Payload, latest.Payload);
            foreach (byte[] old in older)
                Assert.IsNull(receiver.Receive(new(old)));
        }

        [Test]
        public void Receive_DroppedFragment_ExpiresBoundedAssemblyWithoutResurrectingItsSequence()
        {
            double now = 0;
            List<byte[]> datagrams = new();
            MultiplayerSnapshotDatagrams sender = CreateSender(datagrams);
            sender.TrySend(Packet(2000, 1));
            MultiplayerSnapshotDatagrams receiver = new(128, _ => true, () => now);
            receiver.Receive(new(datagrams[0]));
            Assert.Greater(receiver.BufferedBytes, 0);
            now = MultiplayerSnapshotDatagrams.AssemblyLifetimeMilliseconds;
            receiver.Expire();
            Assert.AreEqual(0, receiver.BufferedBytes);
            foreach (byte[] fragment in datagrams.Skip(1))
                Assert.IsNull(receiver.Receive(new(fragment)));
            Assert.AreEqual(0, receiver.BufferedBytes);
        }

        [Test]
        public void Receive_NewerCompleteSnapshot_DiscardsOlderPartialSnapshots()
        {
            List<byte[]> datagrams = new();
            MultiplayerSnapshotDatagrams sender = CreateSender(datagrams);
            sender.TrySend(Packet(2000, 1));
            byte[][] older = datagrams.ToArray();
            datagrams.Clear();
            sender.TrySend(Packet(900, 2));
            MultiplayerSnapshotDatagrams receiver = CreateSender(new());
            receiver.Receive(new(older[0]));
            MultiplayerPacket latest = null;

            foreach (byte[] datagram in datagrams)
                latest = receiver.Receive(new(datagram)) ?? latest;

            Assert.IsNotNull(latest);
            Assert.Zero(receiver.BufferedBytes);
            foreach (byte[] old in older)
                Assert.IsNull(receiver.Receive(new(old)));
            Assert.Zero(receiver.BufferedAssemblies);
        }

        [Test]
        public void Receive_TooManyPartialSnapshots_EvictsOldestWithinCountBound()
        {
            List<byte[]> datagrams = new();
            MultiplayerSnapshotDatagrams sender = CreateSender(datagrams);
            MultiplayerSnapshotDatagrams receiver = CreateSender(new());
            byte[][] oldest = null;
            for (
                int index = 0;
                index <= MultiplayerSnapshotDatagrams.MaximumConcurrentAssemblies;
                index++
            )
            {
                datagrams.Clear();
                sender.TrySend(Packet(1000, index));
                oldest ??= datagrams.ToArray();
                receiver.Receive(new(datagrams[0]));
                Assert.LessOrEqual(
                    receiver.BufferedAssemblies,
                    MultiplayerSnapshotDatagrams.MaximumConcurrentAssemblies
                );
            }

            foreach (byte[] datagram in oldest)
                Assert.IsNull(receiver.Receive(new(datagram)));
            MultiplayerPacket newest = null;
            foreach (byte[] datagram in datagrams.Skip(1))
                newest = receiver.Receive(new(datagram)) ?? newest;
            Assert.IsNotNull(newest);
            Assert.Zero(receiver.BufferedAssemblies);
        }

        [Test]
        public void Receive_LargePartialSnapshots_EvictsOldestBeforeExceedingByteBudget()
        {
            byte[] first = null;
            MultiplayerSnapshotDatagrams sender = new(
                1170,
                datagram =>
                {
                    if (first == null)
                    {
                        first = datagram.ToArray();
                        return true;
                    }
                    return false;
                }
            );
            MultiplayerSnapshotDatagrams receiver = new(1170, _ => true);
            byte[] oldest = null;
            for (int index = 0; index < 3; index++)
            {
                first = null;
                Assert.IsFalse(sender.TrySend(Packet(3 * 1024 * 1024, index)));
                oldest ??= first;
                receiver.Receive(new(first));
                Assert.LessOrEqual(
                    receiver.BufferedBytes,
                    MultiplayerSnapshotDatagrams.MaximumBufferedBytes
                );
            }

            Assert.AreEqual(2, receiver.BufferedAssemblies);
            Assert.IsNull(receiver.Receive(new(oldest)));
            Assert.AreEqual(2, receiver.BufferedAssemblies);
        }

        [Test]
        public void Expire_OnlyOneAssemblyExpires_PreservesOtherAdmittedSnapshots()
        {
            double now = 0;
            List<byte[]> datagrams = new();
            MultiplayerSnapshotDatagrams sender = CreateSender(datagrams);
            MultiplayerSnapshotDatagrams receiver = new(128, _ => true, () => now);
            sender.TrySend(Packet(1000, 1));
            byte[][] first = datagrams.ToArray();
            datagrams.Clear();
            sender.TrySend(Packet(1000, 2));
            receiver.Receive(new(datagrams[0]));
            now = 500;
            receiver.Receive(new(first[0]));
            now = 1000;

            receiver.Expire();

            Assert.AreEqual(1, receiver.BufferedAssemblies);
            MultiplayerPacket older = null;
            foreach (byte[] datagram in first.Skip(1))
                older = receiver.Receive(new(datagram)) ?? older;
            Assert.IsNotNull(older);
            Assert.Zero(receiver.BufferedBytes);
            foreach (byte[] datagram in datagrams)
                Assert.IsNull(receiver.Receive(new(datagram)));
        }

        [Test]
        public void TrySend_ProviderBackpressure_DropsRemainderAndSendsOnlyNewWorkOnNextAttempt()
        {
            List<byte[]> accepted = new();
            int attempts = 0;
            bool blocked = true;
            MultiplayerSnapshotDatagrams sender = new(
                128,
                datagram =>
                {
                    attempts++;
                    if (blocked && attempts == 3)
                        return false;
                    accepted.Add(datagram.ToArray());
                    return true;
                }
            );
            Assert.IsFalse(sender.TrySend(Packet(3000, 1)));
            Assert.AreEqual(3, attempts);
            byte[][] incomplete = accepted.ToArray();
            accepted.Clear();
            blocked = false;
            MultiplayerPacket newest = Packet(500, 2);
            Assert.IsTrue(sender.TrySend(newest));
            MultiplayerSnapshotDatagrams receiver = CreateSender(new List<byte[]>());
            foreach (byte[] old in incomplete)
                Assert.IsNull(receiver.Receive(new(old)));
            MultiplayerPacket complete = null;
            foreach (byte[] datagram in accepted)
                complete = receiver.Receive(new(datagram)) ?? complete;
            CollectionAssert.AreEqual(newest.Payload, complete.Payload);
            Assert.AreEqual(0, receiver.BufferedBytes);
        }

        [Test]
        public void TrySend_ContinuousBackpressure_AttemptsOncePerSnapshotWithoutQueuedRetries()
        {
            int attempts = 0;
            MultiplayerSnapshotDatagrams sender = new(
                128,
                _ =>
                {
                    attempts++;
                    return false;
                }
            );
            for (int index = 0; index < 100; index++)
                Assert.IsFalse(sender.TrySend(Packet(1000, index)));
            Assert.AreEqual(100, attempts);
            Assert.AreEqual(0, sender.BufferedBytes);
        }

        [Test]
        public void TrySend_ReentrantSnapshot_DropsConcurrentWorkWithoutAcquiringTheReliableGate()
        {
            MultiplayerSnapshotDatagrams sender = null;
            int nestedAttempts = 0;
            sender = new(
                128,
                _ =>
                {
                    nestedAttempts++;
                    Assert.IsFalse(sender.TrySend(Packet(10, 2)));
                    return true;
                }
            );
            Assert.IsTrue(sender.TrySend(Packet(10, 1)));
            Assert.AreEqual(2, nestedAttempts);
        }

        [Test]
        public void Receive_ChangedDuplicateFragment_RejectsConflictingBytes()
        {
            List<byte[]> datagrams = new();
            CreateSender(datagrams).TrySend(Packet(1000, 1));
            MultiplayerSnapshotDatagrams receiver = CreateSender(new List<byte[]>());
            receiver.Receive(new(datagrams[0]));
            datagrams[0][datagrams[0].Length - 1] ^= 1;
            Assert.Throws<InvalidDataException>(() => receiver.Receive(new(datagrams[0])));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Receive_OneDataErasurePerParityGroup_RestoresExactSnapshot(int erasedOffset)
        {
            List<byte[]> datagrams = new();
            MultiplayerSnapshotDatagrams sender = CreateSender(datagrams);
            MultiplayerPacket original = Packet(850, 12);
            sender.TrySend(original);
            MultiplayerSnapshotDatagrams receiver = CreateSender(new());
            MultiplayerPacket restored = null;

            foreach (byte[] datagram in datagrams.AsEnumerable().Reverse())
            {
                if (datagram[5] == 0 && FragmentIndex(datagram) % 4 == erasedOffset)
                    continue;
                restored = receiver.Receive(new(datagram)) ?? restored;
            }

            Assert.IsNotNull(restored);
            Assert.AreEqual(original.Metadata, restored.Metadata);
            CollectionAssert.AreEqual(original.Payload, restored.Payload);
            Assert.Zero(receiver.BufferedBytes);
        }

        [Test]
        public void Receive_AllParityFragmentsLost_RestoresSnapshotFromDataAlone()
        {
            List<byte[]> datagrams = new();
            MultiplayerPacket original = Packet(1000, 12);
            CreateSender(datagrams).TrySend(original);
            MultiplayerSnapshotDatagrams receiver = CreateSender(new());
            MultiplayerPacket restored = null;

            foreach (byte[] datagram in datagrams.Where(datagram => datagram[5] == 0).Reverse())
                restored = receiver.Receive(new(datagram)) ?? restored;

            Assert.IsNotNull(restored);
            CollectionAssert.AreEqual(original.Payload, restored.Payload);
            Assert.Zero(receiver.BufferedBytes);
        }

        [Test]
        public void Receive_TwoDataErasuresInOneGroup_RetainsIncompleteSnapshotUntilExpiry()
        {
            double now = 0;
            List<byte[]> datagrams = new();
            CreateSender(datagrams).TrySend(Packet(1000, 12));
            MultiplayerSnapshotDatagrams receiver = new(128, _ => true, () => now);

            foreach (byte[] datagram in datagrams)
            {
                if (datagram[5] == 0 && FragmentIndex(datagram) < 2)
                    continue;
                Assert.IsNull(receiver.Receive(new(datagram)));
            }

            Assert.AreEqual(1, receiver.BufferedAssemblies);
            Assert.Greater(receiver.BufferedBytes, 0);
            now = MultiplayerSnapshotDatagrams.AssemblyLifetimeMilliseconds;
            receiver.Expire();
            Assert.Zero(receiver.BufferedBytes);
            foreach (byte[] datagram in datagrams)
                Assert.IsNull(receiver.Receive(new(datagram)));
        }

        [Test]
        public void Receive_DuplicateParityChangesBytes_RejectsConflictWithoutAdditionalStorage()
        {
            List<byte[]> datagrams = new();
            CreateSender(datagrams).TrySend(Packet(1000, 12));
            byte[] parity = datagrams.First(datagram => datagram[5] == 1);
            MultiplayerSnapshotDatagrams receiver = CreateSender(new());
            Assert.IsNull(receiver.Receive(new(parity)));
            int buffered = receiver.BufferedBytes;
            Assert.IsNull(receiver.Receive(new(parity)));
            Assert.AreEqual(buffered, receiver.BufferedBytes);
            byte[] changed = (byte[])parity.Clone();
            changed[changed.Length - 1] ^= 1;

            Assert.Throws<InvalidDataException>(() => receiver.Receive(new(changed)));

            Assert.AreEqual(buffered, receiver.BufferedBytes);
        }

        [Test]
        public void Receive_ActualDataConflictsWithRecoveredFragment_RejectsChangedBytes()
        {
            List<byte[]> datagrams = new();
            CreateSender(datagrams).TrySend(Packet(1000, 12));
            MultiplayerSnapshotDatagrams receiver = CreateSender(new());
            receiver.Receive(new(datagrams.First(datagram => datagram[5] == 1)));
            foreach (
                byte[] datagram in datagrams.Where(datagram =>
                    datagram[5] == 0 && FragmentIndex(datagram) is > 0 and < 4
                )
            )
                receiver.Receive(new(datagram));
            byte[] recovered = (byte[])datagrams.First(datagram => datagram[5] == 0).Clone();
            recovered[recovered.Length - 1] ^= 1;

            Assert.Throws<InvalidDataException>(() => receiver.Receive(new(recovered)));
        }

        [Test]
        public void Receive_ParityConflictsWithCompleteDataGroup_RejectsInconsistentGroup()
        {
            List<byte[]> datagrams = new();
            CreateSender(datagrams).TrySend(Packet(1000, 12));
            MultiplayerSnapshotDatagrams receiver = CreateSender(new());
            foreach (
                byte[] datagram in datagrams.Where(datagram =>
                    datagram[5] == 0 && FragmentIndex(datagram) < 4
                )
            )
                receiver.Receive(new(datagram));
            byte[] parity = (byte[])datagrams.First(datagram => datagram[5] == 1).Clone();
            parity[parity.Length - 1] ^= 1;

            Assert.Throws<InvalidDataException>(() => receiver.Receive(new(parity)));
        }

        [Test]
        public void TrySend_ParityBackpressure_StopsAtFirstRejectedDatagram()
        {
            int attempts = 0;
            MultiplayerSnapshotDatagrams sender = new(128, _ => ++attempts < 5);

            Assert.IsFalse(sender.TrySend(Packet(1000, 12)));

            Assert.AreEqual(5, attempts);
            Assert.Zero(sender.BufferedBytes);
        }

        [Test]
        public void Receive_ParityIsFirstArrival_CountsEveryDataParityAndReceiptBuffer()
        {
            List<byte[]> datagrams = new();
            MultiplayerPacket packet = Packet(1000, 12);
            CreateSender(datagrams).TrySend(packet);
            int dataCount = datagrams.Count(datagram => datagram[5] == 0);
            int parityCount = datagrams.Count(datagram => datagram[5] == 1);
            MultiplayerSnapshotDatagrams receiver = CreateSender(new());

            receiver.Receive(new(datagrams.First(datagram => datagram[5] == 1)));

            int expected =
                4
                + Encoding.UTF8.GetByteCount(packet.Metadata)
                + packet.Payload.Length
                + dataCount
                + parityCount * (128 - 22)
                + parityCount;
            Assert.AreEqual(expected, receiver.BufferedBytes);
            receiver.ClearAssembly();
            Assert.Zero(receiver.BufferedBytes);
        }

        [TestCase("version")]
        [TestCase("flag")]
        [TestCase("group")]
        [TestCase("length")]
        public void Receive_InvalidParityGeometry_RejectsBeforeBuffering(string mutation)
        {
            List<byte[]> datagrams = new();
            CreateSender(datagrams).TrySend(Packet(1000, 12));
            byte[] malformed = (byte[])datagrams.First(datagram => datagram[5] == 1).Clone();
            switch (mutation)
            {
                case "version":
                    malformed[4] = 1;
                    break;
                case "flag":
                    malformed[5] = 2;
                    break;
                case "group":
                    malformed[19] = 1;
                    break;
                case "length":
                    malformed = malformed.Take(malformed.Length - 1).ToArray();
                    break;
            }
            MultiplayerSnapshotDatagrams receiver = CreateSender(new());

            Assert.Throws<InvalidDataException>(() => receiver.Receive(new(malformed)));

            Assert.Zero(receiver.BufferedBytes);
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(14)]
        [TestCase(18)]
        [TestCase(20)]
        public void Receive_InvalidEnvelopeGeometry_RejectsBeforeBuffering(int offset)
        {
            List<byte[]> datagrams = new();
            CreateSender(datagrams).TrySend(Packet(1000, 1));
            byte[] malformed = datagrams[0];
            malformed[offset] = 0xff;
            MultiplayerSnapshotDatagrams receiver = CreateSender(new List<byte[]>());
            Assert.Throws<InvalidDataException>(() => receiver.Receive(new(malformed)));
            Assert.AreEqual(0, receiver.BufferedBytes);
        }

        [Test]
        public void TrySend_OrderedPacketOrExcessivePayload_RejectsWrongTransportWork()
        {
            MultiplayerSnapshotDatagrams sender = CreateSender(new List<byte[]>());
            Assert.Throws<ArgumentException>(() =>
                sender.TrySend(new(MultiplayerMessageType.TacticalEffects))
            );
            Assert.Throws<InvalidDataException>(() =>
                sender.TrySend(
                    new(
                        MultiplayerMessageType.TacticalSnapshot,
                        new string('x', MultiplayerSnapshotDatagrams.MaximumMetadataBytes + 1)
                    )
                )
            );
            Assert.Throws<InvalidDataException>(() =>
                sender.TrySend(
                    new(
                        MultiplayerMessageType.TacticalSnapshot,
                        payload: new byte[MultiplayerSnapshotDatagrams.MaximumPayloadBytes + 1]
                    )
                )
            );
        }

        /// <summary>
        /// Reads the explicit data index or parity group start from one transport datagram.
        /// </summary>
        /// <param name="datagram">The complete recorded snapshot datagram.</param>
        /// <returns>The unsigned network-order fragment index.</returns>
        private static int FragmentIndex(byte[] datagram) => datagram[18] << 8 | datagram[19];

        /// <summary>
        /// Creates a sender that copies accepted datagrams for controlled delivery.
        /// </summary>
        /// <param name="datagrams">The recorded outgoing datagrams.</param>
        /// <returns>The bounded snapshot channel.</returns>
        private static MultiplayerSnapshotDatagrams CreateSender(List<byte[]> datagrams) =>
            new(
                128,
                datagram =>
                {
                    datagrams.Add(datagram.ToArray());
                    return true;
                }
            );

        /// <summary>
        /// Creates one independently identified snapshot payload.
        /// </summary>
        /// <param name="size">The payload byte count.</param>
        /// <param name="seed">The exact payload seed.</param>
        /// <returns>The replaceable snapshot packet.</returns>
        private static MultiplayerPacket Packet(int size, int seed)
        {
            byte[] bytes = new byte[size];
            new Random(seed).NextBytes(bytes);
            return new(MultiplayerMessageType.TacticalSnapshot, "{\"frame\":" + seed + "}", bytes);
        }
    }
}
