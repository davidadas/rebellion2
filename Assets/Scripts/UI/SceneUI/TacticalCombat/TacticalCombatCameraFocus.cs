using UnityEngine;

/// <summary>
/// Captures the live presentation references required by a category camera frame.
/// </summary>
public readonly struct TacticalCombatCameraFocus
{
    public int ObjectIndex { get; }
    public Vector3 Position { get; }
    public Quaternion Rotation { get; }
    public float Span { get; }
    public Vector3? SecondaryPosition { get; }

    /// <summary>
    /// Captures an object's pose, middle-model span, and optional secondary presentation position.
    /// </summary>
    /// <param name="objectIndex">The primary object's battle registration identifier.</param>
    /// <param name="position">The primary presentation's world position.</param>
    /// <param name="rotation">The primary presentation's world rotation.</param>
    /// <param name="span">The greater of the middle model's X and Y spans, or zero when it has no model bounds.</param>
    /// <param name="secondaryPosition">The secondary presentation's world position, or null when it has none.</param>
    public TacticalCombatCameraFocus(
        int objectIndex,
        Vector3 position,
        Quaternion rotation,
        float span,
        Vector3? secondaryPosition
    )
    {
        ObjectIndex = objectIndex;
        Position = position;
        Rotation = rotation;
        Span = span;
        SecondaryPosition = secondaryPosition;
    }
}
