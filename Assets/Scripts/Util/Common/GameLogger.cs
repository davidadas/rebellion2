using System;
using System.IO;
using UnityEngine;

/// <summary>
/// A static logger class for logging messages to the Unity Console and optionally to a file.
/// </summary>
public static class GameLogger
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Debug
    }

    private static string logFilePath = $"{Application.persistentDataPath}/log.txt";
    private static bool logToFile = false;
    private static bool includeTimestamp = true;

    /// <summary>
    /// Configures the logger settings.
    /// </summary>
    /// <param name="filePath">Path to the log file. Defaults to Application.persistentDataPath if null.</param>
    /// <param name="enableFileLogging">Whether to log messages to a file.</param>
    /// <param name="addTimestamps">Whether to include timestamps in log messages.</param>
    public static void Configure(string filePath = null, bool enableFileLogging = false, bool addTimestamps = true)
    {
        logFilePath = filePath ?? logFilePath;
        logToFile = enableFileLogging;
        includeTimestamp = addTimestamps;

        if (logToFile)
        {
            InitializeLogFile();
        }
    }

    /// <summary>
    /// Logs a message to the Unity Console and optionally to a file.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="level">The log level (e.g., Info, Warning, Error).</param>
    public static void Log(string message, LogLevel level = LogLevel.Info)
    {
        string logMessage = FormatMessage(message, level);

        // Log the message to the Unity Console based on its level.
        switch (level)
        {
            case LogLevel.Info:
                Debug.Log(logMessage);
                break;
            case LogLevel.Warning:
                Debug.LogWarning(logMessage);
                break;
            case LogLevel.Error:
                Debug.LogError(logMessage);
                break;
            case LogLevel.Debug:
                Debug.Log($"DEBUG: {logMessage}");
                break;
        }

        // If logging to a file is enabled, write the message to the log file.
        if (logToFile)
        {
            WriteToFile(logMessage);
        }
    }

    private static string FormatMessage(string message, LogLevel level)
    {
        string timestamp = includeTimestamp ? $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] " : string.Empty;
        return $"{timestamp}[{level}] {message}";
    }

    private static void InitializeLogFile()
    {
        try
        {
            if (!File.Exists(logFilePath))
            {
                File.Create(logFilePath).Dispose(); // Create and immediately close the file.
            }
            WriteToFile($"Log initialized at {DateTime.Now}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to initialize log file: {ex.Message}");
        }
    }

    private static void WriteToFile(string message)
    {
        try
        {
            File.AppendAllText(logFilePath, message + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to write to log file: {ex.Message}");
        }
    }
}
