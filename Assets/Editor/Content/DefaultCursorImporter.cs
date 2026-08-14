using System;
using UnityEditor;

/// <summary>
/// Applies the required Unity import settings to the externally synchronized application cursor.
/// </summary>
public sealed class DefaultCursorImporter : AssetPostprocessor
{
    private const string _cursorPath = "Assets/Resources/UI/DefaultCursor.png";

    /// <summary>
    /// Configures the cursor without relying on a tracked metadata file.
    /// </summary>
    public void OnPreprocessTexture()
    {
        if (!string.Equals(assetPath, _cursorPath, StringComparison.OrdinalIgnoreCase))
            return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Cursor;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
    }

    /// <summary>
    /// Invalidates an older cursor import when this policy changes.
    /// </summary>
    public override uint GetVersion()
    {
        return 1;
    }
}
