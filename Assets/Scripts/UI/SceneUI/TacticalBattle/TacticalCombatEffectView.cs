using System;
using UnityEngine;

/// <summary>
/// Owns the short lifetime and fade of one tactical weapon or destruction effect.
/// </summary>
public sealed class TacticalCombatEffectView : MonoBehaviour
{
    private Color initialColor;
    private float elapsedTime;
    private float lifetime;
    private Material material;
    private Vector3 initialScale;
    private bool expands;

    /// <summary>
    /// Configures the effect's owned material, lifetime, and optional expansion.
    /// </summary>
    /// <param name="ownedMaterial">The effect material destroyed with this view.</param>
    /// <param name="duration">The effect lifetime in seconds.</param>
    /// <param name="expand">Whether the effect expands while fading.</param>
    public void Initialize(Material ownedMaterial, float duration, bool expand)
    {
        material = ownedMaterial ?? throw new ArgumentNullException(nameof(ownedMaterial));
        if (duration <= 0f)
            throw new ArgumentOutOfRangeException(nameof(duration));

        lifetime = duration;
        expands = expand;
        initialColor = material.color;
        initialScale = transform.localScale;
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
        material.color = new Color(initialColor.r, initialColor.g, initialColor.b, 1f - progress);
        if (expands)
            transform.localScale = initialScale * Mathf.Lerp(1f, 3f, progress);
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
