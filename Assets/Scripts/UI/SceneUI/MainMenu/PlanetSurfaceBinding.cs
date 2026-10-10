using System;
using UnityEngine;

/// <summary>
/// Replaces an externally loaded planet model's material with the unified surface, cloud, and
/// day-night renderer used by the main-menu backdrop.
/// </summary>
public sealed class PlanetSurfaceBinding : MonoBehaviour, IContentInitializable
{
    private static readonly int _baseMapProperty = Shader.PropertyToID("_BaseMap");
    private static readonly int _baseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int _cloudMapProperty = Shader.PropertyToID("_CloudMap");
    private static readonly int _cloudRotationSpeedProperty = Shader.PropertyToID(
        "_CloudRotationSpeed"
    );
    private static readonly int _sunDirectionProperty = Shader.PropertyToID("_SunDirection");

    [SerializeField]
    private string shaderName;

    [SerializeField]
    private string cloudTextureAddress;

    [SerializeField]
    private float cloudRotationSpeed;

    [SerializeField]
    private Vector3 sunDirection;

    private Texture2D suppliedCloudTexture;
    private Material surfaceMaterial;

    /// <summary>
    /// Configures the shader, cloud texture address, and independent cloud drift speed.
    /// </summary>
    /// <param name="shader">The unified planet shader name.</param>
    /// <param name="textureAddress">The content address for the cloud coverage texture.</param>
    /// <param name="rotationSpeed">The cloud rotation speed in degrees per second.</param>
    public void Configure(string shader, string textureAddress, float rotationSpeed)
    {
        if (string.IsNullOrWhiteSpace(shader))
            throw new ArgumentException("A planet surface shader is required.", nameof(shader));
        if (string.IsNullOrWhiteSpace(textureAddress))
        {
            throw new ArgumentException(
                "A cloud texture address is required.",
                nameof(textureAddress)
            );
        }

        shaderName = shader;
        cloudTextureAddress = textureAddress;
        suppliedCloudTexture = null;
        cloudRotationSpeed = rotationSpeed;
    }

    /// <summary>
    /// Configures the shader, cloud texture, cloud drift, and world-space direction toward the sun.
    /// </summary>
    /// <param name="shader">The unified planet shader name.</param>
    /// <param name="textureAddress">The content address for the cloud coverage texture.</param>
    /// <param name="rotationSpeed">The cloud rotation speed in degrees per second.</param>
    /// <param name="directionToSun">The world-space direction from the planet to the sun.</param>
    public void Configure(
        string shader,
        string textureAddress,
        float rotationSpeed,
        Vector3 directionToSun
    )
    {
        if (directionToSun.sqrMagnitude <= Mathf.Epsilon)
            throw new ArgumentException("A sun direction is required.", nameof(directionToSun));

        Configure(shader, textureAddress, rotationSpeed);
        sunDirection = directionToSun.normalized;
    }

    /// <summary>
    /// Configures the shader with a caller-owned cloud texture and world-space sun direction.
    /// </summary>
    /// <param name="shader">The unified planet shader name.</param>
    /// <param name="cloudTexture">The already loaded cloud coverage texture.</param>
    /// <param name="rotationSpeed">The cloud rotation speed in degrees per second.</param>
    /// <param name="directionToSun">The world-space direction from the planet to the sun.</param>
    public void Configure(
        string shader,
        Texture2D cloudTexture,
        float rotationSpeed,
        Vector3 directionToSun
    )
    {
        if (string.IsNullOrWhiteSpace(shader))
            throw new ArgumentException("A planet surface shader is required.", nameof(shader));
        if (cloudTexture == null)
            throw new ArgumentNullException(nameof(cloudTexture));
        if (directionToSun.sqrMagnitude <= Mathf.Epsilon)
            throw new ArgumentException("A sun direction is required.", nameof(directionToSun));

        shaderName = shader;
        cloudTextureAddress = null;
        suppliedCloudTexture = cloudTexture;
        cloudRotationSpeed = rotationSpeed;
        sunDirection = directionToSun.normalized;
    }

    /// <summary>
    /// Creates the unified material after the external planet model and content are available.
    /// </summary>
    /// <param name="contentAssets">The active external content source.</param>
    public void InitializeContent(IContentAssetSource contentAssets)
    {
        if (contentAssets == null)
            throw new ArgumentNullException(nameof(contentAssets));

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length != 1)
        {
            throw new InvalidOperationException(
                $"The planet surface requires exactly one renderer, but found {renderers.Length}."
            );
        }

        Material sourceMaterial = renderers[0].sharedMaterial;
        Texture baseMap = sourceMaterial?.mainTexture;
        if (baseMap == null)
            throw new InvalidOperationException("The loaded planet material has no base texture.");

        Shader shader = Shader.Find(shaderName);
        if (shader == null)
            throw new InvalidOperationException($"Shader not found: {shaderName}");

        DestroySurfaceMaterial();
        surfaceMaterial = new Material(shader) { name = "PlanetSurface" };
        surfaceMaterial.SetTexture(_baseMapProperty, baseMap);
        surfaceMaterial.SetTextureScale("_BaseMap", sourceMaterial.mainTextureScale);
        surfaceMaterial.SetTextureOffset("_BaseMap", sourceMaterial.mainTextureOffset);
        surfaceMaterial.SetColor(_baseColorProperty, ResolveBaseColor(sourceMaterial));
        Texture2D cloudMap =
            suppliedCloudTexture
            ?? ContentBindings.RequireTexture(contentAssets, cloudTextureAddress);
        cloudMap.wrapModeU = TextureWrapMode.Repeat;
        cloudMap.wrapModeV = TextureWrapMode.Clamp;
        cloudMap.filterMode = FilterMode.Bilinear;
        cloudMap.anisoLevel = 8;
        surfaceMaterial.SetTexture(_cloudMapProperty, cloudMap);
        surfaceMaterial.SetFloat(_cloudRotationSpeedProperty, cloudRotationSpeed);
        if (sunDirection.sqrMagnitude > Mathf.Epsilon)
        {
            surfaceMaterial.SetVector(
                _sunDirectionProperty,
                new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0f)
            );
        }
        renderers[0].sharedMaterial = surfaceMaterial;
    }

    /// <summary>
    /// Releases the runtime-owned material when the authored binding is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        DestroySurfaceMaterial();
    }

    /// <summary>
    /// Reads the imported material's base tint without depending on one shader's property naming.
    /// </summary>
    /// <param name="material">The imported planet material.</param>
    /// <returns>The imported base tint, or white when none is declared.</returns>
    private static Color ResolveBaseColor(Material material)
    {
        if (material == null)
            return Color.white;
        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");
        if (material.HasProperty("_Color"))
            return material.GetColor("_Color");
        return Color.white;
    }

    /// <summary>
    /// Releases the currently owned surface material in the active Unity lifetime mode.
    /// </summary>
    private void DestroySurfaceMaterial()
    {
        if (surfaceMaterial == null)
            return;
        if (Application.isPlaying)
            Destroy(surfaceMaterial);
        else
            DestroyImmediate(surfaceMaterial);
        surfaceMaterial = null;
    }
}
