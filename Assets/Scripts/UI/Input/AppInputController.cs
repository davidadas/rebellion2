using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Input;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Routes global input actions to application-level runtime commands.
/// </summary>
public sealed class AppInputController : MonoBehaviour, PlayerInputActions.IGlobalActions
{
    private readonly Stack<InputContext> _contextStack = new();
    private InputManager _inputManager;
    private CancelStack _cancelStack;
    private GameRuntime _runtime;
    private Func<string, Task<bool>> _loadSave;
    private int _shortcutSuppressionCount;

    /// <summary>
    /// Occurs when global input requests the scene's Options menu.
    /// </summary>
    public event Action OptionsMenuRequested;

    /// <summary>
    /// Gets whether a modal input owner currently suppresses menu and gameplay shortcuts.
    /// </summary>
    public bool ShortcutsSuppressed => _shortcutSuppressionCount > 0;

    /// <summary>
    /// Gets the current input context.
    /// </summary>
    private InputContext CurrentContext =>
        _contextStack.Count > 0 ? _contextStack.Peek() : InputContext.None;

    /// <summary>
    /// Initializes the input context stack.
    /// </summary>
    private void Awake()
    {
        EnsureBaseContext();
    }

    /// <summary>
    /// Detaches global input handlers.
    /// </summary>
    private void OnDestroy()
    {
        DetachInputActions();
    }

    /// <summary>
    /// Connects this controller to input actions and runtime commands.
    /// </summary>
    /// <param name="inputManager">The input manager to listen to.</param>
    /// <param name="cancelStack">The cancel stack used before opening settings.</param>
    /// <param name="runtime">The runtime that receives global commands.</param>
    /// <param name="loadSave">Starts the application saved-game transition.</param>
    public void Initialize(
        InputManager inputManager,
        CancelStack cancelStack,
        GameRuntime runtime,
        Func<string, Task<bool>> loadSave = null
    )
    {
        if (_inputManager != null)
            DetachInputActions();

        _inputManager = inputManager;
        _cancelStack = cancelStack;
        _runtime = runtime;
        _loadSave = loadSave;

        EnsureBaseContext();
        AttachInputActions();
    }

    /// <summary>
    /// Replaces the current input context.
    /// </summary>
    /// <param name="context">The context to make active.</param>
    public void SetContext(InputContext context)
    {
        _contextStack.Clear();
        _contextStack.Push(context);
    }

    /// <summary>
    /// Pushes an input context onto the context stack.
    /// </summary>
    /// <param name="context">The context to push.</param>
    public void PushContext(InputContext context)
    {
        _contextStack.Push(context);
    }

    /// <summary>
    /// Restores the previous input context.
    /// </summary>
    public void PopContext()
    {
        if (_contextStack.Count > 1)
            _contextStack.Pop();
    }

    /// <summary>
    /// Suppresses global menu and gameplay shortcuts until this independent input owner releases its lease.
    /// UI submission and cancellation routing remain available to the modal presentation.
    /// </summary>
    /// <returns>An idempotent lease that does not modify the scene's input context.</returns>
    public IDisposable SuppressShortcuts()
    {
        _shortcutSuppressionCount++;
        return new ShortcutSuppression(this);
    }

    /// <summary>
    /// Attaches global action handlers to the input action asset.
    /// </summary>
    private void AttachInputActions()
    {
        if (_inputManager == null)
            return;

        _inputManager.Actions.Global.AddCallbacks(this);
        _inputManager.Actions.Global.Enable();
    }

    /// <summary>
    /// Detaches global action handlers from the input action asset.
    /// </summary>
    private void DetachInputActions()
    {
        if (_inputManager == null || !_inputManager.TryGetActions(out PlayerInputActions actions))
            return;

        actions.Global.RemoveCallbacks(this);
        actions.Global.Disable();
    }

    /// <summary>
    /// Handles the cancel action.
    /// </summary>
    /// <param name="context">The input callback context.</param>
    public void OnCancelOrSettings(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (CurrentContext == InputContext.Cutscene || LoadingScreenController.IsLoadInProgress)
            return;

        if (Keyboard.current?.shiftKey.isPressed == true)
            return;

        _cancelStack?.TryCancel();
    }

    /// <summary>
    /// Handles the dedicated game-menu shortcut.
    /// </summary>
    /// <param name="context">The input callback context.</param>
    public void OnOpenGameMenu(InputAction.CallbackContext context)
    {
        if (
            !context.performed
            || _shortcutSuppressionCount > 0
            || CurrentContext == InputContext.Cutscene
            || LoadingScreenController.IsLoadInProgress
        )
            return;

        OptionsMenuRequested?.Invoke();
    }

    /// <summary>
    /// Handles the quick save action.
    /// </summary>
    /// <param name="context">The input callback context.</param>
    public async void OnQuickSave(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (!CanUseGameplayShortcuts())
            return;

        try
        {
            if (_runtime != null)
                await _runtime.QuickSaveAsync();
        }
        catch (OperationCanceledException)
        {
            // Closing a coordinated session cancels requests whose completion is no longer known.
        }
    }

    /// <summary>
    /// Handles the quick load action.
    /// </summary>
    /// <param name="context">The input callback context.</param>
    public async void OnQuickLoad(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (!CanUseGameplayShortcuts())
            return;

        try
        {
            if (_loadSave != null)
                await _loadSave(SaveGameManager.QuickSaveFileName);
        }
        catch (OperationCanceledException)
        {
            // Closing a coordinated session cancels requests whose completion is no longer known.
        }
    }

    /// <summary>
    /// Handles the decrease game speed action.
    /// </summary>
    /// <param name="context">The input callback context.</param>
    public void OnDecreaseGameSpeed(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (!CanUseGameplayShortcuts())
            return;

        GameManager gameManager = _runtime?.GetActiveGameManager();
        gameManager?.StepGameSpeed(false);
    }

    /// <summary>
    /// Handles the increase game speed action.
    /// </summary>
    /// <param name="context">The input callback context.</param>
    public void OnIncreaseGameSpeed(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (!CanUseGameplayShortcuts())
            return;

        GameManager gameManager = _runtime?.GetActiveGameManager();
        gameManager?.StepGameSpeed(true);
    }

    /// <summary>
    /// Returns the next slower supported game speed.
    /// </summary>
    /// <param name="speed">The current game speed.</param>
    /// <returns>The next slower speed, bounded at paused.</returns>
    internal static TickSpeed GetSlowerGameSpeed(TickSpeed speed)
    {
        return GameManager.GetAdjacentGameSpeed(speed, false);
    }

    /// <summary>
    /// Returns the next faster supported game speed.
    /// </summary>
    /// <param name="speed">The current game speed.</param>
    /// <returns>The next faster speed, bounded at fast.</returns>
    internal static TickSpeed GetFasterGameSpeed(TickSpeed speed)
    {
        return GameManager.GetAdjacentGameSpeed(speed, true);
    }

    /// <summary>
    /// Returns whether gameplay shortcut actions are allowed in the current input context.
    /// </summary>
    /// <returns>True when gameplay shortcuts can run; otherwise false.</returns>
    private bool CanUseGameplayShortcuts()
    {
        return _shortcutSuppressionCount == 0
            && !UIInputFocus.IsTextEntryActive()
            && !LoadingScreenController.IsLoadInProgress
            && (
                CurrentContext == InputContext.Strategy
                || CurrentContext == InputContext.Tactical
                || CurrentContext == InputContext.None
            );
    }

    /// <summary>
    /// Ensures the context stack always has a default context.
    /// </summary>
    private void EnsureBaseContext()
    {
        if (_contextStack.Count == 0)
            _contextStack.Push(InputContext.None);
    }

    /// <summary>
    /// Releases only the shortcut suppression owned by one modal lifetime.
    /// </summary>
    private sealed class ShortcutSuppression : IDisposable
    {
        private AppInputController _owner;

        /// <summary>
        /// Retains the input controller whose suppression has already been acquired.
        /// </summary>
        /// <param name="owner">The input controller owning the suppression count.</param>
        public ShortcutSuppression(AppInputController owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// Releases this lease once, preserving any other owner's suppression and scene context.
        /// </summary>
        public void Dispose()
        {
            AppInputController owner = _owner;
            _owner = null;
            if (owner != null)
                owner._shortcutSuppressionCount--;
        }
    }
}
