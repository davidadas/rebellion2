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
                "{\"revision\":4}",
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
    }
}
