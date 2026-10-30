using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml;
using Rebellion.Game;
using Rebellion.Multiplayer;
using Rebellion.Util.Random;
using Rebellion.Util.Serialization;
using UnityEngine;

namespace Rebellion.Tests.NativeAudit
{
    /// <summary>
    /// Compares snapshot capture and restoration against ordinary XML persistence on identical saves.
    /// </summary>
    public static class SnapshotSerializationBenchmark
    {
        private const int _sampleCount = 5;

        /// <summary>
        /// Runs explicit save paths supplied to Unity's batch-mode audit entry point.
        /// </summary>
        public static void Run()
        {
            VerifyAssembly(typeof(GameRoot).Assembly, "Scripts");
            VerifyAssembly(typeof(SnapshotSerializationBenchmark).Assembly, "Tests/NativeAudit");
            Report report = new()
            {
                build = typeof(GameRoot)
                    .Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                    .Single(attribute => attribute.Key == "SimulationBuild")
                    .Value,
                cases = Argument("-snapshotBenchmarkSaves")
                    .Split('|')
                    .Select(MeasureSave)
                    .ToArray(),
            };
            File.WriteAllText(
                Argument("-snapshotBenchmarkOutput"),
                JsonUtility.ToJson(report, true)
            );
        }

        /// <summary>
        /// Refuses measurements when a loaded assembly predates any of its source files.
        /// </summary>
        /// <param name="assembly">The loaded assembly that will execute the measured code.</param>
        /// <param name="sourceDirectory">The source directory relative to Assets.</param>
        private static void VerifyAssembly(Assembly assembly, string sourceDirectory)
        {
            DateTime newestSource = Directory
                .GetFiles(
                    Path.Combine(Application.dataPath, sourceDirectory),
                    "*.cs",
                    SearchOption.AllDirectories
                )
                .Max(File.GetLastWriteTimeUtc);
            if (File.GetLastWriteTimeUtc(assembly.Location) < newestSource)
                throw new InvalidOperationException(
                    "The loaded assembly is older than its sources: " + assembly.GetName().Name
                );
        }

        /// <summary>
        /// Reads a required command-line value without choosing an implicit input or output.
        /// </summary>
        /// <param name="name">The explicit argument name.</param>
        /// <returns>The supplied value.</returns>
        private static string Argument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, name);
            if (index < 0 || index + 1 >= arguments.Length)
                throw new ArgumentException("Missing benchmark argument: " + name);
            return arguments[index + 1];
        }

        /// <summary>
        /// Warms both paths and alternates their measured captures of one unchanged saved world.
        /// </summary>
        /// <param name="path">The exact existing save to compare.</param>
        /// <returns>The input identity, random position, and individual measurements.</returns>
        private static SaveResult MeasureSave(string path)
        {
            using FileStream input = File.OpenRead(path);
            GameRoot game = (GameRoot)
                new GameSerializer(
                    typeof(GameRoot),
                    new GameSerializerSettings { IgnoreUnknownElements = true }
                ).Deserialize(input);
            string fingerprint = MultiplayerSnapshotCodec.ComputeStateFingerprint(game);
            Measure(game, fingerprint, true);
            Measure(game, fingerprint, false);
            List<Sample> samples = new();
            for (int index = 0; index < _sampleCount; index++)
            {
                samples.Add(Measure(game, fingerprint, index % 2 == 0));
                samples.Add(Measure(game, fingerprint, index % 2 != 0));
            }
            SystemRandomProvider random = (SystemRandomProvider)game.Random;
            return new SaveResult
            {
                file = Path.GetFileName(path),
                fileHash = Hash(File.ReadAllBytes(path)),
                tick = game.CurrentTick,
                seed = random.Seed,
                randomCalls = random.CallCount,
                fingerprint = fingerprint,
                samples = samples.ToArray(),
            };
        }

        /// <summary>
        /// Measures complete compressed capture and verified restoration without advancing gameplay.
        /// </summary>
        /// <param name="game">The unchanged source world.</param>
        /// <param name="fingerprint">The source's persisted-state fingerprint.</param>
        /// <param name="xml">Whether to use the ordinary save serializer.</param>
        /// <returns>The elapsed times, payload size, and state-equivalence checks.</returns>
        private static Sample Measure(GameRoot game, string fingerprint, bool xml)
        {
            Stopwatch timer = Stopwatch.StartNew();
            MultiplayerSnapshot snapshot = xml
                ? CaptureXml(game)
                : MultiplayerSnapshotCodec.Capture(game);
            double capture = timer.Elapsed.TotalMilliseconds;
            timer.Restart();
            GameRoot restored = xml
                ? RestoreXml(snapshot)
                : MultiplayerSnapshotCodec.Restore(snapshot.Payload, snapshot.Hash);
            double restore = timer.Elapsed.TotalMilliseconds;
            return new Sample
            {
                format = xml ? "xml" : "json",
                captureMs = capture,
                restoreMs = restore,
                compressedBytes = snapshot.Payload.Length,
                sameState =
                    MultiplayerSnapshotCodec.ComputeStateFingerprint(restored) == fingerprint,
                unchangedSource =
                    MultiplayerSnapshotCodec.ComputeStateFingerprint(game) == fingerprint,
            };
        }

        /// <summary>
        /// Captures XML with the same compression level and integrity digest as the current wire codec.
        /// </summary>
        /// <param name="game">The source world.</param>
        /// <returns>The compressed XML and its byte-integrity digest.</returns>
        private static MultiplayerSnapshot CaptureXml(GameRoot game)
        {
            using MemoryStream state = new();
            new GameSerializer(
                typeof(GameRoot),
                new GameSerializerSettings
                {
                    Idendent = false,
                    NewLineHandling = NewLineHandling.Entitize,
                }
            ).Serialize(state, game);
            byte[] bytes = state.ToArray();
            using MemoryStream compressed = new();
            using (
                GZipStream gzip = new(
                    compressed,
                    System.IO.Compression.CompressionLevel.Fastest,
                    true
                )
            )
                gzip.Write(bytes, 0, bytes.Length);
            return new MultiplayerSnapshot(compressed.ToArray(), Hash(bytes));
        }

        /// <summary>
        /// Expands and verifies a controlled XML benchmark payload before restoring its model.
        /// </summary>
        /// <param name="snapshot">The payload captured by this audit.</param>
        /// <returns>The independently restored world.</returns>
        private static GameRoot RestoreXml(MultiplayerSnapshot snapshot)
        {
            using MemoryStream compressed = new(snapshot.Payload, false);
            using GZipStream gzip = new(compressed, CompressionMode.Decompress);
            using MemoryStream state = new();
            gzip.CopyTo(state);
            if (Hash(state.ToArray()) != snapshot.Hash)
                throw new InvalidDataException("The benchmark snapshot integrity check failed.");
            state.Position = 0;
            return (GameRoot)new GameSerializer(typeof(GameRoot)).Deserialize(state);
        }

        /// <summary>
        /// Computes the same byte-integrity digest used by snapshot envelopes.
        /// </summary>
        /// <param name="bytes">The complete input bytes.</param>
        /// <returns>The lowercase SHA-256 digest.</returns>
        private static string Hash(byte[] bytes)
        {
            using SHA256 algorithm = SHA256.Create();
            return BitConverter
                .ToString(algorithm.ComputeHash(bytes))
                .Replace("-", "")
                .ToLowerInvariant();
        }

        [Serializable]
        private sealed class Report
        {
            public string build;
            public SaveResult[] cases;
        }

        [Serializable]
        private sealed class SaveResult
        {
            public string file;
            public string fileHash;
            public int tick;
            public int seed;
            public long randomCalls;
            public string fingerprint;
            public Sample[] samples;
        }

        [Serializable]
        private sealed class Sample
        {
            public string format;
            public double captureMs;
            public double restoreMs;
            public int compressedBytes;
            public bool sameState;
            public bool unchangedSource;
        }
    }
}
