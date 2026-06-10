using System;
using System.IO;
using System.Linq;
using Rebellion.Util.Common;

[UnityEditor.InitializeOnLoad]
public static class PersistentSimulationRunner
{
    private const string _jobDirectory = "/tmp/rebellion2-sim-jobs";
    private const string _logDirectory = "/tmp/rebellion2-sim-logs";
    private const double _pollIntervalSeconds = 1.0;

    private static bool _isRunningJob;
    private static double _nextPollAt;

    /// <summary>
    /// Registers the editor update poller.
    /// </summary>
    static PersistentSimulationRunner()
    {
        UnityEditor.EditorApplication.update += OnEditorUpdate;
    }

    /// <summary>
    /// Polls for queued simulation jobs while the editor is idle.
    /// </summary>
    private static void OnEditorUpdate()
    {
        if (_isRunningJob || UnityEditor.EditorApplication.isCompiling)
            return;

        if (UnityEditor.EditorApplication.timeSinceStartup < _nextPollAt)
            return;

        _nextPollAt = UnityEditor.EditorApplication.timeSinceStartup + _pollIntervalSeconds;
        try
        {
            TryRunNextJob();
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogException(ex);
        }
    }

    /// <summary>
    /// Runs the next queued simulation job if one is available.
    /// </summary>
    /// <returns>True if a job was found and started.</returns>
    private static bool TryRunNextJob()
    {
        if (UnityEditor.EditorApplication.isCompiling)
            return false;

        string nextJob = GetNextJobPath();
        if (string.IsNullOrWhiteSpace(nextJob))
            return false;

        RunJob(nextJob);
        return true;
    }

    /// <summary>
    /// Runs the next queued simulation job from the editor menu.
    /// </summary>
    [UnityEditor.MenuItem("Tools/Simulation/Run Next Queued Simulation")]
    private static void RunNextQueuedJob()
    {
        try
        {
            if (!TryRunNextJob())
                UnityEngine.Debug.Log("No queued simulation job found.");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogException(ex);
        }
    }

    /// <summary>
    /// Returns the path of the next queued job file.
    /// </summary>
    /// <returns>The next job path, or null if no job is queued.</returns>
    private static string GetNextJobPath()
    {
        if (!Directory.Exists(_jobDirectory))
            return null;

        return Directory
            .GetFiles(_jobDirectory, "*.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>
    /// Runs a queued simulation job file.
    /// </summary>
    /// <param name="jobPath">The path to the queued job file.</param>
    private static void RunJob(string jobPath)
    {
        string runningPath = Path.ChangeExtension(jobPath, ".running");
        string completePath = Path.ChangeExtension(jobPath, ".done");
        string failedPath = Path.ChangeExtension(jobPath, ".failed");

        _isRunningJob = true;

        try
        {
            if (string.IsNullOrWhiteSpace(jobPath) || !File.Exists(jobPath))
                throw new FileNotFoundException("Simulation job file does not exist.", jobPath);

            if (File.Exists(runningPath) || File.Exists(completePath) || File.Exists(failedPath))
                return;

            File.Move(jobPath, runningPath);

            SimulationJob job = UnityEngine.JsonUtility.FromJson<SimulationJob>(
                File.ReadAllText(runningPath)
            );
            if (job == null)
                throw new InvalidOperationException(
                    $"Could not parse simulation job: {runningPath}"
                );

            if (string.IsNullOrWhiteSpace(job.OutputPath))
            {
                throw new InvalidOperationException(
                    $"Simulation job is missing OutputPath: {runningPath}"
                );
            }

            string logPath = GetLogPath(job.OutputPath);
            GameLogger.Configure(logPath, enableFileLogging: true);
            string startMessage =
                $"[PersistentSim] running job={Path.GetFileName(runningPath)} seed={(job.Seed >= 0 ? job.Seed.ToString() : "random")} ticks={job.TickCount} output={job.OutputPath}";
            UnityEngine.Debug.Log(startMessage);
            LogToFile(logPath, startMessage);

            HeadlessSimulationRunner.SimulationRunResult result =
                HeadlessSimulationRunner.RunPersistentSimulation(
                    job.TickCount > 0 ? job.TickCount : 300,
                    job.OutputPath,
                    job.Seed >= 0 ? job.Seed : null
                );

            File.WriteAllText(
                completePath,
                UnityEngine.JsonUtility.ToJson(
                    new SimulationJobResult
                    {
                        JobPath = runningPath,
                        OutputPath = result.OutputPath,
                        TicksCompleted = result.TicksCompleted,
                        Seed = result.Seed,
                    },
                    true
                )
            );
            File.Delete(runningPath);
            string completeMessage =
                $"[PersistentSim] completed job={Path.GetFileName(completePath)}";
            UnityEngine.Debug.Log(completeMessage);
            LogToFile(logPath, completeMessage);
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(failedPath))
                File.WriteAllText(failedPath, ex.ToString());
            if (File.Exists(runningPath))
                File.Delete(runningPath);
            UnityEngine.Debug.LogException(ex);
        }
        finally
        {
            _isRunningJob = false;
        }
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

    [Serializable]
    private sealed class SimulationJob
    {
        public int TickCount = 300;
        public string OutputPath = string.Empty;
        public int Seed = -1;
    }

    [Serializable]
    private sealed class SimulationJobResult
    {
        public string JobPath;
        public string OutputPath;
        public int TicksCompleted;
        public int Seed = -1;
    }
}
