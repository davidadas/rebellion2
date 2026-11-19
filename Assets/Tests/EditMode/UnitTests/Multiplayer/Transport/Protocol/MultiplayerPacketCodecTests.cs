using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerPacketCodecTests
    {
        [Test]
        public async Task WriteAndReadAsync_ValidPacket_RoundTripsMetadataAndBinaryPayloadAsync()
        {
            MultiplayerPacket expected = new MultiplayerPacket(
                MultiplayerMessageType.SnapshotCommit,
                "{\"hash\":\"payload-hash\"}",
                new byte[] { 0, 1, 2, 254, 255 }
            );
            using MemoryStream stream = new MemoryStream();

            await MultiplayerPacketCodec.WriteAsync(stream, expected, CancellationToken.None);
            stream.Position = 0;
            MultiplayerPacket actual = await MultiplayerPacketCodec.ReadAsync(
                stream,
                CancellationToken.None
            );

            Assert.That(actual.Type, Is.EqualTo(expected.Type));
            Assert.That(actual.Metadata, Is.EqualTo(expected.Metadata));
            Assert.That(actual.Payload, Is.EqualTo(expected.Payload));
        }

        [Test]
        public void ReadAsync_TruncatedPacket_ThrowsEndOfStreamException()
        {
            byte[] truncated =
            {
                (byte)'R',
                (byte)'2',
                (byte)'M',
                (byte)'P',
                1,
                (byte)MultiplayerMessageType.Hello,
                0,
                0,
                0,
                5,
                0,
                0,
                0,
                0,
                (byte)'{',
            };
            using MemoryStream stream = new MemoryStream(truncated);

            Assert.ThrowsAsync<EndOfStreamException>(async () =>
                await MultiplayerPacketCodec.ReadAsync(stream, CancellationToken.None)
            );
        }

        [Test]
        public void ReadAsync_OversizedPayload_RejectsBeforeAllocation()
        {
            byte[] header =
            {
                (byte)'R',
                (byte)'2',
                (byte)'M',
                (byte)'P',
                1,
                (byte)MultiplayerMessageType.SnapshotCommit,
                0,
                0,
                0,
                0,
                0x08,
                0x00,
                0x00,
                0x01,
            };
            using MemoryStream stream = new MemoryStream(header);

            Assert.ThrowsAsync<InvalidDataException>(async () =>
                await MultiplayerPacketCodec.ReadAsync(stream, CancellationToken.None)
            );
        }

        [Test]
        public async Task ReadAsync_EmptyStream_ReturnsNullAsync()
        {
            using MemoryStream stream = new MemoryStream();

            MultiplayerPacket packet = await MultiplayerPacketCodec.ReadAsync(
                stream,
                CancellationToken.None
            );

            Assert.That(packet, Is.Null);
        }

        [TestCase(
            MultiplayerMessageType.TacticalCommand,
            0,
            MultiplayerPacketCodec.MaximumTacticalCommandBytes + 1
        )]
        [TestCase(
            MultiplayerMessageType.TacticalSnapshot,
            0,
            MultiplayerPacketCodec.MaximumTacticalSnapshotBytes + 1
        )]
        [TestCase(
            MultiplayerMessageType.TacticalCheckpoint,
            0,
            MultiplayerPacketCodec.MaximumTacticalSnapshotBytes + 1
        )]
        [TestCase(
            MultiplayerMessageType.TacticalEffects,
            0,
            MultiplayerPacketCodec.MaximumTacticalEffectsBytes + 1
        )]
        [TestCase(MultiplayerMessageType.TacticalReady, 0, 1)]
        [TestCase(MultiplayerMessageType.TacticalCommandResult, 0, 1)]
        [TestCase(MultiplayerMessageType.TacticalSnapshotReceipt, 0, 1)]
        [TestCase(MultiplayerMessageType.TacticalMovieReceipt, 0, 1)]
        [TestCase(MultiplayerMessageType.TacticalResync, 0, 1)]
        [TestCase(
            MultiplayerMessageType.TacticalSnapshot,
            MultiplayerPacketCodec.MaximumTacticalMetadataBytes + 1,
            0
        )]
        public void ReadAsync_ExcessiveTacticalHeader_RejectsBeforeReadingOrAllocatingBody(
            MultiplayerMessageType type,
            int metadataLength,
            int payloadLength
        )
        {
            using MemoryStream stream = new(Header(type, metadataLength, payloadLength));

            Assert.ThrowsAsync<InvalidDataException>(async () =>
                await MultiplayerPacketCodec.ReadAsync(stream, CancellationToken.None)
            );

            Assert.AreEqual(14, stream.Position);
        }

        [TestCase(
            MultiplayerMessageType.TacticalCommand,
            MultiplayerPacketCodec.MaximumTacticalCommandBytes + 1
        )]
        [TestCase(
            MultiplayerMessageType.TacticalSnapshot,
            MultiplayerPacketCodec.MaximumTacticalSnapshotBytes + 1
        )]
        [TestCase(
            MultiplayerMessageType.TacticalCheckpoint,
            MultiplayerPacketCodec.MaximumTacticalSnapshotBytes + 1
        )]
        [TestCase(
            MultiplayerMessageType.TacticalEffects,
            MultiplayerPacketCodec.MaximumTacticalEffectsBytes + 1
        )]
        [TestCase(MultiplayerMessageType.TacticalReady, 1)]
        [TestCase(MultiplayerMessageType.TacticalCommandResult, 1)]
        [TestCase(MultiplayerMessageType.TacticalSnapshotReceipt, 1)]
        [TestCase(MultiplayerMessageType.TacticalMovieReceipt, 1)]
        [TestCase(MultiplayerMessageType.TacticalResync, 1)]
        public void WriteAsync_ExcessiveTacticalPayload_RejectsBeforeWritingHeader(
            MultiplayerMessageType type,
            int payloadLength
        )
        {
            using MemoryStream stream = new();
            MultiplayerPacket packet = new(type, payload: new byte[payloadLength]);

            Assert.ThrowsAsync<InvalidDataException>(async () =>
                await MultiplayerPacketCodec.WriteAsync(stream, packet, CancellationToken.None)
            );

            Assert.AreEqual(0, stream.Length);
        }

        [Test]
        public void WriteAsync_TacticalMetadataUtf8ExceedsByteLimit_RejectsBeforeWritingHeader()
        {
            using MemoryStream stream = new();
            MultiplayerPacket packet = new(
                MultiplayerMessageType.TacticalReady,
                new string('\u96ea', 683)
            );

            Assert.ThrowsAsync<InvalidDataException>(async () =>
                await MultiplayerPacketCodec.WriteAsync(stream, packet, CancellationToken.None)
            );

            Assert.AreEqual(0, stream.Length);
        }

        [Test]
        public async Task WriteAndReadAsync_ExactTacticalCommandBounds_RoundTripsAsync()
        {
            using MemoryStream stream = new();
            MultiplayerPacket expected = new(
                MultiplayerMessageType.TacticalCommand,
                new string('x', MultiplayerPacketCodec.MaximumTacticalMetadataBytes),
                new byte[MultiplayerPacketCodec.MaximumTacticalCommandBytes]
            );
            await MultiplayerPacketCodec.WriteAsync(stream, expected, CancellationToken.None);
            stream.Position = 0;

            MultiplayerPacket actual = await MultiplayerPacketCodec.ReadAsync(
                stream,
                CancellationToken.None
            );

            Assert.AreEqual(expected.Metadata, actual.Metadata);
            CollectionAssert.AreEqual(expected.Payload, actual.Payload);
        }

        [Test]
        public async Task WriteAndReadAsync_CampaignSnapshotAboveTacticalLimits_RetainsBulkContractAsync()
        {
            using MemoryStream stream = new();
            MultiplayerPacket expected = new(
                MultiplayerMessageType.SnapshotCommit,
                new string('x', MultiplayerPacketCodec.MaximumTacticalMetadataBytes + 1),
                new byte[MultiplayerPacketCodec.MaximumTacticalCommandBytes + 1]
            );
            await MultiplayerPacketCodec.WriteAsync(stream, expected, CancellationToken.None);
            stream.Position = 0;

            MultiplayerPacket actual = await MultiplayerPacketCodec.ReadAsync(
                stream,
                CancellationToken.None
            );

            Assert.AreEqual(expected.Metadata, actual.Metadata);
            CollectionAssert.AreEqual(expected.Payload, actual.Payload);
        }

        /// <summary>
        /// Creates a header without allocating or supplying its declared body.
        /// </summary>
        /// <param name="type">The declared wire message type.</param>
        /// <param name="metadataLength">The declared metadata byte length.</param>
        /// <param name="payloadLength">The declared payload byte length.</param>
        /// <returns>The complete header bytes.</returns>
        private static byte[] Header(
            MultiplayerMessageType type,
            int metadataLength,
            int payloadLength
        )
        {
            byte[] header =
            {
                (byte)'R',
                (byte)'2',
                (byte)'M',
                (byte)'P',
                1,
                (byte)type,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
            };
            for (int index = 0; index < 4; index++)
            {
                header[6 + index] = (byte)(metadataLength >> (24 - index * 8));
                header[10 + index] = (byte)(payloadLength >> (24 - index * 8));
            }
            return header;
        }
    }
}
