using UnityEngine;

/// <summary>
/// One frame of keyboard camera input for the tactical battle: a pan rate in the range -1..1
/// per axis, discrete zoom steps, and one-shot view commands.
/// </summary>
internal readonly struct TacticalCameraInput
{
    /// <summary>
    /// Creates a frame of camera input.
    /// </summary>
    /// <param name="pan">The pan rate, right and forward relative to the camera.</param>
    /// <param name="yaw">The turn rate around the focus.</param>
    /// <param name="pitch">The tilt rate toward top-down.</param>
    /// <param name="zoomSteps">Discrete zoom steps; positive zooms in.</param>
    /// <param name="resetView">Whether to reframe the fleet.</param>
    /// <param name="cycleShips">Whether to select and frame the next friendly ship.</param>
    /// <param name="togglePause">Whether to pause or resume playback.</param>
    internal TacticalCameraInput(
        Vector2 pan,
        float yaw,
        float pitch,
        float zoomSteps,
        bool resetView,
        bool cycleShips,
        bool togglePause
    )
    {
        Pan = pan;
        Yaw = yaw;
        Pitch = pitch;
        ZoomSteps = zoomSteps;
        ResetView = resetView;
        CycleShips = cycleShips;
        TogglePause = togglePause;
    }

    internal Vector2 Pan { get; }

    internal float Yaw { get; }

    internal float Pitch { get; }

    internal float ZoomSteps { get; }

    internal bool ResetView { get; }

    internal bool CycleShips { get; }

    internal bool TogglePause { get; }

    /// <summary>
    /// Gets whether this frame carries any camera input.
    /// </summary>
    internal bool IsEmpty =>
        Pan == Vector2.zero
        && Yaw == 0f
        && Pitch == 0f
        && ZoomSteps == 0f
        && !ResetView
        && !CycleShips
        && !TogglePause;
}
