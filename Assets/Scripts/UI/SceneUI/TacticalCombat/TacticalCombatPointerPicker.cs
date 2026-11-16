using System;
using Rebellion.Gameplay;
using UnityEngine;
using Vector3N = System.Numerics.Vector3;

/// <summary>
/// Resolves a single tactical ray against displayed objects and navigation markers.
/// </summary>
/// <param name="point">The normalized viewport point.</param>
/// <param name="unit">Receives an object hit, or null for a navigation marker.</param>
/// <param name="waypoint">Receives a navigation marker position.</param>
/// <returns>Whether the ray intersects an addressed object.</returns>
public delegate bool TacticalCombatPick(
    Vector2 point,
    out TacticalCombatUnit unit,
    out Vector3N waypoint
);

/// <summary>
/// Owns ordered pixel-neighborhood picking without combining hits from different rays.
/// </summary>
public static class TacticalCombatPointerPicker
{
    private static readonly Vector2Int[] _offsets =
    {
        new(0, 0),
        new(-1, -1),
        new(1, -1),
        new(1, 1),
        new(-1, 1),
        new(-2, -2),
        new(0, -2),
        new(2, -2),
        new(2, 0),
        new(2, 2),
        new(0, 2),
        new(-2, 2),
        new(-2, 0),
    };

    /// <summary>
    /// Picks the first addressed hit in the primary or secondary pointer's neighborhood.
    /// </summary>
    /// <param name="point">The normalized viewport press.</param>
    /// <param name="pixels">The viewport's pixel dimensions.</param>
    /// <param name="secondary">Whether to include the outer secondary-pointer ring.</param>
    /// <param name="pick">The renderer's single-ray picking boundary.</param>
    /// <param name="unit">Receives the first object hit.</param>
    /// <param name="waypoint">Receives the first navigation marker hit.</param>
    /// <returns>Whether one of the ordered rays hit an addressed object.</returns>
    public static bool TryPick(
        Vector2 point,
        Vector2Int pixels,
        bool secondary,
        TacticalCombatPick pick,
        out TacticalCombatUnit unit,
        out Vector3N waypoint
    )
    {
        if (pick == null)
            throw new ArgumentNullException(nameof(pick));
        if (pixels.x <= 0 || pixels.y <= 0)
            throw new ArgumentOutOfRangeException(nameof(pixels));
        for (int index = 0; index < (secondary ? _offsets.Length : 5); index++)
        {
            Vector2Int offset = _offsets[index];
            Vector2 sample =
                point + new Vector2((float)offset.x / pixels.x, -(float)offset.y / pixels.y);
            if (pick(sample, out unit, out waypoint))
                return true;
        }
        unit = null;
        waypoint = default;
        return false;
    }
}
