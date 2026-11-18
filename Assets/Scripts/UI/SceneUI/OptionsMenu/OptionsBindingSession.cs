using System;
using System.Collections.Generic;
using System.Text;
using Rebellion.Util.Logging;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Manages the controls list and input rebinding against authored binding slots.
/// </summary>
internal sealed class OptionsBindingSession : IDisposable
{
    private const string _restartMessage =
        "The shortcut change failed. Restart the game before changing shortcuts again.";

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
    private readonly Dictionary<InputControl, string> _candidateModifiers = new();
    private InputActionRebindingExtensions.RebindingOperation _operation;
    private InputAction _listeningAction;
    private InputBinding[] _previousActionOverrides;
    private bool _rebindApplied;
    private InputAction _conflictOldAction;
    private int _conflictOldIndex;
    private InputAction _editingAction;
    private bool _rebindOwnsShortcutInput;
    private bool _textEntryOwnsShortcutInput;
    private bool _shortcutReleaseScheduled;

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
    /// Raised after a failed edit is cleaned up, with a player-facing recovery message.
    /// </summary>
    internal event Action<string> Failed;

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
    internal bool HasPendingConflict => _conflictOldAction != null;

    /// <summary>
    /// Creates a binding editor for the supplied input manager.
    /// </summary>
    /// <param name="inputManager">The application input manager.</param>
    internal OptionsBindingSession(InputManager inputManager)
    {
        _inputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
    }

    /// <summary>
    /// Rebuilds the controls rows from the authored Global and Strategy binding slots.
    /// </summary>
    internal void Rebuild()
    {
        if (!EnsureBindingsAvailable())
            return;

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
        if (!EnsureBindingsAvailable())
            return;

        if (
            _editingAction != null
            || _operation != null
            || HasPendingConflict
            || row < 0
            || row >= _targets.Count
        )
            return;

        BindingTarget target = _targets[row];
        if (target.Action == null)
            return;
        if (!secondary && HasReservedPrimary(target.Action))
            return;

        try
        {
            _editingAction = target.Action;
            _previousActionOverrides = CaptureOverrides(target.Action);
            ListeningRow = row;
            ListeningSecondary = secondary;
            SetRebindShortcutInputOwnership(true);
            PresentationChanged?.Invoke();
            StartRebind(target.Action, secondary ? target.Secondary : target.Primary);
        }
        catch (Exception exception)
        {
            RecoverRebind(exception);
        }
    }

    /// <summary>
    /// Restores the authored defaults for one binding row.
    /// </summary>
    /// <param name="row">The controls row to restore.</param>
    internal void RestoreDefault(int row)
    {
        if (!EnsureBindingsAvailable())
            return;

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
        if (!EnsureBindingsAvailable())
            return;

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

        InputAction oldAction = _conflictOldAction;
        InputBinding[] oldOverrides = null;
        try
        {
            oldOverrides = CaptureOverrides(oldAction);
            if (clearOld)
            {
                UnbindTopLevel(oldAction, _conflictOldIndex);
                Changed?.Invoke();
            }
            else
                RestoreOverrides(_editingAction, _previousActionOverrides);

            Rebuild();
            PresentationChanged?.Invoke();
            ClearEdit();
        }
        catch (Exception exception)
        {
            RecoverRebind(exception, oldAction, oldOverrides);
        }
    }

    /// <summary>
    /// Suppresses bindable actions while a text field owns keyboard input.
    /// </summary>
    /// <param name="active">Whether text entry is active.</param>
    internal void SetTextEntryActive(bool active)
    {
        _textEntryOwnsShortcutInput = active;
        if (active)
        {
            CancelScheduledShortcutRelease();
            ApplyShortcutSuppression();
        }
        else
            ReleaseShortcutSuppression();
    }

    /// <summary>
    /// Cancels the active interactive rebind operation.
    /// </summary>
    internal void CancelRebind()
    {
        try
        {
            _operation?.Cancel();
        }
        catch (Exception exception)
        {
            RecoverRebind(exception);
        }
    }

    /// <summary>
    /// Releases the active rebind operation and restores suppressed input state.
    /// </summary>
    public void Dispose()
    {
        try
        {
            StopCapture();
            RestoreOverrides(_editingAction, _previousActionOverrides);
        }
        catch (Exception exception)
        {
            RecoverRebind(exception);
        }
        finally
        {
            ClearEdit();
            _textEntryOwnsShortcutInput = false;
            CancelScheduledShortcutRelease();
            ReleaseShortcutSuppression();
        }
    }

    /// <summary>
    /// Creates an unbound listening action so modifier presses are not committed as the base key.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="slot">The slot.</param>
    private void StartRebind(InputAction action, BindingSlot slot)
    {
        _rebindApplied = false;
        _candidateModifiers.Clear();
        _listeningAction = new InputAction(type: InputActionType.Value);

        _operation = _listeningAction
            .PerformInteractiveRebinding()
            .WithRebindAddingNewBinding()
            .WithCancelingThrough("<Keyboard>/escape")
            .WithControlsExcluding("<Keyboard>/anyKey")
            .WithControlsExcluding("<Mouse>/leftButton")
            .WithControlsExcluding("<Mouse>/rightButton")
            .WithControlsExcluding("<Pointer>/delta")
            .WithControlsExcluding("<Pointer>/position")
            .WithControlsExcluding("<Pointer>/press");
        if (!IsModifierAction(action.name))
        {
            foreach (string path in _modifierControlPaths)
                _operation.WithControlsExcluding(path);
        }

        _operation
            .OnGeneratePath(GetRebindPath)
            .OnApplyBinding((_, path) => ApplyRebindPath(action, slot, path))
            .OnCancel(_ => FinishRebind(action, slot, false))
            .OnComplete(_ => FinishRebind(action, slot, true))
            .Start();

        // Observe each event after Unity has added its candidates, before capture settles.
        InputSystem.onEvent += CaptureCandidateModifiers;
    }

    /// <summary>
    /// Remembers the modifier held when each control first becomes a rebind candidate.
    /// </summary>
    /// <param name="eventPtr">The input event that supplied the candidates.</param>
    /// <param name="device">The device supplying the event.</param>
    private void CaptureCandidateModifiers(InputEventPtr eventPtr, InputDevice device)
    {
        if (_operation == null || !eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
            return;

        string modifier = GetPressedModifierPath(device as Keyboard ?? Keyboard.current, eventPtr);
        foreach (InputControl control in _operation.candidates)
            _candidateModifiers.TryAdd(control, modifier);
    }

    /// <summary>
    /// Converts vertical mouse-wheel input into a directional binding path.
    /// </summary>
    /// <param name="control">The selected input control.</param>
    /// <returns>The binding path to apply.</returns>
    private static string GetRebindPath(InputControl control)
    {
        if (control?.path == "/Mouse/scroll" && control is Vector2Control vector)
        {
            return vector.ReadValue().y < 0 ? "<Mouse>/scroll/down" : "<Mouse>/scroll/up";
        }
        if (control?.path == "/Mouse/scroll/y" && control is AxisControl axis)
        {
            return axis.ReadValue() < 0 ? "<Mouse>/scroll/down" : "<Mouse>/scroll/up";
        }

        return null;
    }

    /// <summary>
    /// Applies a captured key to either the plain or modifier-composite alternative.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="slot">The slot.</param>
    /// <param name="path">The path.</param>
    private void ApplyRebindPath(InputAction action, BindingSlot slot, string path)
    {
        try
        {
            string modifierPath = null;
            if (!IsModifierAction(action.name))
                _candidateModifiers.TryGetValue(_operation.selectedControl, out modifierPath);
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
        catch (Exception exception)
        {
            RecoverRebind(exception);
        }
    }

    /// <summary>
    /// Finalizes a rebind and asks for confirmation when its active signature is duplicated.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="slot">The slot.</param>
    /// <param name="completed">Whether completed.</param>
    private void FinishRebind(InputAction action, BindingSlot slot, bool completed)
    {
        if (_editingAction != action)
            return;

        try
        {
            StopCapture();
            if (!completed || !_rebindApplied)
            {
                RestoreOverrides(action, _previousActionOverrides);
                Rebuild();
                PresentationChanged?.Invoke();
                ClearEdit();
                return;
            }

            int activeIndex = GetActiveTopLevelIndex(action, slot);
            (InputAction other, int otherIndex) = FindConflict(action, activeIndex);
            if (other != null)
            {
                _conflictOldAction = other;
                _conflictOldIndex = otherIndex;
                Rebuild();
                PresentationChanged?.Invoke();
                ConflictRequested?.Invoke(
                    $"That input is already bound to \"{GetActionLabel(other.name)}\". Move it here and clear the old binding?"
                );
                return;
            }

            Changed?.Invoke();
            Rebuild();
            PresentationChanged?.Invoke();
            ClearEdit();
        }
        catch (Exception exception)
        {
            RecoverRebind(exception);
        }
    }

    /// <summary>
    /// Releases capture resources and listening state even when disposal fails.
    /// </summary>
    private void StopCapture()
    {
        InputSystem.onEvent -= CaptureCandidateModifiers;
        _candidateModifiers.Clear();
        InputActionRebindingExtensions.RebindingOperation operation = _operation;
        InputAction listeningAction = _listeningAction;
        _operation = null;
        _listeningAction = null;
        ListeningRow = -1;
        ListeningSecondary = false;
        try
        {
            operation?.Dispose();
        }
        finally
        {
            try
            {
                listeningAction?.Dispose();
            }
            finally
            {
                SetRebindShortcutInputOwnership(false);
            }
        }
    }

    /// <summary>
    /// Rolls back a failed shortcut edit and reports recovery after releasing capture ownership.
    /// </summary>
    /// <param name="exception">The edit failure.</param>
    /// <param name="oldAction">The action displaced during conflict resolution.</param>
    /// <param name="oldOverrides">The displaced action's overrides before resolution.</param>
    private void RecoverRebind(
        Exception exception,
        InputAction oldAction = null,
        InputBinding[] oldOverrides = null
    )
    {
        List<Exception> failures = new() { exception };
        bool restored = TryRecover(StopCapture, failures);
        restored &= TryRecover(() => RestoreOverrides(oldAction, oldOverrides), failures);
        restored &= TryRecover(
            () => RestoreOverrides(_editingAction, _previousActionOverrides),
            failures
        );
        ClearEdit();
        restored &= TryRecover(Rebuild, failures);
        if (!restored)
            _inputManager.RequireInputRestart();
        // Notification errors must not change whether the binding state was restored.
        TryRecover(() => PresentationChanged?.Invoke(), failures);
        Exception failure = failures.Count == 1 ? exception : new AggregateException(failures);
        GameLogger.Log($"Shortcut rebinding failed: {failure}", GameLogger.LogLevel.Error);
        NotifyFailure(
            restored
                ? "Couldn't change this shortcut. Your previous bindings were restored. Try again."
                : _restartMessage
        );
    }

    /// <summary>
    /// Rejects further binding access after an unrecoverable edit while keeping settings available.
    /// </summary>
    /// <returns>Whether the current runtime bindings remain available for editing.</returns>
    private bool EnsureBindingsAvailable()
    {
        if (!_inputManager.RequiresInputRestart)
            return true;
        NotifyFailure(_restartMessage);
        return false;
    }

    /// <summary>
    /// Presents a binding failure without allowing notification errors to escape cleanup.
    /// </summary>
    /// <param name="message">The player-facing recovery message.</param>
    private void NotifyFailure(string message)
    {
        try
        {
            Failed?.Invoke(message);
        }
        catch (Exception notificationException)
        {
            GameLogger.Log(
                $"Shortcut error notification failed: {notificationException}",
                GameLogger.LogLevel.Error
            );
        }
    }

    /// <summary>
    /// Attempts one recovery step without skipping the remaining cleanup steps on failure.
    /// </summary>
    /// <param name="recover">The recovery step.</param>
    /// <param name="failures">The collected edit and recovery failures.</param>
    /// <returns>Whether the recovery step completed.</returns>
    private static bool TryRecover(Action recover, ICollection<Exception> failures)
    {
        try
        {
            recover();
            return true;
        }
        catch (Exception exception)
        {
            failures.Add(exception);
            return false;
        }
    }

    /// <summary>
    /// Finds another active top-level binding with the same key and modifier signature.
    /// </summary>
    /// <param name="rebound">The rebound.</param>
    /// <param name="reboundIndex">The rebound index.</param>
    /// <returns>The matching conflict.</returns>
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
    /// <param name="action">The action.</param>
    /// <returns>The result of capture overrides.</returns>
    private static InputBinding[] CaptureOverrides(InputAction action)
    {
        InputBinding[] result = new InputBinding[action.bindings.Count];
        for (int index = 0; index < action.bindings.Count; index++)
            result[index] = action.bindings[index];
        return result;
    }

    /// <summary>
    /// Restores a previously captured set of binding overrides.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="overrides">The overrides.</param>
    private static void RestoreOverrides(InputAction action, IReadOnlyList<InputBinding> overrides)
    {
        if (action == null || overrides == null)
            return;

        if (action.bindings.Count != overrides.Count)
            throw new InvalidOperationException(
                "Shortcut bindings changed during the edit and could not be restored."
            );

        for (int index = 0; index < overrides.Count; index++)
            if (action.bindings[index].id != overrides[index].id)
                throw new InvalidOperationException(
                    "Shortcut bindings changed during the edit and could not be restored."
                );

        action.RemoveAllBindingOverrides();
        for (int index = 0; index < overrides.Count; index++)
            action.ApplyBindingOverride(index, overrides[index]);
    }

    /// <summary>
    /// Clears one active top-level binding and any composite parts it owns.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="index">The index.</param>
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
    /// <param name="action">The action.</param>
    /// <param name="slot">The slot.</param>
    private static void ClearComposite(InputAction action, BindingSlot slot)
    {
        action.ApplyBindingOverride(slot.ModifierIndex, string.Empty);
        action.ApplyBindingOverride(slot.BindingIndex, string.Empty);
    }

    /// <summary>
    /// Clears the completed edit, pending conflict, and rollback snapshot.
    /// </summary>
    private void ClearEdit()
    {
        _conflictOldAction = null;
        _editingAction = null;
        _conflictOldIndex = -1;
        _previousActionOverrides = null;
        _rebindApplied = false;
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
            CancelScheduledShortcutRelease();
            ApplyShortcutSuppression();
            return;
        }

        if (!_textEntryOwnsShortcutInput)
            ScheduleShortcutRelease();
    }

    /// <summary>
    /// Marks application shortcuts as suppressed while an editor owns keyboard input.
    /// </summary>
    private void ApplyShortcutSuppression()
    {
        _inputManager.SetApplicationShortcutsSuppressed(true);
    }

    /// <summary>
    /// Restores shortcuts after the input event that ended capture has been fully dispatched.
    /// </summary>
    private void ScheduleShortcutRelease()
    {
        if (_shortcutReleaseScheduled)
            return;

        _shortcutReleaseScheduled = true;
        InputSystem.onAfterUpdate += HandleInputUpdateFinished;
    }

    /// <summary>
    /// Releases shortcut ownership at a stable input-system update boundary.
    /// </summary>
    private void HandleInputUpdateFinished()
    {
        CancelScheduledShortcutRelease();
        ReleaseShortcutSuppression();
    }

    /// <summary>
    /// Removes a pending shortcut-restore callback.
    /// </summary>
    private void CancelScheduledShortcutRelease()
    {
        if (!_shortcutReleaseScheduled)
            return;

        InputSystem.onAfterUpdate -= HandleInputUpdateFinished;
        _shortcutReleaseScheduled = false;
    }

    /// <summary>
    /// Allows application shortcuts after exclusive keyboard input ends.
    /// </summary>
    private void ReleaseShortcutSuppression()
    {
        if (_rebindOwnsShortcutInput || _textEntryOwnsShortcutInput)
            return;

        _inputManager.SetApplicationShortcutsSuppressed(false);
    }

    /// <summary>
    /// Returns a normalized signature for a plain or modifier-composite binding.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="bindingIndex">The binding index.</param>
    /// <returns>The requested binding signature.</returns>
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
    /// <param name="action">The action.</param>
    /// <param name="compositeIndex">The composite index.</param>
    /// <returns>The requested composite paths.</returns>
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
    /// Returns the single modifier held in a candidate's event, or null when none or several are held.
    /// </summary>
    /// <param name="keyboard">The candidate's keyboard or the active keyboard for another device.</param>
    /// <param name="eventPtr">The candidate's state event.</param>
    /// <returns>The requested pressed modifier path.</returns>
    private static string GetPressedModifierPath(Keyboard keyboard, InputEventPtr eventPtr)
    {
        if (keyboard == null)
            return null;

        string result = null;
        int count = 0;
        if (
            IsModifierPressed(keyboard.leftCtrlKey, eventPtr)
            || IsModifierPressed(keyboard.rightCtrlKey, eventPtr)
        )
        {
            result = "<Keyboard>/ctrl";
            count++;
        }
        if (
            IsModifierPressed(keyboard.leftShiftKey, eventPtr)
            || IsModifierPressed(keyboard.rightShiftKey, eventPtr)
        )
        {
            result = "<Keyboard>/shift";
            count++;
        }
        if (
            IsModifierPressed(keyboard.leftAltKey, eventPtr)
            || IsModifierPressed(keyboard.rightAltKey, eventPtr)
        )
        {
            result = "<Keyboard>/alt";
            count++;
        }
        if (
            IsModifierPressed(keyboard.leftMetaKey, eventPtr)
            || IsModifierPressed(keyboard.rightMetaKey, eventPtr)
        )
        {
            result = "<Keyboard>/leftMeta";
            count++;
        }
        return count == 1 ? result : null;
    }

    /// <summary>
    /// Reads a modifier from its event, retaining held state when that event omits the control.
    /// </summary>
    /// <param name="control">The modifier key.</param>
    /// <param name="eventPtr">The candidate's state event.</param>
    /// <returns>Whether the modifier was held for the candidate.</returns>
    private static bool IsModifierPressed(ButtonControl control, InputEventPtr eventPtr)
    {
        return
            eventPtr.deviceId == control.device.deviceId
            && control.ReadValueFromEvent(eventPtr, out float value)
            ? value >= control.pressPointOrDefault
            : control.isPressed;
    }

    /// <summary>
    /// Gets the two named binding slots authored for an action.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns>The requested binding slots.</returns>
    private static (BindingSlot primary, BindingSlot secondary) GetBindingSlots(InputAction action)
    {
        return (GetBindingSlot(action, "Primary"), GetBindingSlot(action, "Secondary"));
    }

    /// <summary>
    /// Resolves one plain/composite binding pair by its authored slot name.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="name">The name.</param>
    /// <returns>The requested binding slot.</returns>
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
    /// <param name="action">The action.</param>
    /// <param name="slot">The slot.</param>
    /// <returns>The requested active top level index.</returns>
    private static int GetActiveTopLevelIndex(InputAction action, BindingSlot slot)
    {
        return string.IsNullOrEmpty(action.bindings[slot.PlainIndex].effectivePath)
            ? slot.CompositeIndex
            : slot.PlainIndex;
    }

    /// <summary>
    /// Formats the active alternative in one binding slot for the Options table.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="slot">The slot.</param>
    /// <returns>The formatted slot.</returns>
    private static string FormatSlot(InputAction action, BindingSlot slot)
    {
        InputBinding plain = action.bindings[slot.PlainIndex];
        if (!string.IsNullOrEmpty(plain.effectivePath))
            return ShortenPath(plain.effectivePath);

        (string modifier, string key) = GetCompositePaths(action, slot.CompositeIndex);
        if (string.IsNullOrEmpty(modifier) || string.IsNullOrEmpty(key))
            return "UNBOUND";
        return $"{ShortenPath(modifier)}+{ShortenPath(key)}";
    }

    /// <summary>
    /// Converts one control path to its compact human-readable display form.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The result of shorten path.</returns>
    private static string ShortenPath(string path)
    {
        if (path is "<Keyboard>/leftMeta" or "<Keyboard>/rightMeta")
            return "CMD";
        if (path == "<Mouse>/scroll/up")
            return "WHEEL UP";
        if (path == "<Mouse>/scroll/down")
            return "WHEEL DOWN";

        string display = InputControlPath.ToHumanReadableString(
            path,
            InputControlPath.HumanReadableStringOptions.OmitDevice,
            control: Keyboard.current
        );
        if (!IsReadableKeyName(display))
            display = InputControlPath.ToHumanReadableString(
                path,
                InputControlPath.HumanReadableStringOptions.OmitDevice
            );
        return ShortenKey(display);
    }

    /// <summary>
    /// Checks whether a platform key name can be displayed as a shortcut caption.
    /// </summary>
    /// <param name="name">The platform key name.</param>
    /// <returns>True when the name contains visible text and no control characters.</returns>
    private static bool IsReadableKeyName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        foreach (char character in name)
            if (char.IsControl(character))
                return false;
        return true;
    }

    /// <summary>
    /// Checks whether an action map is exposed by the Options controls page.
    /// </summary>
    /// <param name="map">The map.</param>
    /// <returns>True when the bindable map condition is met; otherwise false.</returns>
    private static bool IsBindableMap(string map)
    {
        return map is "Global" or "Strategy";
    }

    /// <summary>
    /// Identifies system-reserved cancel and game-menu shortcuts.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns>True when the reserved primary condition is met; otherwise false.</returns>
    private static bool HasReservedPrimary(InputAction action)
    {
        return action?.actionMap?.name == "Global"
            && action.name is "CancelOrSettings" or "OpenGameMenu";
    }

    /// <summary>
    /// Checks whether an action intentionally binds a modifier as its complete input.
    /// </summary>
    /// <param name="actionName">The action name.</param>
    /// <returns>True when the modifier action condition is met; otherwise false.</returns>
    private static bool IsModifierAction(string actionName)
    {
        return actionName is "MultiSelectModifier" or "RangeSelectModifier";
    }

    /// <summary>
    /// Shortens a human-readable key name for the compact binding columns.
    /// </summary>
    /// <param name="display">The display.</param>
    /// <returns>The result of shorten key.</returns>
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
            .Replace("COMMAND", "CMD")
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
    /// <param name="name">The name.</param>
    /// <returns>The result of humanize.</returns>
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
            "AdvisorBuildShips" => "Build Ships",
            "AdvisorBuildTroops" => "Build Troops",
            "AdvisorBuildFacilities" => "Build Facilities",
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
        /// <param name="plainIndex">The plain index.</param>
        /// <param name="compositeIndex">The composite index.</param>
        /// <param name="modifierIndex">The modifier index.</param>
        /// <param name="bindingIndex">The binding index.</param>
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
        /// <param name="action">The action.</param>
        /// <param name="primary">The primary.</param>
        /// <param name="secondary">The secondary.</param>
        internal BindingTarget(InputAction action, BindingSlot primary, BindingSlot secondary)
        {
            Action = action;
            Primary = primary;
            Secondary = secondary;
        }
    }
}
