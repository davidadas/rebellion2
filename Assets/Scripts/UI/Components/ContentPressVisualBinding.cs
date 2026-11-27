using System;
using UnityEngine;

/// <summary>
/// Binds the released and pressed textures of the press visual on this object to stable content
/// addresses that are resolved from installation content at runtime. The addresses are authored
/// strings, so they survive the player build's removal of development-content object references.
/// </summary>
[RequireComponent(typeof(RawImagePressVisual))]
public sealed class ContentPressVisualBinding : MonoBehaviour
{
    [SerializeField]
    private string upAddress;

    [SerializeField]
    private string downAddress;

    private RawImagePressVisual cachedPressVisual;

    /// <summary>
    /// Gets the released-state content address resolved by this binding.
    /// </summary>
    public string UpAddress => upAddress;

    /// <summary>
    /// Gets the pressed-state content address resolved by this binding.
    /// </summary>
    public string DownAddress => downAddress;

    /// <summary>
    /// Assigns the released and pressed content addresses resolved when the binding is applied.
    /// </summary>
    /// <param name="releasedAddress">The released-state content address.</param>
    /// <param name="pressedAddress">The pressed-state content address, or null when it matches the released state.</param>
    public void SetAddresses(string releasedAddress, string pressedAddress)
    {
        if (string.IsNullOrWhiteSpace(releasedAddress))
            throw new ArgumentException(
                "A released-state content address is required.",
                nameof(releasedAddress)
            );

        upAddress = releasedAddress;
        downAddress = pressedAddress;
    }

    /// <summary>
    /// Resolves the bound textures from the supplied content source and applies them.
    /// </summary>
    /// <param name="contentAssets">The active content asset source.</param>
    public void Bind(IContentAssetSource contentAssets)
    {
        var (released, pressed) = ResolveTextures(contentAssets);
        ResolvePressVisual().SetInteractiveTextures(released, pressed);
    }

    /// <summary>
    /// Resolves the authored textures from the supplied asset store.
    /// </summary>
    /// <param name="contentAssets">The active content asset source.</param>
    /// <returns>The released and pressed textures.</returns>
    internal (Texture2D Released, Texture2D Pressed) ResolveTextures(
        IContentAssetSource contentAssets
    )
    {
        if (contentAssets == null)
            throw new ArgumentNullException(nameof(contentAssets));
        if (string.IsNullOrWhiteSpace(upAddress))
            throw new MissingReferenceException($"{name} press-visual address is missing.");

        Texture2D upTexture = ContentBindings.RequireTexture(contentAssets, upAddress);
        Texture2D downTexture = string.IsNullOrWhiteSpace(downAddress)
            ? upTexture
            : ContentBindings.RequireTexture(contentAssets, downAddress);
        return (upTexture, downTexture);
    }

    /// <summary>
    /// Resolves and caches the press visual required by this binding.
    /// </summary>
    /// <returns>The press visual on this object.</returns>
    private RawImagePressVisual ResolvePressVisual()
    {
        if (cachedPressVisual == null)
            cachedPressVisual = GetComponent<RawImagePressVisual>();
        return cachedPressVisual;
    }
}
