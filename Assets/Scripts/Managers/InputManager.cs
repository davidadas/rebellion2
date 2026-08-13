using Rebellion.Input;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the generated input action asset and binding override persistence.
/// </summary>
public sealed class InputManager : MonoBehaviour
{
    private PlayerInputActions _actions;

    /// <summary>
    /// Gets the generated input action wrapper.
    /// </summary>
    public PlayerInputActions Actions
    {
        get { return _actions ??= new PlayerInputActions(); }
    }

    /// <summary>
    /// Gets the generated input action asset.
    /// </summary>
    public InputActionAsset Asset => Actions.asset;

    /// <summary>
    /// Attempts to return the generated input action wrapper without creating it.
    /// </summary>
    /// <param name="actions">The generated input action wrapper when one has been created.</param>
    /// <returns>True when input actions have been created; otherwise false.</returns>
    public bool TryGetActions(out PlayerInputActions actions)
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
        foreach (InputActionMap actionMap in _actions.asset.actionMaps)
            actionMap.Disable();
    }

    /// <summary>
    /// Saves all runtime binding overrides.
    /// </summary>
    /// <returns>The serialized binding override data.</returns>
    public string SaveBindingOverrides()
    {
        RestoreReservedEscapeBinding(Actions.asset);
        return Actions.asset.SaveBindingOverridesAsJson();
    }

    /// <summary>
    /// Replaces runtime binding overrides with the supplied override data.
    /// </summary>
    /// <param name="bindingOverrides">The serialized binding override data.</param>
    public void LoadBindingOverrides(string bindingOverrides)
    {
        InputActionAsset asset = Actions.asset;
        asset.RemoveAllBindingOverrides();
        if (!string.IsNullOrWhiteSpace(bindingOverrides))
            asset.LoadBindingOverridesFromJson(bindingOverrides);
        RestoreReservedEscapeBinding(asset);
    }

    /// <summary>
    /// Removes persisted overrides from the fixed Escape binding and its chord alternative.
    /// </summary>
    private static void RestoreReservedEscapeBinding(InputActionAsset asset)
    {
        InputAction action = asset.FindAction("Global/CancelOrSettings", true);
        bool clearingPrimaryChord = false;
        for (int index = 0; index < action.bindings.Count; index++)
        {
            InputBinding binding = action.bindings[index];
            if (!binding.isPartOfComposite)
            {
                clearingPrimaryChord = binding.name == "PrimaryChord";
                if (binding.name == "Primary" || clearingPrimaryChord)
                    action.RemoveBindingOverride(index);
                continue;
            }

            if (clearingPrimaryChord)
                action.RemoveBindingOverride(index);
        }
    }
}
