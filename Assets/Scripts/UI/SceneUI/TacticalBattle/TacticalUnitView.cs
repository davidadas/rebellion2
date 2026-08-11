using Rebellion.Game.Tactical;
using UnityEngine;

/// <summary>
/// Projects one tactical simulation unit into the battle scene.
/// </summary>
public sealed class TacticalUnitView : MonoBehaviour
{
    private TacticalUnitState unit;

    /// <summary>
    /// Raised when the player selects this unit in tactical space.
    /// </summary>
    public event System.Action<TacticalUnitState> Selected;

    /// <summary>
    /// Gets the tactical unit projected by this view.
    /// </summary>
    internal TacticalUnitState Unit => unit;

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

        Synchronize(unit.Position);
    }

    /// <summary>
    /// Applies the unit's latest state at a presentation-specific position.
    /// </summary>
    /// <param name="position">The position to present without changing simulation state.</param>
    public void Synchronize(System.Numerics.Vector3 position)
    {
        if (unit == null)
            return;

        gameObject.SetActive(unit.IsActive);
        transform.localPosition = ToUnityVector(position);
        Vector3 forward = ToUnityVector(unit.Forward);
        if (forward.sqrMagnitude > 0f)
            transform.localRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    /// <summary>
    /// Forwards Unity's world-object selection to the tactical renderer.
    /// </summary>
    private void OnMouseDown()
    {
        if (unit?.IsActive == true)
            Selected?.Invoke(unit);
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
