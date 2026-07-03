using UnityEngine;

/// <summary>
/// Owns the generated input action asset and binding override persistence.
/// </summary>
public sealed class InputActionsManager : MonoBehaviour
{
    private Rebellion.Input.PlayerInputActions _actions;

    /// <summary>
    /// Gets the generated input action wrapper.
    /// </summary>
    public Rebellion.Input.PlayerInputActions Actions =>
        _actions ??= new Rebellion.Input.PlayerInputActions();

    /// <summary>
    /// Gets the generated input action asset.
    /// </summary>
    public UnityEngine.InputSystem.InputActionAsset Asset => Actions.asset;

    /// <summary>
    /// Attempts to return the generated input action wrapper without creating it.
    /// </summary>
    /// <param name="actions">The generated input action wrapper when one has been created.</param>
    /// <returns>True when input actions have been created; otherwise false.</returns>
    public bool TryGetActions(out Rebellion.Input.PlayerInputActions actions)
    {
        actions = _actions;
        return actions != null;
    }

    /// <summary>
    /// Disables and disposes the generated input action wrapper.
    /// </summary>
    private void OnDestroy()
    {
        if (_actions == null)
            return;

        DisableAllActionMaps();
        _actions.Dispose();
        _actions = null;
    }

    /// <summary>
    /// Disables every action map on the generated input action asset.
    /// </summary>
    private void DisableAllActionMaps()
    {
        foreach (UnityEngine.InputSystem.InputActionMap actionMap in _actions.asset.actionMaps)
            actionMap.Disable();
    }

    /// <summary>
    /// Saves all runtime binding overrides.
    /// </summary>
    /// <returns>The serialized binding override data.</returns>
    public string SaveBindingOverrides()
    {
        return UnityEngine.InputSystem.InputActionRebindingExtensions.SaveBindingOverridesAsJson(
            Actions.asset
        );
    }

    /// <summary>
    /// Replaces runtime binding overrides with the supplied override data.
    /// </summary>
    /// <param name="bindingOverrides">The serialized binding override data.</param>
    public void LoadBindingOverrides(string bindingOverrides)
    {
        UnityEngine.InputSystem.InputActionRebindingExtensions.RemoveAllBindingOverrides(
            Actions.asset
        );

        if (string.IsNullOrWhiteSpace(bindingOverrides))
            return;

        UnityEngine.InputSystem.InputActionRebindingExtensions.LoadBindingOverridesFromJson(
            Actions.asset,
            bindingOverrides
        );
    }
}
