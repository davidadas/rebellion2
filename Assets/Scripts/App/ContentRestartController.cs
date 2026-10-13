using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Presents saved-game loading and reconstructs application content only when required.
/// </summary>
internal sealed class ContentRestartController : MonoBehaviour
{
    private const string _loadingViewResourcePath = "UI/ContentRestartLoadingView";
    private const string _objectName = "Content Restart Controller";

    private static ContentRestartController _activeController;

    private AppBootstrap _bootstrap;
    private bool _reconstructContent;

    /// <summary>
    /// Starts a one-shot saved-game transition owned independently from the current scene.
    /// </summary>
    /// <param name="bootstrap">The bootstrap that owns the active content services.</param>
    /// <param name="reconstructContent">Whether the save requires another content identity.</param>
    internal static void LoadSave(AppBootstrap bootstrap, bool reconstructContent)
    {
        if (bootstrap == null)
            throw new ArgumentNullException(nameof(bootstrap));
        if (_activeController != null)
            return;

        GameObject controllerObject = new GameObject(_objectName);
        if (Application.isPlaying)
            DontDestroyOnLoad(controllerObject);
        ContentRestartController controller =
            controllerObject.AddComponent<ContentRestartController>();
        _activeController = controller;
        controller._bootstrap = bootstrap;
        controller._reconstructContent = reconstructContent;
        ContentRestartLoadingView loadingPrefab = Resources.Load<ContentRestartLoadingView>(
            _loadingViewResourcePath
        );
        if (loadingPrefab == null)
        {
            Destroy(controllerObject);
            throw new MissingReferenceException(
                $"Content restart loading prefab is missing: {_loadingViewResourcePath}"
            );
        }

        ContentRestartLoadingView loadingView = Instantiate(
            loadingPrefab,
            controllerObject.transform,
            false
        );
        loadingView.Initialize(bootstrap.GetContentPack().ContentRootPath);
        controller.StartCoroutine(controller.LoadStrategy());
    }

    /// <summary>
    /// Removes the persistent loading presentation after replacement gameplay is visible.
    /// </summary>
    internal static void CompletePendingLoad()
    {
        if (_activeController == null)
            return;

        if (Application.isPlaying)
            Destroy(_activeController.gameObject);
        else
            DestroyImmediate(_activeController.gameObject);
    }

    /// <summary>
    /// Clears the active-controller registration when the transition finishes.
    /// </summary>
    private void OnDestroy()
    {
        if (_activeController == this)
            _activeController = null;
    }

    /// <summary>
    /// Gives the loading presentation a frame, optionally replaces content services, and opens the
    /// strategy scene for the pending save.
    /// </summary>
    /// <returns>The saved-game transition coroutine.</returns>
    private IEnumerator LoadStrategy()
    {
        yield return null;

        if (_reconstructContent)
        {
            if (AudioManager.Instance != null)
                Destroy(AudioManager.Instance.gameObject);
            Destroy(_bootstrap.gameObject);
            yield return null;

            _bootstrap = AppBootstrap.EnsureExists();
        }

        _bootstrap.LoadScene("StrategyView");
    }
}
