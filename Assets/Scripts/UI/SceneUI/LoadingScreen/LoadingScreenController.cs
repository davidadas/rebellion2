using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Coordinates saved-game loading and reconstructs application content only when required.
/// </summary>
internal sealed class LoadingScreenController : MonoBehaviour
{
    private const string _objectName = "Loading Screen Controller";

    private static LoadingScreenController _activeController;

    private AppBootstrap _bootstrap;
    private Action _transitionCompleted;
    private Coroutine _recoveryRoutine;
    private string _saveFileName;
    private bool _reconstructContent;
    private bool _loadInProgress;

    /// <summary>
    /// Gets whether a saved-game loading transition is currently active.
    /// </summary>
    internal static bool IsLoadInProgress => _activeController && _activeController._loadInProgress;

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
        controller._transitionCompleted = null;
        controller._reconstructContent = false;
        controller._loadInProgress = true;

        if (!Application.isPlaying)
        {
            AudioManager.Instance?.StopMusic();
            SaveLoadPreparation preparation = bootstrap
                .PrepareSaveLoadAsync(fileName)
                .GetAwaiter()
                .GetResult();
            controller._reconstructContent = bootstrap.ApplySaveLoadPreparation(preparation);
            bootstrap.SuspendForSaveLoad();
            return;
        }

        AudioManager.Instance?.StopMusic();
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
    /// Releases the active loading transition after replacement gameplay is ready.
    /// </summary>
    /// <param name="transitionCompleted">The action to run after loading completes.</param>
    internal static void CompletePendingLoad(Action transitionCompleted = null)
    {
        LoadingScreenController controller = _activeController;
        if (controller?._loadInProgress != true)
            return;

        if (!Application.isPlaying)
        {
            controller._loadInProgress = false;
            DestroyImmediate(controller.gameObject);
            transitionCompleted?.Invoke();
            return;
        }

        controller._transitionCompleted = transitionCompleted;
        controller.ReleaseTransition();
    }

    /// <summary>
    /// Returns a failed save load to the main menu.
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
            controller.EnterTerminalFailure();
            return true;
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
    /// Releases a failed recovery transition after the main-menu scene is available.
    /// </summary>
    /// <param name="exception">The main-menu initialization failure.</param>
    /// <returns>True when an active loading transition was released.</returns>
    internal static bool CompleteFailedRecovery(Exception exception)
    {
        if (!_activeController || !_activeController._loadInProgress)
            return false;

        if (exception != null)
            Debug.LogException(exception);

        LoadingScreenController controller = _activeController;
        controller._transitionCompleted = null;
        if (!Application.isPlaying)
        {
            controller._loadInProgress = false;
            DestroyImmediate(controller.gameObject);
            return true;
        }

        controller.ReleaseTransition();
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
    /// Releases a failed recovery when the main menu cannot be loaded.
    /// </summary>
    /// <param name="exception">The recovery-scene failure.</param>
    private void HandleRecoverySceneFailure(Exception exception)
    {
        Debug.LogException(exception);
        EnterTerminalFailure();
    }

    /// <summary>
    /// Releases loading state when application recovery cannot continue.
    /// </summary>
    private void EnterTerminalFailure()
    {
        _transitionCompleted = null;
        _loadInProgress = false;
        if (Application.isPlaying)
            Destroy(gameObject);
    }

    /// <summary>
    /// Clears the active-controller registration when the transition finishes.
    /// </summary>
    private void OnDestroy()
    {
        _transitionCompleted = null;

        if (_activeController == this)
            _activeController = null;
    }

    /// <summary>
    /// Clears loading state, invokes the pending transition callback, and removes the coordinator.
    /// </summary>
    private void ReleaseTransition()
    {
        _loadInProgress = false;
        _saveFileName = null;
        Action transitionCompleted = _transitionCompleted;
        _transitionCompleted = null;
        if (_activeController == this)
            _activeController = null;
        transitionCompleted?.Invoke();
        Destroy(gameObject);
    }

    /// <summary>
    /// Optionally replaces content services and opens the strategy scene for the pending save.
    /// </summary>
    /// <returns>The saved-game transition coroutine.</returns>
    private IEnumerator LoadStrategy()
    {
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
}
