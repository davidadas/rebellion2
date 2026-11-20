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
    /// <param name="preserveVisibleSubject">Whether to fit the visible subject when a cover crop would clip it.</param>
    /// <returns>PNG bytes keyed by their distinct output dimensions.</returns>
    internal static IReadOnlyDictionary<Vector2Int, byte[]> GeneratePngCuts(
        string sourcePath,
        IEnumerable<Vector2Int> sizes,
        bool fitEntireSource = false,
        bool preserveVisibleSubject = false
    ) =>
        GeneratePlacedPngCuts(
                sourcePath,
                sizes?.Select(size => (Size: size, Offset: Vector2.zero)),
                fitEntireSource,
                preserveVisibleSubject
            )
            .ToDictionary(cut => cut.Key.Size, cut => cut.Value);

    /// <summary>
    /// Creates the requested output sizes with independently placed artwork from one decoded source.
    /// </summary>
    /// <param name="sourcePath">The absolute source image path.</param>
    /// <param name="placements">The output sizes and normalized offsets.</param>
    /// <param name="fitEntireSource">Whether to fit the whole source before applying each offset.</param>
    /// <param name="preserveVisibleSubject">Whether to fit the visible subject before applying each offset.</param>
    /// <returns>PNG bytes keyed by each distinct size and placement.</returns>
    internal static IReadOnlyDictionary<
        (Vector2Int Size, Vector2 Offset),
        byte[]
    > GeneratePlacedPngCuts(
        string sourcePath,
        IEnumerable<(Vector2Int Size, Vector2 Offset)> placements,
        bool fitEntireSource,
        bool preserveVisibleSubject
    )
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Choose a source image.", nameof(sourcePath));
        string source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source))
            throw new FileNotFoundException("The source image does not exist.", source);
        if (placements == null)
            throw new ArgumentNullException(nameof(placements));
        var targets = placements.Distinct().ToArray();
        if (targets.Length == 0 || targets.Any(target => target.Size.x <= 0 || target.Size.y <= 0))
            throw new ArgumentException(
                "Choose at least one positive output size.",
                nameof(placements)
            );

        Texture2D sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(sourceTexture, File.ReadAllBytes(source), false))
                throw new InvalidDataException("The source image could not be decoded.");
            sourceTexture.wrapMode = TextureWrapMode.Clamp;
            RectInt contentBounds = GetVisibleContentBounds(sourceTexture);
            return targets.ToDictionary(
                target => target,
                target =>
                    CreatePngCut(
                        sourceTexture,
                        contentBounds,
                        target.Size,
                        target.Offset,
                        fitEntireSource,
                        preserveVisibleSubject
                    )
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
    /// <param name="offset">The artwork displacement in output proportions.</param>
    /// <param name="fitEntireSource">Whether the complete source canvas must remain visible.</param>
    /// <param name="preserveVisibleSubject">Whether to fit artwork that would be clipped by the cover crop.</param>
    /// <returns>The encoded PNG bytes.</returns>
    private static byte[] CreatePngCut(
        Texture2D source,
        RectInt contentBounds,
        Vector2Int target,
        Vector2 offset,
        bool fitEntireSource,
        bool preserveVisibleSubject
    )
    {
        Rect crop = GetSubjectCenteredCrop(source, contentBounds, target);
        if (fitEntireSource)
            crop = new Rect(0, 0, source.width, source.height);
        else if (
            preserveVisibleSubject
            && (
                contentBounds.xMin < crop.xMin
                || contentBounds.xMax > crop.xMax
                || contentBounds.yMin < crop.yMin
                || contentBounds.yMax > crop.yMax
            )
        )
        {
            crop = new Rect(contentBounds.min, contentBounds.size);
        }
        float scale = Mathf.Min(target.x / crop.width, target.y / crop.height);
        float fittedX = (target.x - crop.width * scale) * 0.5f + target.x * offset.x;
        float fittedY = (target.y - crop.height * scale) * 0.5f + target.y * offset.y;
        Texture2D output = new Texture2D(target.x, target.y, TextureFormat.RGBA32, false);
        try
        {
            Color[] pixels = new Color[target.x * target.y];
            for (int y = 0; y < target.y; y++)
            {
                int row = y * target.x;
                float sampleY = (y + 0.5f - fittedY) / scale;
                if (sampleY < 0 || sampleY > crop.height)
                    continue;
                for (int x = 0; x < target.x; x++)
                {
                    float sampleX = (x + 0.5f - fittedX) / scale;
                    if (sampleX >= 0 && sampleX <= crop.width)
                        pixels[row + x] = source.GetPixelBilinear(
                            (crop.x + sampleX - 0.5f) / source.width,
                            (crop.y + sampleY - 0.5f) / source.height
                        );
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
    /// <returns>The crop in source pixels.</returns>
    private static Rect GetSubjectCenteredCrop(
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
        return new Rect(cropX, cropY, cropWidth, cropHeight);
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
