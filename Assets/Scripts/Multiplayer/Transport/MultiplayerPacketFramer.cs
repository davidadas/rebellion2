using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Frames ordered protocol bytes for a message transport with a fixed packet limit.
    /// </summary>
    public sealed class MultiplayerPacketFramer
    {
        private const byte _connectionFrame = 0;
        private const byte _dataFrame = 1;
        private readonly byte[] _buffer;
        private readonly Func<ArraySegment<byte>, bool> _trySend;

        /// <summary>
        /// Creates a single-writer framer with provider backpressure.
        /// </summary>
        /// <param name="maximumFrameBytes">The provider's maximum message size, including the frame tag.</param>
        /// <param name="trySend">Copies and accepts a frame, or returns false while the provider queue is full.</param>
        public MultiplayerPacketFramer(
            int maximumFrameBytes,
            Func<ArraySegment<byte>, bool> trySend
        )
        {
            if (maximumFrameBytes <= 1)
                throw new ArgumentOutOfRangeException(nameof(maximumFrameBytes));
            _trySend = trySend ?? throw new ArgumentNullException(nameof(trySend));
            _buffer = new byte[maximumFrameBytes];
            _buffer[0] = _dataFrame;
        }

        /// <summary>
        /// Creates the control frame that initiates a connection without entering the game codec.
        /// </summary>
        /// <returns>The connection control frame.</returns>
        public static ArraySegment<byte> CreateConnectionFrame() => new(new[] { _connectionFrame });

        /// <summary>
        /// Validates a transport frame and exposes its protocol bytes.
        /// </summary>
        /// <param name="frame">The received transport message.</param>
        /// <returns>The protocol bytes, or an empty range for a connection control frame.</returns>
        public static ArraySegment<byte> ReadFrame(ArraySegment<byte> frame)
        {
            if (frame.Count == 1 && frame.Array[frame.Offset] == _connectionFrame)
                return default;
            if (frame.Count <= 1 || frame.Array[frame.Offset] != _dataFrame)
                throw new InvalidDataException("The multiplayer transport frame is invalid.");
            return new ArraySegment<byte>(frame.Array, frame.Offset + 1, frame.Count - 1);
        }

        /// <summary>
        /// Splits bytes to the provider limit without dropping frames under backpressure.
        /// </summary>
        /// <param name="data">The ordered protocol bytes.</param>
        /// <param name="cancellationToken">Cancels waits for send capacity.</param>
        /// <returns>The pending ordered write.</returns>
        public async Task WriteAsync(ArraySegment<byte> data, CancellationToken cancellationToken)
        {
            int offset = 0;
            while (offset < data.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int length = Math.Min(_buffer.Length - 1, data.Count - offset);
                Buffer.BlockCopy(data.Array, data.Offset + offset, _buffer, 1, length);
                if (!_trySend(new ArraySegment<byte>(_buffer, 0, length + 1)))
                {
                    await Task.Yield();
                    continue;
                }
                offset += length;
            }
        }
    }
}
