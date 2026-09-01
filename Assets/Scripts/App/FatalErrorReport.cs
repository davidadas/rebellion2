using System;
using System.IO;
using System.Text;
using Rebellion.Game;
using UnityEngine;

/// <summary>
/// Contains a player-visible fatal startup error and its persisted diagnostic report.
/// </summary>
internal sealed class FatalErrorReport
{
    internal string ErrorID { get; }

    internal string Stage { get; }

    internal string Message { get; }

    internal string Contents { get; }

    internal string DirectoryPath { get; }

    internal string FilePath { get; }

    internal string WriteFailure { get; }

    /// <summary>
    /// Creates a diagnostic report and writes it beneath the application's persistent data path.
    /// </summary>
    /// <param name="exception">The fatal exception.</param>
    /// <param name="stage">The application stage that could not complete.</param>
    /// <param name="directoryPath">The report directory, or null to use the application log directory.</param>
    /// <param name="timestamp">The report timestamp, or null to use the current local time.</param>
    /// <param name="identifier">The unique identifier suffix, or null to generate one.</param>
    /// <returns>The diagnostic report, including any failure encountered while writing it.</returns>
    internal static FatalErrorReport Create(
        Exception exception,
        string stage,
        string directoryPath = null,
        DateTimeOffset? timestamp = null,
        string identifier = null
    )
    {
        if (exception == null)
            throw new ArgumentNullException(nameof(exception));

        DateTimeOffset occurredAt = timestamp ?? DateTimeOffset.Now;
        string suffix = string.IsNullOrWhiteSpace(identifier)
            ? Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant()
            : identifier.Trim().ToUpperInvariant();
        string errorID = $"LOAD-{occurredAt:yyyyMMdd-HHmmss}-{suffix}";
        string resolvedStage = string.IsNullOrWhiteSpace(stage) ? "Application startup" : stage;
        string resolvedDirectory =
            directoryPath ?? Path.Combine(Application.persistentDataPath, "Logs");
        string contents = BuildContents(exception, resolvedStage, errorID, occurredAt);
        string filePath = Path.Combine(resolvedDirectory, $"error-{errorID}.txt");
        string writeFailure = null;

        try
        {
            Directory.CreateDirectory(resolvedDirectory);
            File.WriteAllText(filePath, contents);
        }
        catch (Exception writeException)
        {
            filePath = null;
            writeFailure = writeException.Message;
        }

        return new FatalErrorReport(
            errorID,
            resolvedStage,
            exception.Message,
            contents,
            resolvedDirectory,
            filePath,
            writeFailure
        );
    }

    /// <summary>
    /// Stores one complete fatal-error report.
    /// </summary>
    private FatalErrorReport(
        string errorID,
        string stage,
        string message,
        string contents,
        string directoryPath,
        string filePath,
        string writeFailure
    )
    {
        ErrorID = errorID;
        Stage = stage;
        Message = message;
        Contents = contents;
        DirectoryPath = directoryPath;
        FilePath = filePath;
        WriteFailure = writeFailure;
    }

    /// <summary>
    /// Formats application, launch, and exception details for diagnostics.
    /// </summary>
    /// <param name="exception">The fatal exception.</param>
    /// <param name="stage">The application stage that failed.</param>
    /// <param name="errorID">The player-visible error identifier.</param>
    /// <param name="occurredAt">The local failure timestamp.</param>
    /// <returns>The complete plain-text report.</returns>
    private static string BuildContents(
        Exception exception,
        string stage,
        string errorID,
        DateTimeOffset occurredAt
    )
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine($"Error ID: {errorID}");
        report.AppendLine($"Occurred: {occurredAt:O}");
        report.AppendLine($"Stage: {stage}");
        report.AppendLine($"Product: {Application.productName}");
        report.AppendLine($"Version: {Application.version}");
        report.AppendLine($"Unity: {Application.unityVersion}");
        report.AppendLine($"Platform: {Application.platform}");
        AppendLaunchContext(report);
        report.AppendLine();
        report.AppendLine("Exception:");
        report.AppendLine(exception.ToString());
        return report.ToString();
    }

    /// <summary>
    /// Appends available content and save identifiers without requiring initialized runtime services.
    /// </summary>
    /// <param name="report">The report receiving launch metadata.</param>
    private static void AppendLaunchContext(StringBuilder report)
    {
        try
        {
            GameSummary summary = GameLaunchContext.Summary;
            report.AppendLine($"Content pack: {summary?.PackID ?? "Unavailable"}");
            report.AppendLine($"Content version: {summary?.PackVersion ?? "Unavailable"}");
            report.AppendLine($"Scenario: {summary?.ScenarioID ?? "Unavailable"}");
            report.AppendLine($"Player faction: {summary?.PlayerFactionID ?? "Unavailable"}");
            report.AppendLine(
                $"Launch type: {(GameLaunchContext.IsLoadGame ? "Load game" : "New game")}"
            );
            report.AppendLine($"Save file: {GameLaunchContext.SaveFileName ?? "None"}");
        }
        catch (Exception metadataException)
        {
            report.AppendLine($"Launch metadata unavailable: {metadataException.Message}");
        }
    }
}
