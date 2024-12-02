using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// A static logger class for logging messages to the Unity Console and optionally to a file.
/// Supports various data types and logging methods.
/// </summary>
public static class GameLogger
{
    /// <summary>
    /// Defines the different levels of logging.
    /// </summary>
    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Debug,
    }

    // Default log file path and configuration settings
    private static string logFilePath = $"{Application.persistentDataPath}/log.txt";
    private static bool logToFile = false;
    private static bool includeTimestamp = true;

    /// <summary>
    /// Configures the logger settings.
    /// </summary>
    /// <param name="filePath">Path to the log file. Defaults to Application.persistentDataPath if null.</param>
    /// <param name="enableFileLogging">Whether to log messages to a file.</param>
    /// <param name="addTimestamps">Whether to include timestamps in log messages.</param>
    public static void Configure(
        string filePath = null,
        bool enableFileLogging = false,
        bool addTimestamps = true
    )
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
    /// Logs a message of any type to the Unity Console and optionally to a file.
    /// </summary>
    /// <typeparam name="T">The type of the message to log.</typeparam>
    /// <param name="message">The message to log.</param>
    /// <param name="level">The log level (e.g., Info, Warning, Error).</param>
    public static void Log<T>(T message, LogLevel level = LogLevel.Info)
    {
        string logMessage = FormatMessage(message, level);
        LogToUnityConsole(logMessage, level);
        if (logToFile)
            WriteToFile(logMessage);
    }

    /// <summary>
    /// Logs a formatted message to the Unity Console and optionally to a file.
    /// </summary>
    /// <param name="level">The log level (e.g., Info, Warning, Error).</param>
    /// <param name="format">A composite format string.</param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    public static void LogFormat(LogLevel level, string format, params object[] args)
    {
        string message = string.Format(format, args);
        Log(message, level);
    }

    /// <summary>
    /// Logs an exception to the Unity Console and optionally to a file.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="level">The log level (defaults to Error).</param>
    public static void LogException(Exception exception, LogLevel level = LogLevel.Error)
    {
        string message =
            $"Exception: {exception.GetType().Name}\nMessage: {exception.Message}\nStackTrace: {exception.StackTrace}";
        Log(message, level);
    }

    /// <summary>
    /// Logs an object as JSON to the Unity Console and optionally to a file.
    /// </summary>
    /// <param name="obj">The object to log.</param>
    /// <param name="level">The log level (e.g., Info, Warning, Error).</param>
    public static void LogObject(object obj, LogLevel level = LogLevel.Info)
    {
        string message = JsonUtility.ToJson(obj, true);
        Log(message, level);
    }

    /// <summary>
    /// Formats the log message with timestamp and log level.
    /// </summary>
    private static string FormatMessage<T>(T message, LogLevel level)
    {
        string timestamp = includeTimestamp
            ? $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] "
            : string.Empty;
        return $"{timestamp}[{level}] {message}";
    }

    /// <summary>
    /// Logs the message to the Unity Console based on the specified log level.
    /// </summary>
    private static void LogToUnityConsole(string message, LogLevel level)
    {
        switch (level)
        {
            case LogLevel.Info:
                Debug.Log(message);
                break;
            case LogLevel.Warning:
                Debug.LogWarning(message);
                break;
            case LogLevel.Error:
                Debug.LogError(message);
                break;
            case LogLevel.Debug:
                Debug.Log($"DEBUG: {message}");
                break;
        }
    }

    /// <summary>
    /// Initializes the log file if file logging is enabled.
    /// </summary>
    private static void InitializeLogFile()
    {
        try
        {
            if (!File.Exists(logFilePath))
            {
                // Create and immediately close the file.
                File.Create(logFilePath).Dispose();
            }
            WriteToFile($"Log initialized at {DateTime.Now}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to initialize log file: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes a message to the log file.
    /// </summary>
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
