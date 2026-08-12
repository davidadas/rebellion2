using System;
using Rebellion.Input;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the generated input action asset and binding override persistence.
/// </summary>
public sealed class InputManager : MonoBehaviour
{
    private PlayerInputActions _actions;
    private InputBindingStore _bindingStore;

    /// <summary>
    /// Gets the generated input action wrapper.
    /// </summary>
    public PlayerInputActions Actions
    {
        get
        {
            KeyboardChordProcessor.EnsureRegistered();
            if (_actions == null)
            {
                _actions = new PlayerInputActions();
                _bindingStore = new InputBindingStore(_actions.asset);
            }

            return _actions;
        }
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
        _ = Actions;
        return _bindingStore.SaveOverrides();
    }

    /// <summary>
    /// Replaces runtime binding overrides with the supplied override data.
    /// </summary>
    /// <param name="bindingOverrides">The serialized binding override data.</param>
    public void LoadBindingOverrides(string bindingOverrides)
    {
        _ = Actions;
        _bindingStore.LoadOverrides(bindingOverrides);
    }

    /// <summary>
    /// Returns the identifier for a binding slot.
    /// </summary>
    internal Guid GetBindingSlotId(string actionPath, int slot)
    {
        _ = Actions;
        return _bindingStore.GetSlotId(actionPath, slot);
    }

    /// <summary>
    /// Changes the binding assigned to a slot.
    /// </summary>
    internal void ApplyBindingSlotOverride(string actionPath, int slot, string path)
    {
        _ = Actions;
        _bindingStore.ApplySlotOverride(actionPath, slot, path);
    }

    /// <summary>
    /// Returns the control path assigned to a binding slot.
    /// </summary>
    internal string GetEffectiveBindingSlotPath(string actionPath, int slot)
    {
        _ = Actions;
        return _bindingStore.GetEffectiveSlotPath(actionPath, slot);
    }

    /// <summary>
    /// Checks whether a control name represents a modifier key.
    /// </summary>
    internal static bool IsModifierControlName(string controlName)
    {
        return KeyboardChordProcessor.IsModifierControlName(controlName);
    }
}
