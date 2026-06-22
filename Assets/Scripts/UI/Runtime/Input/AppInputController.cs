using Rebellion.Input;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class AppInputController : MonoBehaviour, PlayerInputActions.IGlobalActions
{
    private InputActionsManager inputActionsManager;
    private CancelStack cancelStack;
    private GameRuntime runtime;

    public void Initialize(
        InputActionsManager inputActionsManager,
        CancelStack cancelStack,
        GameRuntime runtime
    )
    {
        if (this.inputActionsManager != null)
            DisableCurrentInputActions();

        this.inputActionsManager = inputActionsManager;
        this.cancelStack = cancelStack;
        this.runtime = runtime;

        this.inputActionsManager.Actions.Global.AddCallbacks(this);
        this.inputActionsManager.Actions.Global.Enable();
    }

    private void OnDestroy()
    {
        DisableCurrentInputActions();
    }

    private void DisableCurrentInputActions()
    {
        if (
            inputActionsManager == null
            || !inputActionsManager.TryGetActions(out PlayerInputActions actions)
        )
            return;

        actions.Global.RemoveCallbacks(this);
        actions.Global.Disable();
    }

    public void OnCancelOrSettings(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        cancelStack?.TryCancel();
    }

    public void OnQuickSave(InputAction.CallbackContext context)
    {
        if (context.performed)
            runtime?.QuickSave();
    }

    public void OnQuickLoad(InputAction.CallbackContext context)
    {
        if (context.performed)
            runtime?.QuickLoad();
    }
}
