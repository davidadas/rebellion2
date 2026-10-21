using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Presents saved-game and main-menu content transitions independently from the outgoing scene.
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
    private Action _continueAccepted;
    private LoadingScreenView _view;
    private Coroutine _finishRoutine;
    private Coroutine _prepareRoutine;
    private Coroutine _recoveryRoutine;
    private Task _preparationTask;
    private ContentLoadOperation _operation;
    private bool _loadInProgress;
    private bool _quitOnContinue;

    /// <summary>
    /// Gets whether a saved-game or content loading transition is currently active.
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

        if (IsLoadInProgress)
            return;
        ContentLoadOperation operation = bootstrap.CreateContentLoadOperation();
        BeginLoad(
            bootstrap,
            operation,
            operation.PrepareSavedGameAsync(SaveGameManager.Instance, fileName)
        );
    }

    /// <summary>
    /// Reloads verified content and returns to the main menu within the running application.
    /// </summary>
    /// <param name="bootstrap">The bootstrap that owns the outgoing content.</param>
    /// <param name="packID">The installed content pack to select.</param>
    /// <param name="disabledModIDs">The complete pending disabled-mod preferences.</param>
    internal static void ReloadContent(
        AppBootstrap bootstrap,
        string packID,
        string[] disabledModIDs
    )
    {
        if (bootstrap == null)
            throw new ArgumentNullException(nameof(bootstrap));
        if (IsLoadInProgress)
            return;
        ContentLoadOperation operation = bootstrap.CreateContentLoadOperation();
        BeginLoad(
            bootstrap,
            operation,
            operation.PrepareContentReloadAsync(packID, disabledModIDs)
        );
    }

    /// <summary>
    /// Starts a custom game through the shared content-reconstruction and recovery lifecycle.
    /// </summary>
    /// <param name="bootstrap">The bootstrap that owns the outgoing content.</param>
    /// <param name="contentPack">The selected custom-game content.</param>
    /// <param name="setup">The complete custom-game configuration.</param>
    internal static void StartCustomGame(
        AppBootstrap bootstrap,
        ContentPack contentPack,
        Rebellion.Game.GameSetup setup
    )
    {
        if (bootstrap == null)
            throw new ArgumentNullException(nameof(bootstrap));
        if (IsLoadInProgress)
            return;
        ContentLoadOperation operation = bootstrap.CreateContentLoadOperation();
        operation.PrepareCustomGame(contentPack, setup);
        BeginLoad(bootstrap, operation, Task.CompletedTask);
    }

    /// <summary>
    /// Initializes the shared persistent presentation for a scene or content transition.
    /// </summary>
    /// <param name="bootstrap">The bootstrap that owns the outgoing content.</param>
    /// <param name="operation">The application operation that owns this load's decisions.</param>
    /// <param name="preparation">The operation's pending preparation.</param>
    private static void BeginLoad(
        AppBootstrap bootstrap,
        ContentLoadOperation operation,
        Task preparation
    )
    {
        LoadingScreenController controller = GetOrCreateController();
        if (controller._loadInProgress)
            return;

        controller._bootstrap = bootstrap;
        controller._operation = operation;
        controller._preparationTask = preparation;
        controller._transitionCompleted = null;
        controller._continueAccepted = null;
        controller._quitOnContinue = false;
        controller.EnsurePresentation();
        controller._loadInProgress = true;

        if (!Application.isPlaying)
        {
            AudioManager.Instance?.StopMusic();
            controller._view.SetVisible(true);
            preparation.GetAwaiter().GetResult();
            controller.PrepareDestinationLaunch();
            bootstrap.SuspendForLoad();
            return;
        }

        AudioManager.Instance?.FadeOutMusic(_fadeToBlackDuration);
        controller._view.PrepareEntrance();
        controller.StartCoroutine(controller.LoadContent());
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
    /// <param name="bootstrap">The application whose scene completed initialization.</param>
    /// <param name="transitionCompleted">The action to run after the loading view is gone.</param>
    /// <param name="presentationReady">The action to run when the loading view becomes ready.</param>
    /// <param name="continueAccepted">The action to run when the player accepts the ready state.</param>
    /// <returns>True when completion was accepted for the current application services.</returns>
    internal static bool CompletePendingLoad(
        AppBootstrap bootstrap,
        Action transitionCompleted = null,
        Action presentationReady = null,
        Action continueAccepted = null
    )
    {
        if (
            !_activeController
            || !_activeController._loadInProgress
            || _activeController._bootstrap != bootstrap
        )
            return false;

        LoadingScreenController controller = _activeController;
        try
        {
            controller._operation.Complete(
                bootstrap.GetContentPack(),
                bootstrap.GetUserSettingsManager()
            );
        }
        catch (Exception exception)
        {
            RecoverFromLoadFailure(exception);
            return false;
        }

        if (!Application.isPlaying)
        {
            DestroyImmediate(_activeController.gameObject);
            return true;
        }

        _activeController._transitionCompleted = transitionCompleted;
        _activeController._continueAccepted = continueAccepted;

        if (controller._operation.DestinationScene == "MainMenu")
        {
            _activeController.BeginFinishTransition();
        }
        else if (_activeController._view != null)
        {
            _activeController._view.ShowReady();
            presentationReady?.Invoke();
        }
        else if (_activeController._view == null)
        {
            _activeController._loadInProgress = false;
            _activeController._quitOnContinue = false;
            Action completed = _activeController._transitionCompleted;
            _activeController._transitionCompleted = null;
            completed?.Invoke();
        }
        return true;
    }

    /// <summary>
    /// Keeps the loading presentation active while a failed load returns to the main menu.
    /// </summary>
    /// <param name="exception">The optional failure shown and logged during recovery.</param>
    /// <returns>True when an active loading transition began recovery.</returns>
    internal static bool RecoverFromLoadFailure(Exception exception = null)
    {
        if (!_activeController || !_activeController._loadInProgress)
            return false;

        LoadingScreenController controller = _activeController;
        if (exception != null)
            Debug.LogException(exception);
        if (!controller._operation.BeginRecovery())
        {
            controller.EnterTerminalFailure(exception);
            return true;
        }

        controller._transitionCompleted = null;
        controller._continueAccepted = null;
        controller._quitOnContinue = false;
        controller._view?.ShowLoadFailure(exception?.Message);

        try
        {
            controller._bootstrap?.GetRuntime()?.EndGame();
        }
        catch (Exception cleanupFailure)
        {
            Debug.LogException(cleanupFailure);
            controller.EnterTerminalFailure(cleanupFailure);
            return true;
        }

        if (!Application.isPlaying)
        {
            controller._loadInProgress = false;
            DestroyImmediate(controller.gameObject);
            return true;
        }

        if (controller._recoveryRoutine == null)
        {
            controller._recoveryRoutine = controller.StartCoroutine(controller.ReturnToMainMenu());
        }
        return true;
    }

    /// <summary>
    /// Routes main-menu initialization failures through the active transition's recovery policy.
    /// </summary>
    /// <param name="bootstrap">The application whose menu failed initialization.</param>
    /// <param name="exception">The main-menu initialization failure.</param>
    /// <returns>True when the active transition handled the failure.</returns>
    internal static bool CompleteFailedRecovery(AppBootstrap bootstrap, Exception exception)
    {
        if (!_activeController || _activeController._bootstrap != bootstrap)
            return false;

        return RecoverFromLoadFailure(exception);
    }

    /// <summary>
    /// Waits for the incoming strategy scene transition to release the shared scene loader before
    /// starting recovery navigation.
    /// </summary>
    /// <returns>The recovery-navigation coroutine.</returns>
    private IEnumerator ReturnToMainMenu()
    {
        yield return new WaitForSecondsRealtime(_failureDisplayDuration);

        if (_operation.RequiresContentRestart)
        {
            Exception reconstructionFailure = null;
            yield return ReconstructBootstrap(exception => reconstructionFailure = exception);
            if (reconstructionFailure != null)
            {
                HandleRecoverySceneFailure(reconstructionFailure);
                yield break;
            }
        }

        if (!_bootstrap)
            _bootstrap = AppBootstrap.Instance;
        if (!_bootstrap)
        {
            HandleRecoverySceneFailure(
                new InvalidOperationException(
                    "Application services are unavailable during recovery."
                )
            );
            yield break;
        }

        try
        {
            _bootstrap.GetRuntime()?.EndGame();
        }
        catch (Exception exception)
        {
            HandleRecoverySceneFailure(exception);
            yield break;
        }

        while (_bootstrap.IsSceneTransitionInProgress)
            yield return null;

        PrepareDestinationLaunch();
        LoadDestinationScene(_operation.DestinationScene, HandleRecoverySceneFailure);
        _recoveryRoutine = null;
    }

    /// <summary>
    /// Keeps an exit action available when returning to the main menu fails.
    /// </summary>
    /// <param name="exception">The recovery-scene failure.</param>
    private void HandleRecoverySceneFailure(Exception exception)
    {
        Debug.LogException(exception);
        EnterTerminalFailure(exception);
    }

    /// <summary>
    /// Keeps the loading presentation interactive when application recovery cannot continue.
    /// </summary>
    /// <param name="exception">The unrecoverable application failure.</param>
    private void EnterTerminalFailure(Exception exception)
    {
        _transitionCompleted = null;
        _continueAccepted = null;
        _operation.Fail();
        _quitOnContinue = true;
        _loadInProgress = true;
        _view?.ShowTerminalFailure(
            _operation.RequiresContentRestart
                ? "Couldn't load the required content or restore your previous setup. "
                    + "Please restart the game and try again."
                : exception?.Message
        );
    }

    /// <summary>
    /// Clears the active-controller registration when the transition finishes.
    /// </summary>
    private void OnDestroy()
    {
        _transitionCompleted = null;
        _continueAccepted = null;
        _quitOnContinue = false;
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
    /// Validates content, optionally replaces application services, and opens the destination scene.
    /// </summary>
    /// <returns>The content transition coroutine.</returns>
    private IEnumerator LoadContent()
    {
        yield return FadeBlack(0f, 1f, _fadeToBlackDuration);
        _view.SetPresentationVisible(true);
        yield return FadeBlack(1f, 0f, _fadeFromBlackDuration);

        while (!_preparationTask.IsCompleted)
            yield return null;

        if (_operation.IsRecovering)
            yield break;

        if (_preparationTask.IsCanceled || _preparationTask.IsFaulted)
        {
            RecoverFromLoadFailure(
                _preparationTask.Exception?.GetBaseException()
                    ?? new InvalidOperationException("Content preparation was canceled.")
            );
            yield break;
        }

        try
        {
            _bootstrap.SuspendForLoad();
        }
        catch (Exception exception)
        {
            RecoverFromLoadFailure(exception);
            yield break;
        }

        if (_operation.RequiresContentRestart)
        {
            Exception reconstructionFailure = null;
            yield return ReconstructBootstrap(exception => reconstructionFailure = exception);

            if (reconstructionFailure != null)
            {
                RecoverFromLoadFailure(reconstructionFailure);
                yield break;
            }
        }

        PrepareDestinationLaunch();
        LoadDestinationScene(_operation.DestinationScene, HandleSceneLoadFailure);
    }

    /// <summary>
    /// Supplies scene launch data after application services have been initialized.
    /// </summary>
    private void PrepareDestinationLaunch()
    {
        GameLaunchContext.Reset(_operation.Pack);
        if (_operation.SaveFileName != null)
            GameLaunchContext.PrepareSaveLoad(_operation.SaveFileName);
        else if (_operation.CustomGameSetup is Rebellion.Game.GameSetup setup)
        {
            GameLaunchContext.ApplyCustomGameSetup(setup);
            GameLaunchContext.PlayIntroCutscene = true;
        }
    }

    /// <summary>
    /// Releases outgoing or partially initialized services before creating the staged bootstrap.
    /// </summary>
    /// <param name="completed">Receives the initialization failure, or null after success.</param>
    /// <returns>The service-reconstruction coroutine.</returns>
    private IEnumerator ReconstructBootstrap(Action<Exception> completed)
    {
        Exception failure = null;
        try
        {
            _bootstrap?.GetRuntime()?.EndGame();
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        if (AudioManager.Instance != null)
            Destroy(AudioManager.Instance.gameObject);
        if (_bootstrap != null)
            Destroy(_bootstrap.gameObject);
        _bootstrap = null;
        yield return null;

        if (failure == null)
        {
            try
            {
                _bootstrap = AppBootstrap.EnsureExists(_operation.Pack);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }
        completed(failure);
    }

    /// <summary>
    /// Routes rejected scene transitions and synchronous initialization failures to their owner.
    /// </summary>
    /// <param name="sceneName">The destination scene.</param>
    /// <param name="failed">The transition's failure handler.</param>
    private void LoadDestinationScene(string sceneName, Action<Exception> failed)
    {
        try
        {
            if (!_bootstrap.LoadScene(sceneName, failed))
                failed(
                    new InvalidOperationException($"The {sceneName} scene could not start loading.")
                );
        }
        catch (Exception exception)
        {
            failed(exception);
        }
    }

    /// <summary>
    /// Returns a failed scene transition to the application recovery path.
    /// </summary>
    /// <param name="exception">The scene-transition failure.</param>
    private void HandleSceneLoadFailure(Exception exception)
    {
        RecoverFromLoadFailure(exception);
    }

    /// <summary>
    /// Begins removing the loading presentation after the player accepts the ready state.
    /// </summary>
    private void HandleContinueRequested()
    {
        if (!_loadInProgress)
            return;

        if (_quitOnContinue)
        {
            Application.Quit();
            return;
        }

        if (_operation.IsRecovering)
            return;

        _continueAccepted?.Invoke();
        _continueAccepted = null;
        BeginFinishTransition();
        _quitOnContinue = false;
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
        if (_operation.IsRecovering && _operation.RequiresContentRestart)
        {
            _view.ShowLoadFailure(
                "Couldn't load the required content. Your previous setup has been restored. "
                    + "Returning to the main menu."
            );
            yield return new WaitForSecondsRealtime(_failureDisplayDuration);
        }

        yield return FadeBlack(0f, 1f, _fadeToBlackDuration);
        _view.SetPresentationVisible(false);
        yield return FadeBlack(1f, 0f, _fadeFromBlackDuration);
        _view.SetVisible(false);
        _loadInProgress = false;
        _preparationTask = null;
        _operation = null;
        _finishRoutine = null;
        Action transitionCompleted = _transitionCompleted;
        _transitionCompleted = null;
        _continueAccepted = null;
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
