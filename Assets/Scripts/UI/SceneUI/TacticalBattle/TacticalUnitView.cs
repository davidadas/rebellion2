using Rebellion.Game.Tactical;
using Rebellion.Game.Units;
using UnityEngine;

/// <summary>
/// Projects one tactical simulation unit into the battle scene.
/// </summary>
public sealed class TacticalUnitView : MonoBehaviour
{
    private TacticalPersistentEffectView gravityWellEffect;
    private GameObject highlightObject;
    private Mesh highlightMesh;
    private TacticalPersistentEffectView tractorLockEffect;
    private int tractorLockCount;
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
    /// Creates the capital ship's wireframe highlight box.
    /// </summary>
    /// <param name="bounds">The ship presentation bounds in local space.</param>
    public void ConfigureHighlight(Bounds bounds)
    {
        highlightObject = new GameObject("Ship Highlight");
        highlightObject.transform.SetParent(transform, false);
        MeshFilter filter = highlightObject.AddComponent<MeshFilter>();
        highlightObject.AddComponent<MeshRenderer>();
        highlightMesh = CreateHighlightMesh(bounds);
        filter.sharedMesh = highlightMesh;
        highlightObject.SetActive(false);
    }

    /// <summary>
    /// Creates the persistent tractor-lock and gravity-well effects for this unit.
    /// </summary>
    /// <param name="tractorFrames">The tractor-lock animation frames.</param>
    /// <param name="gravityWellFrames">The gravity-well animation frames.</param>
    /// <param name="bounds">The unit presentation bounds in local space.</param>
    public void ConfigurePersistentEffects(
        Sprite[] tractorFrames,
        Sprite[] gravityWellFrames,
        Bounds bounds
    )
    {
        float diameter = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * 1.15f;
        tractorLockEffect = CreatePersistentEffect("Tractor Lock Effect", tractorFrames, diameter);
        gravityWellEffect = CreatePersistentEffect(
            "Gravity Well Effect",
            gravityWellFrames,
            diameter
        );
        RefreshPersistentEffects();
    }

    /// <summary>
    /// Records one established or released tractor lock affecting this unit.
    /// </summary>
    /// <param name="active">Whether a new lock was established instead of released.</param>
    public void SetTractorLockActive(bool active)
    {
        tractorLockCount = Mathf.Max(0, tractorLockCount + (active ? 1 : -1));
        RefreshPersistentEffects();
    }

    /// <summary>
    /// Applies the faction highlight material and visibility.
    /// </summary>
    /// <param name="material">The faction-colored wireframe material.</param>
    /// <param name="visible">Whether the highlight should be visible.</param>
    public void SetHighlighted(Material material, bool visible)
    {
        if (highlightObject == null)
            return;

        highlightObject.GetComponent<MeshRenderer>().sharedMaterial = material;
        highlightObject.SetActive(visible);
    }

    /// <summary>
    /// Releases the runtime-generated highlight mesh.
    /// </summary>
    private void OnDestroy()
    {
        if (highlightMesh != null)
            Destroy(highlightMesh);
    }

    /// <summary>
    /// Builds the eight-corner, twelve-edge box used to identify faction capital ships.
    /// </summary>
    /// <param name="bounds">The local presentation bounds.</param>
    /// <returns>The generated line mesh.</returns>
    private static Mesh CreateHighlightMesh(Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Mesh mesh = new Mesh { name = "Tactical Ship Highlight" };
        mesh.vertices = new[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, max.z),
            new Vector3(min.x, max.y, max.z),
        };
        mesh.SetIndices(
            new[] { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7 },
            MeshTopology.Lines,
            0
        );
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Creates one child effect using an ordered animation sequence.
    /// </summary>
    /// <param name="name">The effect hierarchy name.</param>
    /// <param name="frames">The animation frames in playback order.</param>
    /// <param name="diameter">The effect diameter in local space.</param>
    /// <returns>The configured persistent effect.</returns>
    private TacticalPersistentEffectView CreatePersistentEffect(
        string name,
        Sprite[] frames,
        float diameter
    )
    {
        GameObject effectObject = new GameObject(name);
        effectObject.transform.SetParent(transform, false);
        TacticalPersistentEffectView effect =
            effectObject.AddComponent<TacticalPersistentEffectView>();
        effect.Initialize(frames, diameter);
        return effect;
    }

    /// <summary>
    /// Applies gravity-well precedence over the tractor-lock presentation.
    /// </summary>
    private void RefreshPersistentEffects()
    {
        if (tractorLockEffect == null || gravityWellEffect == null || unit == null)
            return;

        bool hasGravityWell = unit.Unit is CapitalShip { HasGravityWell: true };
        gravityWellEffect.SetVisible(hasGravityWell);
        tractorLockEffect.SetVisible(!hasGravityWell && tractorLockCount > 0);
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
