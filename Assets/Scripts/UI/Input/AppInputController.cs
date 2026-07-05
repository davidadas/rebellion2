using System.Collections.Generic;
using Rebellion.Input;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Routes global input actions to application-level runtime commands.
/// </summary>
public sealed class AppInputController : MonoBehaviour
{
    private readonly Stack<InputContext> _contextStack = new();
    private InputManager _inputManager;
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
        _contextStack.Push(InputContext.None);
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
    /// <param name="runtime">The runtime that receives global commands.</param>
    public void Initialize(InputManager inputManager, GameRuntime runtime)
    {
        if (_inputManager != null)
            DetachInputActions();

        _inputManager = inputManager;
        _runtime = runtime;

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

        PlayerInputActions.GlobalActions globalActions = _inputManager.Actions.Global;
        globalActions.CancelOrSettings.performed += OnCancelOrSettings;
        globalActions.QuickSave.performed += OnQuickSave;
        globalActions.QuickLoad.performed += OnQuickLoad;
        globalActions.Enable();
    }

    /// <summary>
    /// Detaches global action handlers from the input action asset.
    /// </summary>
    private void DetachInputActions()
    {
        if (_inputManager == null || !_inputManager.TryGetActions(out PlayerInputActions actions))
            return;

        PlayerInputActions.GlobalActions globalActions = actions.Global;
        globalActions.CancelOrSettings.performed -= OnCancelOrSettings;
        globalActions.QuickSave.performed -= OnQuickSave;
        globalActions.QuickLoad.performed -= OnQuickLoad;
        globalActions.Disable();
    }

    /// <summary>
    /// Handles the cancel or settings action.
    /// </summary>
    /// <param name="context">The input callback context.</param>
    private void OnCancelOrSettings(InputAction.CallbackContext context)
    {
        if (CurrentContext == InputContext.Cutscene)
            return;

        _runtime?.ToggleSettingsMenu();
    }

    /// <summary>
    /// Handles the quick save action.
    /// </summary>
    /// <param name="context">The input callback context.</param>
    private void OnQuickSave(InputAction.CallbackContext context)
    {
        if (!CanUseGameplayShortcuts())
            return;

        _runtime?.QuickSave();
    }

    /// <summary>
    /// Handles the quick load action.
    /// </summary>
    /// <param name="context">The input callback context.</param>
    private void OnQuickLoad(InputAction.CallbackContext context)
    {
        if (!CanUseGameplayShortcuts())
            return;

        _runtime?.QuickLoad();
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
}
