using System.Linq;
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
        get { return _actions ??= CreateActions(); }
    }

    /// <summary>
    /// Gets the generated input action asset.
    /// </summary>
    public InputActionAsset Asset => Actions.asset;

    /// <summary>
    /// Captures the currently held, user-configured strategy selection modifiers.
    /// </summary>
    /// <returns>The active selection modifier state.</returns>
    public SelectionModifierState GetSelectionModifierState()
    {
        PlayerInputActions.StrategyActions strategy = Actions.Strategy;
        return new SelectionModifierState(
            strategy.MultiSelectModifier.IsPressed(),
            strategy.RangeSelectModifier.IsPressed()
        );
    }

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
        RemoveReservedShortcutOverrides(Actions.asset);
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
        RemoveReservedShortcutOverrides(asset);
    }

    /// <summary>
    /// Applies the platform-specific default for shortcuts authored with the Control modifier.
    /// </summary>
    /// <param name="asset">The input action asset to update.</param>
    /// <param name="useCommandKey">Whether Command replaces Control.</param>
    internal static void SetShortcutModifier(InputActionAsset asset, bool useCommandKey)
    {
        foreach (InputActionMap actionMap in asset.actionMaps)
        {
            foreach (InputAction action in actionMap.actions)
            {
                bool hasControlDefault = action.bindings.Any(binding =>
                    binding.path == "<Keyboard>/ctrl"
                );
                if (!hasControlDefault)
                    continue;

                for (int index = 0; index < action.bindings.Count; index++)
                {
                    string path = action.bindings[index].path;
                    if (path == "<Keyboard>/ctrl")
                    {
                        if (useCommandKey)
                            action.ChangeBinding(index).WithPath("<Keyboard>/leftMeta");
                    }
                    else if (path == "<Keyboard>/leftMeta")
                    {
                        action.ChangeBinding(index).WithPath(string.Empty);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Creates the generated actions and selects the native desktop shortcut modifier.
    /// </summary>
    private static PlayerInputActions CreateActions()
    {
        PlayerInputActions actions = new PlayerInputActions();
        bool useCommandKey =
            Application.platform == RuntimePlatform.OSXEditor
            || Application.platform == RuntimePlatform.OSXPlayer;
        SetShortcutModifier(actions.asset, useCommandKey);
        return actions;
    }

    /// <summary>
    /// Prevents persisted rebinding data from replacing the fixed Escape and Shift+Escape
    /// navigation shortcuts.
    /// </summary>
    /// <param name="asset">The asset.</param>
    private static void RemoveReservedShortcutOverrides(InputActionAsset asset)
    {
        RemovePrimaryShortcutOverride(asset.FindAction("Global/CancelOrSettings", true));
        RemovePrimaryShortcutOverride(asset.FindAction("Global/OpenGameMenu", true));
    }

    /// <summary>
    /// Removes overrides from one action's authored primary binding, including every part of
    /// its optional composite chord.
    /// </summary>
    /// <param name="action">The action.</param>
    private static void RemovePrimaryShortcutOverride(InputAction action)
    {
        bool insidePrimaryChord = false;
        for (int index = 0; index < action.bindings.Count; index++)
        {
            InputBinding binding = action.bindings[index];
            if (!binding.isPartOfComposite)
            {
                insidePrimaryChord = binding.name == "PrimaryChord";
                if (binding.name == "Primary" || insidePrimaryChord)
                    action.RemoveBindingOverride(index);
                continue;
            }

            if (insidePrimaryChord)
                action.RemoveBindingOverride(index);
        }
    }
}
