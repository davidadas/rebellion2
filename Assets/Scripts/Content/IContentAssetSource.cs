using UnityEngine;

/// <summary>
/// Resolves visual assets by stable content address.
/// </summary>
public interface IContentAssetSource
{
    Texture2D GetTexture(string address);

    Sprite GetSprite(string address);
}
