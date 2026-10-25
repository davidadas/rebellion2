using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using Rebellion.Game;
using Rebellion.Util.Serialization;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Contains a compressed game snapshot and the hash of its canonical XML bytes.
    /// </summary>
    public sealed class MultiplayerSnapshot
    {
        /// <summary>
        /// Gets the compressed snapshot payload.
        /// </summary>
        public byte[] Payload { get; }

        /// <summary>
        /// Gets the lowercase SHA-256 state hash.
        /// </summary>
        public string Hash { get; }

        /// <summary>
        /// Creates a snapshot value.
        /// </summary>
        /// <param name="payload">The compressed snapshot payload.</param>
        /// <param name="hash">The canonical state hash.</param>
        public MultiplayerSnapshot(byte[] payload, string hash)
        {
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
        }
    }

    /// <summary>
    /// Serializes deterministic launch settings and complete synchronized game states.
    /// </summary>
    public static class MultiplayerSnapshotCodec
    {
        private const int _maximumExpandedSnapshotLength = 512 * 1024 * 1024;

        /// <summary>
        /// Serializes launch settings to their XML wire representation.
        /// </summary>
        /// <param name="summary">The launch settings.</param>
        /// <returns>The serialized settings.</returns>
        public static byte[] SerializeSummary(GameSummary summary)
        {
            if (summary == null)
                throw new ArgumentNullException(nameof(summary));
            return Serialize(typeof(GameSummary), summary);
        }

        /// <summary>
        /// Deserializes launch settings from their XML wire representation.
        /// </summary>
        /// <param name="payload">The serialized settings.</param>
        /// <returns>The restored launch settings.</returns>
        public static GameSummary DeserializeSummary(byte[] payload)
        {
            return (GameSummary)Deserialize(typeof(GameSummary), payload);
        }

        /// <summary>
        /// Captures a complete game as compressed XML and hashes the uncompressed bytes.
        /// </summary>
        /// <param name="game">The game to capture.</param>
        /// <returns>The compressed snapshot and canonical state hash.</returns>
        public static MultiplayerSnapshot Capture(GameRoot game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));

            byte[] xml = Serialize(typeof(GameRoot), game);
            return new MultiplayerSnapshot(Compress(xml), ComputeHash(xml));
        }

        /// <summary>
        /// Restores and validates a complete game snapshot.
        /// </summary>
        /// <param name="snapshot">The compressed snapshot bytes.</param>
        /// <param name="expectedHash">The expected canonical state hash.</param>
        /// <returns>The restored game.</returns>
        public static GameRoot Restore(byte[] snapshot, string expectedHash)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (string.IsNullOrWhiteSpace(expectedHash))
                throw new ArgumentException("A snapshot hash is required.", nameof(expectedHash));

            byte[] xml = Decompress(snapshot);
            string actualHash = ComputeHash(xml);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The multiplayer snapshot hash does not match.");
            return (GameRoot)Deserialize(typeof(GameRoot), xml);
        }

        /// <summary>
        /// Serializes one supported object to a byte array.
        /// </summary>
        /// <param name="type">The persisted root type.</param>
        /// <param name="value">The value to serialize.</param>
        /// <returns>The serialized XML bytes.</returns>
        private static byte[] Serialize(Type type, object value)
        {
            using MemoryStream stream = new MemoryStream();
            new GameSerializer(type).Serialize(stream, value);
            return stream.ToArray();
        }

        /// <summary>
        /// Deserializes one supported object from a byte array.
        /// </summary>
        /// <param name="type">The persisted root type.</param>
        /// <param name="payload">The serialized XML bytes.</param>
        /// <returns>The restored object.</returns>
        private static object Deserialize(Type type, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidDataException("The multiplayer payload is empty.");
            using MemoryStream stream = new MemoryStream(payload, writable: false);
            return new GameSerializer(type).Deserialize(stream);
        }

        /// <summary>
        /// Compresses canonical XML bytes for transport.
        /// </summary>
        /// <param name="source">The XML bytes.</param>
        /// <returns>The compressed representation.</returns>
        private static byte[] Compress(byte[] source)
        {
            using MemoryStream destination = new MemoryStream();
            using (GZipStream gzip = new GZipStream(destination, CompressionLevel.Fastest, true))
                gzip.Write(source, 0, source.Length);
            return destination.ToArray();
        }

        /// <summary>
        /// Decompresses a bounded snapshot payload.
        /// </summary>
        /// <param name="source">The compressed representation.</param>
        /// <returns>The canonical XML bytes.</returns>
        private static byte[] Decompress(byte[] source)
        {
            using MemoryStream compressed = new MemoryStream(source, writable: false);
            using GZipStream gzip = new GZipStream(compressed, CompressionMode.Decompress);
            using MemoryStream destination = new MemoryStream();
            byte[] buffer = new byte[81920];
            int read;
            while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (destination.Length + read > _maximumExpandedSnapshotLength)
                    throw new InvalidDataException("The multiplayer snapshot is too large.");
                destination.Write(buffer, 0, read);
            }
            return destination.ToArray();
        }

        /// <summary>
        /// Computes a lowercase SHA-256 hash for canonical state bytes.
        /// </summary>
        /// <param name="source">The bytes to hash.</param>
        /// <returns>The lowercase hexadecimal hash.</returns>
        private static string ComputeHash(byte[] source)
        {
            using SHA256 algorithm = SHA256.Create();
            return BitConverter
                .ToString(algorithm.ComputeHash(source))
                .Replace("-", "")
                .ToLowerInvariant();
        }
    }
}
