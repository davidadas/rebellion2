using System;
using UnityEngine;

/// <summary>
/// Applies every content binding beneath a view hierarchy from one content source.
/// </summary>
public static class ContentBindings
{
    /// <summary>
    /// Resolves and applies every texture and press-visual binding beneath the supplied root.
    /// </summary>
    /// <param name="root">The root object whose bindings are applied, including inactive children.</param>
    /// <param name="contentAssets">The active content asset source.</param>
    public static void Apply(GameObject root, IContentAssetSource contentAssets)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));
        if (contentAssets == null)
            throw new ArgumentNullException(nameof(contentAssets));

        foreach (
            ContentTextureBinding binding in root.GetComponentsInChildren<ContentTextureBinding>(
                true
            )
        )
            binding.Bind(contentAssets);
        foreach (
            ContentSpriteBinding binding in root.GetComponentsInChildren<ContentSpriteBinding>(true)
        )
            binding.Bind(contentAssets);
        foreach (
            ContentPressVisualBinding binding in root.GetComponentsInChildren<ContentPressVisualBinding>(
                true
            )
        )
            binding.Bind(contentAssets);
        foreach (
            IContentInitializable initializable in root.GetComponentsInChildren<IContentInitializable>(
                true
            )
        )
            initializable.InitializeContent(contentAssets);
    }

    /// <summary>
    /// Resolves a required texture from the content source, failing loudly with the address.
    /// </summary>
    /// <param name="contentAssets">The active content asset source.</param>
    /// <param name="address">The content address to resolve.</param>
    /// <returns>The resolved texture.</returns>
    public static Texture2D RequireTexture(IContentAssetSource contentAssets, string address)
    {
        if (contentAssets == null)
            throw new ArgumentNullException(nameof(contentAssets));
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("A content texture address is required.", nameof(address));

        return contentAssets.GetTexture(address)
            ?? throw new InvalidOperationException($"Content texture is missing: {address}");
    }

    /// <summary>
    /// Resolves a required sprite from the content source, failing loudly with the address.
    /// </summary>
    /// <param name="contentAssets">The active content asset source.</param>
    /// <param name="address">The content address to resolve.</param>
    /// <returns>The resolved sprite.</returns>
    public static Sprite RequireSprite(IContentAssetSource contentAssets, string address)
    {
        if (contentAssets == null)
            throw new ArgumentNullException(nameof(contentAssets));
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("A content sprite address is required.", nameof(address));

        return contentAssets.GetSprite(address)
            ?? throw new InvalidOperationException($"Content sprite is missing: {address}");
    }

    /// <summary>
    /// Resolves a required bordered sprite from the content source, failing loudly with the address.
    /// </summary>
    /// <param name="contentAssets">The active content asset source.</param>
    /// <param name="address">The content address to resolve.</param>
    /// <param name="border">The sprite's nine-slice border in pixels.</param>
    /// <returns>The resolved sprite.</returns>
    public static Sprite RequireSprite(
        IContentAssetSource contentAssets,
        string address,
        Vector4 border
    )
    {
        if (contentAssets == null)
            throw new ArgumentNullException(nameof(contentAssets));
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("A content sprite address is required.", nameof(address));

        return contentAssets.GetSprite(address, border)
            ?? throw new InvalidOperationException($"Content sprite is missing: {address}");
    }
}
