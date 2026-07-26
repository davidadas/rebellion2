using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneLoader : MonoBehaviour
{
    private Coroutine _loadCoroutine;

    internal bool IsLoading => _loadCoroutine != null;

    internal void Load(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            throw new ArgumentException("Scene name cannot be empty.", nameof(sceneName));
        if (_loadCoroutine != null)
            return;

        _loadCoroutine = StartCoroutine(LoadScene(sceneName));
    }

    private IEnumerator LoadScene(string sceneName)
    {
        Task initialization = ResourceManager.InitializeAsync();
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
}
