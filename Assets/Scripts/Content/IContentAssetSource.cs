using UnityEngine;

/// <summary>
/// Resolves visual assets by stable content address.
/// </summary>
public interface IContentAssetSource
{
    Texture2D GetTexture(string address);

    Sprite GetSprite(string address);

    /// <summary>
    /// Resolves a sprite with an explicit nine-slice border.
    /// </summary>
    /// <param name="address">The stable content address.</param>
    /// <param name="border">The sprite border in pixels.</param>
    /// <returns>The resolved sprite.</returns>
    Sprite GetSprite(string address, Vector4 border);
}
