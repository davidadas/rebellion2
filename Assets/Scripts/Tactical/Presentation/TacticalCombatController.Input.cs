using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Vector3N = System.Numerics.Vector3;

namespace Rebellion.Tactical.Presentation
{
    public sealed partial class TacticalCombatController : IDisposable
    {
        /// <summary>
        /// Transfers battle input ownership to or from the application menu without changing pause state.
        /// </summary>
        /// <param name="open">Whether an application menu currently owns user input.</param>
        public void SetApplicationMenuOpen(bool open)
        {
            _applicationMenuOpen = open;
            if (open)
                foreach (TacticalCombatHudControl definition in TacticalCombatHudLayout.Controls)
                    if (TryCameraControl(definition.Control, out _))
                        _view.ApplyFrameKeyboardActivation(definition.Control, false);
            if (!open)
                CaptureHeldKeys();
            RefreshControls();
        }

        /// <summary>
        /// Captures held keys when battle input resumes so the same press cannot reopen or traverse a panel.
        /// </summary>
        private void CaptureHeldKeys()
        {
            _inspectionKeyHeld = Keyboard.current?.tabKey.isPressed == true;
            _escapeKeyHeld = Keyboard.current?.escapeKey.isPressed == true;
        }

        /// <summary>
        /// Routes native camera and direct-command transitions while preserving their distinct release semantics.
        /// </summary>
        /// <param name="key">The native keyboard key.</param>
        /// <param name="pressed">Whether this is a key-down event.</param>
        /// <returns>Whether the battle currently owns the addressed transition.</returns>
        private bool HandleKeyboardEvent(KeyCode key, bool pressed)
        {
            if (key == KeyCode.Tab && !pressed)
                _inspectionKeyHeld = false;
            if (key == KeyCode.Escape && !pressed)
                _escapeKeyHeld = false;
            if (
                !_ready
                || _disposed
                || _completing
                || _applicationMenuOpen
                || _session.IsComplete
                || _session.PendingAttackRunMovie != null
            )
                return false;
            if (key == KeyCode.Escape)
            {
                if (pressed && !_escapeKeyHeld)
                {
                    _escapeKeyHeld = true;
                    MenuControlRequested?.Invoke(TacticalCombatControl.OpenOptions);
                }
                return true;
            }
            if (TacticalCombatKeyboard.TryGetCameraControl(key, out TacticalCombatControl control))
            {
                _view.ApplyFrameKeyboardActivation(control, pressed);
                return true;
            }
            if (key == KeyCode.Tab)
            {
                if (pressed && _inspectionKeyHeld)
                    return true;
                _inspectionKeyHeld = pressed;
                Keyboard keyboard = Keyboard.current;
                bool shift =
                    keyboard != null
                    && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
                _view.HandleInspectionKey(Key.Tab, pressed, shift);
                if (pressed)
                    _view.HandleInspectionKey(Key.Tab, false, shift);
                return true;
            }
            if (key == KeyCode.Space)
            {
                if (pressed)
                    _session.Selection.QueueNearestTarget();
                return true;
            }
            if (TacticalCombatKeyboard.TryGetObjectCategoryControl(key, out control))
            {
                if (pressed)
                {
                    _view.ClearCategoryKeyboardState();
                    _view.ApplyFrameKeyboardActivation(control, true);
                }
                return true;
            }
            if (pressed && TacticalCombatKeyboard.TryGetCameraCategory(key, out int category))
            {
                _view.Renderer.ShowCategory(category);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Routes primary-camera controls while retaining the selected participant's live focus.
        /// </summary>
        /// <param name="command">The requested camera operation.</param>
        private void ApplyCameraCommand(TacticalCombatCameraCommand command)
        {
            if (command == TacticalCombatCameraCommand.Focus)
            {
                if (_session.Selection.PrimaryWaypoint is Vector3N point)
                    _view.Renderer.Focus(point);
                else
                    _view.Renderer.Focus(_session.Selection.PrimaryUnit);
            }
            else
                _view.Renderer.ApplyCameraCommand(command);
        }

        /// <summary>
        /// Translates a screen pointer into normalized battle viewport coordinates.
        /// </summary>
        /// <param name="eventData">The source pointer event.</param>
        /// <returns>The normalized point.</returns>
        private Vector2 GetViewportPoint(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _view.Viewport,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 local
            );
            Rect rect = _view.Viewport.rect;
            return new Vector2(
                (local.x - rect.xMin) / rect.width,
                (local.y - rect.yMin) / rect.height
            );
        }

        /// <summary>
        /// Queues pointer-down selection or contextual extension using ordered neighboring pixel picks.
        /// </summary>
        /// <param name="eventData">The source viewport press.</param>
        private void HandlePointerDown(PointerEventData eventData)
        {
            if (
                !_ready
                || _applicationMenuOpen
                || _session.IsArriving
                || _session.IsComplete
                || _session.PendingAttackRunMovie != null
            )
                return;
            bool right = eventData.button == PointerEventData.InputButton.Right;
            if (!right && eventData.button != PointerEventData.InputButton.Left)
                return;
            bool extend = Keyboard.current?.ctrlKey.isPressed == true;
            bool hit = _view.Renderer.TryPickNeighborhood(
                GetViewportPoint(eventData),
                right,
                out TacticalCombatUnit unit,
                out Vector3N waypoint
            );
            if (!right && !extend)
            {
                if (hit)
                    _view.ClearCategoryKeyboardState();
                _session.Selection.QueueClear();
            }
            if (hit)
            {
                if (unit != null)
                    _session.Selection.QueueObject(unit, extend);
                else
                    _session.Selection.QueueWaypoint(waypoint, extend);
            }
            _hudRefresh = 0;
        }

        /// <summary>
        /// Applies a semantic HUD command to the current player selection.
        /// </summary>
        /// <param name="control">The requested control.</param>
        private void HandleControl(TacticalCombatControl control)
        {
            if (
                !_ready
                || _disposed
                || _completing
                || _applicationMenuOpen
                || _session.PendingAttackRunMovie != null
            )
                return;
            if (_session.IsComplete)
                return;
            if (control == TacticalCombatControl.Pause)
                _session.IsPaused = !_session.IsPaused;
            else if (TryCameraControl(control, out TacticalCombatCameraCommand camera))
                ApplyCameraCommand(camera);
            else if (
                control
                is >= TacticalCombatControl.NavigationRing1
                    and <= TacticalCombatControl.NavigationRing4
            )
            {
                int index = (int)control - (int)TacticalCombatControl.NavigationRing1;
                _navigationSelected[index] = !_navigationSelected[index];
                _view.Renderer.ToggleNavigationRing(3 - index);
            }
            else if (control == TacticalCombatControl.OpenMenu)
                _hud.OpenMenu();
            else if (control == TacticalCombatControl.CloseMenu)
                _hud.CloseMenu();
            else if (control == TacticalCombatControl.Withdraw)
            {
                if (_session.PreparePlayerWithdrawal())
                    _hud.OpenWithdrawalConfirmation();
            }
            else if (control == TacticalCombatControl.ConfirmWithdrawal)
            {
                _session.ConfirmPlayerWithdrawal();
                _hud.CloseWithdrawalConfirmation();
            }
            else if (control == TacticalCombatControl.CancelWithdrawal)
                _hud.CloseWithdrawalConfirmation();
            else if (control == TacticalCombatControl.OpenOrders)
                _hud.OpenOrders(SelectedRequestedGroup()?.Order ?? TacticalCombatOrder.None);
            else if (control == TacticalCombatControl.OpenTactics)
            {
                TacticalCombatGroup group = SelectedRequestedGroup();
                _hud.OpenTactics(
                    group?.Order ?? TacticalCombatOrder.None,
                    group?.CloseAttack == true
                );
            }
            else if (control == TacticalCombatControl.CancelPanel)
                _hud.CloseEditor();
            else if (control == TacticalCombatControl.ConfirmPanel)
                ConfirmPanel();
            else if (_hud.Panel is TacticalCombatHudPanel.Orders or TacticalCombatHudPanel.Tactics)
                _hud.SelectControl(control);
            else if (
                control is TacticalCombatControl.PreviousObject or TacticalCombatControl.NextObject
            )
                _session.Selection.QueueDisplayMode(control == TacticalCombatControl.NextObject);
            else if (control == TacticalCombatControl.ResolveImmediately)
                _session.RequestImmediateResult();
            else if (control == TacticalCombatControl.OpenOptions)
            {
                MenuControlRequested?.Invoke(control);
            }
            else if (
                control
                is TacticalCombatControl.HighlightAlliance
                    or TacticalCombatControl.HighlightEmpire
            )
            {
                int faction = control == TacticalCombatControl.HighlightAlliance ? 0 : 1;
                _factionHighlighted[faction] = !_factionHighlighted[faction];
                _view.Renderer.SetFactionHighlight(faction, _factionHighlighted[faction]);
            }
            else if (control == TacticalCombatControl.Superlaser)
                _session.Selection.QueueStationTarget();
            else if (control == TacticalCombatControl.ToggleAutomation)
                _session.Selection.QueueToggleAutomation();
            else if (
                control
                is >= TacticalCombatControl.ObjectGroup1
                    and <= TacticalCombatControl.ObjectGroup8
            )
            {
                int category = (int)control - (int)TacticalCombatControl.ObjectGroup1;
                TacticalCombatSelectionKind kind = _session.Selection.Kind;
                if (
                    _session.GetObjectGroup(_session.PlayerSide, category) != null
                    || kind == TacticalCombatSelectionKind.MultipleObjects
                    || kind == TacticalCombatSelectionKind.SingleObject
                        && _session.IsDirectControlEnabled
                )
                    _session.Selection.QueueObjectCategory(category);
            }
            else if (
                control
                is >= TacticalCombatControl.FighterGroup1
                    and <= TacticalCombatControl.FighterGroup4
            )
                _session.Selection.QueueFighterCategory(
                    (int)control - (int)TacticalCombatControl.FighterGroup1
                );
            RefreshHud();
            RefreshControls();
            _hudRefresh = 0;
        }

        /// <summary>
        /// Resolves the current requested owner without promoting copied highlights into command membership.
        /// </summary>
        /// <returns>The context's requested owner, or null for a temporary multi-selection.</returns>
        private TacticalCombatGroup SelectedRequestedGroup() =>
            _session.Selection.Kind == TacticalCombatSelectionKind.MultipleObjects
                ? null
                : _session.Selection.Group
                    ?? (
                        _session.Selection.PrimaryUnit is TacticalCombatUnit unit
                            ? _session.GetRequestedGroup(unit)
                            : null
                    );

        /// <summary>
        /// Commits only the editor's confirmed mission, maneuver, and engagement choices.
        /// </summary>
        private void ConfirmPanel()
        {
            if (_hud.Panel == TacticalCombatHudPanel.Orders && _hud.HasOrderChanges)
                _session.Selection.RequestOrder(_hud.PendingOrder);
            else if (_hud.Panel == TacticalCombatHudPanel.Tactics)
            {
                TacticalCombatGroup group = SelectedRequestedGroup();
                TacticalCombatOrder order = _hud.GetPendingManeuver();
                if (_hud.HasManeuverChanges && (group?.Order ?? TacticalCombatOrder.None) != order)
                    _session.Selection.RequestOrder(order);
                if (
                    _hud.HasAttackModeChanges
                    && (group == null || group.CloseAttack != _hud.PendingCloseAttack)
                )
                    _session.Selection.RequestAttackMode(_hud.PendingCloseAttack);
            }
            _hud.CloseEditor();
        }

        /// <summary>
        /// Installs the right-panel context only at the gameplay owner's actual replacement boundary.
        /// </summary>
        /// <param name="kind">The newly installed selection context.</param>
        private void HandleContextReplaced(TacticalCombatSelectionKind kind)
        {
            _view.ClearCategoryKeyboardState();
            _hud.ReplaceContext(kind);
            _hudRefresh = 0;
        }

        /// <summary>
        /// Resolves one camera image into the same command used by its keyboard shortcut.
        /// </summary>
        /// <param name="control">The image control.</param>
        /// <param name="command">Receives the camera command.</param>
        /// <returns>Whether the control addresses the camera.</returns>
        private static bool TryCameraControl(
            TacticalCombatControl control,
            out TacticalCombatCameraCommand command
        )
        {
            command = control switch
            {
                TacticalCombatControl.CameraZoomIn => TacticalCombatCameraCommand.ZoomIn,
                TacticalCombatControl.CameraZoomOut => TacticalCombatCameraCommand.ZoomOut,
                TacticalCombatControl.CameraRotateLeft => TacticalCombatCameraCommand.RotateLeft,
                TacticalCombatControl.CameraRotateRight => TacticalCombatCameraCommand.RotateRight,
                TacticalCombatControl.CameraTiltUp => TacticalCombatCameraCommand.TiltUp,
                TacticalCombatControl.CameraTiltDown => TacticalCombatCameraCommand.TiltDown,
                TacticalCombatControl.SaveCamera => TacticalCombatCameraCommand.Save,
                TacticalCombatControl.RestoreCamera => TacticalCombatCameraCommand.Restore,
                _ => TacticalCombatCameraCommand.Focus,
            };
            return control
                is TacticalCombatControl.CameraZoomIn
                    or TacticalCombatControl.CameraZoomOut
                    or TacticalCombatControl.CameraRotateLeft
                    or TacticalCombatControl.CameraRotateRight
                    or TacticalCombatControl.CameraTiltUp
                    or TacticalCombatControl.CameraTiltDown
                    or TacticalCombatControl.SaveCamera
                    or TacticalCombatControl.RestoreCamera
                    or TacticalCombatControl.FocusCamera;
        }
    }
}
