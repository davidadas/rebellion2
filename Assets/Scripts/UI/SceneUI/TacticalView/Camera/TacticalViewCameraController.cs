using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Owns player navigation for the tactical view's Unity camera.
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Camera))]
public sealed class TacticalViewCameraController : MonoBehaviour
{
    private const float _pointerDragThreshold = 4f;
    private const float _pointerRadiansPerPixel = 0.1f * Mathf.Deg2Rad;
    private const float _yawRadiansPerSecond = 90f * Mathf.Deg2Rad;
    private const float _minimumElevation = -85f * Mathf.Deg2Rad;
    private const float _maximumElevation = 85f * Mathf.Deg2Rad;
    private const float _wheelZoomInMultiplier = 0.85f;
    private const float _wheelZoomOutMultiplier = 1.15f;
    private const float _keyboardZoomInMultiplier = 0.9f;
    private const float _keyboardZoomOutMultiplier = 1.1f;
    private const float _twoButtonZoomRatePerPixel = 0.005f;
    private const float _fastPanMultiplier = 3f;
    private const float _panDistanceMultiplier = 0.75f;
    private const float _windowsScrollDeltaPerUnit = 120f;

    private Vector3 _playableMinimum;
    private Vector3 _playableMaximum;
    private Vector3 _pivot;
    private Vector2 _lastPointerPosition;
    private Vector2 _rightPointerPressPosition;
    private float _distance;
    private float _elevation;
    private float _maximumDistance;
    private float _minimumDistance;
    private float _minimumPanSpeed;
    private float _yaw;
    private bool _configured;
    private bool _rightPointerDragging;
    private bool _rightPointerGestureActive;
    private Camera _controlledCamera;
    private Vector3[] _framingCorners;
    private Rect _framingViewport;
    private float _framingAspect;
    private float _framingFieldOfView;

    internal bool IsConfigured => _configured;
    internal float Distance => _distance;
    internal Vector3 Pivot => _pivot;

    public event Action<Vector2> RightClicked;

    /// <summary>
    /// Captures the required camera component.
    /// </summary>
    private void Awake()
    {
        _controlledCamera = GetComponent<Camera>();
        if (_controlledCamera == null)
            throw new MissingComponentException($"{name} requires a Camera component.");
    }

    /// <summary>
    /// Processes tactical camera navigation from the active input devices.
    /// </summary>
    private void Update()
    {
        if (!_configured)
            return;

        if (
            _framingCorners != null
            && (
                !Mathf.Approximately(_framingAspect, _controlledCamera.aspect)
                || !Mathf.Approximately(_framingFieldOfView, _controlledCamera.fieldOfView)
            )
        )
            ApplyFraming();
        HandlePointerInput();
        HandleKeyboardInput();
    }

    /// <summary>
    /// Clears transient pointer state when the camera is disabled.
    /// </summary>
    private void OnDisable()
    {
        _rightPointerGestureActive = false;
        _rightPointerDragging = false;
    }

    /// <summary>
    /// Configures camera navigation from authored map bounds and the current camera pose.
    /// </summary>
    /// <param name="playableBounds">The volume within which the camera pivot may move.</param>
    /// <param name="pivot">The point around which the authored starting view orbits.</param>
    public void Configure(BattleMapBounds playableBounds, Vector3 pivot)
    {
        if (playableBounds == null)
            throw new ArgumentNullException(nameof(playableBounds));
        if (
            playableBounds.MaximumX <= playableBounds.MinimumX
            || playableBounds.MaximumY <= playableBounds.MinimumY
            || playableBounds.MaximumZ <= playableBounds.MinimumZ
        )
        {
            throw new ArgumentException(
                "Tactical camera bounds must have positive size on every axis.",
                nameof(playableBounds)
            );
        }

        _playableMinimum = new Vector3(
            playableBounds.MinimumX,
            playableBounds.MinimumY,
            playableBounds.MinimumZ
        );
        _playableMaximum = new Vector3(
            playableBounds.MaximumX,
            playableBounds.MaximumY,
            playableBounds.MaximumZ
        );
        _pivot = ClampToPlayableVolume(pivot);

        Vector3 playableSize = _playableMaximum - _playableMinimum;
        float largestDimension = Mathf.Max(playableSize.x, playableSize.y, playableSize.z);
        _controlledCamera = GetComponent<Camera>();
        _minimumDistance = Mathf.Max(_controlledCamera.nearClipPlane * 2f, largestDimension / 600f);
        _minimumPanSpeed = largestDimension / 12f;

        Vector3 eyeOffset = transform.position - _pivot;
        if (eyeOffset.sqrMagnitude <= Mathf.Epsilon)
            eyeOffset = -transform.forward * _minimumDistance;

        _distance = Mathf.Max(_minimumDistance, eyeOffset.magnitude);
        _maximumDistance = Mathf.Max(_distance, largestDimension * (5f / 3f));
        _yaw = NormalizeRadians(Mathf.Atan2(eyeOffset.z, eyeOffset.x));
        _elevation = Mathf.Clamp(
            Mathf.Asin(Mathf.Clamp(eyeOffset.y / eyeOffset.magnitude, -1f, 1f)),
            _minimumElevation,
            _maximumElevation
        );
        _configured = true;
        _framingCorners = null;
    }

    /// <summary>
    /// Fits every supplied hull inside the usable viewport without changing the authored viewing angle.
    /// Reframes on projection changes until the player navigates; never changes deployment positions.
    /// </summary>
    /// <param name="hulls">World-space hull envelopes to include in the opening view.</param>
    /// <param name="usableViewport">The normalized unobstructed rectangle containing the screen center.</param>
    public void Frame(IReadOnlyList<Bounds> hulls, Rect usableViewport)
    {
        RequireConfiguration();
        if (hulls == null)
            throw new ArgumentNullException(nameof(hulls));
        if (
            !(
                usableViewport.xMin >= 0f
                && usableViewport.yMin >= 0f
                && usableViewport.xMax <= 1f
                && usableViewport.yMax <= 1f
                && usableViewport.xMin < 0.5f
                && usableViewport.yMin < 0.5f
                && usableViewport.xMax > 0.5f
                && usableViewport.yMax > 0.5f
            )
        )
            throw new ArgumentException(
                "The usable viewport must contain the projection center inside the camera viewport.",
                nameof(usableViewport)
            );
        _framingCorners = null;
        if (hulls.Count == 0)
            return;
        Bounds combined = hulls[0];
        Vector3[] corners = new Vector3[hulls.Count * 8];
        for (int index = 0; index < hulls.Count; index++)
        {
            Bounds hull = hulls[index];
            if (
                !IsFinite(hull.center)
                || !IsFinite(hull.size)
                || hull.size.x <= 0f
                || hull.size.y <= 0f
                || hull.size.z <= 0f
            )
                throw new ArgumentException(
                    "Framing requires finite, positive hull bounds.",
                    nameof(hulls)
                );
            combined.Encapsulate(hull);
            for (int corner = 0; corner < 8; corner++)
                corners[index * 8 + corner] =
                    hull.center
                    + Vector3.Scale(
                        hull.extents,
                        new Vector3(
                            (corner & 1) == 0 ? -1f : 1f,
                            (corner & 2) == 0 ? -1f : 1f,
                            (corner & 4) == 0 ? -1f : 1f
                        )
                    );
        }
        _pivot = ClampToPlayableVolume(combined.center);
        _framingViewport = usableViewport;
        _framingCorners = corners;
        ApplyFraming();
    }

    /// <summary>
    /// Fits cached hull corners using the actual perspective projection.
    /// </summary>
    private void ApplyFraming()
    {
        _framingAspect = _controlledCamera.aspect;
        _framingFieldOfView = _controlledCamera.fieldOfView;
        float verticalSlope = Mathf.Tan(_framingFieldOfView * Mathf.Deg2Rad * 0.5f);
        float horizontalSlope = verticalSlope * _framingAspect;
        float left = (_framingViewport.xMin * 2f - 1f) * horizontalSlope;
        float right = (_framingViewport.xMax * 2f - 1f) * horizontalSlope;
        float bottom = (_framingViewport.yMin * 2f - 1f) * verticalSlope;
        float top = (_framingViewport.yMax * 2f - 1f) * verticalSlope;
        Quaternion inverse = Quaternion.Inverse(transform.rotation);
        float distance = _minimumDistance;
        foreach (Vector3 corner in _framingCorners)
        {
            Vector3 local = inverse * (corner - _pivot);
            distance = Mathf.Max(
                distance,
                local.x / left - local.z,
                local.x / right - local.z,
                local.y / bottom - local.z,
                local.y / top - local.z,
                _controlledCamera.nearClipPlane - local.z
            );
        }
        // A small numerical margin keeps corners off the clipping planes after float projection.
        _distance = distance + Mathf.Max(0.001f, distance * 0.00001f);
        _maximumDistance = Mathf.Max(_maximumDistance, _distance);
        transform.position = _pivot - transform.forward * _distance;
        Vector3 eyeDirection = -transform.forward;
        _yaw = NormalizeRadians(Mathf.Atan2(eyeDirection.z, eyeDirection.x));
        _elevation = Mathf.Asin(Mathf.Clamp(eyeDirection.y, -1f, 1f));
        Vector3 farthest = Vector3.Max(
            Abs(_playableMinimum - transform.position),
            Abs(_playableMaximum - transform.position)
        );
        _controlledCamera.farClipPlane = Mathf.Max(
            _controlledCamera.farClipPlane,
            farthest.magnitude + 100f
        );
    }

    /// <summary>
    /// Returns component magnitudes for clipping-plane calculations.
    /// </summary>
    /// <param name="value">The corner displacement.</param>
    /// <returns>The absolute displacement.</returns>
    private static Vector3 Abs(Vector3 value) =>
        new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

    /// <summary>
    /// Checks whether all coordinates are finite.
    /// </summary>
    /// <param name="value">The coordinates to validate.</param>
    /// <returns>True when no coordinate is NaN or infinite.</returns>
    private static bool IsFinite(Vector3 value) =>
        !float.IsNaN(value.x)
        && !float.IsInfinity(value.x)
        && !float.IsNaN(value.y)
        && !float.IsInfinity(value.y)
        && !float.IsNaN(value.z)
        && !float.IsInfinity(value.z);

    /// <summary>
    /// Orbits the camera around its current pivot using a pointer delta.
    /// </summary>
    /// <param name="pointerDelta">The pointer movement in screen pixels.</param>
    internal void Orbit(Vector2 pointerDelta)
    {
        RequireConfiguration();
        if (pointerDelta.sqrMagnitude <= Mathf.Epsilon)
            return;
        _framingCorners = null;
        _yaw = NormalizeRadians(_yaw - pointerDelta.x * _pointerRadiansPerPixel);
        _elevation = Mathf.Clamp(
            _elevation - pointerDelta.y * _pointerRadiansPerPixel,
            _minimumElevation,
            _maximumElevation
        );
        ApplyTransform();
    }

    /// <summary>
    /// Centers navigation on a radar location while preserving zoom and orbit.
    /// </summary>
    /// <param name="point">The requested world-space pivot.</param>
    public void Recenter(Vector3 point)
    {
        RequireConfiguration();
        _framingCorners = null;
        _pivot = ClampToPlayableVolume(point);
        ApplyTransform();
    }

    /// <summary>
    /// Pans the camera pivot relative to the current planar view direction.
    /// </summary>
    /// <param name="input">The horizontal and forward navigation input.</param>
    /// <param name="deltaTime">The unscaled elapsed time.</param>
    /// <param name="fast">Whether accelerated navigation is active.</param>
    internal void Pan(Vector2 input, float deltaTime, bool fast)
    {
        RequireConfiguration();
        if (deltaTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        if (input.sqrMagnitude <= Mathf.Epsilon || deltaTime <= 0f)
            return;

        _framingCorners = null;
        Vector3 forward = _pivot - transform.position;
        forward.y = 0f;
        forward = forward.sqrMagnitude <= Mathf.Epsilon ? Vector3.forward : forward.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 direction = forward * input.y + right * input.x;
        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        float panSpeed = Mathf.Max(_minimumPanSpeed, _distance * _panDistanceMultiplier);
        if (fast)
            panSpeed *= _fastPanMultiplier;
        _pivot = ClampToPlayableVolume(_pivot + direction * panSpeed * deltaTime);
        ApplyTransform();
    }

    /// <summary>
    /// Rotates the camera around its pivot from keyboard input.
    /// </summary>
    /// <param name="input">The signed turn input.</param>
    /// <param name="deltaTime">The unscaled elapsed time.</param>
    internal void Turn(float input, float deltaTime)
    {
        RequireConfiguration();
        if (deltaTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        if (Mathf.Approximately(input, 0f) || deltaTime <= 0f)
            return;

        _framingCorners = null;
        _yaw = NormalizeRadians(
            _yaw + Mathf.Clamp(input, -1f, 1f) * _yawRadiansPerSecond * deltaTime
        );
        ApplyTransform();
    }

    /// <summary>
    /// Applies one platform-normalized mouse-wheel zoom step.
    /// </summary>
    /// <param name="scrollDelta">The raw input-system scroll delta.</param>
    /// <param name="platform">The runtime platform producing the delta.</param>
    internal void ZoomByScroll(float scrollDelta, RuntimePlatform platform)
    {
        RequireConfiguration();
        if (Mathf.Approximately(scrollDelta, 0f))
            return;

        float scrollDeltaPerUnit =
            platform == RuntimePlatform.WindowsEditor || platform == RuntimePlatform.WindowsPlayer
                ? _windowsScrollDeltaPerUnit
                : 1f;
        float normalizedScrollDelta = scrollDelta / scrollDeltaPerUnit;
        float multiplier =
            normalizedScrollDelta >= 0f
                ? Mathf.Pow(_wheelZoomInMultiplier, normalizedScrollDelta)
                : Mathf.Pow(_wheelZoomOutMultiplier, -normalizedScrollDelta);
        Zoom(multiplier);
    }

    /// <summary>
    /// Processes pointer orbit and zoom gestures.
    /// </summary>
    private void HandlePointerInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        bool pointerOverInterface = EventSystem.current?.IsPointerOverGameObject() == true;
        float scrollDelta = mouse.scroll.ReadValue().y;
        if (!pointerOverInterface && !Mathf.Approximately(scrollDelta, 0f))
            ZoomByScroll(scrollDelta, Application.platform);

        Vector2 pointerPosition = mouse.position.ReadValue();
        if (mouse.rightButton.wasPressedThisFrame && !pointerOverInterface)
        {
            _rightPointerGestureActive = true;
            _rightPointerDragging = false;
            _rightPointerPressPosition = pointerPosition;
            _lastPointerPosition = pointerPosition;
        }

        if (_rightPointerGestureActive && mouse.rightButton.isPressed)
        {
            Vector2 pointerDelta = pointerPosition - _lastPointerPosition;
            _lastPointerPosition = pointerPosition;
            _rightPointerDragging |=
                Vector2.Distance(_rightPointerPressPosition, pointerPosition)
                    > _pointerDragThreshold
                || mouse.leftButton.isPressed;
            if (_rightPointerDragging)
            {
                if (mouse.leftButton.isPressed)
                    Zoom(Mathf.Max(0.001f, 1f + _twoButtonZoomRatePerPixel * pointerDelta.y));
                else
                    Orbit(pointerDelta);
            }
        }

        if (_rightPointerGestureActive && mouse.rightButton.wasReleasedThisFrame)
        {
            bool clicked =
                !_rightPointerDragging
                && !mouse.leftButton.isPressed
                && !pointerOverInterface
                && Vector2.Distance(_rightPointerPressPosition, pointerPosition)
                    <= _pointerDragThreshold;
            _rightPointerGestureActive = false;
            _rightPointerDragging = false;
            if (clicked)
                RightClicked?.Invoke(pointerPosition);
        }
    }

    /// <summary>
    /// Processes keyboard pan, turn, and zoom controls.
    /// </summary>
    private void HandleKeyboardInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.numpadPlusKey.isPressed || keyboard.equalsKey.isPressed)
            Zoom(_keyboardZoomInMultiplier);
        if (keyboard.numpadMinusKey.isPressed || keyboard.minusKey.isPressed)
            Zoom(_keyboardZoomOutMultiplier);

        Vector2 panInput = Vector2.zero;
        if (keyboard.aKey.isPressed)
            panInput.x--;
        if (keyboard.dKey.isPressed)
            panInput.x++;
        if (keyboard.sKey.isPressed)
            panInput.y--;
        if (keyboard.wKey.isPressed)
            panInput.y++;
        bool fastPan = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        Pan(panInput, Time.unscaledDeltaTime, fastPan);

        float turnInput = 0f;
        if (keyboard.qKey.isPressed)
            turnInput++;
        if (keyboard.eKey.isPressed)
            turnInput--;
        Turn(turnInput, Time.unscaledDeltaTime);
    }

    /// <summary>
    /// Applies a multiplicative zoom around the current pivot.
    /// </summary>
    /// <param name="multiplier">The positive distance multiplier.</param>
    private void Zoom(float multiplier)
    {
        if (multiplier <= 0f)
            throw new ArgumentOutOfRangeException(nameof(multiplier));

        _framingCorners = null;
        _distance = Mathf.Clamp(_distance * multiplier, _minimumDistance, _maximumDistance);
        ApplyTransform();
    }

    /// <summary>
    /// Applies the current orbital camera state to the Unity transform.
    /// </summary>
    private void ApplyTransform()
    {
        float horizontalDistance = _distance * Mathf.Cos(_elevation);
        Vector3 offset = new Vector3(
            horizontalDistance * Mathf.Cos(_yaw),
            _distance * Mathf.Sin(_elevation),
            horizontalDistance * Mathf.Sin(_yaw)
        );
        transform.position = _pivot + offset;
        transform.rotation = Quaternion.LookRotation(_pivot - transform.position, Vector3.up);
    }

    /// <summary>
    /// Clamps a camera pivot to the authored playable volume.
    /// </summary>
    /// <param name="point">The requested pivot.</param>
    /// <returns>The playable pivot.</returns>
    private Vector3 ClampToPlayableVolume(Vector3 point)
    {
        return new Vector3(
            Mathf.Clamp(point.x, _playableMinimum.x, _playableMaximum.x),
            Mathf.Clamp(point.y, _playableMinimum.y, _playableMaximum.y),
            Mathf.Clamp(point.z, _playableMinimum.z, _playableMaximum.z)
        );
    }

    /// <summary>
    /// Ensures navigation is not used before receiving authored map bounds.
    /// </summary>
    private void RequireConfiguration()
    {
        if (!_configured)
            throw new InvalidOperationException("The tactical camera is not configured.");
    }

    /// <summary>
    /// Normalizes a radian angle to one positive turn.
    /// </summary>
    /// <param name="angle">The source angle.</param>
    /// <returns>The normalized angle.</returns>
    private static float NormalizeRadians(float angle)
    {
        float fullTurn = Mathf.PI * 2f;
        float normalizedAngle = angle % fullTurn;
        return normalizedAngle < 0f ? normalizedAngle + fullTurn : normalizedAngle;
    }
}
