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
        {
            GameStartupTrace.Log($"Ignored duplicate scene request for '{sceneName}'.");
            return;
        }

        AudioManager.Instance?.StopSfx();
        GameStartupTrace.Log($"SceneLoader accepted '{sceneName}'.");
        _loadCoroutine = StartCoroutine(LoadScene(sceneName));
    }

    /// <summary>
    /// Waits for scene content and then performs the requested scene transition.
    /// </summary>
    /// <param name="sceneName">The Unity scene name to load.</param>
    /// <returns>The transition coroutine.</returns>
    private IEnumerator LoadScene(string sceneName)
    {
        GameStartupTrace.Log($"Waiting for '{sceneName}' content preload.");
        Task initialization = GetSceneInitializationAsync(sceneName);
        while (!initialization.IsCompleted)
            yield return null;

        if (initialization.IsFaulted)
        {
            _loadCoroutine = null;
            GameStartupTrace.Complete($"Content initialization for '{sceneName}' failed.");
            FatalErrorScreen.Show(
                initialization.Exception?.GetBaseException()
                    ?? new InvalidOperationException(
                        "Local content initialization failed without an exception."
                    ),
                $"{sceneName} content loading",
                allowMainMenuReturn: sceneName == "StrategyView"
            );
            yield break;
        }

        GameStartupTrace.Log($"Content preload complete; loading '{sceneName}' scene asset.");
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (operation == null)
        {
            _loadCoroutine = null;
            GameStartupTrace.Complete($"Unity could not start loading '{sceneName}'.");
            FatalErrorScreen.Show(
                new InvalidOperationException($"Scene was not found: {sceneName}"),
                $"{sceneName} scene loading",
                allowMainMenuReturn: sceneName == "StrategyView"
            );
            yield break;
        }

        yield return operation;
        GameStartupTrace.Log($"Unity activated '{sceneName}'.");
        _loadCoroutine = null;
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

        return bootstrap.InitializeMainMenuSceneAsync();
    }
}
