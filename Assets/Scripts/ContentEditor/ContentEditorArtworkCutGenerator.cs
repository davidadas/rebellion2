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
    /// Checks whether the output fits within the source resolution on both axes.
    /// </summary>
    /// <param name="source">The decoded source dimensions.</param>
    /// <param name="target">The desired output dimensions.</param>
    /// <returns>Whether the output can be cut without exceeding the source resolution.</returns>
    internal static bool CanFitSource(Vector2Int source, Vector2Int target) =>
        target.x <= source.x && target.y <= source.y;

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
    /// <param name="fillOpaqueSource">Whether to crop opaque artwork so it fills the output at every placement.</param>
    /// <param name="preventUpscaling">Whether to reject outputs larger than the source on either axis.</param>
    /// <returns>PNG bytes keyed by each distinct size and placement.</returns>
    internal static IReadOnlyDictionary<
        (Vector2Int Size, Vector2 Offset),
        byte[]
    > GeneratePlacedPngCuts(
        string sourcePath,
        IEnumerable<(Vector2Int Size, Vector2 Offset)> placements,
        bool fitEntireSource,
        bool preserveVisibleSubject,
        bool fillOpaqueSource = false,
        bool preventUpscaling = false
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
            Vector2Int sourceSize = new(sourceTexture.width, sourceTexture.height);
            if (preventUpscaling && targets.Any(target => !CanFitSource(sourceSize, target.Size)))
                throw new ArgumentException("Choose source artwork large enough for every cut.");
            sourceTexture.wrapMode = TextureWrapMode.Clamp;
            Color32[] sourcePixels = sourceTexture.GetPixels32();
            RectInt contentBounds = GetVisibleContentBounds(
                sourceTexture.width,
                sourceTexture.height,
                sourcePixels,
                out bool opaque
            );
            bool fillFrame = fillOpaqueSource && opaque;
            return targets.ToDictionary(
                target => target,
                target =>
                    CreatePngCut(
                        sourceTexture,
                        opaque ? null : sourcePixels,
                        contentBounds,
                        target.Size,
                        target.Offset,
                        fitEntireSource,
                        preserveVisibleSubject,
                        fillFrame
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
    /// <param name="transparentPixels">The source pixels when alpha-aware sampling is required.</param>
    /// <param name="contentBounds">The visible subject bounds used to preserve framing.</param>
    /// <param name="target">The exact output size.</param>
    /// <param name="offset">The artwork displacement in output proportions.</param>
    /// <param name="fitEntireSource">Whether the complete source canvas must remain visible.</param>
    /// <param name="preserveVisibleSubject">Whether to fit artwork that would be clipped by the cover crop.</param>
    /// <param name="fillFrame">Whether to constrain placement to a fully filled crop.</param>
    /// <returns>The encoded PNG bytes.</returns>
    private static byte[] CreatePngCut(
        Texture2D source,
        Color32[] transparentPixels,
        RectInt contentBounds,
        Vector2Int target,
        Vector2 offset,
        bool fitEntireSource,
        bool preserveVisibleSubject,
        bool fillFrame
    )
    {
        Rect crop = GetSubjectCenteredCrop(source, contentBounds, target);
        if (fillFrame)
        {
            crop.x = Mathf.Clamp(crop.x - crop.width * offset.x, 0, source.width - crop.width);
            crop.y = Mathf.Clamp(crop.y - crop.height * offset.y, 0, source.height - crop.height);
            offset = Vector2.zero;
        }
        else if (fitEntireSource)
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
                    {
                        float sourceX = crop.x + sampleX - 0.5f;
                        float sourceY = crop.y + sampleY - 0.5f;
                        pixels[row + x] =
                            transparentPixels == null
                                ? source.GetPixelBilinear(
                                    sourceX / source.width,
                                    sourceY / source.height
                                )
                                : SampleTransparentPixel(
                                    transparentPixels,
                                    source.width,
                                    source.height,
                                    sourceX,
                                    sourceY
                                );
                    }
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
    /// Interpolates premultiplied pixels and returns straight alpha for PNG encoding.
    /// </summary>
    /// <param name="pixels">The source image pixels in row order.</param>
    /// <param name="width">The source image width.</param>
    /// <param name="height">The source image height.</param>
    /// <param name="x">The horizontal sample coordinate in source pixels.</param>
    /// <param name="y">The vertical sample coordinate in source pixels.</param>
    /// <returns>The filtered straight-alpha pixel.</returns>
    internal static Color SampleTransparentPixel(
        Color32[] pixels,
        int width,
        int height,
        float x,
        float y
    )
    {
        x = Mathf.Clamp(x, 0, width - 1);
        y = Mathf.Clamp(y, 0, height - 1);
        int left = Mathf.FloorToInt(x);
        int right = Mathf.Min(left + 1, width - 1);
        int bottom = Mathf.FloorToInt(y) * width;
        int top = Mathf.Min(bottom + width, (height - 1) * width);
        float horizontalMix = x - left;
        Color lower = Color.LerpUnclamped(
            Premultiply(pixels[bottom + left]),
            Premultiply(pixels[bottom + right]),
            horizontalMix
        );
        Color upper = Color.LerpUnclamped(
            Premultiply(pixels[top + left]),
            Premultiply(pixels[top + right]),
            horizontalMix
        );
        Color filtered = Color.LerpUnclamped(lower, upper, y - bottom / width);
        return filtered.a > 0
            ? new Color(
                filtered.r / filtered.a,
                filtered.g / filtered.a,
                filtered.b / filtered.a,
                filtered.a
            )
            : Color.clear;
    }

    /// <summary>
    /// Weights the source color by its contribution to the filtered pixel.
    /// </summary>
    /// <param name="pixel">The source pixel with straight alpha.</param>
    /// <returns>The corresponding premultiplied pixel.</returns>
    private static Color Premultiply(Color pixel) =>
        new(pixel.r * pixel.a, pixel.g * pixel.a, pixel.b * pixel.a, pixel.a);

    /// <summary>
    /// Finds the smallest source rectangle containing pixels that contribute visible alpha.
    /// </summary>
    /// <param name="width">The source image width.</param>
    /// <param name="height">The source image height.</param>
    /// <param name="pixels">The source image pixels in row order.</param>
    /// <param name="opaque">Whether all source pixels are fully opaque.</param>
    /// <returns>The visible bounds, or the full canvas when the source is fully transparent.</returns>
    private static RectInt GetVisibleContentBounds(
        int width,
        int height,
        Color32[] pixels,
        out bool opaque
    )
    {
        int minimumX = width;
        int minimumY = height;
        int maximumX = -1;
        int maximumY = -1;
        opaque = true;
        for (int index = 0; index < pixels.Length; index++)
        {
            opaque &= pixels[index].a == byte.MaxValue;
            if (pixels[index].a <= 1)
                continue;
            int x = index % width;
            int y = index / width;
            minimumX = Mathf.Min(minimumX, x);
            minimumY = Mathf.Min(minimumY, y);
            maximumX = Mathf.Max(maximumX, x);
            maximumY = Mathf.Max(maximumY, y);
        }
        return maximumX < minimumX || maximumY < minimumY
            ? new RectInt(0, 0, width, height)
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
