using System;
using UnityEngine;

/// <summary>
/// Converts a horizontal pointer drag into a temporary, clamped camera-orbit target.
/// </summary>
internal sealed class LimitedYawDrag
{
    private readonly float degreesPerViewportWidth;
    private readonly float maximumYaw;
    private float pointerStartX;
    private float yawStart;

    /// <summary>
    /// Gets whether a left-button gesture currently owns the orbit target.
    /// </summary>
    internal bool IsActive { get; private set; }

    /// <summary>
    /// Gets the clamped yaw selected by the latest drag.
    /// </summary>
    internal float TargetYaw { get; private set; }

    /// <summary>
    /// Initializes a horizontal camera-orbit drag mapper.
    /// </summary>
    /// <param name="maximumYaw">The absolute horizontal limit in degrees.</param>
    /// <param name="degreesPerViewportWidth">The horizontal sensitivity.</param>
    internal LimitedYawDrag(float maximumYaw, float degreesPerViewportWidth)
    {
        if (maximumYaw <= 0f)
            throw new ArgumentOutOfRangeException(nameof(maximumYaw));
        if (degreesPerViewportWidth <= 0f)
            throw new ArgumentOutOfRangeException(nameof(degreesPerViewportWidth));

        this.maximumYaw = maximumYaw;
        this.degreesPerViewportWidth = degreesPerViewportWidth;
    }

    /// <summary>
    /// Captures the beginning of a drag from the camera's current yaw.
    /// </summary>
    /// <param name="pointerX">The pointer's screen-space x-coordinate.</param>
    /// <param name="currentYaw">The camera's current yaw when the gesture begins.</param>
    internal void Begin(float pointerX, float currentYaw)
    {
        pointerStartX = pointerX;
        yawStart = Mathf.Clamp(currentYaw, -maximumYaw, maximumYaw);
        TargetYaw = yawStart;
        IsActive = true;
    }

    /// <summary>
    /// Updates the orbit target from the active horizontal drag.
    /// </summary>
    /// <param name="pointerX">The pointer's current screen-space x-coordinate.</param>
    /// <param name="viewportWidth">The active viewport width in pixels.</param>
    internal void Move(float pointerX, float viewportWidth)
    {
        if (!IsActive || viewportWidth <= 0f)
            return;

        float dragDegrees = (pointerX - pointerStartX) / viewportWidth * degreesPerViewportWidth;
        TargetYaw = Mathf.Clamp(yawStart + dragDegrees, -maximumYaw, maximumYaw);
    }

    /// <summary>
    /// Releases the current drag and requests a return to the centered view.
    /// </summary>
    internal void End()
    {
        TargetYaw = 0f;
        IsActive = false;
    }

    /// <summary>
    /// Recenters the target and clears any active gesture.
    /// </summary>
    internal void Reset()
    {
        TargetYaw = 0f;
        pointerStartX = 0f;
        yawStart = 0f;
        IsActive = false;
    }
}
