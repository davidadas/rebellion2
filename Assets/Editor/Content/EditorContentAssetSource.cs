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

    private readonly string contentRoot;
    private readonly string packRoot;

    public EditorContentAssetSource()
    {
        ContentPack pack = ContentPackLoader.OpenActive();
        contentRoot = pack.ContentRootPath;
        packRoot = pack.PackRootPath;
    }

    public Texture2D GetTexture(string address)
    {
        string assetPath = ResolveAssetPath(address);
        ConfigureImporter(assetPath, TextureImporterType.Default, SpriteImportMode.None);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    public Sprite GetSprite(string address)
    {
        string assetPath = ResolveAssetPath(address);
        ConfigureImporter(assetPath, TextureImporterType.Sprite, SpriteImportMode.Single);
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static void ConfigureImporter(
        string assetPath,
        TextureImporterType textureType,
        SpriteImportMode spriteImportMode
    )
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"Content texture importer is missing: {assetPath}");

        bool changed = false;
        changed |= SetIfDifferent(importer.textureType, textureType, value => importer.textureType = value);
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
        changed |= SetIfDifferent(importer.mipmapEnabled, false, value => importer.mipmapEnabled = value);
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

    private static bool SetIfDifferent<T>(T current, T expected, Action<T> assign)
    {
        if (Equals(current, expected))
            return false;

        assign(expected);
        return true;
    }

    private string ResolveAssetPath(string address)
    {
        string normalized = address?.Trim().Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("A content address is required.", nameof(address));

        string root;
        string relative;
        if (normalized.StartsWith("application/", StringComparison.Ordinal))
        {
            root = contentRoot;
            relative = normalized;
        }
        else if (normalized.StartsWith("pack/", StringComparison.Ordinal))
        {
            root = packRoot;
            relative = normalized["pack/".Length..];
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
