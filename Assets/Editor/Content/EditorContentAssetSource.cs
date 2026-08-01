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

    private readonly string _contentRoot;
    private readonly string _packRoot;

    /// <summary>
    /// Opens the active content pack used to resolve editor-preview assets.
    /// </summary>
    public EditorContentAssetSource()
    {
        ContentPack pack = ContentPackLoader.OpenActive();
        _contentRoot = pack.ContentRootPath;
        _packRoot = pack.PackRootPath;
    }

    /// <summary>
    /// Loads an imported preview texture for a content address.
    /// </summary>
    /// <param name="address">The application- or pack-relative content address.</param>
    /// <returns>The imported texture.</returns>
    public Texture2D GetTexture(string address)
    {
        string assetPath = ResolveAssetPath(address);
        ConfigureImporter(assetPath, TextureImporterType.Default, SpriteImportMode.None);
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
        ConfigureImporter(assetPath, TextureImporterType.Sprite, SpriteImportMode.Single);
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    /// <summary>
    /// Applies the import mode required by one preview asset.
    /// </summary>
    /// <param name="assetPath">The Unity asset path.</param>
    /// <param name="textureType">The required texture type.</param>
    /// <param name="spriteImportMode">The required sprite import mode.</param>
    private static void ConfigureImporter(
        string assetPath,
        TextureImporterType textureType,
        SpriteImportMode spriteImportMode
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

        string root;
        string relative;
        if (normalized.StartsWith("Application/", StringComparison.Ordinal))
        {
            root = _contentRoot;
            relative = normalized;
        }
        else if (normalized.StartsWith("Pack/", StringComparison.Ordinal))
        {
            root = _packRoot;
            relative = normalized["Pack/".Length..];
        }
        else
        {
            throw new ArgumentException($"Unsupported content address: {address}", nameof(address));
        }

        string path = Path.Combine(root, relative);
        if (!Path.HasExtension(path))
        {
            foreach (string extension in _textureExtensions)
            {
                if (File.Exists(path + extension))
                {
                    path += extension;
                    break;
                }
            }
        }

        string assetPath = "Assets" + path[Application.dataPath.Length..].Replace('\\', '/');
        if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
            throw new FileNotFoundException($"Imported content asset not found: {assetPath}");
        return assetPath;
    }
}
