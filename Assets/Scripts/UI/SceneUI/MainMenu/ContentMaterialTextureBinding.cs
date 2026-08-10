using System;
using UnityEngine;

/// <summary>
/// Binds a content texture (resolved at runtime from installation content) onto a fresh material on
/// this object's renderer. Mirrors <c>ContentTextureBinding</c>, which does the same for a RawImage,
/// so a primitive-built 3D layer can still source its texture and shader from content instead of a
/// baked material. Applied during the content-binding pass via <see cref="IContentInitializable"/>.
/// </summary>
[RequireComponent(typeof(Renderer))]
public sealed class ContentMaterialTextureBinding : MonoBehaviour, IContentInitializable
{
    [SerializeField]
    private string address;

    [SerializeField]
    private string shaderName = "Unlit/Transparent";

    /// <summary>
    /// Sets the content texture address and the shader used for the created material.
    /// </summary>
    /// <param name="contentAddress">The application- or pack-relative content texture address.</param>
    /// <param name="materialShaderName">The shader to build the material from.</param>
    public void Configure(string contentAddress, string materialShaderName)
    {
        if (string.IsNullOrWhiteSpace(contentAddress))
            throw new ArgumentException("A content texture address is required.", nameof(contentAddress));
        if (string.IsNullOrWhiteSpace(materialShaderName))
            throw new ArgumentException("A shader name is required.", nameof(materialShaderName));

        address = contentAddress;
        shaderName = materialShaderName;
    }

    /// <summary>
    /// Builds the material from the configured shader and the resolved content texture.
    /// </summary>
    /// <param name="contentAssets">The active content asset source.</param>
    public void InitializeContent(IContentAssetSource contentAssets)
    {
        if (contentAssets == null)
            throw new ArgumentNullException(nameof(contentAssets));

        Shader shader = Shader.Find(shaderName);
        if (shader == null)
            throw new InvalidOperationException($"Shader not found: {shaderName}");

        Material material = new Material(shader)
        {
            mainTexture = ContentBindings.RequireTexture(contentAssets, address),
        };
        GetComponent<Renderer>().sharedMaterial = material;
    }
}
