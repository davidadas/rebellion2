using System;
using UnityEngine;

/// <summary>
/// Builds a material at composition time from a named shader and an optional content texture, then
/// assigns it to this object's renderer. Materials baked into the generated prefab at build time do
/// not render here (custom-shader materials come through as the error shader, and glTFast's imported
/// transparent materials don't draw in the built-in pipeline), so rig layers that need a specific
/// shader or a content-sourced texture are materialised at runtime. Applied during the content
/// binding pass via <see cref="IContentInitializable"/>.
/// </summary>
[RequireComponent(typeof(Renderer))]
public sealed class RuntimeMaterialBinding : MonoBehaviour, IContentInitializable
{
    [SerializeField]
    private string shaderName;

    [SerializeField]
    private string textureAddress;

    /// <summary>
    /// Sets the shader the material is built from and, optionally, a content texture to bind.
    /// </summary>
    /// <param name="shader">The shader name resolved via <c>Shader.Find</c>.</param>
    /// <param name="contentTextureAddress">The content texture address, or null for no texture.</param>
    public void Configure(string shader, string contentTextureAddress = null)
    {
        if (string.IsNullOrWhiteSpace(shader))
            throw new ArgumentException("A shader name is required.", nameof(shader));

        shaderName = shader;
        textureAddress = contentTextureAddress;
    }

    /// <summary>
    /// Resolves the shader (and optional content texture) and assigns the built material.
    /// </summary>
    /// <param name="contentAssets">The active content asset source.</param>
    public void InitializeContent(IContentAssetSource contentAssets)
    {
        if (contentAssets == null)
            throw new ArgumentNullException(nameof(contentAssets));

        Shader shader = Shader.Find(shaderName);
        if (shader == null)
            throw new InvalidOperationException($"Shader not found: {shaderName}");

        Material material = new Material(shader);
        if (!string.IsNullOrWhiteSpace(textureAddress))
        {
            Texture2D texture = ContentBindings.RequireTexture(contentAssets, textureAddress);
            // Smooth, wrapping sampling so very slow rotation drifts sub-pixel instead of snapping
            // texel-to-texel, and the equirectangular seam wraps cleanly.
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 8;
            material.mainTexture = texture;
        }

        GetComponent<Renderer>().sharedMaterial = material;
    }
}
