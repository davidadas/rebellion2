using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerPacketFramerTests
    {
        [Test]
        public async Task WriteAsync_LargeByteRange_PreservesBytesWithinMessageLimitAsync()
        {
            byte[] input = new byte[16384];
            new Random(32).NextBytes(input);
            List<byte[]> frames = new();
            MultiplayerPacketFramer framer = new(
                1170,
                frame =>
                {
                    frames.Add(frame.ToArray());
                    return true;
                }
            );
            await framer.WriteAsync(new ArraySegment<byte>(input), CancellationToken.None);
            Assert.That(frames.All(frame => frame.Length <= 1170), Is.True);
            Assert.That(
                frames.SelectMany(frame =>
                    MultiplayerPacketFramer.ReadFrame(new ArraySegment<byte>(frame))
                ),
                Is.EqualTo(input)
            );
        }

        [Test]
        public async Task WriteAsync_Backpressure_RetriesWithoutDroppingBytesAsync()
        {
            int attempts = 0;
            List<byte> accepted = new();
            MultiplayerPacketFramer framer = new(
                4,
                frame =>
                {
                    attempts++;
                    if (attempts % 2 == 1)
                        return false;
                    accepted.AddRange(MultiplayerPacketFramer.ReadFrame(frame));
                    return true;
                }
            );
            await framer.WriteAsync(
                new ArraySegment<byte>(new byte[] { 1, 2, 3, 4, 5 }),
                CancellationToken.None
            );
            Assert.That(accepted, Is.EqualTo(new byte[] { 1, 2, 3, 4, 5 }));
            Assert.That(attempts, Is.EqualTo(4));
        }

        [Test]
        public async Task WriteAsync_CancelledDuringBackpressure_StopsRetryingAsync()
        {
            using CancellationTokenSource cancellation = new();
            MultiplayerPacketFramer framer = new(
                4,
                _ =>
                {
                    cancellation.Cancel();
                    return false;
                }
            );
            bool cancelled = false;
            try
            {
                await framer.WriteAsync(
                    new ArraySegment<byte>(new byte[] { 1 }),
                    cancellation.Token
                );
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            Assert.That(cancelled, Is.True);
        }

        [Test]
        public void ReadFrame_ConnectionControlFrame_ReturnsNoGameBytes()
        {
            Assert.That(
                MultiplayerPacketFramer
                    .ReadFrame(MultiplayerPacketFramer.CreateConnectionFrame())
                    .Count,
                Is.Zero
            );
        }

        [TestCase(new byte[0])]
        [TestCase(new byte[] { 1 })]
        [TestCase(new byte[] { 7, 1 })]
        public void ReadFrame_MalformedFrame_RejectsData(byte[] frame)
        {
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerPacketFramer.ReadFrame(new ArraySegment<byte>(frame))
            );
        }

        [Test]
        public void ReadFrame_ByteRangeHasOffset_ReturnsOnlyProtocolBytes()
        {
            byte[] bytes = { 99, 1, 2, 3, 99 };
            Assert.That(
                MultiplayerPacketFramer.ReadFrame(new ArraySegment<byte>(bytes, 1, 3)),
                Is.EqualTo(new byte[] { 2, 3 })
            );
        }
    }
}
