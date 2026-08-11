using System;
using UnityEngine;

/// <summary>
/// Owns the lifetime and optional travel of one tactical weapon effect.
/// </summary>
public sealed class TacticalCombatEffectView : MonoBehaviour
{
    private float elapsedTime;
    private float lifetime;
    private LineRenderer line;
    private Material material;
    private Vector3 sourcePosition;
    private Vector3 targetPosition;

    /// <summary>
    /// Configures the effect's owned material and lifetime.
    /// </summary>
    /// <param name="ownedMaterial">The effect material destroyed with this view.</param>
    /// <param name="duration">The effect lifetime in seconds.</param>
    public void Initialize(Material ownedMaterial, float duration)
    {
        material = ownedMaterial ?? throw new ArgumentNullException(nameof(ownedMaterial));
        if (duration <= 0f)
            throw new ArgumentOutOfRangeException(nameof(duration));

        lifetime = duration;
    }

    /// <summary>
    /// Configures a beam that advances from its source to its target over its lifetime.
    /// </summary>
    /// <param name="ownedMaterial">The effect material destroyed with this view.</param>
    /// <param name="beam">The line renderer used to draw the beam.</param>
    /// <param name="source">The beam origin in the line renderer's coordinate space.</param>
    /// <param name="target">The beam destination in the line renderer's coordinate space.</param>
    /// <param name="duration">The beam travel time in seconds.</param>
    public void InitializeTravelingBeam(
        Material ownedMaterial,
        LineRenderer beam,
        Vector3 source,
        Vector3 target,
        float duration
    )
    {
        Initialize(ownedMaterial, duration);
        line = beam ?? throw new ArgumentNullException(nameof(beam));
        sourcePosition = source;
        targetPosition = target;
        line.SetPosition(0, sourcePosition);
        line.SetPosition(1, sourcePosition);
    }

    /// <summary>
    /// Advances the transient effect and releases it after its configured lifetime.
    /// </summary>
    private void Update()
    {
        if (material == null)
            return;

        elapsedTime += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsedTime / lifetime);
        if (line != null)
            line.SetPosition(1, Vector3.Lerp(sourcePosition, targetPosition, progress));

        if (elapsedTime >= lifetime)
            Destroy(gameObject);
    }

    /// <summary>
    /// Releases the material instantiated exclusively for this effect.
    /// </summary>
    private void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }
}
