using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerPacketStreamTests
    {
        [Test]
        public async Task ReadAsync_FragmentedLargePacket_RestoresPayloadAndMetadataAsync()
        {
            byte[] payload = new byte[1024 * 1024];
            new Random(173).NextBytes(payload);
            MultiplayerPacketStream stream = null;
            stream = new MultiplayerPacketStream(
                (data, _) =>
                {
                    for (int offset = 0; offset < data.Count; offset += 1169)
                        stream.Append(
                            new ArraySegment<byte>(
                                data.Array,
                                data.Offset + offset,
                                Math.Min(1169, data.Count - offset)
                            )
                        );
                    return Task.CompletedTask;
                },
                MultiplayerPacketCodec.MaximumPacketLength
            );
            using (stream)
            {
                await MultiplayerPacketCodec.WriteAsync(
                    stream,
                    new MultiplayerPacket(
                        MultiplayerMessageType.SnapshotCommit,
                        "metadata",
                        payload
                    ),
                    CancellationToken.None
                );
                MultiplayerPacket packet = await MultiplayerPacketCodec.ReadAsync(
                    stream,
                    CancellationToken.None
                );
                Assert.That(packet.Payload, Is.EqualTo(payload));
                Assert.That(packet.Metadata, Is.EqualTo("metadata"));
                Assert.That(packet.Type, Is.EqualTo(MultiplayerMessageType.SnapshotCommit));
            }
        }

        [Test]
        public async Task ReadAsync_TwoQueuedPackets_PreservesTheirOrderAsync()
        {
            MultiplayerPacketStream stream = null;
            stream = new MultiplayerPacketStream(
                (data, _) =>
                {
                    stream.Append(data);
                    return Task.CompletedTask;
                },
                MultiplayerPacketCodec.MaximumPacketLength
            );
            using (stream)
            {
                await MultiplayerPacketCodec.WriteAsync(
                    stream,
                    new MultiplayerPacket(MultiplayerMessageType.Hello, "first"),
                    CancellationToken.None
                );
                await MultiplayerPacketCodec.WriteAsync(
                    stream,
                    new MultiplayerPacket(MultiplayerMessageType.Ready, "second"),
                    CancellationToken.None
                );
                MultiplayerPacket first = await MultiplayerPacketCodec.ReadAsync(
                    stream,
                    CancellationToken.None
                );
                MultiplayerPacket second = await MultiplayerPacketCodec.ReadAsync(
                    stream,
                    CancellationToken.None
                );
                Assert.That(first.Metadata, Is.EqualTo("first"));
                Assert.That(second.Metadata, Is.EqualTo("second"));
            }
        }

        [Test]
        public async Task Append_ProviderReusesBuffer_PreservesReceivedBytesAsync()
        {
            using MultiplayerPacketStream stream = CreateStream(16);
            byte[] input = { 1, 2, 3 };
            stream.Append(new ArraySegment<byte>(input));
            Array.Clear(input, 0, input.Length);
            byte[] output = new byte[3];
            await stream.ReadAsync(output, 0, output.Length, CancellationToken.None);
            Assert.That(output, Is.EqualTo(new byte[] { 1, 2, 3 }));
        }

        [Test]
        public async Task ReadAsync_FragmentArrivesAfterRead_CompletesPendingReadAsync()
        {
            using MultiplayerPacketStream stream = CreateStream(16);
            using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(5));
            byte[] output = new byte[3];
            Task<int> read = stream.ReadAsync(output, 0, output.Length, cancellation.Token);
            stream.Append(new ArraySegment<byte>(new byte[] { 4, 5, 6 }));
            Assert.That(await read, Is.EqualTo(3));
            Assert.That(output, Is.EqualTo(new byte[] { 4, 5, 6 }));
        }

        [Test]
        public async Task ReadAsync_CancelledPendingRead_AllowsSubsequentReadAsync()
        {
            using MultiplayerPacketStream stream = CreateStream(16);
            using CancellationTokenSource cancellation = new();
            byte[] output = new byte[1];
            Task<int> pending = stream.ReadAsync(output, 0, 1, cancellation.Token);
            cancellation.Cancel();
            bool cancelled = false;
            try
            {
                await pending;
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            Assert.That(cancelled, Is.True);
            stream.Append(new ArraySegment<byte>(new byte[] { 7 }));
            Assert.That(
                await stream.ReadAsync(output, 0, 1, CancellationToken.None),
                Is.EqualTo(1)
            );
            Assert.That(output[0], Is.EqualTo(7));
        }

        [Test]
        public void Append_BufferLimitExceeded_RejectsFragment()
        {
            using MultiplayerPacketStream stream = CreateStream(3);
            stream.Append(new ArraySegment<byte>(new byte[] { 1, 2 }));
            Assert.Throws<InvalidDataException>(() =>
                stream.Append(new ArraySegment<byte>(new byte[] { 3, 4 }))
            );
        }

        [Test]
        public async Task Append_PreviousBytesConsumed_ReusesBufferCapacityAsync()
        {
            using MultiplayerPacketStream stream = CreateStream(2);
            stream.Append(new ArraySegment<byte>(new byte[] { 1, 2 }));
            byte[] output = new byte[2];
            await stream.ReadAsync(output, 0, 2, CancellationToken.None);
            Assert.DoesNotThrow(() => stream.Append(new ArraySegment<byte>(new byte[] { 3, 4 })));
        }

        [Test]
        public async Task Complete_ReaderWaiting_ReturnsEndOfStreamAsync()
        {
            using MultiplayerPacketStream stream = CreateStream(16);
            Task<int> read = stream.ReadAsync(new byte[1], 0, 1, CancellationToken.None);
            stream.Complete();
            Assert.That(await read, Is.Zero);
        }

        [Test]
        public async Task Complete_ReceivedBytesRemain_DrainsBytesBeforeEndOfStreamAsync()
        {
            using MultiplayerPacketStream stream = CreateStream(16);
            stream.Append(new ArraySegment<byte>(new byte[] { 1, 2 }));
            stream.Complete();
            byte[] output = new byte[2];
            Assert.That(
                await stream.ReadAsync(output, 0, 2, CancellationToken.None),
                Is.EqualTo(2)
            );
            Assert.That(await stream.ReadAsync(output, 0, 2, CancellationToken.None), Is.Zero);
        }

        [Test]
        public void Complete_PacketTruncated_ReportsIncompletePacket()
        {
            using MultiplayerPacketStream stream = CreateStream(16);
            stream.Append(new ArraySegment<byte>(new byte[] { (byte)'R' }));
            stream.Complete();
            Assert.ThrowsAsync<EndOfStreamException>(async () =>
                await MultiplayerPacketCodec.ReadAsync(stream, CancellationToken.None)
            );
        }

        /// <summary>
        /// Creates a receive stream with a bounded queue and no outgoing traffic.
        /// </summary>
        /// <param name="maximumBytes">The receive queue limit.</param>
        /// <returns>The receive stream.</returns>
        private static MultiplayerPacketStream CreateStream(int maximumBytes)
        {
            return new MultiplayerPacketStream((_, _) => Task.CompletedTask, maximumBytes);
        }
    }
}
