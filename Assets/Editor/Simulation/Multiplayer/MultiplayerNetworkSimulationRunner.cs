using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Generation;
using Rebellion.Multiplayer;
using Rebellion.Simulation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Runs an AI-driven game through the direct peer transport and verifies every replicated turn.
/// </summary>
public static class MultiplayerNetworkSimulationRunner
{
    private const string _tickCountFlag = "-networkSimTicks";
    private const string _outputPathFlag = "-networkSimOut";
    private const string _seedFlag = "-networkSimSeed";
    private const int _defaultTickCount = 100;
    private const int _defaultSeed = 314159;

    /// <summary>
    /// Runs the command-line network simulation entry point.
    /// </summary>
    public static async void Run()
    {
        try
        {
            string[] args = Environment.GetCommandLineArgs();
            int tickCount = ReadIntArgument(args, _tickCountFlag, _defaultTickCount);
            int seed = ReadIntArgument(args, _seedFlag, _defaultSeed);
            string outputPath = ReadStringArgument(
                args,
                _outputPathFlag,
                Path.GetFullPath("Temp/multiplayer-network-simulation.json")
            );
            MultiplayerNetworkSimulationReport report = await RunAsync(tickCount, seed);
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
            UnityEngine.Debug.Log(
                $"[NetworkSim] complete ticks={report.ticksCompleted} bytes={report.bytesTransferred} output={outputPath}"
            );
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// Builds one deterministic AI game and transfers every completed turn over a loopback peer.
    /// </summary>
    /// <param name="tickCount">The number of turns to process.</param>
    /// <param name="seed">The deterministic generation seed.</param>
    /// <returns>The completed network simulation report.</returns>
    private static async Task<MultiplayerNetworkSimulationReport> RunAsync(int tickCount, int seed)
    {
        if (tickCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(tickCount));

        ContentPack contentPack = ContentPackLoader.OpenActive();
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Large,
            Difficulty = GameDifficulty.Medium,
            VictoryCondition = GameVictoryCondition.Conquest,
            ResourceAvailability = GameResourceAvailability.Normal,
            StartingFactionIDs = contentPack.Scenario.PlayableFactionIDs.ToArray(),
            PlayerFactionID = contentPack.Scenario.PlayableFactionIDs.First(),
            PackID = contentPack.Definition.ID,
            PackVersion = contentPack.Definition.Version,
            ScenarioID = contentPack.Scenario.ID,
            Seed = seed,
        };
        GameRoot hostGame = new GameBuilder(summary, contentPack.GameData).Build();
        foreach (Faction faction in hostGame.GetFactions())
        {
            Player player = hostGame.GetFactionPlayer(faction.InstanceID);
            hostGame.SetFactionController(
                faction.InstanceID,
                player.PlayerID,
                PlayerControllerType.AI
            );
        }

        using MultiplayerPeer host = new MultiplayerPeer();
        using MultiplayerPeer guest = new MultiplayerPeer();
        host.StartHost(0);
        await guest.ConnectAsync("127.0.0.1", host.ListeningPort);
        await WaitForEventAsync(host, MultiplayerTransportEventType.Connected);
        await WaitForEventAsync(guest, MultiplayerTransportEventType.Connected);

        long bytesTransferred = 0;
        bool peerProposalApplied = false;
        GameRoot guestGame = null;
        string finalHash = null;
        using GameSessionHolder session = new GameSessionHolder(
            new GameSession(hostGame, contentPack.GameData)
        );
        for (int index = 0; index < tickCount; index++)
        {
            session.Value.Tick.ProcessTick();
            hostGame = session.Value.Game;
            MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(hostGame);
            bytesTransferred += snapshot.Payload.Length;
            await host.SendAsync(CreateSnapshotPacket(snapshot, hostGame.CurrentTick));
            MultiplayerTransportEvent committed = await WaitForEventAsync(
                guest,
                MultiplayerTransportEventType.PacketReceived
            );
            MultiplayerNetworkSimulationMetadata committedMetadata =
                JsonUtility.FromJson<MultiplayerNetworkSimulationMetadata>(
                    committed.Packet.Metadata
                );
            if (
                committed.Packet.Type != MultiplayerMessageType.SnapshotCommit
                || committedMetadata?.hash != snapshot.Hash
                || !committed.Packet.Payload.SequenceEqual(snapshot.Payload)
            )
                throw new InvalidDataException(
                    $"Peer received a changed snapshot at tick {hostGame.CurrentTick}."
                );
            guestGame = RestoreSnapshot(committed.Packet);
            if (guestGame.CurrentTick != hostGame.CurrentTick)
                throw new InvalidDataException(
                    $"Peer restored the wrong turn at tick {hostGame.CurrentTick}."
                );
            finalHash = snapshot.Hash;

            if (!peerProposalApplied && index >= tickCount / 2)
            {
                guestGame.SetGameSpeed(TickSpeed.Fast);
                MultiplayerSnapshot proposal = MultiplayerSnapshotCodec.Capture(guestGame);
                bytesTransferred += proposal.Payload.Length;
                await guest.SendAsync(CreateSnapshotPacket(proposal, hostGame.CurrentTick));
                MultiplayerTransportEvent proposed = await WaitForEventAsync(
                    host,
                    MultiplayerTransportEventType.PacketReceived
                );
                hostGame = RestoreSnapshot(proposed.Packet);
                session.Replace(new GameSession(hostGame, contentPack.GameData));
                if (hostGame.GetGameSpeed() != TickSpeed.Fast)
                    throw new InvalidDataException("The host did not apply the peer proposal.");
                peerProposalApplied = true;
            }
        }

        return new MultiplayerNetworkSimulationReport
        {
            ticksRequested = tickCount,
            ticksCompleted = hostGame.CurrentTick,
            seed = seed,
            bytesTransferred = bytesTransferred,
            peerProposalApplied = peerProposalApplied,
            finalStateHash = finalHash,
        };
    }

    /// <summary>
    /// Creates one snapshot packet with simulation-friendly revision metadata.
    /// </summary>
    /// <param name="snapshot">The captured game state.</param>
    /// <param name="revision">The completed game turn.</param>
    /// <returns>The packet to send.</returns>
    private static MultiplayerPacket CreateSnapshotPacket(
        MultiplayerSnapshot snapshot,
        long revision
    )
    {
        MultiplayerNetworkSimulationMetadata metadata = new MultiplayerNetworkSimulationMetadata
        {
            revision = revision,
            hash = snapshot.Hash,
        };
        return new MultiplayerPacket(
            MultiplayerMessageType.SnapshotCommit,
            JsonUtility.ToJson(metadata),
            snapshot.Payload
        );
    }

    /// <summary>
    /// Restores a game from one simulation snapshot packet.
    /// </summary>
    /// <param name="packet">The received snapshot packet.</param>
    /// <returns>The restored game state.</returns>
    private static GameRoot RestoreSnapshot(MultiplayerPacket packet)
    {
        MultiplayerNetworkSimulationMetadata metadata =
            JsonUtility.FromJson<MultiplayerNetworkSimulationMetadata>(packet.Metadata);
        return MultiplayerSnapshotCodec.Restore(packet.Payload, metadata.hash);
    }

    /// <summary>
    /// Waits for one transport event while discarding unrelated events.
    /// </summary>
    /// <param name="peer">The peer to poll.</param>
    /// <param name="eventType">The required event type.</param>
    /// <returns>The matching transport event.</returns>
    private static async Task<MultiplayerTransportEvent> WaitForEventAsync(
        MultiplayerPeer peer,
        MultiplayerTransportEventType eventType
    )
    {
        Stopwatch timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            while (peer.TryDequeue(out MultiplayerTransportEvent transportEvent))
            {
                if (transportEvent.Type == eventType)
                    return transportEvent;
                if (transportEvent.Type == MultiplayerTransportEventType.Disconnected)
                    throw new IOException(transportEvent.Error ?? "The peer disconnected.");
            }
            await Task.Delay(1);
        }
        throw new TimeoutException($"Timed out waiting for {eventType}.");
    }

    /// <summary>
    /// Reads a positive integer command-line argument.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="flag">The value flag.</param>
    /// <param name="fallback">The value used when the flag is absent.</param>
    /// <returns>The parsed value.</returns>
    private static int ReadIntArgument(string[] args, string flag, int fallback)
    {
        string text = ReadStringArgument(args, flag, null);
        if (text == null)
            return fallback;
        if (!int.TryParse(text, out int value) || value <= 0)
            throw new ArgumentException($"{flag} requires a positive integer.");
        return value;
    }

    /// <summary>
    /// Reads a string command-line argument.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="flag">The value flag.</param>
    /// <param name="fallback">The value used when the flag is absent.</param>
    /// <returns>The requested value.</returns>
    private static string ReadStringArgument(string[] args, string flag, string fallback)
    {
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], flag, StringComparison.Ordinal))
                return args[index + 1];
        }
        return fallback;
    }

    /// <summary>
    /// Owns a replaceable simulation session for the host side of the network run.
    /// </summary>
    private sealed class GameSessionHolder : IDisposable
    {
        /// <summary>
        /// Gets the current host session.
        /// </summary>
        public GameSession Value { get; private set; }

        /// <summary>
        /// Creates a session holder.
        /// </summary>
        /// <param name="session">The initial host session.</param>
        public GameSessionHolder(GameSession session)
        {
            Value = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <summary>
        /// Replaces and disposes the previous host session.
        /// </summary>
        /// <param name="session">The replacement session.</param>
        public void Replace(GameSession session)
        {
            GameSession previous = Value;
            Value = session ?? throw new ArgumentNullException(nameof(session));
            previous.Dispose();
        }

        /// <summary>
        /// Disposes the current host session.
        /// </summary>
        public void Dispose()
        {
            Value?.Dispose();
            Value = null;
        }
    }

    /// <summary>
    /// Carries snapshot validation metadata within the simulation transport.
    /// </summary>
    [Serializable]
    private sealed class MultiplayerNetworkSimulationMetadata
    {
        public long revision;
        public string hash;
    }

    /// <summary>
    /// Records the observable outcome of one network simulation.
    /// </summary>
    [Serializable]
    private sealed class MultiplayerNetworkSimulationReport
    {
        public int ticksRequested;
        public int ticksCompleted;
        public int seed;
        public long bytesTransferred;
        public bool peerProposalApplied;
        public string finalStateHash;
    }
}
