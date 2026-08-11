using UnityEditor;
using UnityEngine;

/// <summary>
/// Configures the generated Options-menu chrome sprites (rounded navy panels) as 9-sliced sprites
/// so their borders stay crisp at any size, without relying on committed .meta files.
/// </summary>
public sealed class OptionsMenuSpritePostprocessor : AssetPostprocessor
{
    private const string _directory = "Assets/UI/OptionsMenu/";

    /// <summary>
    /// Applies sprite import settings to Options-menu chrome textures.
    /// </summary>
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(_directory) || !assetPath.EndsWith(".png"))
            return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = GetSliceBorder(assetPath);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }

    /// <summary>
    /// Resolves the 9-slice border for one chrome sprite by role.
    /// </summary>
    /// <param name="path">The sprite asset path.</param>
    /// <returns>The slice border, or zero for fixed-aspect art.</returns>
    private static Vector4 GetSliceBorder(string path)
    {
        if (path.Contains("toggle") || path.Contains("knob"))
            return Vector4.zero;
        if (path.Contains("badge"))
            return new Vector4(6f, 6f, 6f, 6f);
        return new Vector4(7f, 7f, 7f, 7f);
    }
}
