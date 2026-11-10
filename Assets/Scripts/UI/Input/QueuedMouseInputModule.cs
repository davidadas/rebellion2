using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Preserves mouse transitions between frames while retaining the shared UI gesture and navigation rules.
/// </summary>
public sealed class QueuedMouseInputModule : StandaloneInputModule
{
    private static readonly int[] _pointerIds = { kMouseLeftId, kMouseRightId, kMouseMiddleId };
    private readonly List<MouseSample> _pending = new List<MouseSample>();
    private readonly MouseState _mouseState = new MouseState();
    private readonly double[] _pressTimes = { double.NaN, double.NaN, double.NaN };
    private Mouse _mouse;
    private Vector2 _capturedPosition;
    private byte _capturedButtons;
    private byte _processedButtons;
    private MouseSample _sample;
    private Vector2 _scroll;
    private bool _processingSample;
    private bool _resetRequested;
    private bool _usingQueuedMouse;

    /// <summary>
    /// Registers mouse capture when the runtime event system becomes available.
    /// </summary>
    protected override void OnEnable()
    {
        base.OnEnable();
        ResetMouse(Mouse.current);
        if (Application.isPlaying)
        {
            InputSystem.onEvent += CaptureMouse;
            InputSystem.onDeviceChange += HandleDeviceChange;
        }
    }

    /// <summary>
    /// Releases capture and cancels incomplete gestures when the module is disabled.
    /// </summary>
    protected override void OnDisable()
    {
        InputSystem.onEvent -= CaptureMouse;
        InputSystem.onDeviceChange -= HandleDeviceChange;
        _pending.Clear();
        CancelMousePointers();
        base.OnDisable();
    }

    /// <summary>
    /// Resets pointer state on focus or input-source changes before updating navigation.
    /// </summary>
    public override void UpdateModule()
    {
        if (!eventSystem.isFocused || _mouse != Mouse.current)
            ResetMouse(Mouse.current);
        bool useQueuedMouse =
            eventSystem.isFocused
            && _mouse != null
            && Pointer.current == _mouse
            && input.touchCount == 0;
        if (_resetRequested || _usingQueuedMouse != useQueuedMouse)
        {
            CancelMousePointers();
            _resetRequested = false;
            _usingQueuedMouse = useQueuedMouse;
        }
        base.UpdateModule();
    }

    /// <summary>
    /// Processes queued mouse transitions before dispatching navigation once for the frame.
    /// </summary>
    public override void Process()
    {
        if (!isActiveAndEnabled)
            return;
        if (!_usingQueuedMouse)
        {
            _pending.Clear();
            _processedButtons = _capturedButtons;
            base.Process();
            return;
        }

        bool used = SendUpdateEventToSelectedObject();
        if (!isActiveAndEnabled || _resetRequested)
            return;
        if (input.mousePresent)
        {
            _processingSample = true;
            try
            {
                _scroll = Vector2.zero;
                for (int i = 0; i < _pending.Count; i++)
                {
                    _sample = _pending[i];
                    ProcessMouseEvent();
                    if (!isActiveAndEnabled || _resetRequested)
                        return;
                }
                _pending.Clear();

                _sample = new MouseSample(
                    _mouse.position.ReadValue(),
                    _processedButtons,
                    InputState.currentTime,
                    false
                );
                _scroll = input.mouseScrollDelta;
                ProcessMouseEvent();
            }
            finally
            {
                _processingSample = false;
            }
        }

        if (eventSystem.sendNavigationEvents)
        {
            if (!used)
                used = SendMoveEventToSelectedObject();
            if (!used)
                SendSubmitEventToSelectedObject();
        }
    }

    /// <summary>
    /// Cancels outstanding mouse gestures when another module takes input ownership.
    /// </summary>
    public override void DeactivateModule()
    {
        CancelMousePointers();
        ResetMouse(Mouse.current);
        base.DeactivateModule();
    }

    /// <summary>
    /// Creates the shared pointer presentation for the next ordered mouse sample.
    /// </summary>
    /// <param name="id">The mouse device index requested by the shared dispatcher.</param>
    /// <returns>The mouse pointers and their individual button transitions.</returns>
    protected override MouseState GetMousePointerEventData(int id)
    {
        if (!_processingSample)
            return base.GetMousePointerEventData(id);

        bool created = GetPointerData(kMouseLeftId, out PointerEventData left, true);
        left.Reset();
        Vector2 position =
            Cursor.lockState == CursorLockMode.Locked ? new Vector2(-1, -1) : _sample.Position;
        left.delta =
            created || Cursor.lockState == CursorLockMode.Locked
                ? Vector2.zero
                : position - left.position;
        left.position = position;
        left.scrollDelta = _scroll;
        left.button = PointerEventData.InputButton.Left;
        eventSystem.RaycastAll(left, m_RaycastResultCache);
        left.pointerCurrentRaycast = FindFirstRaycast(m_RaycastResultCache);
        m_RaycastResultCache.Clear();
        SetButtonState(0, left);

        for (int button = 1; button < _pointerIds.Length; button++)
        {
            GetPointerData(_pointerIds[button], out PointerEventData pointer, true);
            pointer.Reset();
            CopyFromTo(left, pointer);
            pointer.button = (PointerEventData.InputButton)button;
            SetButtonState(button, pointer);
        }
        _processedButtons = _sample.Buttons;
        return _mouseState;
    }

    /// <summary>
    /// Records button edges and held movement with the coordinates from their input event.
    /// </summary>
    /// <param name="eventPtr">The uncollapsed input event.</param>
    /// <param name="device">The device supplying the event.</param>
    private void CaptureMouse(InputEventPtr eventPtr, InputDevice device)
    {
        if (
            device is not Mouse mouse
            || mouse != Mouse.current
            || EventSystem.current != eventSystem
            || !eventSystem.isFocused
            || InputState.currentUpdateType == InputUpdateType.Editor
            || !eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()
        )
            return;

        if (_mouse != mouse)
            ResetMouse(mouse);

        Vector2 position = mouse.position.ReadValueFromEvent(eventPtr, out Vector2 value)
            ? value
            : mouse.position.ReadValue();
        byte buttons = 0;
        if (ReadButton(mouse.leftButton, eventPtr))
            buttons |= 1;
        if (ReadButton(mouse.rightButton, eventPtr))
            buttons |= 2;
        if (ReadButton(mouse.middleButton, eventPtr))
            buttons |= 4;
        bool buttonTransition = buttons != _capturedButtons;
        if (buttonTransition || _capturedButtons != 0 && position != _capturedPosition)
        {
            MouseSample sample = new MouseSample(
                position,
                buttons,
                eventPtr.time,
                buttonTransition
            );
            if (
                !buttonTransition
                && _pending.Count > 0
                && !_pending[_pending.Count - 1].IsTransition
            )
                _pending[_pending.Count - 1] = sample;
            else
                _pending.Add(sample);
        }
        _capturedButtons = buttons;
        _capturedPosition = position;
    }

    /// <summary>
    /// Reads a button from the event, retaining its current value for a partial state update.
    /// </summary>
    /// <param name="button">The mouse button control.</param>
    /// <param name="eventPtr">The input event to inspect.</param>
    /// <returns>Whether the button is pressed in this event.</returns>
    private static bool ReadButton(ButtonControl button, InputEventPtr eventPtr)
    {
        return button.ReadValueFromEvent(eventPtr, out float value)
            ? value >= button.pressPointOrDefault
            : button.isPressed;
    }

    /// <summary>
    /// Supplies one edge and its elapsed input time to the shared click dispatcher.
    /// </summary>
    /// <param name="button">The mouse button index.</param>
    /// <param name="pointer">The persistent pointer for this button.</param>
    private void SetButtonState(int button, PointerEventData pointer)
    {
        int mask = 1 << button;
        PointerEventData.FramePressState state = PointerEventData.FramePressState.NotChanged;
        if (((_sample.Buttons ^ _processedButtons) & mask) != 0)
        {
            bool pressed = (_sample.Buttons & mask) != 0;
            state = pressed
                ? PointerEventData.FramePressState.Pressed
                : PointerEventData.FramePressState.Released;
            if (pressed)
            {
                if (!double.IsNaN(_pressTimes[button]))
                    pointer.clickTime =
                        Time.unscaledTime - (float)(_sample.Time - _pressTimes[button]);
                _pressTimes[button] = _sample.Time;
            }
        }
        _mouseState.SetButtonState(pointer.button, state, pointer);
    }

    /// <summary>
    /// Discards transitions belonging to a reset or disconnected mouse.
    /// </summary>
    /// <param name="device">The changed input device.</param>
    /// <param name="change">The device lifecycle transition.</param>
    private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (
            device == _mouse
            && change
                is InputDeviceChange.Removed
                    or InputDeviceChange.Disconnected
                    or InputDeviceChange.Disabled
                    or InputDeviceChange.SoftReset
                    or InputDeviceChange.HardReset
        )
            ResetMouse(null);
    }

    /// <summary>
    /// Establishes the device's current state without interpreting held buttons as new presses.
    /// </summary>
    /// <param name="mouse">The current mouse, or null when none is available.</param>
    private void ResetMouse(Mouse mouse)
    {
        _pending.Clear();
        _mouse = mouse;
        _capturedPosition = mouse?.position.ReadValue() ?? Vector2.zero;
        _capturedButtons = 0;
        if (mouse != null)
        {
            if (mouse.leftButton.isPressed)
                _capturedButtons |= 1;
            if (mouse.rightButton.isPressed)
                _capturedButtons |= 2;
            if (mouse.middleButton.isPressed)
                _capturedButtons |= 4;
        }
        _processedButtons = _capturedButtons;
        for (int i = 0; i < _pressTimes.Length; i++)
            _pressTimes[i] = double.NaN;
        _resetRequested = true;
    }

    /// <summary>
    /// Releases pointer visuals and drags without turning canceled gestures into clicks or drops.
    /// </summary>
    private void CancelMousePointers()
    {
        foreach (int id in _pointerIds)
        {
            PointerEventData pointer = GetLastPointerEventData(id);
            if (pointer == null)
                continue;
            pointer.eligibleForClick = false;
            ExecuteEvents.Execute(pointer.pointerPress, pointer, ExecuteEvents.pointerUpHandler);
            if (pointer.dragging)
                ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.endDragHandler);
            pointer.pointerPress = null;
            pointer.rawPointerPress = null;
            pointer.pointerClick = null;
            pointer.pointerDrag = null;
            pointer.dragging = false;
            HandlePointerExitAndEnter(pointer, null);
            RemovePointerData(pointer);
        }
    }

    /// <summary>
    /// Carries one mouse transition independently of the final state of the rendered frame.
    /// </summary>
    private readonly struct MouseSample
    {
        public Vector2 Position { get; }
        public byte Buttons { get; }
        public double Time { get; }
        public bool IsTransition { get; }

        /// <summary>
        /// Captures the position, held buttons, and event time for ordered dispatch.
        /// </summary>
        /// <param name="position">The mouse position at this transition.</param>
        /// <param name="buttons">The held left, right, and middle button mask.</param>
        /// <param name="time">The input event time in seconds.</param>
        /// <param name="isTransition">Whether a mouse button changed state.</param>
        public MouseSample(Vector2 position, byte buttons, double time, bool isTransition)
        {
            Position = position;
            Buttons = buttons;
            Time = time;
            IsTransition = isTransition;
        }
    }
}
