using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Displays recording controls outside the captured Game view.
/// </summary>
internal sealed class GameRecordingControlsWindow : EditorWindow
{
    private const float _windowWidth = 240.0f;
    private const float _windowHeight = 82.0f;

    private static GameRecordingControlsWindow _instance;
    private bool _closeWithoutStopping;

    /// <summary>
    /// Opens a compact utility window containing the recording stop control.
    /// </summary>
    public static void ShowWindow()
    {
        CloseWindow();

        _instance = CreateInstance<GameRecordingControlsWindow>();
        _instance.titleContent = new GUIContent("Game Recording");
        _instance.minSize = new Vector2(_windowWidth, _windowHeight);
        _instance.maxSize = _instance.minSize;
        _instance.ShowUtility();
        _instance.Focus();
    }

    /// <summary>
    /// Closes the utility window without treating programmatic closure as a stop request.
    /// </summary>
    public static void CloseWindow()
    {
        if (_instance == null)
        {
            return;
        }

        _instance._closeWithoutStopping = true;
        _instance.Close();
        _instance = null;
    }

    /// <summary>
    /// Draws the recording status and stop button.
    /// </summary>
    [SuppressMessage("Roslynator", "RCS1213", Justification = "Invoked by Unity.")]
    private void OnGUI()
    {
        EditorGUILayout.Space(8.0f);
        EditorGUILayout.LabelField("Recording Game View + audio", EditorStyles.boldLabel);
        EditorGUILayout.Space(4.0f);

        Color previousColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.9f, 0.25f, 0.2f);
        if (GUILayout.Button("Stop Recording", GUILayout.Height(30.0f)))
        {
            GameRecordingSession.Stop();
        }

        GUI.backgroundColor = previousColor;
    }

    /// <summary>
    /// Stops recording when the user closes the utility window directly.
    /// </summary>
    [SuppressMessage("Roslynator", "RCS1213", Justification = "Invoked by Unity.")]
    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }

        if (!_closeWithoutStopping && GameRecordingSession.IsRecording)
        {
            GameRecordingSession.Stop();
        }
    }
}
