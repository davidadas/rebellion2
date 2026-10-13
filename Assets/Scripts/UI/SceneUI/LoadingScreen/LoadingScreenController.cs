using System;
using System.Collections;
using System.Threading.Tasks;
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
    private const float _failureDisplayDuration = 1.5f;

    private static LoadingScreenController _activeController;

    private AppBootstrap _bootstrap;
    private Action _transitionCompleted;
    private LoadingScreenView _view;
    private Coroutine _finishRoutine;
    private Coroutine _prepareRoutine;
    private Coroutine _recoveryRoutine;
    private string _saveFileName;
    private bool _reconstructContent;
    private bool _loadInProgress;
    private bool _recoveringFromFailure;

    /// <summary>
    /// Gets whether a saved-game loading transition is currently active.
    /// </summary>
    internal static bool IsLoadInProgress => _activeController && _activeController._loadInProgress;

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
    /// <param name="fileName">The save identifier to prepare and load.</param>
    internal static void LoadSave(AppBootstrap bootstrap, string fileName)
    {
        if (bootstrap == null)
            throw new ArgumentNullException(nameof(bootstrap));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("A save file name is required.", nameof(fileName));

        LoadingScreenController controller = GetOrCreateController();
        if (controller._loadInProgress)
            return;

        controller._bootstrap = bootstrap;
        controller._saveFileName = fileName;
        controller._reconstructContent = false;
        controller._recoveringFromFailure = false;
        controller.EnsurePresentation();
        controller._loadInProgress = true;

        if (!Application.isPlaying)
        {
            AudioManager.Instance?.StopMusic();
            controller._view.SetVisible(true);
            SaveLoadPreparation preparation = bootstrap
                .PrepareSaveLoadAsync(fileName)
                .GetAwaiter()
                .GetResult();
            controller._reconstructContent = bootstrap.ApplySaveLoadPreparation(preparation);
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
        _view.ContinueRequested += HandleContinueRequested;
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

        if (_activeController._recoveringFromFailure)
        {
            _activeController.BeginFinishTransition();
        }
        else if (_activeController._view != null)
        {
            _activeController._view.ShowReady();
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
    /// Keeps the loading presentation active while a failed save load returns to the main menu.
    /// </summary>
    /// <param name="exception">The optional failure shown and logged during recovery.</param>
    /// <returns>True when an active saved-game transition began recovery.</returns>
    internal static bool RecoverFromLoadFailure(Exception exception = null)
    {
        if (!_activeController || !_activeController._loadInProgress)
            return false;

        LoadingScreenController controller = _activeController;
        controller._transitionCompleted = null;
        controller._reconstructContent = false;
        controller._recoveringFromFailure = true;
        controller._view?.ShowLoadFailure(exception?.Message);

        if (exception != null)
            Debug.LogException(exception);

        if (!Application.isPlaying)
        {
            controller._loadInProgress = false;
            DestroyImmediate(controller.gameObject);
            return true;
        }

        AppBootstrap bootstrap = controller._bootstrap;
        if (!bootstrap)
            bootstrap = AppBootstrap.Instance;
        if (bootstrap == null)
        {
            controller._loadInProgress = false;
            controller._view?.SetVisible(false);
            return false;
        }

        bootstrap.GetRuntime()?.EndGame();
        if (controller._recoveryRoutine == null)
        {
            controller._recoveryRoutine = controller.StartCoroutine(
                controller.ReturnToMainMenu(bootstrap)
            );
        }
        return true;
    }

    /// <summary>
    /// Waits for the incoming strategy scene transition to release the shared scene loader before
    /// starting recovery navigation.
    /// </summary>
    /// <param name="bootstrap">The active application bootstrap.</param>
    /// <returns>The recovery-navigation coroutine.</returns>
    private IEnumerator ReturnToMainMenu(AppBootstrap bootstrap)
    {
        yield return new WaitForSecondsRealtime(_failureDisplayDuration);

        while (bootstrap.IsSceneTransitionInProgress)
            yield return null;

        if (!bootstrap.LoadScene("MainMenu", HandleRecoverySceneFailure))
        {
            HandleRecoverySceneFailure(
                new InvalidOperationException(
                    "The main-menu recovery scene could not start loading."
                )
            );
        }
        _recoveryRoutine = null;
    }

    /// <summary>
    /// Hides a failed recovery presentation when the main menu cannot be loaded.
    /// </summary>
    /// <param name="exception">The recovery-scene failure.</param>
    private void HandleRecoverySceneFailure(Exception exception)
    {
        Debug.LogException(exception);
        _loadInProgress = false;
        _recoveringFromFailure = false;
        _view?.SetVisible(false);
    }

    /// <summary>
    /// Clears the active-controller registration when the transition finishes.
    /// </summary>
    private void OnDestroy()
    {
        if (_view != null)
            _view.ContinueRequested -= HandleContinueRequested;

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

        Task<SaveLoadPreparation> preparationTask;
        try
        {
            preparationTask = _bootstrap.PrepareSaveLoadAsync(_saveFileName);
        }
        catch (Exception exception)
        {
            RecoverFromLoadFailure(exception);
            yield break;
        }

        while (!preparationTask.IsCompleted)
            yield return null;

        if (preparationTask.IsCanceled || preparationTask.IsFaulted)
        {
            RecoverFromLoadFailure(
                preparationTask.Exception?.GetBaseException()
                    ?? new InvalidOperationException("Saved-game preparation was canceled.")
            );
            yield break;
        }

        try
        {
            _reconstructContent = _bootstrap.ApplySaveLoadPreparation(preparationTask.Result);
        }
        catch (Exception exception)
        {
            RecoverFromLoadFailure(exception);
            yield break;
        }

        _bootstrap.SuspendForSaveLoad();

        if (_reconstructContent)
        {
            if (AudioManager.Instance != null)
                Destroy(AudioManager.Instance.gameObject);
            Destroy(_bootstrap.gameObject);
            yield return null;

            Exception reconstructionFailure = null;
            try
            {
                _bootstrap = AppBootstrap.EnsureExists();
            }
            catch (Exception exception)
            {
                reconstructionFailure = exception;
            }

            if (reconstructionFailure != null)
            {
                RecoverFromLoadFailure(reconstructionFailure);
                yield break;
            }
        }

        if (!_bootstrap.LoadScene("StrategyView", HandleStrategySceneFailure))
        {
            HandleStrategySceneFailure(
                new InvalidOperationException("The strategy scene could not start loading.")
            );
        }
    }

    /// <summary>
    /// Returns a failed strategy-scene transition to the application recovery path.
    /// </summary>
    /// <param name="exception">The scene-transition failure.</param>
    private void HandleStrategySceneFailure(Exception exception)
    {
        RecoverFromLoadFailure(exception);
    }

    /// <summary>
    /// Begins removing the loading presentation after the player accepts the ready state.
    /// </summary>
    private void HandleContinueRequested()
    {
        if (!_loadInProgress || _recoveringFromFailure)
            return;

        BeginFinishTransition();
    }

    /// <summary>
    /// Starts the exit transition once and only after the loaded scene is ready.
    /// </summary>
    private void BeginFinishTransition()
    {
        if (_view == null || _finishRoutine != null)
            return;

        _finishRoutine = StartCoroutine(FinishTransition());
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
        _recoveringFromFailure = false;
        _saveFileName = null;
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
