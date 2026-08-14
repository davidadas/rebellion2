using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies deterministic Unity import settings to synchronized application textures.
/// </summary>
internal sealed class ApplicationTextureImporter : AssetPostprocessor
{
    private const string _defaultCursorPath = "Assets/Resources/UI/DefaultCursor.png";

    /// <summary>
    /// Configures the synchronized default cursor for hardware-cursor use.
    /// </summary>
    private void OnPreprocessTexture()
    {
        if (!string.Equals(assetPath, _defaultCursorPath, StringComparison.Ordinal))
            return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Cursor;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Point;
        importer.npotScale = TextureImporterNPOTScale.None;
    }
}
