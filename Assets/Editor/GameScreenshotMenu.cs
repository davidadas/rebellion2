using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Captures the active Game view to the project's local screenshot directory.
/// </summary>
public static class GameScreenshotMenu
{
    private const string _menuPath = "Rebellion/Capture Game Screenshot";
    private const string _screenshotDirectoryName = "_screenshots";

    /// <summary>
    /// Saves the current Game view as a timestamped PNG.
    /// </summary>
    [MenuItem(_menuPath, false, 200)]
    public static void CaptureGameScreenshot()
    {
        string screenshotDirectory = Path.Combine(Application.dataPath, _screenshotDirectoryName);
        Directory.CreateDirectory(screenshotDirectory);

        string fileName = $"rebellion2-{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.png";
        string screenshotPath = Path.Combine(screenshotDirectory, fileName);
        ScreenCapture.CaptureScreenshot(screenshotPath);
        Debug.Log($"Game screenshot queued: {screenshotPath}");
    }

    /// <summary>
    /// Enables capture only while the Game view is actively rendering frames.
    /// </summary>
    [MenuItem(_menuPath, true)]
    private static bool CanCaptureGameScreenshot()
    {
        return EditorApplication.isPlaying && !EditorApplication.isPaused;
    }
}
