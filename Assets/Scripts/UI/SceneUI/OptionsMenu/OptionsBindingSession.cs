using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;

/// <summary>
/// Manages the controls list and keyboard rebinding against authored binding slots.
/// </summary>
internal sealed class OptionsBindingSession : IDisposable
{
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

    private readonly InputManager _inputManager;
    private readonly List<OptionsBindingRow> _rows = new List<OptionsBindingRow>();
    private readonly List<BindingTarget> _targets = new List<BindingTarget>();
    private readonly Dictionary<InputAction, bool> _suppressedActionStates =
        new Dictionary<InputAction, bool>();

    private InputActionRebindingExtensions.RebindingOperation _operation;
    private InputAction _listeningAction;
    private string[] _previousActionOverrides;
    private bool _rebindApplied;
    private InputAction _conflictOldAction;
    private int _conflictOldIndex;
    private InputAction _conflictNewAction;
    private bool _rebindOwnsShortcutInput;
    private bool _textEntryOwnsShortcutInput;
    private bool _shortcutRestoreScheduled;

    /// <summary>
    /// Raised after a binding override changes.
    /// </summary>
    internal event Action Changed;

    /// <summary>
    /// Raised when a new binding conflicts with an existing binding.
    /// </summary>
    internal event Action<string> ConflictRequested;

    /// <summary>
    /// Raised when listening or rendered binding state changes.
    /// </summary>
    internal event Action PresentationChanged;

    /// <summary>
    /// Creates a binding editor for the supplied input manager.
    /// </summary>
    /// <param name="inputManager">The application input manager.</param>
    internal OptionsBindingSession(InputManager inputManager)
    {
        _inputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
    }

    /// <summary>
    /// Gets the current controls rows.
    /// </summary>
    internal IReadOnlyList<OptionsBindingRow> Rows => _rows;

    /// <summary>
    /// Gets the row currently listening for input, or negative one when idle.
    /// </summary>
    internal int ListeningRow { get; private set; } = -1;

    /// <summary>
    /// Gets whether the secondary slot is currently listening.
    /// </summary>
    internal bool ListeningSecondary { get; private set; }

    /// <summary>
    /// Gets whether a binding conflict is awaiting a decision.
    /// </summary>
    internal bool HasPendingConflict => _conflictNewAction != null;

    /// <summary>
    /// Rebuilds the controls rows from the authored Global and Strategy binding slots.
    /// </summary>
    internal void Rebuild()
    {
        _rows.Clear();
        _targets.Clear();
        foreach (InputActionMap map in _inputManager.Asset.actionMaps)
        {
            if (!IsBindableMap(map.name))
                continue;

            List<OptionsBindingRow> mapRows = new List<OptionsBindingRow>();
            List<BindingTarget> mapTargets = new List<BindingTarget>();
            foreach (InputAction action in map.actions)
            {
                (BindingSlot primary, BindingSlot secondary) = GetBindingSlots(action);
                mapRows.Add(
                    new OptionsBindingRow(
                        GetActionLabel(action.name),
                        FormatSlot(action, primary),
                        FormatSlot(action, secondary),
                        primaryEditable: !HasReservedPrimary(action)
                    )
                );
                mapTargets.Add(new BindingTarget(action, primary, secondary));
            }

            if (mapRows.Count == 0)
                continue;

            _rows.Add(new OptionsBindingRow(Humanize(map.name), string.Empty, string.Empty, true));
            _targets.Add(default);
            _rows.AddRange(mapRows);
            _targets.AddRange(mapTargets);
        }
    }

    /// <summary>
    /// Starts listening for a replacement for one authored slot.
    /// </summary>
    /// <param name="row">The controls row.</param>
    /// <param name="secondary">Whether to edit the secondary slot.</param>
    internal void BeginRebind(int row, bool secondary)
    {
        if (_operation != null || HasPendingConflict || row < 0 || row >= _targets.Count)
            return;

        BindingTarget target = _targets[row];
        if (target.Action == null)
            return;
        if (!secondary && HasReservedPrimary(target.Action))
            return;

        ListeningRow = row;
        ListeningSecondary = secondary;
        _previousActionOverrides = CaptureOverrides(target.Action);
        SetRebindShortcutInputOwnership(true);
        PresentationChanged?.Invoke();
        StartRebind(target.Action, secondary ? target.Secondary : target.Primary);
    }

    /// <summary>
    /// Restores the authored defaults for one binding row.
    /// </summary>
    /// <param name="row">The controls row to restore.</param>
    internal void RestoreDefault(int row)
    {
        if (_operation != null || HasPendingConflict || row < 0 || row >= _targets.Count)
            return;

        InputAction action = _targets[row].Action;
        if (action == null)
            return;

        action.RemoveAllBindingOverrides();
        Changed?.Invoke();
        Rebuild();
        PresentationChanged?.Invoke();
    }

    /// <summary>
    /// Restores every bindable action to its authored defaults.
    /// </summary>
    internal void RestoreAllDefaults()
    {
        if (_operation != null || HasPendingConflict)
            return;

        _inputManager.Asset.RemoveAllBindingOverrides();
        Changed?.Invoke();
        Rebuild();
        PresentationChanged?.Invoke();
    }

    /// <summary>
    /// Accepts or rejects moving a conflicting binding to the new action.
    /// </summary>
    /// <param name="clearOld">Whether to clear the old action's binding.</param>
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

    /// <summary>
    /// Suppresses bindable actions while a text field owns keyboard input.
    /// </summary>
    /// <param name="active">Whether text entry is active.</param>
    internal void SetTextEntryActive(bool active)
    {
        _textEntryOwnsShortcutInput = active;
        if (active)
            SuppressShortcutInput();
        else
            RestoreShortcutInput();
    }

    /// <summary>
    /// Cancels the active interactive rebind operation.
    /// </summary>
    internal void CancelRebind()
    {
        _operation?.Cancel();
    }

    /// <summary>
    /// Releases the active rebind operation and restores suppressed input state.
    /// </summary>
    public void Dispose()
    {
        _operation?.Dispose();
        _operation = null;
        _listeningAction?.Dispose();
        _listeningAction = null;
        _rebindOwnsShortcutInput = false;
        _textEntryOwnsShortcutInput = false;
        CancelScheduledShortcutRestore();
        RestoreShortcutInput();
        ClearConflict();
    }

    /// <summary>
    /// Creates an unbound listening action so modifier presses are not committed as the base key.
    /// </summary>
    private void StartRebind(InputAction action, BindingSlot slot)
    {
        _rebindApplied = false;
        _listeningAction = new InputAction(type: InputActionType.Button);

        InputActionRebindingExtensions.RebindingOperation candidate = _listeningAction
            .PerformInteractiveRebinding()
            .WithRebindAddingNewBinding()
            .WithCancelingThrough("<Keyboard>/escape")
            .WithControlsExcluding("<Mouse>")
            .WithControlsExcluding("<Keyboard>/anyKey");
        if (!IsModifierAction(action.name))
        {
            foreach (string path in _modifierControlPaths)
                candidate.WithControlsExcluding(path);
        }

        _operation = candidate
            .OnApplyBinding((_, path) => ApplyRebindPath(action, slot, path))
            .OnCancel(_ => FinishRebind(action, slot, false))
            .OnComplete(_ => FinishRebind(action, slot, true))
            .Start();
    }

    /// <summary>
    /// Applies a captured key to either the plain or modifier-composite alternative.
    /// </summary>
    private void ApplyRebindPath(InputAction action, BindingSlot slot, string path)
    {
        string modifierPath = IsModifierAction(action.name) ? null : GetPressedModifierPath();
        if (string.IsNullOrEmpty(modifierPath))
        {
            action.ApplyBindingOverride(slot.PlainIndex, path);
            ClearComposite(action, slot);
        }
        else
        {
            action.ApplyBindingOverride(slot.PlainIndex, string.Empty);
            action.ApplyBindingOverride(slot.ModifierIndex, modifierPath);
            action.ApplyBindingOverride(slot.BindingIndex, path);
        }
        _rebindApplied = true;
    }

    /// <summary>
    /// Finalizes a rebind and asks for confirmation when its active signature is duplicated.
    /// </summary>
    private void FinishRebind(InputAction action, BindingSlot slot, bool completed)
    {
        _operation?.Dispose();
        _operation = null;
        _listeningAction?.Dispose();
        _listeningAction = null;
        ListeningRow = -1;
        ListeningSecondary = false;
        SetRebindShortcutInputOwnership(false);

        if (!completed || !_rebindApplied)
        {
            RestoreOverrides(action, _previousActionOverrides);
            _previousActionOverrides = null;
            Rebuild();
            PresentationChanged?.Invoke();
            return;
        }

        int activeIndex = GetActiveTopLevelIndex(action, slot);
        (InputAction other, int otherIndex) = FindConflict(action, activeIndex);
        if (other != null)
        {
            _conflictOldAction = other;
            _conflictOldIndex = otherIndex;
            _conflictNewAction = action;
            ConflictRequested?.Invoke(
                $"That input is already bound to \"{GetActionLabel(other.name)}\". Move it here and clear the old binding?"
            );
            return;
        }

        Changed?.Invoke();
        _previousActionOverrides = null;
        Rebuild();
        PresentationChanged?.Invoke();
    }

    /// <summary>
    /// Finds another active top-level binding with the same key and modifier signature.
    /// </summary>
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

    /// <summary>
    /// Captures every binding override on an action for cancellation and conflict rejection.
    /// </summary>
    private static string[] CaptureOverrides(InputAction action)
    {
        string[] result = new string[action.bindings.Count];
        for (int index = 0; index < action.bindings.Count; index++)
            result[index] = action.bindings[index].overridePath;
        return result;
    }

    /// <summary>
    /// Restores a previously captured set of binding overrides.
    /// </summary>
    private static void RestoreOverrides(InputAction action, IReadOnlyList<string> overrides)
    {
        if (action == null || overrides == null)
            return;

        action.RemoveAllBindingOverrides();
        int count = Math.Min(action.bindings.Count, overrides.Count);
        for (int index = 0; index < count; index++)
        {
            string overridePath = overrides[index];
            if (overridePath != null)
                action.ApplyBindingOverride(index, overridePath);
        }
    }

    /// <summary>
    /// Clears one active top-level binding and any composite parts it owns.
    /// </summary>
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

    /// <summary>
    /// Clears the composite alternative belonging to an authored slot.
    /// </summary>
    private static void ClearComposite(InputAction action, BindingSlot slot)
    {
        action.ApplyBindingOverride(slot.ModifierIndex, string.Empty);
        action.ApplyBindingOverride(slot.BindingIndex, string.Empty);
    }

    /// <summary>
    /// Clears pending conflict state and its cancellation snapshot.
    /// </summary>
    private void ClearConflict()
    {
        _conflictOldAction = null;
        _conflictNewAction = null;
        _conflictOldIndex = -1;
        _previousActionOverrides = null;
    }

    /// <summary>
    /// Acquires or releases exclusive shortcut ownership for interactive rebinding.
    /// </summary>
    /// <param name="active">Whether an interactive rebind owns shortcut input.</param>
    private void SetRebindShortcutInputOwnership(bool active)
    {
        _rebindOwnsShortcutInput = active;
        if (active)
        {
            CancelScheduledShortcutRestore();
            SuppressShortcutInput();
            return;
        }

        if (!_textEntryOwnsShortcutInput)
            ScheduleShortcutRestore();
    }

    /// <summary>
    /// Disables bindable application shortcuts while an editor owns keyboard input.
    /// </summary>
    private void SuppressShortcutInput()
    {
        if (_suppressedActionStates.Count > 0)
            return;

        foreach (InputActionMap map in _inputManager.Asset.actionMaps)
        {
            if (!IsBindableMap(map.name))
                continue;
            foreach (InputAction action in map.actions)
            {
                _suppressedActionStates[action] = action.enabled;
                action.Disable();
            }
        }
    }

    /// <summary>
    /// Restores shortcuts after the input event that ended capture has been fully dispatched.
    /// </summary>
    private void ScheduleShortcutRestore()
    {
        if (_shortcutRestoreScheduled)
            return;

        _shortcutRestoreScheduled = true;
        InputSystem.onAfterUpdate += HandleInputUpdateFinished;
    }

    /// <summary>
    /// Releases shortcut ownership at a stable input-system update boundary.
    /// </summary>
    private void HandleInputUpdateFinished()
    {
        CancelScheduledShortcutRestore();
        RestoreShortcutInput();
    }

    /// <summary>
    /// Removes a pending shortcut-restore callback.
    /// </summary>
    private void CancelScheduledShortcutRestore()
    {
        if (!_shortcutRestoreScheduled)
            return;

        InputSystem.onAfterUpdate -= HandleInputUpdateFinished;
        _shortcutRestoreScheduled = false;
    }

    /// <summary>
    /// Restores the enabled state captured before exclusive keyboard input began.
    /// </summary>
    private void RestoreShortcutInput()
    {
        if (_rebindOwnsShortcutInput || _textEntryOwnsShortcutInput)
            return;

        foreach (KeyValuePair<InputAction, bool> entry in _suppressedActionStates)
        {
            if (entry.Key != null && entry.Value)
                entry.Key.Enable();
        }
        _suppressedActionStates.Clear();
    }

    /// <summary>
    /// Returns a normalized signature for a plain or modifier-composite binding.
    /// </summary>
    internal static string GetBindingSignature(InputAction action, int bindingIndex)
    {
        InputBinding binding = action.bindings[bindingIndex];
        if (!binding.isComposite)
            return string.IsNullOrEmpty(binding.effectivePath)
                ? string.Empty
                : binding.effectivePath;

        (string modifier, string key) = GetCompositePaths(action, bindingIndex);
        return string.IsNullOrEmpty(modifier) || string.IsNullOrEmpty(key)
            ? string.Empty
            : $"{modifier}+{key}";
    }

    /// <summary>
    /// Returns the modifier and key paths from a Unity OneModifier composite.
    /// </summary>
    private static (string modifier, string key) GetCompositePaths(
        InputAction action,
        int compositeIndex
    )
    {
        string modifier = null;
        string key = null;
        for (int index = compositeIndex + 1; index < action.bindings.Count; index++)
        {
            InputBinding part = action.bindings[index];
            if (!part.isPartOfComposite)
                break;
            if (string.Equals(part.name, "Modifier", StringComparison.OrdinalIgnoreCase))
                modifier = part.effectivePath;
            else if (
                string.Equals(part.name, "Binding", StringComparison.OrdinalIgnoreCase)
                || string.Equals(part.name, "Button", StringComparison.OrdinalIgnoreCase)
            )
                key = part.effectivePath;
        }
        return (modifier, key);
    }

    /// <summary>
    /// Returns the single held keyboard modifier path, or null when none or several are held.
    /// </summary>
    private static string GetPressedModifierPath()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return null;

        string result = null;
        int count = 0;
        if (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)
        {
            result = "<Keyboard>/ctrl";
            count++;
        }
        if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
        {
            result = "<Keyboard>/shift";
            count++;
        }
        if (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed)
        {
            result = "<Keyboard>/alt";
            count++;
        }
        if (keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed)
        {
            result = "<Keyboard>/leftMeta";
            count++;
        }
        return count == 1 ? result : null;
    }

    /// <summary>
    /// Gets the two named binding slots authored for an action.
    /// </summary>
    private static (BindingSlot primary, BindingSlot secondary) GetBindingSlots(InputAction action)
    {
        return (GetBindingSlot(action, "Primary"), GetBindingSlot(action, "Secondary"));
    }

    /// <summary>
    /// Resolves one plain/composite binding pair by its authored slot name.
    /// </summary>
    private static BindingSlot GetBindingSlot(InputAction action, string name)
    {
        int plain = -1;
        int composite = -1;
        int modifier = -1;
        int key = -1;
        for (int index = 0; index < action.bindings.Count; index++)
        {
            InputBinding binding = action.bindings[index];
            if (!binding.isPartOfComposite && binding.name == name)
                plain = index;
            if (!binding.isPartOfComposite && binding.name == name + "Chord")
            {
                composite = index;
                for (int part = index + 1; part < action.bindings.Count; part++)
                {
                    InputBinding compositePart = action.bindings[part];
                    if (!compositePart.isPartOfComposite)
                        break;
                    if (compositePart.name == "Modifier")
                        modifier = part;
                    else if (compositePart.name == "Binding")
                        key = part;
                }
            }
        }

        if (plain < 0 || composite < 0 || modifier < 0 || key < 0)
            throw new InvalidOperationException($"Action '{action}' is missing its {name} slot.");
        return new BindingSlot(plain, composite, modifier, key);
    }

    /// <summary>
    /// Returns the active top-level alternative for a slot.
    /// </summary>
    private static int GetActiveTopLevelIndex(InputAction action, BindingSlot slot)
    {
        return string.IsNullOrEmpty(action.bindings[slot.PlainIndex].effectivePath)
            ? slot.CompositeIndex
            : slot.PlainIndex;
    }

    /// <summary>
    /// Formats the active alternative in one binding slot for the Options table.
    /// </summary>
    private static string FormatSlot(InputAction action, BindingSlot slot)
    {
        InputBinding plain = action.bindings[slot.PlainIndex];
        if (!string.IsNullOrEmpty(plain.effectivePath))
            return ShortenKey(action.GetBindingDisplayString(slot.PlainIndex));

        (string modifier, string key) = GetCompositePaths(action, slot.CompositeIndex);
        if (string.IsNullOrEmpty(modifier) || string.IsNullOrEmpty(key))
            return "UNBOUND";
        return $"{ShortenPath(modifier)}+{ShortenPath(key)}";
    }

    /// <summary>
    /// Converts one control path to its compact human-readable display form.
    /// </summary>
    private static string ShortenPath(string path)
    {
        return ShortenKey(
            InputControlPath.ToHumanReadableString(
                path,
                InputControlPath.HumanReadableStringOptions.OmitDevice
            )
        );
    }

    /// <summary>
    /// Checks whether an action map is exposed by the Options controls page.
    /// </summary>
    private static bool IsBindableMap(string map)
    {
        return map is "Global" or "Strategy";
    }

    /// <summary>
    /// Identifies system-reserved cancel and game-menu shortcuts.
    /// </summary>
    private static bool HasReservedPrimary(InputAction action)
    {
        return action?.actionMap?.name == "Global"
            && action.name is "CancelOrSettings" or "OpenGameMenu";
    }

    /// <summary>
    /// Checks whether an action intentionally binds a modifier as its complete input.
    /// </summary>
    private static bool IsModifierAction(string actionName)
    {
        return actionName is "MultiSelectModifier" or "RangeSelectModifier";
    }

    /// <summary>
    /// Shortens a human-readable key name for the compact binding columns.
    /// </summary>
    private static string ShortenKey(string display)
    {
        if (string.IsNullOrWhiteSpace(display))
            return "UNBOUND";
        return display
            .ToUpperInvariant()
            .Replace("ESCAPE", "ESC")
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

    /// <summary>
    /// Inserts spaces at semantic boundaries in an authored action name.
    /// </summary>
    private static string Humanize(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        StringBuilder builder = new StringBuilder(name.Length + 8);
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

    /// <summary>
    /// Returns the player-facing label for one authored input action.
    /// </summary>
    /// <param name="actionName">The authored input action name.</param>
    /// <returns>The label displayed by the Controls page.</returns>
    private static string GetActionLabel(string actionName)
    {
        return actionName switch
        {
            "CancelOrSettings" => "Cancel",
            "MultiSelectModifier" => "Toggle Selection Modifier",
            "RangeSelectModifier" => "Range Selection Modifier",
            _ => Humanize(actionName),
        };
    }

    /// <summary>
    /// Identifies the authored plain and modifier-composite alternatives for one logical slot.
    /// </summary>
    private readonly struct BindingSlot
    {
        internal readonly int PlainIndex;
        internal readonly int CompositeIndex;
        internal readonly int ModifierIndex;
        internal readonly int BindingIndex;

        /// <summary>
        /// Creates an authored binding-slot index set.
        /// </summary>
        internal BindingSlot(
            int plainIndex,
            int compositeIndex,
            int modifierIndex,
            int bindingIndex
        )
        {
            PlainIndex = plainIndex;
            CompositeIndex = compositeIndex;
            ModifierIndex = modifierIndex;
            BindingIndex = bindingIndex;
        }
    }

    /// <summary>
    /// Associates a controls row with its action and two logical slots.
    /// </summary>
    private readonly struct BindingTarget
    {
        internal readonly InputAction Action;
        internal readonly BindingSlot Primary;
        internal readonly BindingSlot Secondary;

        /// <summary>
        /// Creates a row binding target.
        /// </summary>
        internal BindingTarget(InputAction action, BindingSlot primary, BindingSlot secondary)
        {
            Action = action;
            Primary = primary;
            Secondary = secondary;
        }
    }
}
