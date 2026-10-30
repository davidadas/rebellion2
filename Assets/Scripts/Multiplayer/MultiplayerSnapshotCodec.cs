using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml;
using Rebellion.Game;
using Rebellion.Gameplay;
using Rebellion.Util.Random;
using Rebellion.Util.Serialization;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Contains a compressed game snapshot and the hash of its uncompressed wire bytes.
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
        /// <param name="hash">The uncompressed wire-state hash.</param>
        public MultiplayerSnapshot(byte[] payload, string hash)
        {
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
        }
    }

    /// <summary>
    /// Serializes XML launch settings and complete synchronized wire states.
    /// </summary>
    public static class MultiplayerSnapshotCodec
    {
        private const int _maximumExpandedSnapshotLength = 512 * 1024 * 1024;

        /// <summary>
        /// Captures a completed simulation through the same persisted world contract used by ordinary snapshots.
        /// </summary>
        /// <param name="session">The completed session whose world is captured.</param>
        /// <returns>The compressed world and its integrity hash.</returns>
        public static MultiplayerSnapshot Capture(GameSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (!session.CanCaptureSettledState || !session.CanExecuteCommands)
                throw new InvalidOperationException(
                    "A snapshot requires a completed turn outside simulation execution."
                );
            if (
                session.GetService<IRandomNumberProvider>() is not SystemRandomProvider random
                || !ReferenceEquals(random, session.Game.Random)
            )
                throw new InvalidOperationException(
                    "A snapshot requires the session's active deterministic random stream."
                );
            return Capture(session.Game);
        }

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
        /// Detects persisted state changes through the existing serializer without materializing a snapshot.
        /// </summary>
        /// <param name="game">The stable game state to inspect.</param>
        /// <returns>The local serializer-token fingerprint; this is not a wire integrity hash.</returns>
        public static string ComputeStateFingerprint(GameRoot game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            return MultiplayerStateFingerprint.Compute(game);
        }

        /// <summary>
        /// Captures a complete game through the save serializer and hashes its uncompressed XML.
        /// </summary>
        /// <param name="game">The game to capture.</param>
        /// <returns>The compressed snapshot and canonical state hash.</returns>
        public static MultiplayerSnapshot Capture(GameRoot game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));

            byte[] state = Serialize(typeof(GameRoot), game);
            return CapturePayload(state);
        }

        /// <summary>
        /// Compresses and authenticates bytes produced by an explicitly selected snapshot schema.
        /// </summary>
        /// <param name="state">The complete uncompressed schema payload.</param>
        /// <returns>The bounded snapshot envelope.</returns>
        internal static MultiplayerSnapshot CapturePayload(byte[] state)
        {
            return new MultiplayerSnapshot(Compress(state), ComputeHash(state));
        }

        /// <summary>
        /// Restores and validates a complete game snapshot.
        /// </summary>
        /// <param name="snapshot">The compressed snapshot bytes.</param>
        /// <param name="expectedHash">The expected canonical state hash.</param>
        /// <returns>The restored game.</returns>
        public static GameRoot Restore(byte[] snapshot, string expectedHash)
        {
            return (GameRoot)Deserialize(typeof(GameRoot), RestorePayload(snapshot, expectedHash));
        }

        /// <summary>
        /// Verifies and expands a snapshot before its explicit schema is decoded.
        /// </summary>
        /// <param name="snapshot">The compressed snapshot bytes.</param>
        /// <param name="expectedHash">The expected SHA-256 payload hash.</param>
        /// <returns>The verified uncompressed schema payload.</returns>
        internal static byte[] RestorePayload(byte[] snapshot, string expectedHash)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (string.IsNullOrWhiteSpace(expectedHash))
                throw new ArgumentException("A snapshot hash is required.", nameof(expectedHash));

            byte[] state = Decompress(snapshot);
            string actualHash = ComputeHash(state);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The multiplayer snapshot hash does not match.");
            return state;
        }

        /// <summary>
        /// Serializes one supported object to a byte array.
        /// </summary>
        /// <param name="type">The persisted root type.</param>
        /// <param name="value">The value to serialize.</param>
        /// <returns>The serialized XML bytes.</returns>
        internal static byte[] Serialize(Type type, object value)
        {
            using MemoryStream stream = new MemoryStream();
            new GameSerializer(
                type,
                new GameSerializerSettings
                {
                    RootName = type.Name,
                    Idendent = false,
                    NewLineHandling = NewLineHandling.Entitize,
                }
            ).Serialize(stream, value);
            return stream.ToArray();
        }

        /// <summary>
        /// Deserializes one supported object from a byte array.
        /// </summary>
        /// <param name="type">The persisted root type.</param>
        /// <param name="payload">The serialized XML bytes.</param>
        /// <returns>The restored object.</returns>
        internal static object Deserialize(Type type, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidDataException("The multiplayer payload is empty.");
            using MemoryStream stream = new MemoryStream(payload, writable: false);
            using XmlReader reader = XmlReader.Create(stream);
            reader.MoveToContent();
            if (reader.Name != type.Name)
                throw new InvalidDataException(
                    $"Expected XML root '{type.Name}', received '{reader.Name}'."
                );
            return new GameSerializer(type).Deserialize(reader);
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
