using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

/// <summary>
/// Manages the controls list and keyboard rebinding.
/// </summary>
internal sealed class OptionsBindingEditor : IDisposable
{
    // Modifier Keys.
    private static readonly string[] _modifierControlPaths =
    {
        "<Keyboard>/ctrl",
        "<Keyboard>/shift",
        "<Keyboard>/alt",
        "<Keyboard>/leftCtrl",
        "<Keyboard>/rightCtrl",
        "<Keyboard>/leftShift",
        "<Keyboard>/rightShift",
        "<Keyboard>/leftAlt",
        "<Keyboard>/rightAlt",
        "<Keyboard>/leftMeta",
        "<Keyboard>/rightMeta",
    };

    // Binding List.
    private readonly InputManager _inputManager;
    private readonly List<OptionsBindingRow> _rows = new List<OptionsBindingRow>();
    private readonly List<(InputAction action, int primary, int secondary)> _targets =
        new List<(InputAction, int, int)>();
    private readonly Dictionary<InputAction, bool> _suppressedActionStates =
        new Dictionary<InputAction, bool>();

    // Rebinding State.
    private InputActionRebindingExtensions.RebindingOperation _operation;
    private InputAction _listeningAction;
    private InputAction _reboundAction;
    private InputBinding[] _previousActionOverrides;
    private bool _reboundActionWasEnabled;
    private bool _rebindApplied;
    private int _capturedModifiers;

    // Binding Conflict.
    private InputAction _conflictOldAction;
    private int _conflictOldIndex;
    private InputAction _conflictNewAction;
    private int _conflictNewIndex;

    internal event Action Changed;

    internal event Action<string> ConflictRequested;

    internal event Action PresentationChanged;

    internal OptionsBindingEditor(InputManager inputManager)
    {
        _inputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
    }

    internal IReadOnlyList<OptionsBindingRow> Rows => _rows;

    internal int ListeningRow { get; private set; } = -1;

    internal bool ListeningSecondary { get; private set; }

    internal bool HasPendingConflict => _conflictNewAction != null;

    internal void Rebuild()
    {
        _rows.Clear();
        _targets.Clear();
        foreach (InputActionMap map in _inputManager.Asset.actionMaps)
        {
            if (!IsBindableMap(map.name))
                continue;

            List<OptionsBindingRow> mapRows = new List<OptionsBindingRow>();
            List<(InputAction, int, int)> mapTargets = new List<(InputAction, int, int)>();
            foreach (InputAction action in map.actions)
            {
                if (!IsBindableAction(action.name))
                    continue;

                (int primary, int secondary) = GetTopLevelBindingIndices(action);
                (string primaryKey, string secondaryKey) = FormatKeys(action);
                mapRows.Add(new OptionsBindingRow(Humanize(action.name), primaryKey, secondaryKey));
                mapTargets.Add((action, primary, secondary));
            }

            if (mapRows.Count == 0)
                continue;

            _rows.Add(new OptionsBindingRow(Humanize(map.name), string.Empty, string.Empty, true));
            _targets.Add((null, -1, -1));
            _rows.AddRange(mapRows);
            _targets.AddRange(mapTargets);
        }
    }

    internal void BeginRebind(int row, bool secondary)
    {
        if (_operation != null || HasPendingConflict || row < 0 || row >= _targets.Count)
            return;

        (InputAction action, int primary, int secondaryIndex) = _targets[row];
        if (action == null)
            return;

        int bindingIndex = secondary ? secondaryIndex : primary;
        if (bindingIndex < 0)
            throw new InvalidOperationException(
                $"Bindable action '{action}' has no authored slot."
            );

        ListeningRow = row;
        ListeningSecondary = secondary;
        _previousActionOverrides = CaptureOverrides(action);
        PresentationChanged?.Invoke();
        StartRebind(action, bindingIndex);
    }

    internal void ResolveConflict(bool clearOld)
    {
        if (!HasPendingConflict)
            return;

        if (clearOld)
        {
            UnbindTopLevel(_conflictOldAction, _conflictOldIndex);
            Changed?.Invoke();
        }
        else
        {
            RestoreOverrides(_conflictNewAction, _previousActionOverrides);
        }

        ClearConflict();
        Rebuild();
        PresentationChanged?.Invoke();
    }

    internal void SetTextEntryActive(bool active)
    {
        InputActionAsset asset = _inputManager.Asset;
        if (active)
        {
            if (_suppressedActionStates.Count > 0)
                return;

            foreach (InputActionMap map in asset.actionMaps)
            {
                if (!IsBindableMap(map.name))
                    continue;
                foreach (InputAction action in map.actions)
                {
                    if (!IsBindableAction(action.name))
                        continue;
                    _suppressedActionStates[action] = action.enabled;
                    action.Disable();
                }
            }
            return;
        }

        foreach (KeyValuePair<InputAction, bool> entry in _suppressedActionStates)
        {
            if (entry.Key != null && entry.Value)
                entry.Key.Enable();
        }
        _suppressedActionStates.Clear();
    }

    internal void CancelRebind()
    {
        _operation?.Cancel();
    }

    public void Dispose()
    {
        _operation?.Dispose();
        _operation = null;
        _listeningAction?.Dispose();
        _listeningAction = null;
        RestoreReboundActionState();
        SetTextEntryActive(false);
        ClearConflict();
    }

    private void StartRebind(InputAction action, int bindingIndex)
    {
        KeyboardChordProcessor.EnsureRegistered();
        _capturedModifiers = 0;
        _rebindApplied = false;
        _reboundAction = action;
        _reboundActionWasEnabled = action.enabled;
        action.Disable();

        bool isComposite = action.bindings[bindingIndex].isComposite;
        InputAction rebindSource = action;
        if (isComposite)
        {
            // Listen for both parts of a modifier key binding.
            _listeningAction = new InputAction(type: InputActionType.Button);
            rebindSource = _listeningAction;
        }

        InputActionRebindingExtensions.RebindingOperation candidate = (
            isComposite
                ? rebindSource.PerformInteractiveRebinding()
                : rebindSource.PerformInteractiveRebinding(bindingIndex)
        )
            .WithCancelingThrough("<Keyboard>/escape")
            .WithControlsExcluding("<Mouse>")
            .WithControlsExcluding("<Keyboard>/anyKey");
        if (!IsModifierAction(action.name))
        {
            foreach (string path in _modifierControlPaths)
                candidate.WithControlsExcluding(path);
        }

        _operation = candidate
            .OnPotentialMatch(match => HandlePotentialMatch(match, action))
            .OnApplyBinding((_, path) => ApplyRebindPath(action, bindingIndex, path))
            .OnCancel(_ => FinishRebind(action, bindingIndex, false))
            .OnComplete(_ => FinishRebind(action, bindingIndex, true))
            .Start();
    }

    private static void HandlePotentialMatch(
        InputActionRebindingExtensions.RebindingOperation candidate,
        InputAction action
    )
    {
        InputControl control = candidate.selectedControl;
        if (!IsModifierAction(action?.name))
        {
            while (KeyboardChordProcessor.IsModifier(control))
            {
                candidate.RemoveCandidate(control);
                control = candidate.selectedControl;
            }

            if (control == null)
                return;
        }

        candidate.Complete();
    }

    private void ApplyRebindPath(InputAction action, int bindingIndex, string path)
    {
        InputControl selectedControl = _operation?.selectedControl;
        _capturedModifiers = KeyboardChordProcessor.IsModifier(selectedControl)
            ? 0
            : KeyboardChordProcessor.GetPressedModifiers(Keyboard.current);

        if (action.bindings[bindingIndex].isComposite)
        {
            _rebindApplied = TryApplyCompositeChord(action, bindingIndex, path);
            if (!_rebindApplied)
                RestoreOverrides(action, _previousActionOverrides);
            return;
        }

        action.ApplyBindingOverride(
            bindingIndex,
            new InputBinding
            {
                overridePath = path,
                overrideProcessors = KeyboardChordProcessor.GetProcessorOverride(
                    _capturedModifiers
                ),
            }
        );
        _rebindApplied = true;
    }

    private bool TryApplyCompositeChord(InputAction action, int compositeIndex, string buttonPath)
    {
        string modifierPath = GetSingleModifierPath(_capturedModifiers);
        if (string.IsNullOrEmpty(modifierPath))
            return false;

        int modifierIndex = -1;
        int buttonIndex = -1;
        for (int index = compositeIndex + 1; index < action.bindings.Count; index++)
        {
            InputBinding part = action.bindings[index];
            if (!part.isPartOfComposite)
                break;
            if (string.Equals(part.name, "Modifier", StringComparison.OrdinalIgnoreCase))
                modifierIndex = index;
            else if (string.Equals(part.name, "Button", StringComparison.OrdinalIgnoreCase))
                buttonIndex = index;
        }

        if (modifierIndex < 0 || buttonIndex < 0)
            return false;

        action.ApplyBindingOverride(modifierIndex, modifierPath);
        action.ApplyBindingOverride(buttonIndex, buttonPath);
        return true;
    }

    private void FinishRebind(InputAction action, int bindingIndex, bool completed)
    {
        _operation?.Dispose();
        _operation = null;
        _listeningAction?.Dispose();
        _listeningAction = null;
        RestoreReboundActionState();
        ListeningRow = -1;
        ListeningSecondary = false;

        if (!completed || !_rebindApplied)
        {
            RestoreOverrides(action, _previousActionOverrides);
            _previousActionOverrides = null;
            Rebuild();
            PresentationChanged?.Invoke();
            return;
        }

        (InputAction other, int otherIndex) = FindConflict(action, bindingIndex);
        if (other != null)
        {
            _conflictOldAction = other;
            _conflictOldIndex = otherIndex;
            _conflictNewAction = action;
            _conflictNewIndex = bindingIndex;
            ConflictRequested?.Invoke(
                $"That input is already bound to \"{Humanize(other.name)}\". Move it here and clear the old binding?"
            );
            return;
        }

        Changed?.Invoke();
        _previousActionOverrides = null;
        Rebuild();
        PresentationChanged?.Invoke();
    }

    private (InputAction action, int index) FindConflict(InputAction rebound, int reboundIndex)
    {
        string signature = GetBindingSignature(rebound, reboundIndex);
        if (string.IsNullOrEmpty(signature))
            return (null, -1);

        foreach (InputActionMap map in _inputManager.Asset.actionMaps)
        {
            if (!IsBindableMap(map.name))
                continue;
            foreach (InputAction action in map.actions)
            {
                for (int index = 0; index < action.bindings.Count; index++)
                {
                    if (action.bindings[index].isPartOfComposite)
                        continue;
                    if (action == rebound && index == reboundIndex)
                        continue;
                    if (
                        string.Equals(
                            GetBindingSignature(action, index),
                            signature,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                        return (action, index);
                }
            }
        }

        return (null, -1);
    }

    private static InputBinding[] CaptureOverrides(InputAction action)
    {
        InputBinding[] result = new InputBinding[action.bindings.Count];
        for (int index = 0; index < action.bindings.Count; index++)
        {
            InputBinding binding = action.bindings[index];
            result[index] = new InputBinding
            {
                overridePath = binding.overridePath,
                overrideInteractions = binding.overrideInteractions,
                overrideProcessors = binding.overrideProcessors,
            };
        }
        return result;
    }

    private static void RestoreOverrides(InputAction action, IReadOnlyList<InputBinding> overrides)
    {
        if (action == null || overrides == null)
            return;

        action.RemoveAllBindingOverrides();
        int count = Math.Min(action.bindings.Count, overrides.Count);
        for (int index = 0; index < count; index++)
        {
            InputBinding binding = overrides[index];
            if (
                binding.overridePath != null
                || binding.overrideInteractions != null
                || binding.overrideProcessors != null
            )
                action.ApplyBindingOverride(index, binding);
        }
    }

    private static void UnbindTopLevel(InputAction action, int index)
    {
        if (action == null || index < 0 || index >= action.bindings.Count)
            return;

        action.ApplyBindingOverride(index, string.Empty);
        if (!action.bindings[index].isComposite)
            return;

        for (int partIndex = index + 1; partIndex < action.bindings.Count; partIndex++)
        {
            if (!action.bindings[partIndex].isPartOfComposite)
                break;
            action.ApplyBindingOverride(partIndex, string.Empty);
        }
    }

    private void ClearConflict()
    {
        _conflictOldAction = null;
        _conflictNewAction = null;
        _conflictOldIndex = -1;
        _conflictNewIndex = -1;
        _previousActionOverrides = null;
    }

    private void RestoreReboundActionState()
    {
        if (_reboundActionWasEnabled)
            _reboundAction?.Enable();
        _reboundAction = null;
        _reboundActionWasEnabled = false;
    }

    internal static string GetBindingSignature(InputAction action, int bindingIndex)
    {
        InputBinding binding = action.bindings[bindingIndex];
        if (binding.isComposite)
        {
            string modifierPath = null;
            string buttonPath = null;
            for (int index = bindingIndex + 1; index < action.bindings.Count; index++)
            {
                InputBinding part = action.bindings[index];
                if (!part.isPartOfComposite)
                    break;
                if (string.Equals(part.name, "Modifier", StringComparison.OrdinalIgnoreCase))
                    modifierPath = part.effectivePath;
                else if (string.Equals(part.name, "Button", StringComparison.OrdinalIgnoreCase))
                    buttonPath = part.effectivePath;
            }
            int modifierMask = GetModifierMask(modifierPath);
            return modifierMask == 0 || string.IsNullOrEmpty(buttonPath)
                ? string.Empty
                : $"{buttonPath}|{modifierMask}";
        }

        if (string.IsNullOrEmpty(binding.effectivePath))
            return string.Empty;
        int modifiers = KeyboardChordProcessor.ParseModifierMask(binding.effectiveProcessors);
        return $"{binding.effectivePath}|{modifiers}";
    }

    private static int GetModifierMask(string modifierPath)
    {
        if (string.IsNullOrEmpty(modifierPath))
            return 0;
        if (modifierPath.IndexOf("shift", StringComparison.OrdinalIgnoreCase) >= 0)
            return KeyboardChordProcessor.Shift;
        if (
            modifierPath.IndexOf("ctrl", StringComparison.OrdinalIgnoreCase) >= 0
            || modifierPath.IndexOf("control", StringComparison.OrdinalIgnoreCase) >= 0
        )
            return KeyboardChordProcessor.Control;
        if (modifierPath.IndexOf("alt", StringComparison.OrdinalIgnoreCase) >= 0)
            return KeyboardChordProcessor.Alt;
        if (modifierPath.IndexOf("meta", StringComparison.OrdinalIgnoreCase) >= 0)
            return KeyboardChordProcessor.Meta;
        return 0;
    }

    private static string GetSingleModifierPath(int modifiers)
    {
        return modifiers switch
        {
            KeyboardChordProcessor.Shift => "<Keyboard>/shift",
            KeyboardChordProcessor.Control => "<Keyboard>/ctrl",
            KeyboardChordProcessor.Alt => "<Keyboard>/alt",
            KeyboardChordProcessor.Meta => "<Keyboard>/leftMeta",
            _ => null,
        };
    }

    private static bool IsModifierAction(string actionName)
    {
        return actionName
            is "MultiSelectModifier"
                or "RangeSelectModifier"
                or "AlternateSelectModifier";
    }

    private static (int primary, int secondary) GetTopLevelBindingIndices(InputAction action)
    {
        int primary = -1;
        int secondary = -1;
        for (int index = 0; index < action.bindings.Count; index++)
        {
            if (action.bindings[index].isPartOfComposite)
                continue;
            if (primary < 0)
                primary = index;
            else
            {
                secondary = index;
                break;
            }
        }
        return (primary, secondary);
    }

    private static bool IsBindableMap(string map)
    {
        return map is "Global" or "Strategy";
    }

    private static bool IsBindableAction(string action)
    {
        return action != "CancelOrSettings";
    }

    private static (string primary, string secondary) FormatKeys(InputAction action)
    {
        List<string> keys = new List<string>();
        for (int index = 0; index < action.bindings.Count; index++)
        {
            InputBinding binding = action.bindings[index];
            if (binding.isPartOfComposite)
                continue;

            string display = binding.isComposite
                ? GetCompositeDisplayString(action, index)
                : action.GetBindingDisplayString(index);
            if (string.IsNullOrWhiteSpace(display) && string.IsNullOrEmpty(binding.effectivePath))
                display = "UNBOUND";
            int modifiers = binding.isComposite
                ? 0
                : KeyboardChordProcessor.ParseModifierMask(binding.effectiveProcessors);
            keys.Add(KeyboardChordProcessor.GetDisplayPrefix(modifiers) + ShortenKey(display));
            if (keys.Count == 2)
                break;
        }

        return (keys.Count > 0 ? keys[0] : "UNBOUND", keys.Count > 1 ? keys[1] : "UNBOUND");
    }

    private static string GetCompositeDisplayString(InputAction action, int compositeIndex)
    {
        string modifier = null;
        string button = null;
        for (int index = compositeIndex + 1; index < action.bindings.Count; index++)
        {
            InputBinding part = action.bindings[index];
            if (!part.isPartOfComposite)
                break;

            string display = string.IsNullOrEmpty(part.effectivePath)
                ? string.Empty
                : InputControlPath.ToHumanReadableString(
                    part.effectivePath,
                    InputControlPath.HumanReadableStringOptions.OmitDevice
                );
            if (string.Equals(part.name, "Modifier", StringComparison.OrdinalIgnoreCase))
                modifier = display;
            else if (string.Equals(part.name, "Button", StringComparison.OrdinalIgnoreCase))
                button = display;
        }

        return string.IsNullOrEmpty(modifier) || string.IsNullOrEmpty(button)
            ? string.Empty
            : $"{ShortenKey(modifier)}+{ShortenKey(button)}";
    }

    private static string ShortenKey(string display)
    {
        if (string.IsNullOrWhiteSpace(display))
            return "UNBOUND";
        return display
            .ToUpperInvariant()
            .Replace("LEFT ", "L ")
            .Replace("RIGHT ", "R ")
            .Replace("CONTROL", "CTRL")
            .Replace("DELETE", "DEL")
            .Replace("INSERT", "INS")
            .Replace("BACKSPACE", "BKSP")
            .Replace("PAGE UP", "PG UP")
            .Replace("PAGE DOWN", "PG DN")
            .Replace("NUMPAD ", "NUM ");
    }

    private static string Humanize(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        System.Text.StringBuilder builder = new System.Text.StringBuilder(name.Length + 8);
        for (int index = 0; index < name.Length; index++)
        {
            char value = name[index];
            char previous = index > 0 ? name[index - 1] : '\0';
            bool boundary =
                index > 0
                && (
                    (char.IsUpper(value) && !char.IsUpper(previous))
                    || (char.IsDigit(value) && !char.IsDigit(previous))
                    || (char.IsLetter(value) && char.IsDigit(previous))
                );
            if (boundary)
                builder.Append(' ');
            builder.Append(value);
        }
        return builder.ToString();
    }
}
