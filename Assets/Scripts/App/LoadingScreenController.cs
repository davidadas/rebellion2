using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Presents saved-game loading and reconstructs application content only when required.
/// </summary>
internal sealed class LoadingScreenController : MonoBehaviour
{
    private const string _viewResourcePath = "UI/LoadingScreen";
    private const string _objectName = "Loading Screen Controller";
    private const float _fadeToBlackDuration = 0.55f;
    private const float _fadeFromBlackDuration = 0.26f;

    private static LoadingScreenController _activeController;

    private AppBootstrap _bootstrap;
    private Action _transitionCompleted;
    private LoadingScreenView _view;
    private Coroutine _finishRoutine;
    private Coroutine _prepareRoutine;
    private bool _reconstructContent;
    private bool _loadInProgress;

    /// <summary>
    /// Gets whether a saved-game loading transition is currently active.
    /// </summary>
    internal static bool IsLoadInProgress => _activeController?._loadInProgress == true;

    /// <summary>
    /// Starts preparing the persistent loading presentation after the application has rendered its
    /// initial frame.
    /// </summary>
    /// <param name="bootstrap">The active application bootstrap.</param>
    internal static void Prepare(AppBootstrap bootstrap)
    {
        if (!Application.isPlaying || bootstrap == null)
            return;

        LoadingScreenController controller = GetOrCreateController();
        controller._bootstrap = bootstrap;
        if (controller._view == null && controller._prepareRoutine == null)
            controller._prepareRoutine = controller.StartCoroutine(
                controller.PreparePresentation()
            );
    }

    /// <summary>
    /// Starts a one-shot saved-game transition owned independently from the current scene.
    /// </summary>
    /// <param name="bootstrap">The bootstrap that owns the active content services.</param>
    /// <param name="reconstructContent">Whether the save requires another content identity.</param>
    internal static void LoadSave(AppBootstrap bootstrap, bool reconstructContent)
    {
        if (bootstrap == null)
            throw new ArgumentNullException(nameof(bootstrap));

        LoadingScreenController controller = GetOrCreateController();
        if (controller._loadInProgress)
            return;

        controller._bootstrap = bootstrap;
        controller._reconstructContent = reconstructContent;
        controller.EnsurePresentation();
        controller._loadInProgress = true;

        if (!Application.isPlaying)
        {
            AudioManager.Instance?.StopMusic();
            controller._view.SetVisible(true);
            bootstrap.SuspendForSaveLoad();
            return;
        }

        AudioManager.Instance?.FadeOutMusic(_fadeToBlackDuration);
        controller._view.PrepareEntrance();
        controller.StartCoroutine(controller.LoadStrategy());
    }

    /// <summary>
    /// Creates or returns the application-owned transition controller.
    /// </summary>
    /// <returns>The persistent transition controller.</returns>
    private static LoadingScreenController GetOrCreateController()
    {
        if (_activeController != null)
            return _activeController;

        GameObject controllerObject = new GameObject(_objectName);
        if (Application.isPlaying)
            DontDestroyOnLoad(controllerObject);
        _activeController = controllerObject.AddComponent<LoadingScreenController>();
        return _activeController;
    }

    /// <summary>
    /// Instantiates and begins warming the hidden loading view.
    /// </summary>
    private void EnsurePresentation()
    {
        if (_view != null)
            return;

        LoadingScreenView prefab = Resources.Load<LoadingScreenView>(_viewResourcePath);
        if (prefab == null)
        {
            throw new MissingReferenceException(
                $"Loading-screen prefab is missing: {_viewResourcePath}"
            );
        }

        _view = Instantiate(prefab, transform, false);
        _view.Initialize(_bootstrap.GetContentPack().ContentRootPath);
        _view.SetVisible(false);
    }

    /// <summary>
    /// Removes the persistent loading presentation after replacement gameplay is visible.
    /// </summary>
    /// <param name="transitionCompleted">The action to run after the loading view is gone.</param>
    internal static void CompletePendingLoad(Action transitionCompleted = null)
    {
        if (_activeController == null)
            return;

        if (!Application.isPlaying)
        {
            DestroyImmediate(_activeController.gameObject);
            return;
        }

        if (!_activeController._loadInProgress)
            return;

        _activeController._transitionCompleted = transitionCompleted;

        if (_activeController._view != null && _activeController._finishRoutine == null)
        {
            _activeController._finishRoutine = _activeController.StartCoroutine(
                _activeController.FinishTransition()
            );
        }
        else if (_activeController._view == null)
        {
            _activeController._loadInProgress = false;
            Action completed = _activeController._transitionCompleted;
            _activeController._transitionCompleted = null;
            completed?.Invoke();
        }
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
    /// Defers presentation construction until startup has had time to become responsive.
    /// </summary>
    /// <returns>The preparation coroutine.</returns>
    private IEnumerator PreparePresentation()
    {
        yield return null;
        yield return null;
        EnsurePresentation();
        _prepareRoutine = null;
    }

    /// <summary>
    /// Gives the loading presentation a frame, optionally replaces content services, and opens the
    /// strategy scene for the pending save.
    /// </summary>
    /// <returns>The saved-game transition coroutine.</returns>
    private IEnumerator LoadStrategy()
    {
        yield return FadeBlack(0f, 1f, _fadeToBlackDuration);
        _view.SetPresentationVisible(true);
        yield return FadeBlack(1f, 0f, _fadeFromBlackDuration);
        _bootstrap.SuspendForSaveLoad();

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

    /// <summary>
    /// Fades the loading presentation through black into the newly activated strategy view.
    /// </summary>
    /// <returns>The exit-transition coroutine.</returns>
    private IEnumerator FinishTransition()
    {
        yield return FadeBlack(0f, 1f, _fadeToBlackDuration);
        _view.SetPresentationVisible(false);
        yield return FadeBlack(1f, 0f, _fadeFromBlackDuration);
        _view.SetVisible(false);
        _loadInProgress = false;
        _finishRoutine = null;
        Action transitionCompleted = _transitionCompleted;
        _transitionCompleted = null;
        transitionCompleted?.Invoke();
    }

    /// <summary>
    /// Animates the full-screen black layer with unscaled time.
    /// </summary>
    /// <param name="from">The starting opacity.</param>
    /// <param name="to">The ending opacity.</param>
    /// <param name="duration">The transition duration in seconds.</param>
    /// <returns>The fade coroutine.</returns>
    private IEnumerator FadeBlack(float from, float to, float duration)
    {
        float elapsed = 0f;
        _view.SetBlackOpacity(from);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            _view.SetBlackOpacity(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, progress)));
            yield return null;
        }
        _view.SetBlackOpacity(to);
    }
}
