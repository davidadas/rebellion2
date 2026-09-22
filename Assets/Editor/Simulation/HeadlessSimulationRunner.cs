using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Generation;
using Rebellion.SceneGraph;
using Rebellion.Util.Logging;
using Rebellion.Util.Random;

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
    /// <param name="difficulty">The game difficulty applied to the simulation.</param>
    /// <param name="inputSaveFileName">The optional save file to continue.</param>
    /// <returns>The completed simulation result.</returns>
    public static SimulationRunResult RunPersistentSimulation(
        int tickCount,
        string outputPath,
        int? seed,
        string saveFileName = null,
        string saveDisplayName = null,
        string playerFactionId = null,
        GameDifficulty difficulty = GameDifficulty.Medium,
        string inputSaveFileName = null
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
                Difficulty = difficulty,
                InputSaveFileName = inputSaveFileName,
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
        BaseGameEntity.SetInstanceIdSeed(
            string.IsNullOrWhiteSpace(options.InputSaveFileName) ? options.Seed : null
        );
        AIMissionPlanner.CaptureDiagnostics = true;
        Faction.ResetProjectedMaintenanceDiagnostics();
        Faction.CaptureProjectedMaintenanceDiagnostics = true;

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

            GameRoot game = string.IsNullOrWhiteSpace(options.InputSaveFileName)
                ? CreateGameBuilder(summary, contentPack.GameData, options.Seed).BuildGame()
                : SaveGameManager.Instance.LoadGameData(options.InputSaveFileName);
            summary = game.Summary;
            foreach (Faction faction in game.GetFactions())
            {
                Player player = game.GetFactionPlayer(faction.InstanceID);
                game.SetFactionController(
                    faction.InstanceID,
                    player.PlayerID,
                    PlayerControllerType.AI
                );
            }

            GameManager manager = new GameManager(game, contentPack.GameData);
            if (!string.IsNullOrWhiteSpace(options.InputSaveFileName))
                manager.ReconcileLoadedState();
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
            SpaceCombatCalibrationTracker spaceCombatCalibrationTracker =
                new SpaceCombatCalibrationTracker();
            AttackReadinessTracker attackReadinessTracker = new AttackReadinessTracker();
            Dictionary<string, long> aiFactionTurnStarts = new Dictionary<string, long>(
                StringComparer.Ordinal
            );
            List<long> aiFactionTurnSamples = new List<long>(
                options.TickCount
                    / Math.Max(1, game.Config.AI.TickInterval)
                    * Math.Max(1, game.GetFactions().Count)
            );
            List<(long Elapsed, string FactionId, int Tick)> slowAiFactionTurns = new();
            Dictionary<(string FactionId, string StepName), long> aiFactionStepStarts = new();
            Dictionary<string, List<long>> aiFactionStepSamples = new(StringComparer.Ordinal);
            Dictionary<string, List<long>> aiWorkUnitSamples = new(StringComparer.Ordinal);
            List<(
                long Elapsed,
                int Tick,
                int Candidates,
                int ExactScores,
                string Breakdown
            )> slowMissionPlans = new();
            List<(long Elapsed, int Tick, int Count, string ProductTypeId)> manufactureExecutions =
                new();
            VictoryResult victory = null;
            manager.ResultsResolved += planetaryAssaultTracker.Record;
            manager.ResultsResolved += garrisonRemovalBombardmentTracker.Record;
            manager.ResultsResolved += spaceCombatCalibrationTracker.Record;
            manager.VictoriesResolved += results => victory ??= results.FirstOrDefault();
            manager.ResultsResolved += missionOutcomeTracker.Record;
            manager.ResultsResolved += results => manufacturedUnitTracker.Record(game, results);
            manager.ResultsResolved += specialForcesLifecycleTracker.Record;
            manager.AIFactionTurnStarted += faction =>
                aiFactionTurnStarts[faction.InstanceID] = Stopwatch.GetTimestamp();
            manager.AIFactionTurnCompleted += faction =>
            {
                if (!aiFactionTurnStarts.Remove(faction.InstanceID, out long startedAt))
                    return;

                long elapsed = Stopwatch.GetTimestamp() - startedAt;
                aiFactionTurnSamples.Add(elapsed);
                slowAiFactionTurns.Add((elapsed, faction.InstanceID, game.CurrentTick));
            };
            manager.AIFactionTurnStepStarted += (faction, stepName) =>
                aiFactionStepStarts[(faction.InstanceID, stepName)] = Stopwatch.GetTimestamp();
            manager.AIFactionTurnStepCompleted += (faction, stepName) =>
            {
                if (!aiFactionStepStarts.Remove((faction.InstanceID, stepName), out long startedAt))
                    return;

                if (!aiFactionStepSamples.TryGetValue(stepName, out List<long> samples))
                {
                    samples = new List<long>();
                    aiFactionStepSamples.Add(stepName, samples);
                }

                samples.Add(Stopwatch.GetTimestamp() - startedAt);
            };
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
                ProcessTickIncrementally(
                    manager,
                    gameProcessingStepSamples,
                    aiWorkUnitSamples,
                    slowMissionPlans,
                    manufactureExecutions,
                    game.CurrentTick
                );
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
            LogToFile(
                logPath,
                $"[HeadlessSim] ai-faction-turn median={GetPercentileMilliseconds(aiFactionTurnSamples, 50):F3}ms p90={GetPercentileMilliseconds(aiFactionTurnSamples, 90):F3}ms p99={GetPercentileMilliseconds(aiFactionTurnSamples, 99):F3}ms max={GetPercentileMilliseconds(aiFactionTurnSamples, 100):F3}ms"
            );
            foreach (KeyValuePair<string, List<long>> phase in aiFactionStepSamples)
            {
                LogToFile(
                    logPath,
                    $"[HeadlessSim] ai-step name={phase.Key} median={GetPercentileMilliseconds(phase.Value, 50):F3}ms p90={GetPercentileMilliseconds(phase.Value, 90):F3}ms p99={GetPercentileMilliseconds(phase.Value, 99):F3}ms max={GetPercentileMilliseconds(phase.Value, 100):F3}ms"
                );
            }
            foreach (KeyValuePair<string, List<long>> workUnit in aiWorkUnitSamples)
            {
                LogToFile(
                    logPath,
                    $"[HeadlessSim] ai-work-unit name={workUnit.Key} median={GetPercentileMilliseconds(workUnit.Value, 50):F3}ms p90={GetPercentileMilliseconds(workUnit.Value, 90):F3}ms p99={GetPercentileMilliseconds(workUnit.Value, 99):F3}ms max={GetPercentileMilliseconds(workUnit.Value, 100):F3}ms"
                );
            }
            LogToFile(
                logPath,
                $"[HeadlessSim] projected-maintenance calculations={Faction.ProjectedMaintenanceCalculationCount} elapsed={GetElapsedMilliseconds(Faction.ProjectedMaintenanceElapsedTimestampCount):F3}ms"
            );
            foreach (
                (
                    long elapsed,
                    int tick,
                    int candidates,
                    int exactScores,
                    string breakdown
                ) in slowMissionPlans.OrderByDescending(sample => sample.Elapsed).Take(20)
            )
            {
                LogToFile(
                    logPath,
                    $"[HeadlessSim] ai-slow-mission-plan tick={tick} elapsed={GetElapsedMilliseconds(elapsed):F3}ms candidates={candidates} exactScores={exactScores} scores={breakdown}"
                );
            }
            foreach (
                IGrouping<
                    int,
                    (long Elapsed, int Tick, int Count, string ProductTypeId)
                > tickGroup in manufactureExecutions
                    .GroupBy(sample => sample.Tick)
                    .OrderByDescending(group => group.Sum(sample => sample.Elapsed))
                    .Take(20)
            )
            {
                string batches = string.Join(
                    ",",
                    tickGroup.Select(sample => $"{sample.ProductTypeId}x{sample.Count}")
                );
                LogToFile(
                    logPath,
                    $"[HeadlessSim] ai-slow-manufacturing tick={tickGroup.Key} elapsed={GetElapsedMilliseconds(tickGroup.Sum(sample => sample.Elapsed)):F3}ms proposals={tickGroup.Count()} items={tickGroup.Sum(sample => sample.Count)} batches={batches}"
                );
            }
            foreach (
                (long elapsed, string factionId, int tick) in slowAiFactionTurns
                    .OrderByDescending(sample => sample.Elapsed)
                    .Take(10)
            )
            {
                LogToFile(
                    logPath,
                    $"[HeadlessSim] ai-slow-turn tick={tick} faction={factionId} elapsed={GetElapsedMilliseconds(elapsed):F3}ms"
                );
            }
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
                spaceCombatCalibrationTracker,
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
            AIMissionPlanner.CaptureDiagnostics = false;
            Faction.CaptureProjectedMaintenanceDiagnostics = false;
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
            Player player = game.GetFactionPlayer(faction.InstanceID);
            game.SetFactionController(
                faction.InstanceID,
                faction.InstanceID == playerFaction.InstanceID
                    ? _savedSimulationPlayerId
                    : player.PlayerID,
                faction.InstanceID == playerFaction.InstanceID
                    ? PlayerControllerType.Human
                    : PlayerControllerType.AI
            );
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
    /// Converts high-resolution timestamp counts to elapsed milliseconds.
    /// </summary>
    /// <param name="timestampCount">The elapsed timestamp count.</param>
    /// <returns>The corresponding elapsed milliseconds.</returns>
    private static double GetElapsedMilliseconds(long timestampCount) =>
        timestampCount * 1000d / Stopwatch.Frequency;

    /// <summary>
    /// Drains one incremental game tick while recording each scheduled step.
    /// </summary>
    /// <param name="manager">The game manager processing the tick.</param>
    /// <param name="stepSamples">The collection receiving step durations.</param>
    /// <param name="aiWorkUnitSamples">
    /// The optional collection receiving AI planner and proposal durations keyed by runtime type.
    /// </param>
    /// <param name="slowMissionPlans">The collection receiving detailed mission-planner samples.</param>
    /// <param name="manufactureExecutions">The collection receiving manufacturing execution samples.</param>
    /// <param name="currentTick">The tick being processed.</param>
    private static void ProcessTickIncrementally(
        GameManager manager,
        ICollection<long> stepSamples,
        IDictionary<string, List<long>> aiWorkUnitSamples = null,
        ICollection<(
            long Elapsed,
            int Tick,
            int Candidates,
            int ExactScores,
            string Breakdown
        )> slowMissionPlans = null,
        ICollection<(
            long Elapsed,
            int Tick,
            int Count,
            string ProductTypeId
        )> manufactureExecutions = null,
        int currentTick = 0
    )
    {
        IEnumerator tick = manager.ProcessTickIncrementally();
        try
        {
            bool hasNext;
            do
            {
                long startTimestamp = Stopwatch.GetTimestamp();
                hasNext = tick.MoveNext();
                long elapsed = Stopwatch.GetTimestamp() - startTimestamp;
                stepSamples.Add(elapsed);
                object workUnit = tick.Current;
                if (aiWorkUnitSamples != null && workUnit is AIProposal or IAIProposalPlanner)
                {
                    string workUnitName = workUnit.GetType().Name;
                    if (!aiWorkUnitSamples.TryGetValue(workUnitName, out List<long> samples))
                    {
                        samples = new List<long>();
                        aiWorkUnitSamples.Add(workUnitName, samples);
                    }

                    samples.Add(elapsed);
                    if (workUnit is AIMissionPlanner missionPlanner)
                    {
                        string breakdown = string.Join(
                            ",",
                            missionPlanner
                                .LastScoreDiagnostics.OrderByDescending(entry =>
                                    entry.Value.Elapsed
                                )
                                .Select(entry =>
                                    $"{entry.Key}:{entry.Value.Count}/{GetElapsedMilliseconds(entry.Value.Elapsed):F3}ms"
                                )
                        );
                        slowMissionPlans?.Add(
                            (
                                elapsed,
                                currentTick,
                                missionPlanner.LastCandidateCount,
                                missionPlanner.LastExactScoreCount,
                                breakdown
                            )
                        );
                    }
                    else if (workUnit is AIManufactureProposal manufactureProposal)
                    {
                        manufactureExecutions?.Add(
                            (
                                elapsed,
                                currentTick,
                                manufactureProposal.ManufacturingCount,
                                manufactureProposal.Product?.GetReference()?.GetTypeID()
                                    ?? string.Empty
                            )
                        );
                    }
                }
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
