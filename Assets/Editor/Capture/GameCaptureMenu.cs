using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides editor menu commands for capturing the active Game view.
/// </summary>
public static class GameCaptureMenu
{
    private const string _screenshotMenuPath = "Rebellion/Capture Game Screenshot";
    private const string _startRecordingMenuPath = "Rebellion/Start Game Recording";
    private const string _screenshotDirectoryName = "_screenshots";

    /// <summary>
    /// Saves the current Game view as a timestamped PNG.
    /// </summary>
    [MenuItem(_screenshotMenuPath, false, 200)]
    public static void CaptureGameScreenshot()
    {
        string screenshotDirectory = GetScreenshotDirectory();
        Directory.CreateDirectory(screenshotDirectory);

        string fileName = $"rebellion2-{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.png";
        string screenshotPath = Path.Combine(screenshotDirectory, fileName);
        ScreenCapture.CaptureScreenshot(screenshotPath);
        Debug.Log($"Game screenshot queued: {screenshotPath}");
    }

    /// <summary>
    /// Enables screenshot capture only while the Game view is rendering frames.
    /// </summary>
    [MenuItem(_screenshotMenuPath, true)]
    private static bool CanCaptureGameScreenshot()
    {
        return EditorApplication.isPlaying && !EditorApplication.isPaused;
    }

    /// <summary>
    /// Starts an MP4 recording of the Game view and game audio.
    /// </summary>
    [MenuItem(_startRecordingMenuPath, false, 201)]
    public static void StartGameRecording()
    {
        GameRecordingSession.Start();
    }

    /// <summary>
    /// Enables recording only while the Game view is rendering and no recording is active.
    /// </summary>
    [MenuItem(_startRecordingMenuPath, true)]
    private static bool CanStartGameRecording()
    {
        return !GameRecordingSession.IsRecording
            && EditorApplication.isPlaying
            && !EditorApplication.isPaused;
    }

    /// <summary>
    /// Resolves the local screenshot directory beside the Assets directory.
    /// </summary>
    private static string GetScreenshotDirectory()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", _screenshotDirectoryName));
    }
}
