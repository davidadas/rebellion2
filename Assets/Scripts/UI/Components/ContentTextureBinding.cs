using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds the texture of the raw image on this object to a stable content address that is
/// resolved from installation content at runtime. The address is an authored string, so it
/// survives the player build's removal of development-content object references.
/// </summary>
[RequireComponent(typeof(RawImage))]
public sealed class ContentTextureBinding : MonoBehaviour
{
    [SerializeField]
    private string address;

    private RawImage cachedImage;

    /// <summary>
    /// Gets the stable content address resolved by this binding.
    /// </summary>
    public string Address => address;

    /// <summary>
    /// Assigns the stable content address resolved when the binding is applied.
    /// </summary>
    /// <param name="contentAddress">The application- or pack-relative content address.</param>
    public void SetAddress(string contentAddress)
    {
        address = contentAddress;
    }

    /// <summary>
    /// Resolves the bound texture from the supplied content source and applies it.
    /// </summary>
    /// <param name="contentAssets">The active content asset source.</param>
    public void Bind(IContentAssetSource contentAssets)
    {
        if (contentAssets == null)
            throw new ArgumentNullException(nameof(contentAssets));
        if (string.IsNullOrEmpty(address))
            throw new MissingReferenceException($"{name} content texture address is missing.");

        RawImage image = ResolveImage();
        image.texture =
            contentAssets.GetTexture(address)
            ?? throw new InvalidOperationException($"Content texture is missing: {address}");
    }

    /// <summary>
    /// Resolves and caches the raw image required by this binding.
    /// </summary>
    /// <returns>The raw image on this object.</returns>
    private RawImage ResolveImage()
    {
        if (cachedImage == null)
            cachedImage = GetComponent<RawImage>();
        return cachedImage;
    }
}
