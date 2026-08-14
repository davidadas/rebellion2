using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds the sprite of the image on this object to a stable content address that is resolved from
/// installation content at runtime. The address is an authored string, so it survives the player
/// build's removal of development-content object references.
/// </summary>
[RequireComponent(typeof(Image))]
public sealed class ContentSpriteBinding : MonoBehaviour
{
    [SerializeField]
    private string address;

    [SerializeField]
    private Vector4 border;

    private Image cachedImage;

    /// <summary>
    /// Gets the stable content address resolved by this binding.
    /// </summary>
    public string Address => address;

    /// <summary>
    /// Gets the explicit nine-slice border requested by this binding.
    /// </summary>
    public Vector4 Border => border;

    /// <summary>
    /// Assigns the stable content address resolved when the binding is applied.
    /// </summary>
    /// <param name="contentAddress">The application- or pack-relative content address.</param>
    public void SetAddress(string contentAddress)
    {
        SetAddress(contentAddress, Vector4.zero);
    }

    /// <summary>
    /// Assigns the stable content address and nine-slice border resolved by this binding.
    /// </summary>
    /// <param name="contentAddress">The application- or pack-relative content address.</param>
    /// <param name="spriteBorder">The sprite border in pixels.</param>
    public void SetAddress(string contentAddress, Vector4 spriteBorder)
    {
        if (string.IsNullOrWhiteSpace(contentAddress))
            throw new ArgumentException(
                "A content sprite address is required.",
                nameof(contentAddress)
            );

        address = contentAddress;
        border = spriteBorder;
    }

    /// <summary>
    /// Resolves the bound sprite from the supplied content source and applies it.
    /// </summary>
    /// <param name="contentAssets">The active content asset source.</param>
    public void Bind(IContentAssetSource contentAssets)
    {
        if (contentAssets == null)
            throw new ArgumentNullException(nameof(contentAssets));
        if (string.IsNullOrWhiteSpace(address))
            throw new MissingReferenceException($"{name} content sprite address is missing.");

        Image image = ResolveImage();
        image.sprite = ContentBindings.RequireSprite(contentAssets, address, border);
    }

    /// <summary>
    /// Resolves and caches the image required by this binding.
    /// </summary>
    /// <returns>The image on this object.</returns>
    private Image ResolveImage()
    {
        if (cachedImage == null)
            cachedImage = GetComponent<Image>();
        return cachedImage;
    }
}
