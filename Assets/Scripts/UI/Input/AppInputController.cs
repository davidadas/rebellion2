using System.Collections.Generic;
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
    public void Initialize(InputManager inputManager, CancelStack cancelStack, GameRuntime runtime)
    {
        if (_inputManager != null)
            DetachInputActions();

        _inputManager = inputManager;
        _cancelStack = cancelStack;
        _runtime = runtime;

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
    /// Handles the cancel or settings action.
    /// </summary>
    /// <param name="context">The input callback context.</param>
    public void OnCancelOrSettings(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (CurrentContext == InputContext.Cutscene)
            return;

        if (_cancelStack?.TryCancel() == true)
            return;

        _runtime?.ToggleSettingsMenu();
    }

    /// <summary>
    /// Handles the quick save action.
    /// </summary>
    /// <param name="context">The input callback context.</param>
    public void OnQuickSave(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (!CanUseGameplayShortcuts())
            return;

        _runtime?.QuickSave();
    }

    /// <summary>
    /// Handles the quick load action.
    /// </summary>
    /// <param name="context">The input callback context.</param>
    public void OnQuickLoad(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (!CanUseGameplayShortcuts())
            return;

        _runtime?.QuickLoad();
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
        gameManager?.SetGameSpeed(GetSlowerGameSpeed(gameManager.GetGameSpeed()));
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
        gameManager?.SetGameSpeed(GetFasterGameSpeed(gameManager.GetGameSpeed()));
    }

    /// <summary>
    /// Returns the next slower supported game speed.
    /// </summary>
    /// <param name="speed">The current game speed.</param>
    /// <returns>The next slower speed, bounded at paused.</returns>
    internal static TickSpeed GetSlowerGameSpeed(TickSpeed speed)
    {
        return speed switch
        {
            TickSpeed.Fast => TickSpeed.Medium,
            TickSpeed.Medium => TickSpeed.Slow,
            TickSpeed.Slow => TickSpeed.VerySlow,
            _ => TickSpeed.Paused,
        };
    }

    /// <summary>
    /// Returns the next faster supported game speed.
    /// </summary>
    /// <param name="speed">The current game speed.</param>
    /// <returns>The next faster speed, bounded at fast.</returns>
    internal static TickSpeed GetFasterGameSpeed(TickSpeed speed)
    {
        return speed switch
        {
            TickSpeed.Paused => TickSpeed.VerySlow,
            TickSpeed.VerySlow => TickSpeed.Slow,
            TickSpeed.Slow => TickSpeed.Medium,
            _ => TickSpeed.Fast,
        };
    }

    /// <summary>
    /// Returns whether gameplay shortcut actions are allowed in the current input context.
    /// </summary>
    /// <returns>True when gameplay shortcuts can run; otherwise false.</returns>
    private bool CanUseGameplayShortcuts()
    {
        return CurrentContext == InputContext.Strategy
            || CurrentContext == InputContext.Tactical
            || CurrentContext == InputContext.None;
    }

    /// <summary>
    /// Ensures the context stack always has a default context.
    /// </summary>
    private void EnsureBaseContext()
    {
        if (_contextStack.Count == 0)
            _contextStack.Push(InputContext.None);
    }
}
