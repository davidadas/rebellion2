using System;
using System.IO;
using UnityEngine;

internal enum ContentEditorArtworkEffect
{
    Travel,
    Damage,
}

internal enum ContentEditorArtworkEffectStyle
{
    ShipTravel,
    PersonnelTravel,
    FacilityTravel,
    Damage,
}

/// <summary>
/// Draws original gameplay effects against the silhouette and placement of the selected unit.
/// </summary>
internal static class ContentEditorArtworkEffectGenerator
{
    private static readonly Color _previewBackground = new(0.035f, 0.04f, 0.05f);
    private static readonly Color _plasma = new(0.18f, 0.64f, 1f);
    private static readonly Color _whiteBlue = new(0.78f, 0.94f, 1f);

    /// <summary>
    /// Returns a repeatable seed for the unit and the chosen effect.
    /// </summary>
    /// <param name="unit">The unit's serialized identifier.</param>
    /// <param name="effect">The selected gameplay state.</param>
    /// <returns>The initial variation seed.</returns>
    internal static int GetSeed(string unit, ContentEditorArtworkEffect effect)
    {
        unchecked
        {
            uint hash = 2166136261u;
            foreach (char character in unit)
                hash = (hash ^ character) * 16777619u;
            return (int)((hash ^ (uint)effect) * 16777619u);
        }
    }

    /// <summary>
    /// Decodes one source and generates an authored asset with an optional composed preview.
    /// </summary>
    /// <param name="sourcePath">The resolved normal unit artwork.</param>
    /// <param name="size">The output canvas, or null to match the source exactly.</param>
    /// <param name="style">How the runtime consumes this effect.</param>
    /// <param name="seed">The repeatable variation seed.</param>
    /// <param name="includePreview">Whether to render a composed preview for the editor.</param>
    /// <param name="stretchSource">Whether the runtime stretches both layers into the same frame.</param>
    /// <returns>The encoded state image and optional caller-owned preview texture.</returns>
    internal static (byte[] Png, Texture2D Preview) Generate(
        string sourcePath,
        Vector2Int? size,
        ContentEditorArtworkEffectStyle style,
        int seed,
        bool includePreview,
        bool stretchSource = false
    )
    {
        Texture2D source = new(2, 2, TextureFormat.RGBA32, false);
        Texture2D output = null;
        try
        {
            if (!ImageConversion.LoadImage(source, File.ReadAllBytes(sourcePath)))
                throw new InvalidDataException("The selected unit image could not be decoded.");

            Vector2Int target = size ?? new Vector2Int(source.width, source.height);
            Color[] subject = FitToCanvas(source, target, stretchSource);
            RectInt bounds = FindSubject(subject, target);
            System.Random random = new(seed);
            Color[] effect = style switch
            {
                ContentEditorArtworkEffectStyle.ShipTravel => DrawTravelArc(
                    subject,
                    target,
                    bounds,
                    random
                ),
                ContentEditorArtworkEffectStyle.Damage => DrawDamage(
                    subject,
                    target,
                    bounds,
                    random
                ),
                _ => DrawStarfield(target, random),
            };
            Color[] preview = includePreview ? new Color[subject.Length] : null;
            for (int index = 0; index < effect.Length; index++)
            {
                Color entity = subject[index];
                switch (style)
                {
                    case ContentEditorArtworkEffectStyle.FacilityTravel:
                        effect[index] = Over(effect[index], entity);
                        if (includePreview)
                            preview[index] = effect[index];
                        break;
                    case ContentEditorArtworkEffectStyle.PersonnelTravel:
                        Color field = effect[index];
                        field.a *= 1f - entity.a;
                        effect[index] = field;
                        if (includePreview)
                            preview[index] = Over(Over(_previewBackground, field), entity);
                        break;
                    default:
                        if (includePreview)
                            preview[index] = Over(Over(_previewBackground, entity), effect[index]);
                        break;
                }
            }
            output = CreateTexture(target, effect);
            byte[] png = ImageConversion.EncodeToPNG(output);
            return (png, includePreview ? CreateTexture(target, preview) : null);
        }
        finally
        {
            Release(source);
            Release(output);
        }
    }

    /// <summary>
    /// Resamples the full source canvas using the centered aspect fit of the unit image slot.
    /// </summary>
    /// <param name="source">The readable source texture.</param>
    /// <param name="target">The destination canvas.</param>
    /// <param name="stretchSource">Whether both runtime layers are stretched edge to edge.</param>
    /// <returns>Canvas pixels with the source placement preserved.</returns>
    private static Color[] FitToCanvas(Texture2D source, Vector2Int target, bool stretchSource)
    {
        if (source.width == target.x && source.height == target.y)
            return source.GetPixels();
        float scale = Mathf.Min((float)target.x / source.width, (float)target.y / source.height);
        float width = stretchSource ? target.x : scale * source.width;
        float height = stretchSource ? target.y : scale * source.height;
        float left = (target.x - width) / 2f;
        float bottom = (target.y - height) / 2f;
        Color[] pixels = new Color[target.x * target.y];
        for (int y = 0; y < target.y; y++)
        for (int x = 0; x < target.x; x++)
        {
            float u = (x + 0.5f - left) / width;
            float v = (y + 0.5f - bottom) / height;
            if (u >= 0f && u <= 1f && v >= 0f && v <= 1f)
                pixels[y * target.x + x] = source.GetPixelBilinear(u, v);
        }
        return pixels;
    }

    /// <summary>
    /// Locates the unit's alpha silhouette in the output frame.
    /// </summary>
    /// <param name="pixels">The unit pixels.</param>
    /// <param name="size">The complete canvas dimensions.</param>
    /// <returns>The rectangle containing the visible subject.</returns>
    private static RectInt FindSubject(Color[] pixels, Vector2Int size)
    {
        int left = size.x;
        int right = -1;
        int bottom = size.y;
        int top = -1;
        bool hasTransparency = false;
        for (int index = 0; index < pixels.Length; index++)
        {
            if (pixels[index].a < 0.1f)
                hasTransparency = true;
            if (pixels[index].a < 0.35f)
                continue;
            int x = index % size.x;
            int y = index / size.x;
            left = Mathf.Min(left, x);
            right = Mathf.Max(right, x);
            bottom = Mathf.Min(bottom, y);
            top = Mathf.Max(top, y);
        }
        if (right < left || !hasTransparency)
            throw new InvalidDataException(
                "Choose normal unit artwork with a visible subject on a transparent background."
            );
        return new RectInt(left, bottom, right - left + 1, top - bottom + 1);
    }

    /// <summary>
    /// Draws a narrow travel wake using the rightmost portion of the ship silhouette.
    /// </summary>
    /// <param name="subject">The fitted unit artwork.</param>
    /// <param name="size">The output canvas.</param>
    /// <param name="bounds">The silhouette bounds.</param>
    /// <param name="random">The seeded source of controlled variation.</param>
    /// <returns>A transparent forward layer containing the wake.</returns>
    private static Color[] DrawTravelArc(
        Color[] subject,
        Vector2Int size,
        RectInt bounds,
        System.Random random
    )
    {
        int aftStart = bounds.xMax - Mathf.Max(1, Mathf.RoundToInt(bounds.width * 0.16f));
        int bottom = bounds.yMax;
        int top = bounds.yMin;
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        for (int x = aftStart; x < bounds.xMax; x++)
        {
            if (subject[y * size.x + x].a < 0.55f)
                continue;
            bottom = Mathf.Min(bottom, y);
            top = Mathf.Max(top, y);
        }
        float centerY = bottom <= top ? (bottom + top) * 0.5f : bounds.center.y;
        float radiusY = Mathf.Max((top - bottom) * 0.6f, bounds.height * 0.24f) + size.y * 0.045f;
        float radiusX = size.y * RandomBetween(random, 0.24f, 0.34f);
        float centerX = Mathf.Min(bounds.xMax - radiusX * 0.68f, size.x - 1 - radiusX * 1.12f);
        float phase = RandomBetween(random, 0, 6.283f);
        Color[] pixels = new Color[subject.Length];
        for (int y = 0; y < size.y; y++)
        for (int x = 0; x < size.x; x++)
        {
            float vertical = (y - centerY) / radiusY;
            float horizontal = (x - centerX) / radiusX;
            if (horizontal <= -0.12f || horizontal > 1.5f || Mathf.Abs(vertical) > 1.45f)
                continue;
            float ring = Mathf.Abs(Mathf.Sqrt(horizontal * horizontal + vertical * vertical) - 1f);
            float fade = Mathf.SmoothStep(0f, 1f, (horizontal + 0.12f) / 0.42f);
            float current = 0.77f + 0.23f * Mathf.Sin(vertical * 20f + phase);
            float core = Mathf.Exp(-ring * ring * 880f);
            float glow = Mathf.Exp(-ring * ring * 34f);
            float shock = Mathf.Exp(-Mathf.Pow((ring - 0.22f) * 19f, 2f));
            Color color = Color.Lerp(_plasma, _whiteBlue, core * current);
            color.a = fade * Mathf.Clamp01((core * 0.82f + glow * 0.32f + shock * 0.1f) * current);
            pixels[y * size.x + x] = color;
        }
        return pixels;
    }

    /// <summary>
    /// Draws seeded horizontal travel streaks across a deep, opaque backing field.
    /// </summary>
    /// <param name="size">The output canvas.</param>
    /// <param name="random">The seeded source of star placement and intensity.</param>
    /// <returns>The complete starfield layer.</returns>
    private static Color[] DrawStarfield(Vector2Int size, System.Random random)
    {
        Color[] pixels = new Color[size.x * size.y];
        for (int y = 0; y < size.y; y++)
        {
            float band = Mathf.Sin(y / (float)size.y * Mathf.PI);
            Color background = new(
                0.004f + band * 0.008f,
                0.009f + band * 0.014f,
                0.027f + band * 0.031f
            );
            Array.Fill(pixels, background, y * size.x, size.x);
        }
        for (int index = 0; index < 95; index++)
        {
            int head = random.Next(size.x);
            int y = random.Next(size.y);
            int length = Mathf.Max(
                2,
                Mathf.RoundToInt(size.x * RandomBetween(random, 0.018f, 0.43f))
            );
            bool bright = index < 24;
            float light = bright
                ? RandomBetween(random, 0.52f, 0.98f)
                : RandomBetween(random, 0.12f, 0.52f);
            Color color = Color.Lerp(_plasma, _whiteBlue, light);
            for (int offset = 0; offset < length && head + offset < size.x; offset++)
            {
                color.a = light * Mathf.Pow(1f - offset / (float)length, 2.7f);
                int pixel = y * size.x + head + offset;
                pixels[pixel] = Over(pixels[pixel], color);
                if (y > 0 && bright)
                {
                    color.a *= 0.23f;
                    pixels[pixel - size.x] = Over(pixels[pixel - size.x], color);
                }
            }
        }
        return pixels;
    }

    /// <summary>
    /// Selects separated hull impacts and draws layered hot cores, glow and sparks.
    /// </summary>
    /// <param name="subject">The fitted ship artwork.</param>
    /// <param name="size">The output canvas.</param>
    /// <param name="bounds">The silhouette bounds.</param>
    /// <param name="random">The seeded source of impact positions and shapes.</param>
    /// <returns>A transparent impact layer aligned to the ship artwork.</returns>
    private static Color[] DrawDamage(
        Color[] subject,
        Vector2Int size,
        RectInt bounds,
        System.Random random
    )
    {
        Color[] pixels = new Color[subject.Length];
        const int impacts = 3;
        for (int index = 0; index < impacts; index++)
        {
            float column = (index + RandomBetween(random, 0.22f, 0.78f)) / impacts;
            Vector2 desired = new(
                bounds.xMin + column * bounds.width,
                bounds.yMin + RandomBetween(random, 0.23f, 0.77f) * bounds.height
            );
            Vector2 center = FindClosestHullPixel(subject, size, bounds, desired);
            float radius = Mathf.Min(size.y, bounds.width) * RandomBetween(random, 0.12f, 0.205f);
            float phase = RandomBetween(random, 0, 6.283f);
            DrawImpact(pixels, size, center, radius, phase);
            for (int spark = 0; spark < 5; spark++)
            {
                float angle = RandomBetween(random, 0, 6.283f);
                float distance = radius * RandomBetween(random, 0.45f, 1.3f);
                Vector2 ember = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                DrawImpact(pixels, size, ember, radius * RandomBetween(random, 0.1f, 0.24f), angle);
            }
        }
        return pixels;
    }

    /// <summary>
    /// Finds an opaque hull position near the desired impact point.
    /// </summary>
    /// <param name="subject">The fitted ship artwork.</param>
    /// <param name="size">The output canvas.</param>
    /// <param name="bounds">The silhouette bounds.</param>
    /// <param name="desired">The sampled position within the hull bounds.</param>
    /// <returns>The closest visible hull pixel.</returns>
    private static Vector2 FindClosestHullPixel(
        Color[] subject,
        Vector2Int size,
        RectInt bounds,
        Vector2 desired
    )
    {
        float bestDistance = float.PositiveInfinity;
        Vector2 closest = desired;
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            if (subject[y * size.x + x].a < 0.35f)
                continue;
            Vector2 point = new(x, y);
            float distance = (point - desired).sqrMagnitude;
            if (distance >= bestDistance)
                continue;
            bestDistance = distance;
            closest = point;
        }
        return closest;
    }

    /// <summary>
    /// Blends an irregular plasma burst onto the damage layer.
    /// </summary>
    /// <param name="pixels">The destination effect layer.</param>
    /// <param name="size">The output canvas.</param>
    /// <param name="center">The impact position.</param>
    /// <param name="radius">The nominal pixel radius.</param>
    /// <param name="phase">The orientation of the irregular rim.</param>
    private static void DrawImpact(
        Color[] pixels,
        Vector2Int size,
        Vector2 center,
        float radius,
        float phase
    )
    {
        Color ember = new(1f, 0.17f, 0.02f);
        Color amber = new(1f, 0.55f, 0.07f);
        Color hot = new(1f, 0.96f, 0.67f);
        for (
            int y = Mathf.Max(0, Mathf.FloorToInt(center.y - 1.8f * radius));
            y < size.y && y <= center.y + 1.8f * radius;
            y++
        )
        for (
            int x = Mathf.Max(0, Mathf.FloorToInt(center.x - 1.8f * radius));
            x < size.x && x <= center.x + 1.8f * radius;
            x++
        )
        {
            float dx = x - center.x;
            float dy = y - center.y;
            float angle = Mathf.Atan2(dy, dx);
            float rim =
                0.98f
                + 0.14f * Mathf.Sin(angle * 3f + phase)
                + 0.12f * Mathf.Sin(angle * 7f - phase)
                + 0.06f * Mathf.Cos(angle * 13f + phase);
            float distance = Mathf.Sqrt(dx * dx + dy * dy) / (radius * rim);
            float glow = Mathf.Clamp01(1f - distance / 1.48f);
            float turbulence = 0.88f + 0.12f * Mathf.Cos((dx * 0.37f + dy * 0.23f) * 6f + phase);
            float heat = Mathf.Exp(-distance * distance * 4.2f) * turbulence;
            Color color = Color.Lerp(Color.Lerp(ember, amber, glow), hot, heat);
            color.a = Mathf.Clamp01(0.82f * heat + 0.76f * glow * glow);
            int index = y * size.x + x;
            pixels[index] = Over(pixels[index], color);
        }
    }

    /// <summary>
    /// Composites one straight-alpha foreground pixel over a background pixel.
    /// </summary>
    /// <param name="background">The existing lower layer.</param>
    /// <param name="foreground">The new upper layer.</param>
    /// <returns>The composited straight-alpha pixel.</returns>
    private static Color Over(Color background, Color foreground)
    {
        float retained = background.a * (1f - foreground.a);
        float alpha = foreground.a + retained;
        if (alpha <= 0f)
            return Color.clear;
        Color pixel = (foreground * foreground.a + background * retained) / alpha;
        pixel.a = alpha;
        return pixel;
    }

    /// <summary>
    /// Samples a continuous range using the supplied variation stream.
    /// </summary>
    /// <param name="random">The seeded source of variation.</param>
    /// <param name="minimum">The lower bound.</param>
    /// <param name="maximum">The upper bound.</param>
    /// <returns>A value in the specified range.</returns>
    private static float RandomBetween(System.Random random, float minimum, float maximum) =>
        minimum + (float)random.NextDouble() * (maximum - minimum);

    /// <summary>
    /// Uploads one complete canvas into a readable texture for encoding or preview.
    /// </summary>
    /// <param name="size">The canvas dimensions.</param>
    /// <param name="pixels">The bottom-up pixel buffer.</param>
    /// <returns>A caller-owned texture.</returns>
    private static Texture2D CreateTexture(Vector2Int size, Color[] pixels)
    {
        Texture2D texture = new(size.x, size.y, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }

    /// <summary>
    /// Releases a generated texture in either the editor or the built application.
    /// </summary>
    /// <param name="texture">The texture to release, if allocated.</param>
    private static void Release(Texture2D texture)
    {
        if (texture == null)
            return;
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(texture);
        else
            UnityEngine.Object.DestroyImmediate(texture);
    }
}
