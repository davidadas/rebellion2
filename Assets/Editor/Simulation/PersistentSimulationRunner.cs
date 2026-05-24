using System;
using System.IO;
using System.Linq;
using Rebellion.Util.Common;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PersistentSimulationRunner
{
    private const string _jobDirectory = "/tmp/rebellion2-sim-jobs";
    private const string _logDirectory = "/tmp/rebellion2-sim-logs";
    private const double _pollIntervalSeconds = 1.0;

    private static bool _isRunningJob;
    private static double _nextPollAt;

    static PersistentSimulationRunner()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    private static void OnEditorUpdate()
    {
        if (_isRunningJob || EditorApplication.isCompiling)
            return;

        if (EditorApplication.timeSinceStartup < _nextPollAt)
            return;

        _nextPollAt = EditorApplication.timeSinceStartup + _pollIntervalSeconds;
        TryRunNextJob();
    }

    private static void TryRunNextJob()
    {
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            if (EditorApplication.isCompiling)
                return;

            Directory.CreateDirectory(_jobDirectory);
            string nextJob = Directory
                .GetFiles(_jobDirectory, "*.json")
                .OrderBy(path => path, StringComparer.Ordinal)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(nextJob))
                return;

            RunJob(nextJob);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    [MenuItem("Tools/UI/Run Simulation")]
    private static void RunJob()
    {
        string jobPath = "dummy.txt";
        RunJob(jobPath);
    }

    private static void RunJob(string jobPath)
    {
        string runningPath = Path.ChangeExtension(jobPath, ".running");
        string completePath = Path.ChangeExtension(jobPath, ".done");
        string failedPath = Path.ChangeExtension(jobPath, ".failed");

        if (File.Exists(runningPath) || File.Exists(completePath) || File.Exists(failedPath))
            return;

        File.Move(jobPath, runningPath);
        _isRunningJob = true;

        try
        {
            SimulationJob job = JsonUtility.FromJson<SimulationJob>(File.ReadAllText(runningPath));
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
            Debug.Log(startMessage);
            LogToFile(logPath, startMessage);

            HeadlessSimulationRunner.SimulationRunResult result =
                HeadlessSimulationRunner.RunPersistentSimulation(
                    job.TickCount > 0 ? job.TickCount : 300,
                    job.OutputPath,
                    job.PlayerFactionId,
                    job.Seed >= 0 ? job.Seed : (int?)null,
                    job.AIProfile
                );

            File.WriteAllText(
                completePath,
                JsonUtility.ToJson(
                    new SimulationJobResult
                    {
                        JobPath = runningPath,
                        OutputPath = result.OutputPath,
                        TicksCompleted = result.TicksCompleted,
                        Seed = result.Seed,
                        PlayerFactionId = result.PlayerFactionId,
                    },
                    true
                )
            );
            File.Delete(runningPath);
            string completeMessage =
                $"[PersistentSim] completed job={Path.GetFileName(completePath)}";
            Debug.Log(completeMessage);
            LogToFile(logPath, completeMessage);
        }
        catch (Exception ex)
        {
            File.WriteAllText(failedPath, ex.ToString());
            if (File.Exists(runningPath))
                File.Delete(runningPath);
            Debug.LogException(ex);
        }
        finally
        {
            _isRunningJob = false;
        }
    }

    private static string GetLogPath(string outputPath)
    {
        string resolvedOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(_logDirectory);
        return Path.Combine(
            _logDirectory,
            $"{Path.GetFileNameWithoutExtension(resolvedOutputPath)}.log"
        );
    }

    private static void LogToFile(string logPath, string message)
    {
        File.AppendAllText(logPath, message + Environment.NewLine);
    }

    [Serializable]
    private sealed class SimulationJob
    {
        public int TickCount = 300;
        public string OutputPath = string.Empty;
        public string PlayerFactionId = string.Empty;
        public int Seed = -1;
        public string AIProfile = string.Empty;
    }

    [Serializable]
    private sealed class SimulationJobResult
    {
        public string JobPath;
        public string OutputPath;
        public int TicksCompleted;
        public int Seed = -1;
        public string PlayerFactionId;
    }
}
