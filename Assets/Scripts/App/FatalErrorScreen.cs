using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Presents a content-independent fallback when the application cannot finish loading.
/// </summary>
internal sealed class FatalErrorScreen : MonoBehaviour
{
    private const int _windowWidth = 760;
    private const int _windowHeight = 430;

    private static FatalErrorScreen instance;

    private FatalErrorReport report;
    private bool canReturnToMainMenu;
    private Vector2 reportScrollPosition;

    /// <summary>
    /// Records a fatal loading exception and takes over presentation with the fallback screen.
    /// </summary>
    /// <param name="exception">The exception that prevented loading.</param>
    /// <param name="stage">The application stage that failed.</param>
    /// <param name="allowMainMenuReturn">Whether the initialized application can safely return to its menu.</param>
    internal static void Show(Exception exception, string stage, bool allowMainMenuReturn = false)
    {
        if (exception == null)
            throw new ArgumentNullException(nameof(exception));

        if (instance != null)
            return;

        Debug.LogException(exception);
        GameObject root = new GameObject("FatalErrorScreen");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<FatalErrorScreen>();
        instance.report = FatalErrorReport.Create(exception, stage);
        instance.canReturnToMainMenu = allowMainMenuReturn;
        Time.timeScale = 0f;
        AudioManager.Instance?.StopSfx();
        AudioManager.Instance?.StopMusic();
    }

    /// <summary>
    /// Clears the singleton reference when the fallback screen is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    /// <summary>
    /// Draws the fallback without relying on content packs, prefabs, or authored fonts.
    /// </summary>
    private void OnGUI()
    {
        if (report == null)
            return;

        GUI.depth = int.MinValue;
        Color previousColor = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;

        float width = Mathf.Min(_windowWidth, Screen.width - 40f);
        float height = Mathf.Min(_windowHeight, Screen.height - 40f);
        Rect area = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height
        );
        GUILayout.BeginArea(area);
        DrawHeading();
        DrawReportLocation();
        DrawExceptionSummary();
        GUILayout.FlexibleSpace();
        DrawCommands();
        GUILayout.EndArea();
    }

    /// <summary>
    /// Draws the player-facing failure heading and error identifier.
    /// </summary>
    private void DrawHeading()
    {
        GUIStyle heading = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
        };
        GUIStyle subheading = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
        GUILayout.Label("Rebellion 2 could not finish loading.", heading);
        GUILayout.Space(8f);
        GUILayout.Label($"{report.Stage} failed. Error ID: {report.ErrorID}", subheading);
        GUILayout.Space(12f);
    }

    /// <summary>
    /// Draws the diagnostic report destination or its write failure.
    /// </summary>
    private void DrawReportLocation()
    {
        GUIStyle body = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
        string location =
            report.FilePath != null
                ? $"A diagnostic report was written to:\n{report.FilePath}"
                : $"The diagnostic report could not be written: {report.WriteFailure}";
        GUILayout.Label(location, body);
        GUILayout.Space(12f);
    }

    /// <summary>
    /// Draws the exception summary in a scrollable selectable field.
    /// </summary>
    private void DrawExceptionSummary()
    {
        GUIStyle detail = new GUIStyle(GUI.skin.textArea) { fontSize = 13, wordWrap = true };
        reportScrollPosition = GUILayout.BeginScrollView(
            reportScrollPosition,
            GUILayout.Height(145f)
        );
        GUILayout.TextArea(report.Message, detail, GUILayout.ExpandHeight(true));
        GUILayout.EndScrollView();
    }

    /// <summary>
    /// Draws recovery and diagnostic commands available from the fatal state.
    /// </summary>
    private void DrawCommands()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Copy Report", GUILayout.Height(36f)))
            GUIUtility.systemCopyBuffer = report.Contents;
        if (report.FilePath != null && GUILayout.Button("Open Log Folder", GUILayout.Height(36f)))
            Application.OpenURL(new Uri(report.DirectoryPath).AbsoluteUri);
        if (canReturnToMainMenu && GUILayout.Button("Return to Main Menu", GUILayout.Height(36f)))
            ReturnToMainMenu();
        if (GUILayout.Button("Quit", GUILayout.Height(36f)))
            Application.Quit();
        GUILayout.EndHorizontal();
    }

    /// <summary>
    /// Ends any partial game session and loads the already initialized main menu.
    /// </summary>
    private void ReturnToMainMenu()
    {
        AppBootstrap.Instance?.GetRuntime()?.EndGame();
        Time.timeScale = 1f;
        Destroy(gameObject);
        SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
    }
}
