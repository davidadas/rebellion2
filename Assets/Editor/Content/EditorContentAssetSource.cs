using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Resolves development-preview assets imported beneath Assets/Content.
/// </summary>
public sealed class EditorContentAssetSource : IContentAssetSource
{
    private const int _previewTextureLimit = 4096;
    private static readonly string[] _textureExtensions = { ".png", ".jpg", ".jpeg" };

    private readonly ContentFileResolver _fileResolver;

    /// <summary>
    /// Opens the active content pack used to resolve editor-preview assets.
    /// </summary>
    public EditorContentAssetSource()
    {
        ContentPack pack = ContentPackLoader.OpenActive();
        _fileResolver = pack.FileResolver;
    }

    /// <summary>
    /// Loads an imported preview texture for a content address.
    /// </summary>
    /// <param name="address">The application- or pack-relative content address.</param>
    /// <returns>The imported texture.</returns>
    public Texture2D GetTexture(string address)
    {
        string assetPath = ResolveAssetPath(address);
        ConfigureImporter(assetPath, TextureImporterType.Default, SpriteImportMode.None, null);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    /// <summary>
    /// Loads an imported preview sprite for a content address.
    /// </summary>
    /// <param name="address">The application- or pack-relative content address.</param>
    /// <returns>The imported sprite.</returns>
    public Sprite GetSprite(string address)
    {
        string assetPath = ResolveAssetPath(address);
        ConfigureImporter(assetPath, TextureImporterType.Sprite, SpriteImportMode.Single, null);
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    /// <summary>
    /// Loads an imported preview sprite with an explicit nine-slice border.
    /// </summary>
    /// <param name="address">The application- or pack-relative content address.</param>
    /// <param name="border">The sprite border in pixels.</param>
    /// <returns>The imported sprite.</returns>
    public Sprite GetSprite(string address, Vector4 border)
    {
        string assetPath = ResolveAssetPath(address);
        ConfigureImporter(assetPath, TextureImporterType.Sprite, SpriteImportMode.Single, border);
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    /// <summary>
    /// Applies the import mode required by one preview asset.
    /// </summary>
    /// <param name="assetPath">The Unity asset path.</param>
    /// <param name="textureType">The required texture type.</param>
    /// <param name="spriteImportMode">The required sprite import mode.</param>
    /// <param name="spriteBorder">The explicit sprite border, or null to preserve it.</param>
    private static void ConfigureImporter(
        string assetPath,
        TextureImporterType textureType,
        SpriteImportMode spriteImportMode,
        Vector4? spriteBorder
    )
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException(
                $"Content texture importer is missing: {assetPath}"
            );

        bool changed = false;
        changed |= SetIfDifferent(
            importer.textureType,
            textureType,
            value => importer.textureType = value
        );
        changed |= SetIfDifferent(
            importer.spriteImportMode,
            spriteImportMode,
            value => importer.spriteImportMode = value
        );
        changed |= SetIfDifferent(
            importer.maxTextureSize,
            _previewTextureLimit,
            value => importer.maxTextureSize = value
        );
        changed |= SetIfDifferent(
            importer.mipmapEnabled,
            false,
            value => importer.mipmapEnabled = value
        );
        changed |= SetIfDifferent(
            importer.alphaSource,
            TextureImporterAlphaSource.FromInput,
            value => importer.alphaSource = value
        );
        changed |= SetIfDifferent(
            importer.alphaIsTransparency,
            true,
            value => importer.alphaIsTransparency = value
        );
        if (spriteBorder.HasValue)
        {
            changed |= SetIfDifferent(
                importer.spriteBorder,
                spriteBorder.Value,
                value => importer.spriteBorder = value
            );
            changed |= SetIfDifferent(
                importer.wrapMode,
                TextureWrapMode.Clamp,
                value => importer.wrapMode = value
            );
            changed |= SetIfDifferent(
                importer.filterMode,
                FilterMode.Bilinear,
                value => importer.filterMode = value
            );
            changed |= SetIfDifferent(
                importer.textureCompression,
                TextureImporterCompression.Uncompressed,
                value => importer.textureCompression = value
            );
        }

        if (changed)
            importer.SaveAndReimport();
    }

    /// <summary>
    /// Assigns an importer value only when it differs from the required value.
    /// </summary>
    /// <typeparam name="T">The importer value type.</typeparam>
    /// <param name="current">The current value.</param>
    /// <param name="expected">The required value.</param>
    /// <param name="assign">Assigns the required value.</param>
    /// <returns>True when the value changed.</returns>
    private static bool SetIfDifferent<T>(T current, T expected, Action<T> assign)
    {
        if (Equals(current, expected))
            return false;

        assign(expected);
        return true;
    }

    /// <summary>
    /// Resolves a content address to an imported Unity asset path.
    /// </summary>
    /// <param name="address">The application- or pack-relative content address.</param>
    /// <returns>The imported Unity asset path.</returns>
    private string ResolveAssetPath(string address)
    {
        string normalized = address?.Trim().Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("A content address is required.", nameof(address));

        if (
            !normalized.StartsWith("Application/", StringComparison.Ordinal)
            && !normalized.StartsWith("Pack/", StringComparison.Ordinal)
        )
        {
            throw new ArgumentException($"Unsupported content address: {address}", nameof(address));
        }

        string path = _fileResolver.ResolveFile(normalized, _textureExtensions);
        if (path == null)
            throw new FileNotFoundException($"Content asset not found: {normalized}");

        string dataPath = Path.GetFullPath(Application.dataPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string absolutePath = Path.GetFullPath(path);
        string relativePath = Path.GetRelativePath(dataPath, absolutePath);
        if (
            relativePath == ".."
            || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathFullyQualified(relativePath)
        )
        {
            throw new InvalidOperationException(
                $"Editor content assets must be imported beneath Assets: {absolutePath}"
            );
        }

        string assetPath = "Assets/" + relativePath.Replace('\\', '/');
        if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
            throw new FileNotFoundException($"Imported content asset not found: {assetPath}");
        return assetPath;
    }
}
