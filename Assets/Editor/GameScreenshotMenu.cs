using System;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

/// <summary>
/// Captures the active Game view to the project's local screenshot directory.
/// </summary>
public static class GameScreenshotMenu
{
    private const string _screenshotMenuPath = "Rebellion/Capture Game Screenshot";
    private const string _startRecordingMenuPath = "Rebellion/Start Game Recording";
    private const string _stopRecordingMenuPath = "Rebellion/Stop Game Recording";
    private const string _screenshotDirectoryName = "_screenshots";
    private const string _recordingDirectoryName = "_recordings";

    private static RecorderController _recorderController;
    private static RecorderControllerSettings _recorderControllerSettings;
    private static MovieRecorderSettings _movieRecorderSettings;
    private static string _recordingPath;

    static GameScreenshotMenu()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    /// <summary>
    /// Saves the current Game view as a timestamped PNG.
    /// </summary>
    [MenuItem(_screenshotMenuPath, false, 200)]
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
    [MenuItem(_screenshotMenuPath, true)]
    private static bool CanCaptureGameScreenshot()
    {
        return EditorApplication.isPlaying && !EditorApplication.isPaused;
    }

    /// <summary>
    /// Enables starting only while the Game view can render and no recording is active.
    /// </summary>
    [MenuItem(_startRecordingMenuPath, true)]
    private static bool CanStartGameRecording()
    {
        return !IsRecording() && EditorApplication.isPlaying && !EditorApplication.isPaused;
    }

    /// <summary>
    /// Enables stopping only while a recording is active.
    /// </summary>
    [MenuItem(_stopRecordingMenuPath, true)]
    private static bool CanStopGameRecording()
    {
        return IsRecording();
    }

    private static bool IsRecording()
    {
        return _recorderController != null && _recorderController.IsRecording();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode && _recorderController != null)
        {
            StopGameRecording();
        }
    }

    /// <summary>
    /// Starts an MP4 recording of the Game view and game audio.
    /// </summary>
    [MenuItem(_startRecordingMenuPath, false, 201)]
    public static void StartGameRecording()
    {
        string recordingDirectory = Path.Combine(Application.dataPath, _recordingDirectoryName);
        Directory.CreateDirectory(recordingDirectory);

        string fileName = $"rebellion2-{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}";
        _recordingPath = Path.Combine(recordingDirectory, fileName + ".mp4");
        int outputWidth = GetEvenDimension(Screen.width);
        int outputHeight = GetEvenDimension(Screen.height);

        _recorderControllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        _movieRecorderSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        _movieRecorderSettings.name = "Rebellion 2 Game Recording";
        _movieRecorderSettings.Enabled = true;
        _movieRecorderSettings.EncoderSettings = new CoreEncoderSettings
        {
            Codec = CoreEncoderSettings.OutputCodec.MP4,
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
        };
        _movieRecorderSettings.CaptureAudio = true;
        _movieRecorderSettings.CaptureAlpha = false;
        _movieRecorderSettings.ImageInputSettings = new GameViewInputSettings
        {
            OutputWidth = outputWidth,
            OutputHeight = outputHeight,
        };
        _movieRecorderSettings.OutputFile = Path.Combine(recordingDirectory, fileName);

        _recorderControllerSettings.AddRecorderSettings(_movieRecorderSettings);
        _recorderControllerSettings.SetRecordModeToManual();
        _recorderControllerSettings.FrameRate = 60.0f;

        try
        {
            _recorderController = new RecorderController(_recorderControllerSettings);
            _recorderController.PrepareRecording();
            if (!_recorderController.StartRecording())
            {
                CleanupRecorder();
                _recordingPath = null;
                Debug.LogError(
                    "Game recording could not be started. See preceding Recorder errors."
                );
                return;
            }
        }
        catch
        {
            CleanupRecorder();
            _recordingPath = null;
            throw;
        }

        Debug.Log($"Game recording started: {_recordingPath}");
    }

    private static int GetEvenDimension(int dimension)
    {
        return Mathf.Max(2, dimension - dimension % 2);
    }

    /// <summary>
    /// Stops the active Game view recording and finalizes its MP4 file.
    /// </summary>
    [MenuItem(_stopRecordingMenuPath, false, 202)]
    public static void StopGameRecording()
    {
        try
        {
            _recorderController?.StopRecording();
        }
        finally
        {
            CleanupRecorder();
        }

        if (!string.IsNullOrEmpty(_recordingPath))
        {
            Debug.Log($"Game recording saved: {_recordingPath}");
            _recordingPath = null;
        }
    }

    private static void CleanupRecorder()
    {
        _recorderController = null;

        if (_movieRecorderSettings != null)
        {
            UnityEngine.Object.DestroyImmediate(_movieRecorderSettings);
            _movieRecorderSettings = null;
        }

        if (_recorderControllerSettings != null)
        {
            UnityEngine.Object.DestroyImmediate(_recorderControllerSettings);
            _recorderControllerSettings = null;
        }
    }
}
