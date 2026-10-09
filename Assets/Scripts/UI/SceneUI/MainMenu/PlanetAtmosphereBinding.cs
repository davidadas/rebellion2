using System;
using UnityEngine;

/// <summary>
/// Creates the main-menu planet's atmospheric-scattering material after the prefab is composed.
/// </summary>
[RequireComponent(typeof(Renderer))]
public sealed class PlanetAtmosphereBinding : MonoBehaviour, IContentInitializable
{
    private static readonly int _planetRadiusRatioProperty = Shader.PropertyToID(
        "_PlanetRadiusRatio"
    );
    private static readonly int _sunDirectionProperty = Shader.PropertyToID("_SunDirection");

    [SerializeField]
    private string shaderName;

    [SerializeField]
    private float planetRadius;

    [SerializeField]
    private float atmosphereRadius;

    [SerializeField]
    private Vector3 sunDirection;

    private Material atmosphereMaterial;

    /// <summary>
    /// Configures the atmosphere's shader, radii, and world-space direction toward the sun.
    /// </summary>
    /// <param name="shader">The atmospheric-scattering shader name.</param>
    /// <param name="surfaceRadius">The opaque planet radius.</param>
    /// <param name="outerRadius">The outer atmosphere radius.</param>
    /// <param name="directionToSun">The world-space direction from the planet to the sun.</param>
    public void Configure(
        string shader,
        float surfaceRadius,
        float outerRadius,
        Vector3 directionToSun
    )
    {
        if (string.IsNullOrWhiteSpace(shader))
            throw new ArgumentException("An atmosphere shader is required.", nameof(shader));
        if (surfaceRadius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(surfaceRadius));
        if (outerRadius <= surfaceRadius)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outerRadius),
                "The atmosphere radius must exceed the planet radius."
            );
        }
        if (directionToSun.sqrMagnitude <= Mathf.Epsilon)
            throw new ArgumentException("A sun direction is required.", nameof(directionToSun));

        shaderName = shader;
        planetRadius = surfaceRadius;
        atmosphereRadius = outerRadius;
        sunDirection = directionToSun.normalized;
    }

    /// <summary>
    /// Creates and assigns the runtime material used to integrate atmospheric scattering.
    /// </summary>
    /// <param name="contentAssets">The active content source.</param>
    public void InitializeContent(IContentAssetSource contentAssets)
    {
        if (contentAssets == null)
            throw new ArgumentNullException(nameof(contentAssets));

        Shader shader = Shader.Find(shaderName);
        if (shader == null)
            throw new InvalidOperationException($"Shader not found: {shaderName}");

        DestroyAtmosphereMaterial();
        atmosphereMaterial = new Material(shader) { name = "PlanetAtmosphere" };
        atmosphereMaterial.SetFloat(_planetRadiusRatioProperty, planetRadius / atmosphereRadius);
        atmosphereMaterial.SetVector(
            _sunDirectionProperty,
            new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0f)
        );
        GetComponent<Renderer>().sharedMaterial = atmosphereMaterial;
    }

    /// <summary>
    /// Releases the runtime-owned material when the authored binding is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        DestroyAtmosphereMaterial();
    }

    /// <summary>
    /// Releases the current atmospheric-scattering material in the active Unity lifetime mode.
    /// </summary>
    private void DestroyAtmosphereMaterial()
    {
        if (atmosphereMaterial == null)
            return;
        if (Application.isPlaying)
            Destroy(atmosphereMaterial);
        else
            DestroyImmediate(atmosphereMaterial);
        atmosphereMaterial = null;
    }
}
