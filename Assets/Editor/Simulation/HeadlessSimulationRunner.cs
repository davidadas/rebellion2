using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Generation;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

public static partial class HeadlessSimulationRunner
{
    private const string _tickCountFlag = "-simTicks";
    private const string _outputPathFlag = "-simOut";
    private const string _seedFlag = "-simSeed";
    private const string _difficultyFlag = "-simDifficulty";
    private const string _logDirectory = "/tmp/rebellion2-sim-logs";
    private const string _defaultSimulationSaveFileName = "headless-simulation";
    private const string _savedSimulationPlayerId = "PLAYER1";
    private const int _percentScale = 100;

    /// <summary>
    /// Runs the command-line simulation entry point.
    /// </summary>
    public static void RunDefaultSimulation()
    {
        try
        {
            SimulationOptions options = SimulationOptions.Parse(Environment.GetCommandLineArgs());
            RunSimulation(options);
            UnityEditor.EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogException(ex);
            UnityEditor.EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// Runs a simulation from an already-open editor session.
    /// </summary>
    /// <param name="tickCount">The number of ticks to simulate.</param>
    /// <param name="outputPath">The summary output path.</param>
    /// <param name="seed">The optional generation seed.</param>
    /// <param name="saveFileName">The optional save-file name override.</param>
    /// <param name="saveDisplayName">The optional save display-name override.</param>
    /// <param name="playerFactionId">The optional player-faction override for the saved game.</param>
    /// <returns>The completed simulation result.</returns>
    public static SimulationRunResult RunPersistentSimulation(
        int tickCount,
        string outputPath,
        int? seed,
        string saveFileName = null,
        string saveDisplayName = null,
        string playerFactionId = null
    )
    {
        return RunSimulation(
            new SimulationOptions
            {
                TickCount = tickCount,
                OutputPath = outputPath,
                Seed = seed,
                SaveFileName = saveFileName,
                SaveDisplayName = saveDisplayName,
                PlayerFactionId = playerFactionId,
            }
        );
    }

    /// <summary>
    /// Runs a simulation with the specified options.
    /// </summary>
    /// <param name="options">The simulation options.</param>
    /// <returns>The completed simulation result.</returns>
    private static SimulationRunResult RunSimulation(SimulationOptions options)
    {
        string logPath = GetLogPath(options.OutputPath);
        GameLogger.Configure(logPath, enableFileLogging: true);
        GameLogger.SetMinimumLevel(GameLogger.LogLevel.Warning);
        BaseGameEntity.SetInstanceIdSeed(options.Seed);

        try
        {
            ContentPack contentPack = ContentPackLoader.OpenActive();
            GameSummary summary = new GameSummary
            {
                GalaxySize = GameSize.Large,
                Difficulty = options.Difficulty,
                VictoryCondition = GameVictoryCondition.Conquest,
                ResourceAvailability = GameResourceAvailability.Normal,
                StartingResearchLevel = 1,
                StartingFactionIDs = contentPack.Scenario.PlayableFactionIDs.ToArray(),
                PlayerFactionID = contentPack.Scenario.PlayableFactionIDs.FirstOrDefault(),
                PackID = contentPack.Definition.ID,
                PackVersion = contentPack.Definition.Version,
                ScenarioID = contentPack.Scenario.ID,
            };
            if (options.Seed.HasValue)
                summary.Seed = options.Seed.Value;

            string startMessage =
                $"[HeadlessSim] starting ticks={options.TickCount} seed={options.Seed?.ToString() ?? "random"} galaxySize={summary.GalaxySize}";
            UnityEngine.Debug.Log(startMessage);
            LogToFile(logPath, startMessage);

            GameRoot game = CreateGameBuilder(summary, contentPack.GameData, options.Seed)
                .BuildGame();
            foreach (Faction faction in game.GetFactions())
                faction.PlayerID = null;

            GameManager manager = new GameManager(game, contentPack.GameData);
            ManufacturingIdleTracker idleTracker = new ManufacturingIdleTracker();
            ManufacturedUnitTracker manufacturedUnitTracker = new ManufacturedUnitTracker();
            FleetHistoryTracker fleetHistoryTracker = new FleetHistoryTracker();
            ActivityTracker activityTracker = new ActivityTracker();
            MissionOutcomeTracker missionOutcomeTracker = new MissionOutcomeTracker();
            PersonnelOutcomeTracker personnelOutcomeTracker = new PersonnelOutcomeTracker();
            SpecialForcesLifecycleTracker specialForcesLifecycleTracker =
                new SpecialForcesLifecycleTracker();
            PlanetaryAssaultTracker planetaryAssaultTracker = new PlanetaryAssaultTracker(game);
            GarrisonRemovalBombardmentTracker garrisonRemovalBombardmentTracker =
                new GarrisonRemovalBombardmentTracker(game);
            AttackReadinessTracker attackReadinessTracker = new AttackReadinessTracker();
            VictoryResult victory = null;
            manager.ResultsResolved += planetaryAssaultTracker.Record;
            manager.ResultsResolved += garrisonRemovalBombardmentTracker.Record;
            manager.VictoriesResolved += results => victory ??= results.FirstOrDefault();
            manager.ResultsResolved += missionOutcomeTracker.Record;
            manager.ResultsResolved += manufacturedUnitTracker.Record;
            manager.ResultsResolved += specialForcesLifecycleTracker.Record;
            List<SpecialForces> initialSpecialForces = game.GetSceneNodesByType<SpecialForces>()
                .ToList();
            manufacturedUnitTracker.RecordInitialState(game, initialSpecialForces);
            fleetHistoryTracker.RecordTick(game);
            activityTracker.RecordInitialState(game);
            personnelOutcomeTracker.RecordInitialState(game, activityTracker.AbductionTargetIds);
            specialForcesLifecycleTracker.RecordInitialState(game, initialSpecialForces);
            long gameProcessingTimestampCount = 0;
            long idleTimestampCount = 0;
            long fleetHistoryTimestampCount = 0;
            long activityTimestampCount = 0;
            long personnelTimestampCount = 0;
            long specialForcesTimestampCount = 0;
            long attackReadinessTimestampCount = 0;
            List<long> gameProcessingSamples = new List<long>(options.TickCount);
            List<long> gameProcessingStepSamples = new List<long>(options.TickCount * 8);

            for (int i = 0; i < options.TickCount && victory == null; i++)
            {
                if (i % 25 == 0)
                    LogToFile(logPath, $"[HeadlessSim] tick {i}");
                long startTimestamp = Stopwatch.GetTimestamp();
                ProcessTickIncrementally(manager, gameProcessingStepSamples);
                long gameProcessingElapsed = Stopwatch.GetTimestamp() - startTimestamp;
                gameProcessingTimestampCount += gameProcessingElapsed;
                gameProcessingSamples.Add(gameProcessingElapsed);
                if (game.CurrentTick % ManufacturingIdleTracker.SampleInterval == 0)
                {
                    startTimestamp = Stopwatch.GetTimestamp();
                    idleTracker.RecordSample(game);
                    idleTimestampCount += Stopwatch.GetTimestamp() - startTimestamp;
                }
                startTimestamp = Stopwatch.GetTimestamp();
                fleetHistoryTracker.RecordTick(game);
                fleetHistoryTimestampCount += Stopwatch.GetTimestamp() - startTimestamp;
                startTimestamp = Stopwatch.GetTimestamp();
                activityTracker.RecordTick(game);
                activityTimestampCount += Stopwatch.GetTimestamp() - startTimestamp;
                startTimestamp = Stopwatch.GetTimestamp();
                personnelOutcomeTracker.RecordTick(game, activityTracker.AbductionTargetIds);
                personnelTimestampCount += Stopwatch.GetTimestamp() - startTimestamp;
                if (game.CurrentTick % SpecialForcesLifecycleTracker.SampleInterval == 0)
                {
                    startTimestamp = Stopwatch.GetTimestamp();
                    specialForcesLifecycleTracker.RecordSample(game);
                    specialForcesTimestampCount += Stopwatch.GetTimestamp() - startTimestamp;
                }
                startTimestamp = Stopwatch.GetTimestamp();
                attackReadinessTracker.RecordTick(game);
                attackReadinessTimestampCount += Stopwatch.GetTimestamp() - startTimestamp;
            }

            specialForcesLifecycleTracker.RecordFinalState(game);
            LogToFile(
                logPath,
                $"[HeadlessSim] timing game={GetElapsedSeconds(gameProcessingTimestampCount):F3}s idle={GetElapsedSeconds(idleTimestampCount):F3}s fleets={GetElapsedSeconds(fleetHistoryTimestampCount):F3}s activity={GetElapsedSeconds(activityTimestampCount):F3}s personnel={GetElapsedSeconds(personnelTimestampCount):F3}s specialForces={GetElapsedSeconds(specialForcesTimestampCount):F3}s readiness={GetElapsedSeconds(attackReadinessTimestampCount):F3}s"
            );
            LogToFile(
                logPath,
                $"[HeadlessSim] game-tick median={GetPercentileMilliseconds(gameProcessingSamples, 50):F3}ms p90={GetPercentileMilliseconds(gameProcessingSamples, 90):F3}ms p99={GetPercentileMilliseconds(gameProcessingSamples, 99):F3}ms max={GetPercentileMilliseconds(gameProcessingSamples, 100):F3}ms"
            );
            LogToFile(
                logPath,
                $"[HeadlessSim] game-step median={GetPercentileMilliseconds(gameProcessingStepSamples, 50):F3}ms p90={GetPercentileMilliseconds(gameProcessingStepSamples, 90):F3}ms p99={GetPercentileMilliseconds(gameProcessingStepSamples, 99):F3}ms max={GetPercentileMilliseconds(gameProcessingStepSamples, 100):F3}ms"
            );
            string savePath = SaveSimulation(game, options);
            SimulationSummary report = BuildSimulationSummary(
                game,
                summary,
                options,
                idleTracker,
                manufacturedUnitTracker,
                fleetHistoryTracker,
                activityTracker,
                missionOutcomeTracker,
                personnelOutcomeTracker,
                specialForcesLifecycleTracker,
                planetaryAssaultTracker,
                garrisonRemovalBombardmentTracker,
                attackReadinessTracker,
                victory
            );
            string resolvedPath = WriteSimulationSummary(options.OutputPath, report);
            string completeMessage =
                $"[HeadlessSim] complete ticks={report.TicksCompleted} output={resolvedPath}";
            UnityEngine.Debug.Log(completeMessage);
            LogToFile(logPath, completeMessage);

            return new SimulationRunResult
            {
                TicksCompleted = report.TicksCompleted,
                OutputPath = resolvedPath,
                Seed = options.Seed ?? -1,
                SavePath = savePath,
            };
        }
        finally
        {
            BaseGameEntity.SetInstanceIdSeed(null);
            GameLogger.SetMinimumLevel(GameLogger.LogLevel.Debug);
            GameLogger.Configure(enableFileLogging: false);
        }
    }

    /// <summary>
    /// Saves and reloads a completed simulation.
    /// </summary>
    /// <param name="game">The completed simulated game.</param>
    /// <param name="options">The simulation options containing save configuration.</param>
    /// <returns>The validated save-file path.</returns>
    private static string SaveSimulation(GameRoot game, SimulationOptions options)
    {
        string saveFileName = ResolveSaveFileName(options);
        string saveDisplayName = string.IsNullOrWhiteSpace(options.SaveDisplayName)
            ? $"Simulation {options.Seed?.ToString() ?? "random"} - Tick {game.CurrentTick}"
            : options.SaveDisplayName;
        string playerFactionId = string.IsNullOrWhiteSpace(options.PlayerFactionId)
            ? game.Summary.PlayerFactionID
            : options.PlayerFactionId;

        Faction playerFaction = game.GetFactions()
            .FirstOrDefault(faction => faction.InstanceID == playerFactionId);
        if (playerFaction == null)
        {
            throw new InvalidOperationException(
                $"Cannot save simulation with unknown player faction '{playerFactionId}'."
            );
        }

        game.Summary.PlayerFactionID = playerFaction.InstanceID;
        foreach (Faction faction in game.GetFactions())
        {
            faction.PlayerID =
                faction.InstanceID == playerFaction.InstanceID ? _savedSimulationPlayerId : null;
        }

        SaveGameManager saveManager = SaveGameManager.Instance;
        saveManager.SaveGameData(game, saveFileName, saveDisplayName);
        GameRoot loadedGame = saveManager.LoadGameData(saveFileName);
        if (
            loadedGame.CurrentTick != game.CurrentTick
            || loadedGame.Summary?.PlayerFactionID != playerFaction.InstanceID
        )
        {
            throw new InvalidOperationException(
                $"Saved simulation validation failed for '{saveFileName}'."
            );
        }

        return saveManager.GetSaveFilePath(saveFileName);
    }

    /// <summary>
    /// Returns the explicit save name or the shared simulation save slot.
    /// </summary>
    /// <param name="options">The simulation options.</param>
    /// <returns>The save-file name without its extension.</returns>
    private static string ResolveSaveFileName(SimulationOptions options)
    {
        return string.IsNullOrWhiteSpace(options.SaveFileName)
            ? _defaultSimulationSaveFileName
            : options.SaveFileName;
    }

    /// <summary>
    /// Creates a game builder for the requested scenario.
    /// </summary>
    /// <param name="summary">The game summary used for generation.</param>
    /// <param name="gameData">The active pack's composed game data.</param>
    /// <param name="seed">The optional generation seed.</param>
    /// <returns>The configured game builder.</returns>
    private static GameBuilder CreateGameBuilder(
        GameSummary summary,
        GameDataCatalog gameData,
        int? seed
    )
    {
        return seed.HasValue
            ? new GameBuilder(summary, gameData, new SystemRandomProvider(seed.Value))
            : new GameBuilder(summary, gameData);
    }

    /// <summary>
    /// Returns the log path for a simulation output file.
    /// </summary>
    /// <param name="outputPath">The simulation output path.</param>
    /// <returns>The log file path.</returns>
    private static string GetLogPath(string outputPath)
    {
        string resolvedOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(_logDirectory);
        return Path.Combine(
            _logDirectory,
            $"{Path.GetFileNameWithoutExtension(resolvedOutputPath)}.log"
        );
    }

    /// <summary>
    /// Appends a message to the simulation log file.
    /// </summary>
    /// <param name="logPath">The log file path.</param>
    /// <param name="message">The message to append.</param>
    private static void LogToFile(string logPath, string message)
    {
        File.AppendAllText(logPath, message + Environment.NewLine);
    }

    /// <summary>
    /// Converts high-resolution timestamp counts to elapsed seconds.
    /// </summary>
    /// <param name="timestampCount">The accumulated timestamp count.</param>
    /// <returns>The corresponding elapsed seconds.</returns>
    private static double GetElapsedSeconds(long timestampCount) =>
        timestampCount / (double)Stopwatch.Frequency;

    /// <summary>
    /// Drains one incremental game tick while recording each scheduled step.
    /// </summary>
    /// <param name="manager">The game manager processing the tick.</param>
    /// <param name="stepSamples">The collection receiving step durations.</param>
    private static void ProcessTickIncrementally(GameManager manager, ICollection<long> stepSamples)
    {
        IEnumerator tick = manager.ProcessTickIncrementally();
        try
        {
            bool hasNext;
            do
            {
                long startTimestamp = Stopwatch.GetTimestamp();
                hasNext = tick.MoveNext();
                stepSamples.Add(Stopwatch.GetTimestamp() - startTimestamp);
            } while (hasNext);
        }
        finally
        {
            (tick as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Returns the nearest-rank percentile in milliseconds for timestamp samples.
    /// </summary>
    /// <param name="samples">Elapsed timestamp counts to evaluate.</param>
    /// <param name="percentile">Percentile from zero through one hundred.</param>
    /// <returns>The requested percentile in milliseconds, or zero when no samples exist.</returns>
    private static double GetPercentileMilliseconds(
        IReadOnlyCollection<long> samples,
        int percentile
    )
    {
        if (samples == null || samples.Count == 0)
            return 0;

        long[] ordered = samples.OrderBy(sample => sample).ToArray();
        int rank = (int)Math.Ceiling(percentile / 100d * ordered.Length);
        int index = Math.Clamp(rank - 1, 0, ordered.Length - 1);
        return ordered[index] * 1000d / Stopwatch.Frequency;
    }
}
