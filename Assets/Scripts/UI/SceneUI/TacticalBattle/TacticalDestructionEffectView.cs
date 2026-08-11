using System;
using UnityEngine;

/// <summary>
/// Owns the short object-scaled pyrotechnic burst shown when a tactical unit is destroyed.
/// </summary>
public sealed class TacticalDestructionEffectView : MonoBehaviour
{
    private const float _duration = 1.25f;
    private Material material;

    /// <summary>
    /// Creates and starts the destruction burst.
    /// </summary>
    /// <param name="diameter">The destroyed object's presentation diameter.</param>
    public void Initialize(float diameter)
    {
        if (diameter <= 0f)
            throw new ArgumentOutOfRangeException(nameof(diameter));

        ParticleSystem particles = gameObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.duration = _duration;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(diameter * 0.2f, diameter * 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(diameter * 0.15f, diameter * 0.4f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.85f, 0.25f, 1f),
            new Color(1f, 0.2f, 0.02f, 1f)
        );
        main.maxParticles = 36;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = diameter * 0.2f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.35f), 0f),
                new GradientColorKey(new Color(1f, 0.15f, 0.01f), 0.55f),
                new GradientColorKey(new Color(0.12f, 0.02f, 0f), 1f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = fade;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            throw new InvalidOperationException("The tactical destruction shader is unavailable.");

        material = new Material(shader);
        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = material;
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particles.Play();
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
