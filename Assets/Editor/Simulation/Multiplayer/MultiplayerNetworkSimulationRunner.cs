using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Rebellion.App.PlayerActions;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Generation;
using Rebellion.Multiplayer;
using Rebellion.Util.Serialization;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies two independent simulations using ordered player operations and day barriers over TCP.
/// Full-state diagnostics run only in this explicitly requested Editor verification entry point.
/// </summary>
public static class MultiplayerNetworkSimulationRunner
{
    private const string _tickCountFlag = "-networkSimTicks";
    private const string _outputPathFlag = "-networkSimOut";
    private const string _seedFlag = "-networkSimSeed";
    private const string _galaxySizeFlag = "-networkSimSize";
    private const string _victoryFlag = "-networkSimVictory";
    private const string _inputSaveFlag = "-networkSimSave";
    private const string _preserveControllersFlag = "-networkSimPreserveControllers";
    private const string _fightHumanBattlesFlag = "-networkSimFightHumanBattles";
    private const string _humanParticipantsFlag = "-networkSimHumanParticipants";
    private const string _saveDirectoryFlag = "-saveDirectory";
    private const int _defaultTickCount = 100;
    private const int _defaultSeed = 314159;

    /// <summary>
    /// Runs the command-line verification and writes a report even when synchronization fails.
    /// </summary>
    public static async void Run()
    {
        MultiplayerNetworkSimulationReport report = new MultiplayerNetworkSimulationReport();
        string outputPath = Path.GetFullPath("Temp/multiplayer-network-simulation.json");
        Stopwatch duration = Stopwatch.StartNew();
        int exitCode = 1;
        bool refreshSuppressed = false;
        bool reloadLocked = false;
        try
        {
            VerifyLoadedBuild(report);
            AssetDatabase.DisallowAutoRefresh();
            refreshSuppressed = true;
            EditorApplication.LockReloadAssemblies();
            reloadLocked = true;
            UnityEngine.Debug.Log("[NetworkSim] loaded build verified and reload locked");
            string[] args = Environment.GetCommandLineArgs();
            outputPath = Path.GetFullPath(ReadStringArgument(args, _outputPathFlag, outputPath));
            report.ticksRequested = ReadIntArgument(args, _tickCountFlag, _defaultTickCount);
            report.seed = ReadIntArgument(args, _seedFlag, _defaultSeed);
            report.preserveControllers = args.Any(argument =>
                string.Equals(argument, _preserveControllersFlag, StringComparison.Ordinal)
            );
            report.fightHumanBattles = args.Any(argument =>
                string.Equals(argument, _fightHumanBattlesFlag, StringComparison.Ordinal)
            );
            report.humanParticipants = args.Any(argument =>
                string.Equals(argument, _humanParticipantsFlag, StringComparison.Ordinal)
            );
            if (report.humanParticipants && report.preserveControllers)
                throw new ArgumentException(
                    "Choose either preserved controllers or two human participants."
                );
            if (report.preserveControllers)
                report.scenario = "Configured faction controllers are preserved";
            else if (report.humanParticipants)
                report.scenario = "Both ordered participants use human controllers";
            UnityEngine.Debug.Log("[NetworkSim] controller mode: " + report.scenario);
            report.outputPath = outputPath;
            await RunAsync(args, report);
            report.succeeded = true;
            exitCode = 0;
        }
        catch (Exception exception)
        {
            report.failure = exception.ToString();
            UnityEngine.Debug.LogException(exception);
        }
        finally
        {
            report.wallMilliseconds = duration.Elapsed.TotalMilliseconds;
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
            UnityEngine.Debug.Log(
                $"[NetworkSim] success={report.succeeded} ticks={report.ticksCompleted}/{report.ticksRequested} start={report.startTick} end={report.endTick} bytes={report.bytesTransferred} output={outputPath}"
            );
            if (reloadLocked)
                EditorApplication.UnlockReloadAssemblies();
            if (refreshSuppressed)
                AssetDatabase.AllowAutoRefresh();
            EditorApplication.Exit(exitCode);
        }
    }

    /// <summary>
    /// Verifies the loaded production and verification assemblies and records their exact source digest.
    /// </summary>
    /// <param name="report">Receives the build identities used by this run.</param>
    private static void VerifyLoadedBuild(MultiplayerNetworkSimulationReport report)
    {
        if (EditorApplication.isCompiling)
            throw new InvalidOperationException(
                "The Editor is still compiling the intended build."
            );
        string[] gameSources = Directory.GetFiles(
            "Assets/Scripts",
            "*.cs",
            SearchOption.AllDirectories
        );
        string[] editorSources = Directory.GetFiles(
            "Assets/Editor",
            "*.cs",
            SearchOption.AllDirectories
        );
        string gameAssembly = typeof(GameRoot).Assembly.Location;
        string editorAssembly = typeof(MultiplayerNetworkSimulationRunner).Assembly.Location;
        if (
            gameSources.Any(path =>
                File.GetLastWriteTimeUtc(path) > File.GetLastWriteTimeUtc(gameAssembly)
            )
            || editorSources.Any(path =>
                File.GetLastWriteTimeUtc(path) > File.GetLastWriteTimeUtc(editorAssembly)
            )
        )
            throw new InvalidOperationException(
                "The loaded assemblies predate the intended simulation source."
            );
        report.gameAssemblyMvid =
            typeof(GameRoot).Assembly.ManifestModule.ModuleVersionId.ToString();
        report.editorAssemblyMvid =
            typeof(MultiplayerNetworkSimulationRunner).Assembly.ManifestModule.ModuleVersionId.ToString();
        report.gameAssemblyHash = HashFile(gameAssembly);
        report.editorAssemblyHash = HashFile(editorAssembly);
        StringBuilder sources = new();
        foreach (
            string path in gameSources
                .Concat(editorSources)
                .OrderBy(path => path, StringComparer.Ordinal)
        )
            sources.Append(path).Append(':').Append(HashFile(path)).Append('\n');
        report.sourceHash = HashBytes(Encoding.UTF8.GetBytes(sources.ToString()));
    }

    /// <summary>
    /// Initializes one shared state, then independently advances two stable simulation sessions.
    /// </summary>
    /// <param name="args">The process arguments, including any isolated input save.</param>
    /// <param name="report">Receives partial and completed verification evidence.</param>
    /// <returns>A task completing after every requested day and peer receipt.</returns>
    private static async Task RunAsync(string[] args, MultiplayerNetworkSimulationReport report)
    {
        ContentPack contentPack = ContentPackLoader.OpenActive();
        GameRoot hostGame = LoadOrBuildGame(args, ref contentPack, report);
        if (!report.preserveControllers && !report.humanParticipants)
            ConfigureAIStress(hostGame);
        hostGame.UseDeterministicRuntimeIdentifiers = true;
        hostGame.SetGameSpeed(TickSpeed.Fast);
        report.startTick = hostGame.CurrentTick;
        report.endTick = hostGame.CurrentTick;
        report.seed = hostGame.Summary.Seed;
        report.galaxySize = hostGame.Summary.GalaxySize.ToString();
        report.victoryCondition = hostGame.Summary.VictoryCondition.ToString();
        string[] factions = contentPack.Scenario.PlayableFactionIDs.Take(2).ToArray();
        if (
            factions.Length != 2
            || factions.Distinct(StringComparer.Ordinal).Count() != 2
            || factions.Any(id => hostGame.GetFactionByOwnerInstanceID(id) == null)
        )
            throw new InvalidDataException(
                "The scenario must provide two distinct session factions."
            );
        report.hostFactionID = factions[0];
        report.guestFactionID = factions[1];
        if (report.humanParticipants)
        {
            hostGame.SetFactionController(
                factions[0],
                "NETWORK_VERIFICATION_HOST",
                PlayerControllerType.Human
            );
            hostGame.SetFactionController(
                factions[1],
                "NETWORK_VERIFICATION_GUEST",
                PlayerControllerType.Human
            );
        }
        report.controllers = hostGame
            .GetPlayers()
            .Select(player => new ControllerAssignment
            {
                factionID = player.FactionID,
                controller = player.ControllerType.ToString(),
            })
            .ToArray();

        using GameSession hostSession = new GameSession(hostGame, contentPack.GameData);
        using MultiplayerPeer hostPeer = new MultiplayerPeer();
        using MultiplayerPeer guestPeer = new MultiplayerPeer();
        hostPeer.StartHost(0);
        await guestPeer.ConnectAsync("127.0.0.1", hostPeer.ListeningPort);
        await WaitForEventAsync(hostPeer, MultiplayerTransportEventType.Connected);
        await WaitForEventAsync(guestPeer, MultiplayerTransportEventType.Connected);

        // Initialization is the only world snapshot transferred during this verification.
        MultiplayerSnapshot initialSnapshot = MultiplayerSnapshotCodec.Capture(hostGame);
        hostGame = MultiplayerSnapshotCodec.Restore(initialSnapshot.Payload, initialSnapshot.Hash);
        hostGame.UseDeterministicRuntimeIdentifiers = true;
        hostSession.ReplaceGame(hostGame);
        MultiplayerPacket initialPacket = new MultiplayerPacket(
            MultiplayerMessageType.SnapshotCommit,
            JsonUtility.ToJson(new SnapshotMetadata { hash = initialSnapshot.Hash }),
            initialSnapshot.Payload
        );
        await SendMeasuredAsync(hostPeer, initialPacket, report);
        MultiplayerTransportEvent initialEvent = await WaitForEventAsync(
            guestPeer,
            MultiplayerTransportEventType.PacketReceived
        );
        if (initialEvent.Packet.Type != MultiplayerMessageType.SnapshotCommit)
            throw new InvalidDataException("The guest did not receive the initial world state.");
        SnapshotMetadata initialMetadata = JsonUtility.FromJson<SnapshotMetadata>(
            initialEvent.Packet.Metadata
        );
        GameRoot restoredGuest = MultiplayerSnapshotCodec.Restore(
            initialEvent.Packet.Payload,
            initialMetadata.hash
        );
        restoredGuest.UseDeterministicRuntimeIdentifiers = true;
        using GameSession guestSession = new GameSession(restoredGuest, contentPack.GameData);
        GameRoot guestGame = guestSession.Game;
        if (
            ReferenceEquals(hostGame, guestGame)
            || ReferenceEquals(hostGame.Random, guestGame.Random)
        )
            throw new InvalidOperationException(
                "The peers must own independent graphs and random providers."
            );

        GameManager hostClock = new GameManager(() => hostSession.Game, hostSession.Tick);
        GameManager guestClock = new GameManager(() => guestSession.Game, guestSession.Tick);
        using OrderedPeerHarness peers = new OrderedPeerHarness(
            hostPeer,
            guestPeer,
            hostSession,
            guestSession,
            hostClock,
            guestClock,
            factions,
            report
        );
        OutcomeTracker outcomes = new OutcomeTracker(hostGame);
        hostSession.Pipeline.ResultsResolved += outcomes.Record;
        try
        {
            VerifyState(hostSession.Game, guestSession.Game, report);
            for (int index = 0; index < report.ticksRequested; index++)
            {
                DayTiming timing = await peers.AdvanceDayAsync();
                AssertStableRoots(hostSession, guestSession, hostGame, guestGame);
                Stopwatch verification = Stopwatch.StartNew();
                report.days.Add(timing);
                try
                {
                    VerifyState(hostSession.Game, guestSession.Game, report);
                }
                finally
                {
                    timing.verificationMilliseconds = verification.Elapsed.TotalMilliseconds;
                }
                report.ticksCompleted++;
                report.endTick = hostGame.CurrentTick;

                if (!report.peerProposalApplied && index >= report.ticksRequested / 2)
                {
                    Task<PlayerActionResult> request = peers.Guest.ExecuteAsync(
                        new SetSpeedPlayerAction { Speed = TickSpeed.Fast }
                    );
                    await peers.WaitUntilAsync(
                        () => request.IsCompleted && peers.Host.CanBeginHostTick,
                        "the guest speed request and host receipt"
                    );
                    PlayerActionResult result = await request;
                    if (!result.Accepted)
                        throw new InvalidOperationException(
                            "The guest speed request was rejected: " + result.RejectionReason
                        );
                    if (
                        hostGame.GetFactionPlayer(factions[1]).RequestedGameSpeed != TickSpeed.Fast
                        || guestGame.GetFactionPlayer(factions[1]).RequestedGameSpeed
                            != TickSpeed.Fast
                    )
                        throw new InvalidDataException(
                            "The guest preference did not propagate to both peers."
                        );
                    AssertStableRoots(hostSession, guestSession, hostGame, guestGame);
                    VerifyState(hostGame, guestGame, report);
                    report.peerProposalApplied = true;
                    report.proposalTick = hostGame.CurrentTick;
                }
            }
            if (report.initialSnapshotTransfers != 1 || report.snapshotTransfers != 1)
                throw new InvalidDataException("A settled day transferred another world snapshot.");
            report.stableGameReferences = true;
        }
        catch
        {
            report.endTick = hostGame.CurrentTick;
            if (!report.waitingForCombatResponse)
                CaptureDivergence(hostGame, guestGame, report);
            throw;
        }
        finally
        {
            hostSession.Pipeline.ResultsResolved -= outcomes.Record;
            report.factions = outcomes.Complete(hostGame);
            report.victory = outcomes.Victory;
            if (report.inputSavePath != null)
            {
                report.inputSaveHashAfter = HashFile(report.inputSavePath);
                report.inputSaveUnchanged = report.inputSaveHashBefore == report.inputSaveHashAfter;
                if (!report.inputSaveUnchanged)
                    throw new IOException("The isolated input save changed during verification.");
            }
        }
    }

    /// <summary>
    /// Loads an explicitly isolated save or builds the existing large AI stress scenario.
    /// </summary>
    /// <param name="args">The process arguments.</param>
    /// <param name="contentPack">The active pack, replaced by the save's exact installed content.</param>
    /// <param name="report">Receives input identity and integrity evidence.</param>
    /// <returns>The independent host graph to initialize.</returns>
    private static GameRoot LoadOrBuildGame(
        string[] args,
        ref ContentPack contentPack,
        MultiplayerNetworkSimulationReport report
    )
    {
        string save = ReadStringArgument(args, _inputSaveFlag, null);
        if (save != null)
        {
            string isolatedDirectory = ReadStringArgument(args, _saveDirectoryFlag, null);
            if (
                string.IsNullOrWhiteSpace(isolatedDirectory)
                || !Path.IsPathFullyQualified(isolatedDirectory)
            )
                throw new ArgumentException(
                    "-networkSimSave requires an absolute isolated -saveDirectory."
                );
            isolatedDirectory = Path.GetFullPath(isolatedDirectory);
            string file = Path.GetFullPath(
                Path.IsPathRooted(save) ? save : Path.Combine(isolatedDirectory, save)
            );
            if (!Path.HasExtension(file))
                file += ".sav";
            if (
                !string.Equals(
                    Path.GetDirectoryName(file),
                    isolatedDirectory.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar
                    ),
                    StringComparison.OrdinalIgnoreCase
                )
                || !string.Equals(
                    Path.GetExtension(file),
                    ".sav",
                    StringComparison.OrdinalIgnoreCase
                )
            )
                throw new ArgumentException(
                    "The input save must be a .sav file directly inside the isolated directory."
                );
            report.inputSavePath = file;
            report.inputSaveHashBefore = HashFile(file);
            GameRoot loaded = new SaveGameManager(isolatedDirectory).LoadGameData(
                Path.GetFileNameWithoutExtension(file)
            );
            contentPack = MultiplayerContentSelection.Open(contentPack, loaded.Summary);
            return loaded;
        }

        GameSummary summary = new GameSummary
        {
            GalaxySize = ReadEnumArgument(args, _galaxySizeFlag, GameSize.Large),
            Difficulty = GameDifficulty.Medium,
            VictoryCondition = ReadEnumArgument(args, _victoryFlag, GameVictoryCondition.Conquest),
            ResourceAvailability = GameResourceAvailability.Normal,
            StartingFactionIDs = contentPack.Scenario.PlayableFactionIDs.ToArray(),
            PlayerFactionID = contentPack.Scenario.PlayableFactionIDs.First(),
            PackID = contentPack.Definition.ID,
            PackVersion = contentPack.Definition.Version,
            ScenarioID = contentPack.Scenario.ID,
            Seed = report.seed,
        };
        return new GameBuilder(summary, contentPack.GameData).Build();
    }

    /// <summary>
    /// Reads a supported configuration value for an explicitly requested verification.
    /// </summary>
    /// <typeparam name="T">The configuration enum.</typeparam>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="flag">The requested configuration flag.</param>
    /// <param name="fallback">The existing verification default.</param>
    /// <returns>The validated configuration.</returns>
    private static T ReadEnumArgument<T>(string[] args, string flag, T fallback)
        where T : struct, Enum
    {
        string text = ReadStringArgument(args, flag, fallback.ToString());
        if (!Enum.TryParse(text, true, out T value) || !Enum.IsDefined(typeof(T), value))
            throw new ArgumentException($"Unsupported configuration for {flag}: {text}");
        return value;
    }

    /// <summary>
    /// Preserves the verification scenario's AI controllers without rebinding either graph to UI state.
    /// </summary>
    /// <param name="game">The graph being prepared before its single initial snapshot.</param>
    private static void ConfigureAIStress(GameRoot game)
    {
        foreach (Faction faction in game.GetFactions())
        {
            Player player = game.GetFactionPlayer(faction.InstanceID);
            if (player == null)
                throw new InvalidDataException("A scenario faction has no configured player.");
            game.SetFactionController(faction.InstanceID, player.PlayerID, PlayerControllerType.AI);
        }
        game.LocalPlayerFactionID = null;
    }

    /// <summary>
    /// Rejects replacement of either live graph during ordinary day and operation processing.
    /// </summary>
    /// <param name="host">The host's stable session.</param>
    /// <param name="guest">The guest's stable session.</param>
    /// <param name="originalHost">The original host graph.</param>
    /// <param name="originalGuest">The original guest graph.</param>
    private static void AssertStableRoots(
        GameSession host,
        GameSession guest,
        GameRoot originalHost,
        GameRoot originalGuest
    )
    {
        if (
            !ReferenceEquals(host.Game, originalHost) || !ReferenceEquals(guest.Game, originalGuest)
        )
            throw new InvalidOperationException(
                "A peer replaced its graph during ordered simulation."
            );
    }

    /// <summary>
    /// Compares every disk-persisted member and the full persisted-member fingerprint after settlement.
    /// </summary>
    /// <param name="host">The settled host graph.</param>
    /// <param name="guest">The settled guest graph.</param>
    /// <param name="report">Receives the latest canonical hash and any exact divergence paths.</param>
    private static void VerifyState(
        GameRoot host,
        GameRoot guest,
        MultiplayerNetworkSimulationReport report
    )
    {
        string hostXml = SerializeDiskState(host);
        string guestXml = SerializeDiskState(guest);
        string hostFingerprint = MultiplayerStateFingerprint.Compute(host);
        string guestFingerprint = MultiplayerStateFingerprint.Compute(guest);
        if (
            !string.Equals(hostXml, guestXml, StringComparison.Ordinal)
            || hostFingerprint != guestFingerprint
        )
        {
            WriteDivergence(hostXml, guestXml, report, host.CurrentTick);
            throw new InvalidDataException(
                $"Peer state differs at tick {host.CurrentTick}, path {report.divergencePath}; host={report.divergenceHostPath}; guest={report.divergenceGuestPath}."
            );
        }
        report.stateChecks++;
        report.finalStateHash = HashBytes(Encoding.UTF8.GetBytes(hostXml));
        report.finalFingerprint = hostFingerprint;
        report.finalRandomIndex = host.RandomIndex;
        report.finalIdentifierIndex = host.RuntimeIdentifierIndex;
    }

    /// <summary>
    /// Writes both current graphs after a failed barrier without hiding the original failure.
    /// </summary>
    /// <param name="host">The current host graph.</param>
    /// <param name="guest">The current guest graph.</param>
    /// <param name="report">Receives diagnostic paths or the diagnostic failure.</param>
    private static void CaptureDivergence(
        GameRoot host,
        GameRoot guest,
        MultiplayerNetworkSimulationReport report
    )
    {
        if (report.divergenceHostPath != null)
            return;
        try
        {
            WriteDivergence(
                SerializeDiskState(host),
                SerializeDiskState(guest),
                report,
                host.CurrentTick
            );
        }
        catch (Exception diagnosticFailure)
        {
            report.diagnosticFailure = diagnosticFailure.ToString();
        }
    }

    /// <summary>
    /// Serializes the complete disk schema without creating or modifying a save file.
    /// </summary>
    /// <param name="game">The settled graph to inspect.</param>
    /// <returns>The canonical disk XML.</returns>
    private static string SerializeDiskState(GameRoot game)
    {
        using StringWriter writer = new StringWriter(CultureInfo.InvariantCulture);
        new GameSerializer(typeof(GameRoot)).Serialize(writer, game);
        return writer.ToString();
    }

    /// <summary>
    /// Writes explicit diagnostic artifacts next to the requested verification report.
    /// </summary>
    /// <param name="hostXml">The full host disk schema.</param>
    /// <param name="guestXml">The full guest disk schema.</param>
    /// <param name="report">Receives artifact paths and the first differing XML path.</param>
    /// <param name="tick">The current host day.</param>
    private static void WriteDivergence(
        string hostXml,
        string guestXml,
        MultiplayerNetworkSimulationReport report,
        int tick
    )
    {
        string prefix = Path.Combine(
            Path.GetDirectoryName(report.outputPath),
            Path.GetFileNameWithoutExtension(report.outputPath) + "-tick-" + tick
        );
        Directory.CreateDirectory(Path.GetDirectoryName(prefix));
        report.divergenceHostPath = prefix + "-host.xml";
        report.divergenceGuestPath = prefix + "-guest.xml";
        File.WriteAllText(report.divergenceHostPath, hostXml, Encoding.Unicode);
        File.WriteAllText(report.divergenceGuestPath, guestXml, Encoding.Unicode);
        XmlDocument hostDocument = new XmlDocument();
        XmlDocument guestDocument = new XmlDocument();
        hostDocument.LoadXml(hostXml);
        guestDocument.LoadXml(guestXml);
        report.divergencePath = FindDifference(
            hostDocument.DocumentElement,
            guestDocument.DocumentElement,
            string.Empty
        );
    }

    /// <summary>
    /// Identifies the first differing element, attribute, or value without omitting any schema member.
    /// </summary>
    /// <param name="host">The current host node.</param>
    /// <param name="guest">The corresponding guest node.</param>
    /// <param name="parentPath">The containing XML path.</param>
    /// <returns>The first mismatch path, or null for identical nodes.</returns>
    private static string FindDifference(XmlNode host, XmlNode guest, string parentPath)
    {
        if (host == null || guest == null)
            return host == guest ? null : parentPath + "/(missing node)";
        string path = parentPath + "/" + host.Name;
        if (host.Name != guest.Name || host.Value != guest.Value)
            return path;
        if (host.Attributes?.Count != guest.Attributes?.Count)
            return path + "/@attributes";
        if (host.Attributes != null)
        {
            foreach (XmlAttribute attribute in host.Attributes)
            {
                if (guest.Attributes[attribute.Name]?.Value != attribute.Value)
                    return path + "/@" + attribute.Name;
            }
        }
        if (host.ChildNodes.Count != guest.ChildNodes.Count)
            return path + "/(child count)";
        for (int index = 0; index < host.ChildNodes.Count; index++)
        {
            string difference = FindDifference(
                host.ChildNodes[index],
                guest.ChildNodes[index],
                path + "[" + index + "]"
            );
            if (difference != null)
                return difference;
        }
        return null;
    }

    /// <summary>
    /// Sends one packet through the actual peer while measuring its complete encoded framing.
    /// </summary>
    /// <param name="peer">The sending TCP peer.</param>
    /// <param name="packet">The protocol packet to transfer.</param>
    /// <param name="report">Receives packet and encoded byte totals.</param>
    /// <returns>A task completing after the real transport write.</returns>
    private static async Task SendMeasuredAsync(
        MultiplayerPeer peer,
        MultiplayerPacket packet,
        MultiplayerNetworkSimulationReport report
    )
    {
        using MemoryStream measure = new MemoryStream();
        await MultiplayerPacketCodec.WriteAsync(measure, packet, CancellationToken.None);
        await peer.SendAsync(packet);
        report.bytesTransferred += measure.Length;
        report.packetTransfers++;
        if (packet.Type == MultiplayerMessageType.SnapshotCommit)
        {
            report.snapshotTransfers++;
            if (report.initialSnapshotTransfers == 0)
            {
                report.initialSnapshotTransfers++;
                report.initialSnapshotBytes = measure.Length;
            }
        }
        else
        {
            report.orderedBytesTransferred += measure.Length;
            report.orderedPacketTransfers++;
        }
    }

    /// <summary>
    /// Waits for an initialization transport event while rejecting disconnections.
    /// </summary>
    /// <param name="peer">The peer to poll.</param>
    /// <param name="eventType">The required event.</param>
    /// <returns>The matching event.</returns>
    private static async Task<MultiplayerTransportEvent> WaitForEventAsync(
        MultiplayerPeer peer,
        MultiplayerTransportEventType eventType
    )
    {
        Stopwatch timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            while (peer.TryDequeue(out MultiplayerTransportEvent item))
            {
                if (item.Type == eventType)
                    return item;
                if (item.Type == MultiplayerTransportEventType.Disconnected)
                    throw new IOException(item.Error ?? "The verification peer disconnected.");
                throw new InvalidDataException("An unexpected initialization event was received.");
            }
            await Task.Delay(1);
        }
        throw new TimeoutException($"Timed out waiting for {eventType}.");
    }

    /// <summary>
    /// Reads a positive integer command-line argument.
    /// </summary>
    /// <param name="args">The process arguments.</param>
    /// <param name="flag">The value flag.</param>
    /// <param name="fallback">The value used when the flag is absent.</param>
    /// <returns>The parsed positive integer.</returns>
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
    /// Reads a value following an optional command-line flag.
    /// </summary>
    /// <param name="args">The process arguments.</param>
    /// <param name="flag">The value flag.</param>
    /// <param name="fallback">The value used when the flag is absent.</param>
    /// <returns>The requested value.</returns>
    private static string ReadStringArgument(string[] args, string flag, string fallback)
    {
        for (int index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], flag, StringComparison.Ordinal))
            {
                if (index + 1 == args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
                    throw new ArgumentException($"{flag} requires a value.");
                return args[index + 1];
            }
        }
        return fallback;
    }

    /// <summary>
    /// Hashes an isolated input file without opening it for writes.
    /// </summary>
    /// <param name="path">The isolated file path.</param>
    /// <returns>The SHA-256 content hash.</returns>
    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 algorithm = SHA256.Create();
        return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty);
    }

    /// <summary>
    /// Hashes canonical verification data without changing either simulation.
    /// </summary>
    /// <param name="bytes">The canonical bytes.</param>
    /// <returns>The SHA-256 content hash.</returns>
    private static string HashBytes(byte[] bytes)
    {
        using SHA256 algorithm = SHA256.Create();
        return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", string.Empty);
    }

    /// <summary>
    /// Bridges the real TCP queues to the same ordered session protocol used by the application.
    /// </summary>
    private sealed class OrderedPeerHarness : IDisposable
    {
        private readonly MultiplayerPeer _hostPeer;
        private readonly MultiplayerPeer _guestPeer;
        private readonly GameSession _hostSession;
        private readonly GameSession _guestSession;
        private readonly MultiplayerNetworkSimulationReport _report;
        private readonly Queue<MultiplayerPacket> _hostOutgoing = new Queue<MultiplayerPacket>();
        private readonly Queue<MultiplayerPacket> _guestOutgoing = new Queue<MultiplayerPacket>();
        private readonly string _hostFactionID;
        private readonly string _guestFactionID;
        private bool _hostCombatRequired;
        private bool _guestCombatRequired;
        private CombatSubmission _hostCombatSubmission;
        private CombatSubmission _guestCombatSubmission;

        public MultiplayerGameSession Host { get; }
        public MultiplayerGameSession Guest { get; }

        /// <summary>
        /// Binds independently constructed graphs and authenticated roles to reliable ordered transport.
        /// </summary>
        /// <param name="hostPeer">The connected host TCP peer.</param>
        /// <param name="guestPeer">The connected guest TCP peer.</param>
        /// <param name="hostSession">The stable host simulation.</param>
        /// <param name="guestSession">The stable guest simulation.</param>
        /// <param name="hostClock">The host simulation clock.</param>
        /// <param name="guestClock">The guest simulation clock.</param>
        /// <param name="factions">The host and guest faction identities.</param>
        /// <param name="report">Receives transfer measurements.</param>
        public OrderedPeerHarness(
            MultiplayerPeer hostPeer,
            MultiplayerPeer guestPeer,
            GameSession hostSession,
            GameSession guestSession,
            GameManager hostClock,
            GameManager guestClock,
            string[] factions,
            MultiplayerNetworkSimulationReport report
        )
        {
            _hostPeer = hostPeer;
            _guestPeer = guestPeer;
            _hostSession = hostSession;
            _guestSession = guestSession;
            _report = report;
            _hostFactionID = factions[0];
            _guestFactionID = factions[1];
            string initializationID = Guid.NewGuid().ToString("N");
            Host = new MultiplayerGameSession(
                MultiplayerRole.Host,
                factions[0],
                factions[1],
                hostSession,
                hostClock,
                _hostOutgoing.Enqueue,
                initializationID
            );
            Guest = new MultiplayerGameSession(
                MultiplayerRole.Guest,
                factions[0],
                factions[1],
                guestSession,
                guestClock,
                _guestOutgoing.Enqueue,
                initializationID
            );
            hostSession.Tick.CombatDecisionRequired += HandleHostCombatRequired;
            guestSession.Tick.CombatDecisionRequired += HandleGuestCombatRequired;
            _hostCombatRequired = hostSession.GetService<SpaceCombatCommands>().HasPendingDecision;
            _guestCombatRequired = guestSession
                .GetService<SpaceCombatCommands>()
                .HasPendingDecision;
        }

        /// <summary>
        /// Runs one normal incremental day on each graph and awaits both completion receipts.
        /// </summary>
        /// <returns>Simulation CPU and complete barrier timings for this day.</returns>
        public async Task<DayTiming> AdvanceDayAsync()
        {
            await WaitUntilAsync(() => Host.CanBeginHostTick, "the preceding operation receipts");
            DayTiming timing = new DayTiming { tick = _hostSession.Game.CurrentTick + 1 };
            Stopwatch wall = Stopwatch.StartNew();
            if (!Host.TryBeginTick(true))
                throw new InvalidOperationException("The host refused a settled day boundary.");
            IEnumerator hostTick = _hostSession.Tick.ProcessTickIncrementally();
            IEnumerator guestTick = null;
            bool hostComplete = false;
            bool guestComplete = false;
            try
            {
                while (!hostComplete || !guestComplete || !Host.CanBeginHostTick)
                {
                    bool progressed = await PumpAsync();
                    if (guestTick == null && Guest.TryBeginTick(false))
                    {
                        guestTick = _guestSession.Tick.ProcessTickIncrementally();
                        progressed = true;
                    }
                    if (!hostComplete)
                    {
                        hostComplete = !AdvanceStep(hostTick, out double elapsed);
                        timing.hostTickMilliseconds += elapsed;
                        timing.hostStepMilliseconds.Add(elapsed);
                        progressed = true;
                    }
                    if (guestTick != null && !guestComplete)
                    {
                        guestComplete = !AdvanceStep(guestTick, out double elapsed);
                        timing.guestTickMilliseconds += elapsed;
                        timing.guestStepMilliseconds.Add(elapsed);
                        progressed = true;
                    }
                    if (wall.Elapsed > TimeSpan.FromMinutes(2))
                        throw new TimeoutException(
                            $"The day {timing.tick} barrier did not settle."
                        );
                    if (!progressed)
                        await Task.Delay(1);
                }
                if (
                    _hostSession.Game.CurrentTick != timing.tick
                    || _guestSession.Game.CurrentTick != timing.tick
                )
                    throw new InvalidDataException("A peer completed a different day.");
                timing.barrierMilliseconds = wall.Elapsed.TotalMilliseconds;
                return timing;
            }
            finally
            {
                (hostTick as IDisposable)?.Dispose();
                (guestTick as IDisposable)?.Dispose();
            }
        }

        /// <summary>
        /// Pumps until a bounded verification condition includes remote execution and acknowledgment.
        /// </summary>
        /// <param name="condition">The required settled condition.</param>
        /// <param name="description">The condition included in a timeout diagnostic.</param>
        /// <returns>A task completing when the condition becomes true.</returns>
        public async Task WaitUntilAsync(Func<bool> condition, string description)
        {
            Stopwatch timeout = Stopwatch.StartNew();
            while (!condition())
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(10))
                    throw new TimeoutException("Timed out waiting for " + description + ".");
                if (!await PumpAsync())
                    await Task.Delay(1);
            }
        }

        /// <summary>
        /// Releases ordered-session subscriptions without replacing either graph.
        /// </summary>
        public void Dispose()
        {
            _hostSession.Tick.CombatDecisionRequired -= HandleHostCombatRequired;
            _guestSession.Tick.CombatDecisionRequired -= HandleGuestCombatRequired;
            Host.Dispose();
            Guest.Dispose();
        }

        /// <summary>
        /// Records a host decision event without querying fleets or changing the simulation.
        /// </summary>
        private void HandleHostCombatRequired() => _hostCombatRequired = true;

        /// <summary>
        /// Records a guest decision event without querying fleets or changing the simulation.
        /// </summary>
        private void HandleGuestCombatRequired() => _guestCombatRequired = true;

        /// <summary>
        /// Submits the explicitly enabled fight choice through ordinary ordered player commands.
        /// A pending-combat presentation query runs once per decision event, outside simulation steps.
        /// </summary>
        /// <param name="session">The independently owned simulation.</param>
        /// <param name="ordered">The participant's command owner.</param>
        /// <param name="factionID">The participant's authenticated faction.</param>
        /// <param name="required">Whether a decision event remains unhandled.</param>
        /// <param name="submission">The outstanding command and its report entry.</param>
        /// <returns>True when a new command was submitted.</returns>
        private bool RespondToHumanCombat(
            GameSession session,
            MultiplayerGameSession ordered,
            string factionID,
            ref bool required,
            ref CombatSubmission submission
        )
        {
            if (submission != null)
            {
                if (!submission.Completion.IsCompleted)
                    return false;
                PlayerActionResult result = submission.Completion.GetAwaiter().GetResult();
                submission.Sample.accepted = result.Accepted;
                submission.Sample.rejectionReason = result.RejectionReason;
                submission = null;
                if (!result.Accepted)
                    throw new InvalidOperationException(
                        "The scripted combat response was rejected: " + result.RejectionReason
                    );
            }
            if (!required || !session.CanExecuteCommands)
                return false;
            required = false;
            if (
                session.Game.GetFactionPlayer(factionID)?.ControllerType
                    != PlayerControllerType.Human
                || !session
                    .GetService<SpaceCombatCommands>()
                    .TryGetPendingCombat(out PendingCombatResult pending)
            )
                return false;
            bool attacker = pending.AttackerOwnerInstanceID == factionID;
            bool defender = pending.DefenderOwnerInstanceID == factionID;
            if (
                !attacker && !defender
                || (attacker ? pending.AttackerHasResponded : pending.DefenderHasResponded)
            )
                return false;
            CombatResponseSample sample = new()
            {
                tick = pending.Tick,
                planetID = pending.PlanetInstanceID,
                factionID = factionID,
            };
            _report.combatResponses.Add(sample);
            submission = new CombatSubmission
            {
                Sample = sample,
                Completion = ordered.ExecuteAsync(
                    new ResolveCombatPlayerAction
                    {
                        PlanetID = pending.PlanetInstanceID,
                        Tick = pending.Tick,
                        Retreat = false,
                    }
                ),
            };
            return true;
        }

        /// <summary>
        /// Transfers complete packets before executing queued model operations at writable boundaries.
        /// </summary>
        /// <returns>Whether any network packet was transferred or received.</returns>
        private async Task<bool> PumpAsync()
        {
            bool progressed = await FlushAsync(_hostPeer, _hostOutgoing);
            progressed |= await FlushAsync(_guestPeer, _guestOutgoing);
            progressed |= Drain(_hostPeer, Host);
            progressed |= Drain(_guestPeer, Guest);
            Host.Pump();
            Guest.Pump();
            if (Host.Failure != null)
                throw new InvalidOperationException(
                    "The host stopped ordered replay.",
                    Host.Failure
                );
            if (Guest.Failure != null)
                throw new InvalidOperationException(
                    "The guest stopped ordered replay.",
                    Guest.Failure
                );
            if (_report.fightHumanBattles)
            {
                progressed |= RespondToHumanCombat(
                    _hostSession,
                    Host,
                    _hostFactionID,
                    ref _hostCombatRequired,
                    ref _hostCombatSubmission
                );
                progressed |= RespondToHumanCombat(
                    _guestSession,
                    Guest,
                    _guestFactionID,
                    ref _guestCombatRequired,
                    ref _guestCombatSubmission
                );
            }
            else if (_hostCombatRequired || _guestCombatRequired)
            {
                _report.waitingForCombatResponse = true;
                throw new InvalidOperationException(
                    "Verification reached a human battle decision. Supply -networkSimFightHumanBattles to explicitly script Fight responses."
                );
            }
            progressed |= await FlushAsync(_hostPeer, _hostOutgoing);
            progressed |= await FlushAsync(_guestPeer, _guestOutgoing);
            return progressed;
        }

        /// <summary>
        /// Writes queued synchronous protocol output through asynchronous TCP sends.
        /// </summary>
        /// <param name="peer">The sending TCP peer.</param>
        /// <param name="packets">The ordered outbound queue.</param>
        /// <returns>Whether a packet was transferred.</returns>
        private async Task<bool> FlushAsync(MultiplayerPeer peer, Queue<MultiplayerPacket> packets)
        {
            bool sent = false;
            while (packets.Count > 0)
            {
                await SendMeasuredAsync(peer, packets.Dequeue(), _report);
                sent = true;
            }
            return sent;
        }

        /// <summary>
        /// Queues received transport packets in the corresponding ordered model session.
        /// </summary>
        /// <param name="peer">The receiving TCP peer.</param>
        /// <param name="session">The peer's ordered session.</param>
        /// <returns>Whether a packet was received.</returns>
        private static bool Drain(MultiplayerPeer peer, MultiplayerGameSession session)
        {
            bool received = false;
            while (peer.TryDequeue(out MultiplayerTransportEvent item))
            {
                if (item.Type == MultiplayerTransportEventType.Disconnected)
                    throw new IOException(item.Error ?? "The verification peer disconnected.");
                if (item.Type != MultiplayerTransportEventType.PacketReceived)
                    throw new InvalidDataException(
                        "A connected peer received an unexpected event."
                    );
                session.Receive(item.Packet);
                received = true;
            }
            return received;
        }

        /// <summary>
        /// Measures only normal simulation work, excluding network waits and full-state diagnostics.
        /// </summary>
        /// <param name="tick">The active incremental tick.</param>
        /// <param name="milliseconds">Receives the elapsed simulation step time.</param>
        /// <returns>Whether another incremental step remains.</returns>
        private static bool AdvanceStep(IEnumerator tick, out double milliseconds)
        {
            long start = Stopwatch.GetTimestamp();
            bool more = tick.MoveNext();
            milliseconds = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            return more;
        }
    }

    /// <summary>
    /// Records lifecycle outcomes from resolved results, with graph enumeration only at initialization and completion.
    /// </summary>
    private sealed class OutcomeTracker
    {
        private readonly Dictionary<string, FactionOutcome> _factions = new Dictionary<
            string,
            FactionOutcome
        >(StringComparer.Ordinal);
        private readonly HashSet<string> _seenManufactured = new HashSet<string>(
            StringComparer.Ordinal
        );
        public VictoryOutcome Victory { get; private set; }

        /// <summary>
        /// Records starting ownership and completed units once before the first verified day.
        /// </summary>
        /// <param name="game">The initialized host graph.</param>
        public OutcomeTracker(GameRoot game)
        {
            foreach (Faction faction in game.GetFactions())
            {
                _factions.Add(
                    faction.InstanceID,
                    new FactionOutcome
                    {
                        factionID = faction.InstanceID,
                        displayName = faction.DisplayName,
                        initialPlanets = faction.GetOwnedUnitsByType<Planet>().Count,
                    }
                );
            }
            foreach (IManufacturable item in game.GetRegisteredSceneNodesByType<IManufacturable>())
            {
                if (item.ManufacturingStatus == ManufacturingStatus.Complete)
                    _seenManufactured.Add(item.GetInstanceID());
            }
        }

        /// <summary>
        /// Counts resolved lifecycle events without polling the world or evaluating AI candidates.
        /// </summary>
        /// <param name="results">The existing resolved result batch.</param>
        public void Record(IReadOnlyList<GameResult> results)
        {
            foreach (GameResult result in results)
            {
                switch (result)
                {
                    case PlanetOwnershipChangedResult ownership:
                        if (
                            TryGet(ownership.PreviousOwner?.InstanceID, out FactionOutcome previous)
                        )
                            previous.planetsLost++;
                        if (TryGet(ownership.NewOwner?.InstanceID, out FactionOutcome acquired))
                            acquired.planetsAcquired++;
                        break;
                    case PlanetaryAssaultResult assault:
                        if (TryGet(assault.AttackerOwnerInstanceID, out FactionOutcome attacker))
                        {
                            attacker.assaults++;
                            if (assault.Success)
                                attacker.successfulAssaults++;
                        }
                        break;
                    case OfficerCaptureStateResult capture when capture.IsCaptured:
                        if (
                            TryGet(
                                capture.TargetOfficer?.OwnerInstanceID,
                                out FactionOutcome captured
                            )
                        )
                            captured.personnelCaptured++;
                        break;
                    case OfficerKilledResult killed:
                        if (TryGet(killed.TargetOfficer?.OwnerInstanceID, out FactionOutcome dead))
                            dead.personnelDeaths++;
                        break;
                    case GameObjectDeployedResult deployed:
                        RecordManufactured(deployed.GameObject as IManufacturable);
                        break;
                    case VictoryResult victory:
                        Victory = new VictoryOutcome
                        {
                            tick = victory.Tick,
                            winnerFactionID = victory.Winner?.InstanceID,
                            loserFactionID = victory.Loser?.InstanceID,
                            mode = victory.GameMode?.ToString(),
                        };
                        break;
                }
            }
        }

        /// <summary>
        /// Captures final ownership through faction indexes once after verification finishes.
        /// </summary>
        /// <param name="game">The final host graph.</param>
        /// <returns>Initial, lifecycle, and final totals for every faction.</returns>
        public FactionOutcome[] Complete(GameRoot game)
        {
            foreach (Faction faction in game.GetFactions())
            {
                FactionOutcome outcome = _factions[faction.InstanceID];
                outcome.ownedPlanetIDs = faction
                    .GetOwnedUnitsByType<Planet>()
                    .Select(item => item.InstanceID)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();
                outcome.planets = outcome.ownedPlanetIDs.Length;
                outcome.fleets = faction.GetOwnedUnitsByType<Fleet>().Count;
                outcome.capitalShips = faction.GetOwnedUnitsByType<CapitalShip>().Count;
                outcome.starfighters = faction.GetOwnedUnitsByType<Starfighter>().Count;
                outcome.regiments = faction.GetOwnedUnitsByType<Regiment>().Count;
                outcome.specialForces = faction.GetOwnedUnitsByType<SpecialForces>().Count;
                outcome.officers = faction.GetOwnedUnitsByType<Officer>().Count;
                outcome.buildings = faction.GetOwnedUnitsByType<Building>().Count;
            }
            return _factions
                .Values.OrderBy(item => item.factionID, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Looks up an existing faction result counter without querying scene state.
        /// </summary>
        /// <param name="factionID">The event's faction identity.</param>
        /// <param name="outcome">Receives the configured faction's counters.</param>
        /// <returns>Whether the event belongs to a configured faction.</returns>
        private bool TryGet(string factionID, out FactionOutcome outcome)
        {
            outcome = null;
            return factionID != null && _factions.TryGetValue(factionID, out outcome);
        }

        /// <summary>
        /// Counts a completed manufactured unit once, ignoring initial units and repeated deployments.
        /// </summary>
        /// <param name="item">The result's deployed manufacturable, when present.</param>
        private void RecordManufactured(IManufacturable item)
        {
            if (
                item?.ManufacturingStatus != ManufacturingStatus.Complete
                || string.IsNullOrEmpty(item.ProducerPlanetID)
                || !TryGet(item.GetOwnerInstanceID(), out FactionOutcome outcome)
                || !_seenManufactured.Add(item.GetInstanceID())
            )
                return;
            switch (item)
            {
                case CapitalShip:
                    outcome.manufacturedCapitalShips++;
                    break;
                case Starfighter:
                    outcome.manufacturedStarfighters++;
                    break;
                case Regiment:
                    outcome.manufacturedRegiments++;
                    break;
                case SpecialForces:
                    outcome.manufacturedSpecialForces++;
                    break;
                case Building:
                    outcome.manufacturedBuildings++;
                    break;
            }
        }
    }

    /// <summary>
    /// Carries validation for the single initialization snapshot.
    /// </summary>
    [Serializable]
    private sealed class SnapshotMetadata
    {
        public string hash;
    }

    /// <summary>
    /// Separates simulation step execution, full-state verification, and the complete network barrier.
    /// </summary>
    [Serializable]
    private sealed class DayTiming
    {
        public int tick;
        public double hostTickMilliseconds;
        public double guestTickMilliseconds;
        public List<double> hostStepMilliseconds = new List<double>();
        public List<double> guestStepMilliseconds = new List<double>();
        public double barrierMilliseconds;
        public double verificationMilliseconds;
    }

    /// <summary>
    /// Records a declared victory observed through the existing result pipeline.
    /// </summary>
    [Serializable]
    private sealed class VictoryOutcome
    {
        public int tick;
        public string winnerFactionID;
        public string loserFactionID;
        public string mode;
    }

    /// <summary>
    /// Records faction ownership and event-based gameplay outcomes during the verified interval.
    /// </summary>
    [Serializable]
    private sealed class FactionOutcome
    {
        public string factionID;
        public string displayName;
        public int initialPlanets;
        public int planets;
        public string[] ownedPlanetIDs;
        public int planetsAcquired;
        public int planetsLost;
        public int assaults;
        public int successfulAssaults;
        public int fleets;
        public int capitalShips;
        public int starfighters;
        public int regiments;
        public int specialForces;
        public int officers;
        public int buildings;
        public int manufacturedCapitalShips;
        public int manufacturedStarfighters;
        public int manufacturedRegiments;
        public int manufacturedSpecialForces;
        public int manufacturedBuildings;
        public int personnelCaptured;
        public int personnelDeaths;
    }

    /// <summary>
    /// Retains one outstanding scripted response until its ordered completion is observed.
    /// </summary>
    private sealed class CombatSubmission
    {
        public Task<PlayerActionResult> Completion;
        public CombatResponseSample Sample;
    }

    /// <summary>
    /// Records one explicitly requested fight response without changing controller assignments.
    /// </summary>
    [Serializable]
    private sealed class CombatResponseSample
    {
        public int tick;
        public string planetID;
        public string factionID;
        public bool accepted;
        public string rejectionReason;
    }

    /// <summary>
    /// Records the exact controller configuration entering the independent peer simulations.
    /// </summary>
    [Serializable]
    private sealed class ControllerAssignment
    {
        public string factionID;
        public string controller;
    }

    /// <summary>
    /// Records complete and partial evidence for an explicitly requested ordered-protocol verification.
    /// </summary>
    [Serializable]
    private sealed class MultiplayerNetworkSimulationReport
    {
        public string transport = "MultiplayerPeer TCP loopback with MultiplayerPacketCodec";
        public string scenario =
            "All configured faction controllers use the existing AI stress workload";
        public bool preserveControllers;
        public bool fightHumanBattles;
        public bool humanParticipants;
        public bool waitingForCombatResponse;
        public ControllerAssignment[] controllers;
        public List<CombatResponseSample> combatResponses = new();
        public bool succeeded;
        public string outputPath;
        public string failure;
        public string diagnosticFailure;
        public string gameAssemblyMvid;
        public string editorAssemblyMvid;
        public string gameAssemblyHash;
        public string editorAssemblyHash;
        public string sourceHash;
        public int ticksRequested;
        public int ticksCompleted;
        public int startTick;
        public int endTick;
        public int seed;
        public string galaxySize;
        public string victoryCondition;
        public string hostFactionID;
        public string guestFactionID;
        public double wallMilliseconds;
        public long bytesTransferred;
        public int packetTransfers;
        public long orderedBytesTransferred;
        public int orderedPacketTransfers;
        public int snapshotTransfers;
        public int initialSnapshotTransfers;
        public long initialSnapshotBytes;
        public int stateChecks;
        public bool peerProposalApplied;
        public int proposalTick;
        public bool stableGameReferences;
        public string finalStateHash;
        public string finalFingerprint;
        public long finalRandomIndex;
        public long finalIdentifierIndex;
        public string inputSavePath;
        public string inputSaveHashBefore;
        public string inputSaveHashAfter;
        public bool inputSaveUnchanged;
        public string divergenceHostPath;
        public string divergenceGuestPath;
        public string divergencePath;
        public List<DayTiming> days = new List<DayTiming>();
        public FactionOutcome[] factions;
        public VictoryOutcome victory;
    }
}
