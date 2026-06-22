using UnityEngine;

public sealed class InputActionsManager : MonoBehaviour
{
    private Rebellion.Input.PlayerInputActions _actions;

    public Rebellion.Input.PlayerInputActions Actions =>
        _actions ??= new Rebellion.Input.PlayerInputActions();

    public UnityEngine.InputSystem.InputActionAsset Asset => Actions.asset;

    public bool TryGetActions(out Rebellion.Input.PlayerInputActions actions)
    {
        actions = _actions;
        return actions != null;
    }

    private void OnDestroy()
    {
        if (_actions == null)
            return;

        DisableAllActionMaps();
        _actions.Dispose();
        _actions = null;
    }

    private void DisableAllActionMaps()
    {
        foreach (UnityEngine.InputSystem.InputActionMap actionMap in _actions.asset.actionMaps)
            actionMap.Disable();
    }

    public string SaveBindingOverrides()
    {
        return UnityEngine.InputSystem.InputActionRebindingExtensions.SaveBindingOverridesAsJson(
            Actions.asset
        );
    }

    public void LoadBindingOverrides(string bindingOverrides)
    {
        if (string.IsNullOrWhiteSpace(bindingOverrides))
            return;

        UnityEngine.InputSystem.InputActionRebindingExtensions.LoadBindingOverridesFromJson(
            Actions.asset,
            bindingOverrides
        );
    }
}
