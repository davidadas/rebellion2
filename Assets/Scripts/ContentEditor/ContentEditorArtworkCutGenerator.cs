using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Creates deterministic centered PNG cuts from one high-resolution unit image.
/// </summary>
internal static class ContentEditorArtworkCutGenerator
{
    /// <summary>
    /// Decodes one source image and creates every requested output size without stretching it.
    /// </summary>
    /// <param name="sourcePath">The absolute source image path.</param>
    /// <param name="sizes">The fixed output dimensions to create.</param>
    /// <param name="fitEntireSource">Whether to letterbox the complete source canvas instead of cover-cropping it.</param>
    /// <returns>PNG bytes keyed by their distinct output dimensions.</returns>
    internal static IReadOnlyDictionary<Vector2Int, byte[]> GeneratePngCuts(
        string sourcePath,
        IEnumerable<Vector2Int> sizes,
        bool fitEntireSource = false
    )
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Choose a source image.", nameof(sourcePath));
        string source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source))
            throw new FileNotFoundException("The source image does not exist.", source);
        if (sizes == null)
            throw new ArgumentNullException(nameof(sizes));
        Vector2Int[] targets = sizes.Distinct().ToArray();
        if (targets.Length == 0 || targets.Any(size => size.x <= 0 || size.y <= 0))
            throw new ArgumentException("Choose at least one positive output size.", nameof(sizes));

        Texture2D sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(sourceTexture, File.ReadAllBytes(source), false))
                throw new InvalidDataException("The source image could not be decoded.");
            RectInt contentBounds = GetVisibleContentBounds(sourceTexture);
            return targets.ToDictionary(
                size => size,
                size => CreatePngCut(sourceTexture, contentBounds, size, fitEntireSource)
            );
        }
        finally
        {
            DestroyTexture(sourceTexture);
        }
    }

    /// <summary>
    /// Samples one centered cover crop into an exact output canvas and encodes it as PNG.
    /// </summary>
    /// <param name="source">The readable decoded source texture.</param>
    /// <param name="contentBounds">The visible subject bounds used to preserve framing.</param>
    /// <param name="target">The exact output size.</param>
    /// <param name="fitEntireSource">Whether the complete source canvas must remain visible.</param>
    /// <returns>The encoded PNG bytes.</returns>
    private static byte[] CreatePngCut(
        Texture2D source,
        RectInt contentBounds,
        Vector2Int target,
        bool fitEntireSource
    )
    {
        Rect crop = GetSubjectCenteredUvRect(source, contentBounds, target);
        float fittedWidth = target.x;
        float fittedHeight = target.y;
        float fittedX = 0;
        float fittedY = 0;
        if (fitEntireSource)
        {
            float scale = Mathf.Min(
                (float)target.x / source.width,
                (float)target.y / source.height
            );
            fittedWidth = source.width * scale;
            fittedHeight = source.height * scale;
            fittedX = (target.x - fittedWidth) * 0.5f;
            fittedY = (target.y - fittedHeight) * 0.5f;
        }
        Texture2D output = new Texture2D(target.x, target.y, TextureFormat.RGBA32, false);
        try
        {
            Color[] pixels = new Color[target.x * target.y];
            for (int y = 0; y < target.y; y++)
            {
                int row = y * target.x;
                for (int x = 0; x < target.x; x++)
                {
                    if (
                        fitEntireSource
                        && (
                            x + 0.5f < fittedX
                            || x + 0.5f > fittedX + fittedWidth
                            || y + 0.5f < fittedY
                            || y + 0.5f > fittedY + fittedHeight
                        )
                    )
                    {
                        pixels[row + x] = Color.clear;
                        continue;
                    }
                    float u = fitEntireSource
                        ? (x + 0.5f - fittedX) / fittedWidth
                        : crop.x + ((x + 0.5f) / target.x) * crop.width;
                    float v = fitEntireSource
                        ? (y + 0.5f - fittedY) / fittedHeight
                        : crop.y + ((y + 0.5f) / target.y) * crop.height;
                    pixels[row + x] = source.GetPixelBilinear(u, v);
                }
            }
            output.SetPixels(pixels);
            output.Apply(false, false);
            return ImageConversion.EncodeToPNG(output);
        }
        finally
        {
            DestroyTexture(output);
        }
    }

    /// <summary>
    /// Finds the smallest source rectangle containing pixels that contribute visible alpha.
    /// </summary>
    /// <param name="source">The readable source texture.</param>
    /// <returns>The visible bounds, or the full canvas when the source is fully transparent.</returns>
    private static RectInt GetVisibleContentBounds(Texture2D source)
    {
        int minimumX = source.width;
        int minimumY = source.height;
        int maximumX = -1;
        int maximumY = -1;
        Color32[] pixels = source.GetPixels32();
        for (int index = 0; index < pixels.Length; index++)
        {
            if (pixels[index].a <= 1)
                continue;
            int x = index % source.width;
            int y = index / source.width;
            minimumX = Mathf.Min(minimumX, x);
            minimumY = Mathf.Min(minimumY, y);
            maximumX = Mathf.Max(maximumX, x);
            maximumY = Mathf.Max(maximumY, y);
        }
        return maximumX < minimumX || maximumY < minimumY
            ? new RectInt(0, 0, source.width, source.height)
            : new RectInt(minimumX, minimumY, maximumX - minimumX + 1, maximumY - minimumY + 1);
    }

    /// <summary>
    /// Centers a cover crop on the visible subject while keeping the crop inside the source canvas.
    /// </summary>
    /// <param name="source">The complete source texture.</param>
    /// <param name="contentBounds">The visible subject bounds.</param>
    /// <param name="target">The requested output size.</param>
    /// <returns>The normalized source UV rectangle.</returns>
    private static Rect GetSubjectCenteredUvRect(
        Texture2D source,
        RectInt contentBounds,
        Vector2Int target
    )
    {
        float sourceAspect = (float)source.width / source.height;
        float targetAspect = (float)target.x / target.y;
        float cropWidth = source.width;
        float cropHeight = source.height;
        if (sourceAspect > targetAspect)
            cropWidth = source.height * targetAspect;
        else if (sourceAspect < targetAspect)
            cropHeight = source.width / targetAspect;

        float centerX = contentBounds.x + contentBounds.width * 0.5f;
        float centerY = contentBounds.y + contentBounds.height * 0.5f;
        float cropX = Mathf.Clamp(centerX - cropWidth * 0.5f, 0, source.width - cropWidth);
        float cropY = Mathf.Clamp(centerY - cropHeight * 0.5f, 0, source.height - cropHeight);
        return new Rect(
            cropX / source.width,
            cropY / source.height,
            cropWidth / source.width,
            cropHeight / source.height
        );
    }

    /// <summary>
    /// Releases a temporary texture using the lifecycle appropriate to the current Unity context.
    /// </summary>
    /// <param name="texture">The temporary texture to release.</param>
    private static void DestroyTexture(Texture2D texture)
    {
        if (texture == null)
            return;
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(texture);
        else
            UnityEngine.Object.DestroyImmediate(texture);
    }
}
