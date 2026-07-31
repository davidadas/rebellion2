using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Resolves development-preview assets imported beneath Assets/Content.
/// </summary>
public sealed class EditorContentAssetSource : IContentAssetSource
{
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
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ResolveAssetPath(address));
    }

    public Sprite GetSprite(string address)
    {
        string assetPath = ResolveAssetPath(address);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"Content texture importer is missing: {assetPath}");

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
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
