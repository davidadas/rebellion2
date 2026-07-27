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
    internal void Load(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            throw new ArgumentException("Scene name cannot be empty.", nameof(sceneName));
        if (_loadCoroutine != null)
            return;

        _loadCoroutine = StartCoroutine(LoadScene(sceneName));
    }

    /// <summary>
    /// Waits for scene content and then performs the requested scene transition.
    /// </summary>
    /// <param name="sceneName">The Unity scene name to load.</param>
    /// <returns>The transition coroutine.</returns>
    private IEnumerator LoadScene(string sceneName)
    {
        Task initialization = GetSceneInitializationAsync(sceneName);
        while (!initialization.IsCompleted)
            yield return null;

        if (initialization.IsFaulted)
        {
            _loadCoroutine = null;
            Debug.LogException(
                initialization.Exception?.GetBaseException()
                    ?? new InvalidOperationException(
                        "Local content initialization failed without an exception."
                    )
            );
            yield break;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (operation == null)
        {
            _loadCoroutine = null;
            Debug.LogException(new InvalidOperationException($"Scene was not found: {sceneName}"));
            yield break;
        }

        yield return operation;
        _loadCoroutine = null;
    }

    private static Task GetSceneInitializationAsync(string sceneName)
    {
        AppBootstrap bootstrap = AppBootstrap.Instance;
        if (sceneName == SaveMenuLaunchContext.SaveMenuSceneName)
            return bootstrap.InitializeSaveMenuContentAsync();
        if (sceneName == SaveMenuLaunchContext.StrategyViewSceneName)
            return bootstrap.InitializeStrategyContentAsync();

        return bootstrap.InitializeMainMenuContentAsync();
    }
}
