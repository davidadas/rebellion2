using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Serializes application scene transitions behind required content initialization.
/// </summary>
public sealed class SceneLoader : MonoBehaviour
{
    private Coroutine _loadCoroutine;

    /// <summary>
    /// Gets whether this loader currently owns a scene transition.
    /// </summary>
    internal bool IsLoading => _loadCoroutine != null;

    /// <summary>
    /// Begins a scene transition unless another transition is already active.
    /// </summary>
    /// <param name="sceneName">The Unity scene name to load.</param>
    /// <param name="loadFailed">The optional action invoked when initialization or scene loading fails.</param>
    /// <returns>True when the scene transition was accepted.</returns>
    internal bool Load(string sceneName, Action<Exception> loadFailed = null)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            throw new ArgumentException("Scene name cannot be empty.", nameof(sceneName));
        if (_loadCoroutine != null)
            return false;

        AudioManager.Instance?.StopSfx();
        _loadCoroutine = StartCoroutine(LoadScene(sceneName, loadFailed));
        return true;
    }

    /// <summary>
    /// Waits for scene content and then performs the requested scene transition.
    /// </summary>
    /// <param name="sceneName">The Unity scene name to load.</param>
    /// <param name="loadFailed">The optional action invoked when the transition fails.</param>
    /// <returns>The transition coroutine.</returns>
    private IEnumerator LoadScene(string sceneName, Action<Exception> loadFailed)
    {
        Task initialization;
        try
        {
            initialization = GetSceneInitializationAsync(sceneName);
        }
        catch (Exception exception)
        {
            ReportFailure(exception, loadFailed);
            yield break;
        }

        while (!initialization.IsCompleted)
            yield return null;

        if (initialization.IsCanceled || initialization.IsFaulted)
        {
            ReportFailure(
                initialization.Exception?.GetBaseException()
                    ?? new InvalidOperationException(
                        "Local content initialization was canceled before the scene could load."
                    ),
                loadFailed
            );
            yield break;
        }

        ThreadPriority previousLoadingPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = ThreadPriority.Low;
        try
        {
            AsyncOperation operation;
            try
            {
                operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                ReportFailure(exception, loadFailed);
                yield break;
            }

            if (operation == null)
            {
                ReportFailure(
                    new InvalidOperationException($"Scene was not found: {sceneName}"),
                    loadFailed
                );
                yield break;
            }

            yield return operation;
        }
        finally
        {
            Application.backgroundLoadingPriority = previousLoadingPriority;
            _loadCoroutine = null;
        }
    }

    /// <summary>
    /// Releases the active transition, logs its error, and notifies the requesting workflow.
    /// </summary>
    /// <param name="exception">The transition failure.</param>
    /// <param name="loadFailed">The optional failure callback.</param>
    private void ReportFailure(Exception exception, Action<Exception> loadFailed)
    {
        _loadCoroutine = null;
        if (loadFailed != null)
            loadFailed(exception);
        else
            Debug.LogException(exception);
    }

    /// <summary>
    /// Selects the content initialization task required by a destination scene.
    /// </summary>
    /// <param name="sceneName">The destination scene name.</param>
    /// <returns>The scene's content initialization task.</returns>
    private static Task GetSceneInitializationAsync(string sceneName)
    {
        AppBootstrap bootstrap = AppBootstrap.Instance;
        if (sceneName == "StrategyView")
            return bootstrap.InitializeStrategyContentAsync();
        if (sceneName == "TacticalView")
            return bootstrap.InitializeTacticalContentAsync();

        return bootstrap.InitializeMainMenuSceneAsync();
    }
}
