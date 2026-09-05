using System;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

/// <summary>
/// Configures and owns the lifecycle of an editor Game view recording.
/// </summary>
[InitializeOnLoad]
internal static class GameRecordingSession
{
    private const string _recordingDirectoryName = "_recordings";

    private static RecorderController _recorderController;
    private static RecorderControllerSettings _recorderControllerSettings;
    private static MovieRecorderSettings _movieRecorderSettings;
    private static string _recordingPath;

    /// <summary>
    /// Gets whether this utility currently owns an active Recorder session.
    /// </summary>
    public static bool IsRecording => _recorderController?.IsRecording() == true;

    static GameRecordingSession()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    /// <summary>
    /// Configures and starts an MP4 recording of the Game view and game audio.
    /// </summary>
    public static void Start()
    {
        string recordingDirectory = GetRecordingDirectory();
        Directory.CreateDirectory(recordingDirectory);

        string fileName = $"rebellion2-{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}";
        _recordingPath = Path.Combine(recordingDirectory, fileName + ".mp4");

        GameViewInputSettings gameViewInputSettings = CreateGameViewInputSettings();
        _movieRecorderSettings = CreateMovieRecorderSettings(
            gameViewInputSettings,
            Path.Combine(recordingDirectory, fileName)
        );
        _recorderControllerSettings = CreateRecorderControllerSettings(_movieRecorderSettings);

        if (!TryStartRecorder())
        {
            return;
        }

        GameRecordingControlsWindow.ShowWindow();
        Debug.Log(
            $"Game recording started at {gameViewInputSettings.OutputWidth}x{gameViewInputSettings.OutputHeight}: {_recordingPath}"
        );
    }

    /// <summary>
    /// Stops the current recording, releases its settings, and reports the output path.
    /// </summary>
    public static void Stop()
    {
        try
        {
            _recorderController?.StopRecording();
        }
        finally
        {
            CleanupRecorder();
            GameRecordingControlsWindow.CloseWindow();
        }

        if (!string.IsNullOrEmpty(_recordingPath))
        {
            Debug.Log($"Game recording saved: {_recordingPath}");
            _recordingPath = null;
        }
    }

    /// <summary>
    /// Stops and releases the Recorder session before Unity leaves Play mode.
    /// </summary>
    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode && _recorderController != null)
        {
            Stop();
        }
    }

    /// <summary>
    /// Configures Game view capture at dimensions accepted by the MP4 encoder.
    /// Odd dimensions are expanded by one pixel rather than reduced.
    /// </summary>
    private static GameViewInputSettings CreateGameViewInputSettings()
    {
        GameViewInputSettings settings = new();
        settings.OutputWidth = GetEncoderCompatibleDimension(settings.OutputWidth);
        settings.OutputHeight = GetEncoderCompatibleDimension(settings.OutputHeight);
        return settings;
    }

    /// <summary>
    /// Creates the MP4 and audio settings for a Game view recording.
    /// </summary>
    private static MovieRecorderSettings CreateMovieRecorderSettings(
        GameViewInputSettings gameViewInputSettings,
        string outputPathWithoutExtension
    )
    {
        MovieRecorderSettings settings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        settings.name = "Rebellion 2 Game Recording";
        settings.Enabled = true;
        settings.EncoderSettings = new CoreEncoderSettings
        {
            Codec = CoreEncoderSettings.OutputCodec.MP4,
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
        };
        settings.CaptureAudio = true;
        settings.CaptureAlpha = false;
        settings.ImageInputSettings = gameViewInputSettings;
        settings.OutputFile = outputPathWithoutExtension;
        return settings;
    }

    /// <summary>
    /// Creates a manually controlled, variable-frame-rate Recorder session.
    /// </summary>
    private static RecorderControllerSettings CreateRecorderControllerSettings(
        MovieRecorderSettings movieRecorderSettings
    )
    {
        RecorderControllerSettings settings =
            ScriptableObject.CreateInstance<RecorderControllerSettings>();
        settings.AddRecorderSettings(movieRecorderSettings);
        settings.SetRecordModeToManual();
        settings.FrameRatePlayback = FrameRatePlayback.Variable;
        settings.FrameRate = 30.0f;
        settings.CapFrameRate = false;
        return settings;
    }

    /// <summary>
    /// Starts the configured Recorder session and cleans up if startup fails.
    /// </summary>
    private static bool TryStartRecorder()
    {
        try
        {
            _recorderController = new RecorderController(_recorderControllerSettings);
            _recorderController.PrepareRecording();
            if (_recorderController.StartRecording())
            {
                return true;
            }

            Debug.LogError("Game recording could not be started. See preceding Recorder errors.");
        }
        catch
        {
            CleanupFailedRecordingStart();
            throw;
        }

        CleanupFailedRecordingStart();
        return false;
    }

    /// <summary>
    /// Returns the smallest even encoder dimension that does not reduce the requested size.
    /// </summary>
    private static int GetEncoderCompatibleDimension(int requestedDimension)
    {
        return Mathf.Max(2, requestedDimension + requestedDimension % 2);
    }

    /// <summary>
    /// Resolves the local recording directory beside the Assets directory.
    /// </summary>
    private static string GetRecordingDirectory()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", _recordingDirectoryName));
    }

    /// <summary>
    /// Releases state after Recorder startup fails and discards the incomplete output path.
    /// </summary>
    private static void CleanupFailedRecordingStart()
    {
        CleanupRecorder();
        _recordingPath = null;
    }

    /// <summary>
    /// Releases the Recorder controller and its temporary ScriptableObject settings.
    /// </summary>
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
