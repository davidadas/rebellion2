using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Reads and writes bounded, versioned multiplayer packets.
    /// </summary>
    public static class MultiplayerPacketCodec
    {
        private const int _headerLength = 14;
        private const int _maximumMetadataLength = 1024 * 1024;
        private const int _maximumPayloadLength = 128 * 1024 * 1024;
        private const byte _protocolVersion = 1;
        private static readonly byte[] _magic = { (byte)'R', (byte)'2', (byte)'M', (byte)'P' };

        /// <summary>
        /// Gets the largest supported encoded packet, including its header.
        /// </summary>
        public static int MaximumPacketLength =>
            _headerLength + _maximumMetadataLength + _maximumPayloadLength;

        /// <summary>
        /// Writes one complete packet to a stream.
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="packet">The packet to write.</param>
        /// <param name="cancellationToken">Cancels pending I/O.</param>
        /// <returns>A task that completes when the packet has been flushed.</returns>
        public static async Task WriteAsync(
            Stream stream,
            MultiplayerPacket packet,
            CancellationToken cancellationToken
        )
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (packet == null)
                throw new ArgumentNullException(nameof(packet));

            byte[] metadata = Encoding.UTF8.GetBytes(packet.Metadata ?? string.Empty);
            byte[] payload = packet.Payload ?? Array.Empty<byte>();
            ValidateLengths(metadata.Length, payload.Length);

            byte[] header = new byte[_headerLength];
            Buffer.BlockCopy(_magic, 0, header, 0, _magic.Length);
            header[4] = _protocolVersion;
            header[5] = (byte)packet.Type;
            WriteInt32(header, 6, metadata.Length);
            WriteInt32(header, 10, payload.Length);

            await stream.WriteAsync(header, 0, header.Length, cancellationToken);
            if (metadata.Length > 0)
                await stream.WriteAsync(metadata, 0, metadata.Length, cancellationToken);
            if (payload.Length > 0)
                await stream.WriteAsync(payload, 0, payload.Length, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// Reads one complete packet from a stream.
        /// </summary>
        /// <param name="stream">The source stream.</param>
        /// <param name="cancellationToken">Cancels pending I/O.</param>
        /// <returns>The decoded packet, or null when the stream ends before another header begins.</returns>
        public static async Task<MultiplayerPacket> ReadAsync(
            Stream stream,
            CancellationToken cancellationToken
        )
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            byte[] header = new byte[_headerLength];
            int firstRead = await stream.ReadAsync(header, 0, 1, cancellationToken);
            if (firstRead == 0)
                return null;
            await ReadExactlyAsync(stream, header, 1, header.Length - 1, cancellationToken);

            for (int index = 0; index < _magic.Length; index++)
            {
                if (header[index] != _magic[index])
                    throw new InvalidDataException("The multiplayer packet header is invalid.");
            }
            if (header[4] != _protocolVersion)
            {
                throw new InvalidDataException(
                    $"Unsupported multiplayer protocol version {header[4]}."
                );
            }

            int metadataLength = ReadInt32(header, 6);
            int payloadLength = ReadInt32(header, 10);
            ValidateLengths(metadataLength, payloadLength);
            byte[] metadata = new byte[metadataLength];
            byte[] payload = new byte[payloadLength];
            await ReadExactlyAsync(stream, metadata, 0, metadata.Length, cancellationToken);
            await ReadExactlyAsync(stream, payload, 0, payload.Length, cancellationToken);
            return new MultiplayerPacket(
                (MultiplayerMessageType)header[5],
                Encoding.UTF8.GetString(metadata),
                payload
            );
        }

        /// <summary>
        /// Reads the requested byte count or fails when the stream ends early.
        /// </summary>
        /// <param name="stream">The source stream.</param>
        /// <param name="buffer">The destination buffer.</param>
        /// <param name="offset">The initial destination offset.</param>
        /// <param name="count">The number of bytes required.</param>
        /// <param name="cancellationToken">Cancels pending I/O.</param>
        /// <returns>A task that completes when the requested bytes have been read.</returns>
        private static async Task ReadExactlyAsync(
            Stream stream,
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        )
        {
            int total = 0;
            while (total < count)
            {
                int read = await stream.ReadAsync(
                    buffer,
                    offset + total,
                    count - total,
                    cancellationToken
                );
                if (read == 0)
                    throw new EndOfStreamException("The peer disconnected during a packet.");
                total += read;
            }
        }

        /// <summary>
        /// Validates metadata and payload bounds before allocating or writing buffers.
        /// </summary>
        /// <param name="metadataLength">The metadata byte count.</param>
        /// <param name="payloadLength">The payload byte count.</param>
        private static void ValidateLengths(int metadataLength, int payloadLength)
        {
            if (metadataLength < 0 || metadataLength > _maximumMetadataLength)
                throw new InvalidDataException("The multiplayer metadata length is invalid.");
            if (payloadLength < 0 || payloadLength > _maximumPayloadLength)
                throw new InvalidDataException("The multiplayer payload length is invalid.");
        }

        /// <summary>
        /// Writes a signed 32-bit integer in network byte order.
        /// </summary>
        /// <param name="buffer">The destination buffer.</param>
        /// <param name="offset">The destination offset.</param>
        /// <param name="value">The value to write.</param>
        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        /// <summary>
        /// Reads a signed 32-bit integer in network byte order.
        /// </summary>
        /// <param name="buffer">The source buffer.</param>
        /// <param name="offset">The source offset.</param>
        /// <returns>The decoded value.</returns>
        private static int ReadInt32(byte[] buffer, int offset)
        {
            return (buffer[offset] << 24)
                | (buffer[offset + 1] << 16)
                | (buffer[offset + 2] << 8)
                | buffer[offset + 3];
        }
    }
}
