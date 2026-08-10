using Rebellion.Game.Tactical;
using UnityEngine;

/// <summary>
/// Projects one tactical simulation unit into the battle scene.
/// </summary>
public sealed class TacticalUnitView : MonoBehaviour
{
    private TacticalUnitState unit;

    /// <summary>
    /// Connects this presentation object to its tactical unit.
    /// </summary>
    /// <param name="state">The tactical unit to present.</param>
    public void Initialize(TacticalUnitState state)
    {
        unit = state ?? throw new System.ArgumentNullException(nameof(state));
        Synchronize();
    }

    /// <summary>
    /// Applies the unit's latest visibility, position, and facing.
    /// </summary>
    public void Synchronize()
    {
        if (unit == null)
            return;

        gameObject.SetActive(unit.IsActive);
        transform.localPosition = ToUnityVector(unit.Position);
        Vector3 forward = ToUnityVector(unit.Forward);
        if (forward.sqrMagnitude > 0f)
            transform.localRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    /// <summary>
    /// Converts a simulation vector without leaking Unity types into game state.
    /// </summary>
    /// <param name="value">The simulation vector.</param>
    /// <returns>The equivalent Unity vector.</returns>
    private static Vector3 ToUnityVector(System.Numerics.Vector3 value)
    {
        return new Vector3(value.X, value.Y, value.Z);
    }
}
