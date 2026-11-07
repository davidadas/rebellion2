using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Dispatches tactical image-button releases and repeats held camera controls.
/// </summary>
public sealed class TacticalCombatHudButton : Button, ICanvasRaycastFilter
{
    [SerializeField]
    private int _repeatMilliseconds;
    private Texture2D _hitMask;
    private bool _held;
    private bool _cancelPendingPointerClick;
    private double _nextRepeat;
    private int _repeatCounter;
    public event Action<bool> HoverChanged;

    /// <summary>
    /// Retains a fixed readable image for bitmap-shaped hit testing independently of draw state.
    /// </summary>
    /// <param name="mask">The authored up-image mask, or null for a rectangular control.</param>
    public void SetHitMask(Texture2D mask) => _hitMask = mask;

    /// <summary>
    /// Tests the addressed pixel against the fixed control mask without consulting pressed artwork.
    /// </summary>
    /// <param name="screenPoint">The pointer in screen coordinates.</param>
    /// <param name="eventCamera">The canvas event camera.</param>
    /// <returns>Whether the pointer addresses a visible mask pixel or a rectangular control.</returns>
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (_hitMask == null)
            return true;
        RectTransform rect = (RectTransform)transform;
        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect,
                screenPoint,
                eventCamera,
                out Vector2 local
            )
        )
            return false;
        int x = Mathf.FloorToInt((local.x - rect.rect.xMin) * _hitMask.width / rect.rect.width);
        int y = Mathf.FloorToInt((rect.rect.yMax - local.y) * _hitMask.height / rect.rect.height);
        return x > 0
            && y > 0
            && x < _hitMask.width
            && y < _hitMask.height
            && _hitMask.GetPixel(x, _hitMask.height - 1 - y).a != 0;
    }

    /// <summary>
    /// Captures the authored repeat interval used by camera controls.
    /// </summary>
    /// <param name="milliseconds">The repeat interval, or zero for a single press.</param>
    public void SetRepeatInterval(int milliseconds) => _repeatMilliseconds = milliseconds;

    /// <summary>
    /// Captures a primary pointer press and begins its held-repeat delay.
    /// </summary>
    /// <param name="eventData">The button pointer event.</param>
    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);
        if (
            eventData.button != PointerEventData.InputButton.Left
            || !IsActive()
            || !IsInteractable()
        )
            return;
        _cancelPendingPointerClick = false;
        _held = true;
        _repeatCounter = 0;
        _nextRepeat = Time.unscaledTimeAsDouble + _repeatMilliseconds * 0.001;
    }

    /// <summary>
    /// Ends held-button ownership when the pointer is released.
    /// </summary>
    /// <param name="eventData">The button pointer event.</param>
    public override void OnPointerUp(PointerEventData eventData)
    {
        _held = false;
        base.OnPointerUp(eventData);
    }

    /// <summary>
    /// Dispatches one completed primary click through the standard button release boundary.
    /// </summary>
    /// <param name="eventData">The completed pointer click.</param>
    public override void OnPointerClick(PointerEventData eventData)
    {
        if (!_cancelPendingPointerClick)
            base.OnPointerClick(eventData);
    }

    /// <summary>
    /// Completes an available keyboard click or cancels the addressed control's pending pointer activation on key release.
    /// </summary>
    /// <param name="activate">Whether to dispatch a completed click rather than only release press ownership.</param>
    public void ApplyKeyboardActivation(bool activate)
    {
        if (activate && (!IsActive() || !IsInteractable()))
        {
            _cancelPendingPointerClick = true;
            return;
        }
        _cancelPendingPointerClick = !activate;
        _held = false;
        _repeatCounter = 0;
        base.OnPointerUp(
            new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }
        );
        if (activate)
            onClick.Invoke();
    }

    /// <summary>
    /// Starts the view's shared delayed tooltip.
    /// </summary>
    /// <param name="eventData">The pointer entering this button.</param>
    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        HoverChanged?.Invoke(true);
    }

    /// <summary>
    /// Cancels the view's shared tooltip without changing a held-button command.
    /// </summary>
    /// <param name="eventData">The pointer leaving this button.</param>
    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);
        HoverChanged?.Invoke(false);
    }

    /// <summary>
    /// Releases pointer ownership when the authored control is hidden or disabled.
    /// </summary>
    protected override void OnDisable()
    {
        _held = false;
        HoverChanged?.Invoke(false);
        base.OnDisable();
    }

    /// <summary>
    /// Repeats an active held camera control at its authored unscaled interval.
    /// </summary>
    private void Update()
    {
        if (
            !_held
            || _repeatMilliseconds == 0
            || !IsActive()
            || !IsInteractable()
            || Time.unscaledTimeAsDouble < _nextRepeat
        )
            return;
        double now = Time.unscaledTimeAsDouble;
        while (_held && now >= _nextRepeat)
        {
            _nextRepeat += _repeatMilliseconds * 0.001;
            int previous = _repeatCounter++;
            if (previous > 250 / _repeatMilliseconds)
                onClick.Invoke();
        }
    }
}
