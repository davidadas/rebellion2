using UnityEngine;

/// <summary>
/// Calculates bounded aspect-ratio crops for artwork workflow previews.
/// </summary>
internal static class ContentEditorArtworkCrop
{
    /// <summary>
    /// Finds the centered destination rectangle that shows a complete source without stretching.
    /// </summary>
    /// <param name="source">The complete source canvas dimensions.</param>
    /// <param name="target">The available destination rectangle.</param>
    /// <returns>The largest centered destination rectangle with the source aspect ratio.</returns>
    internal static RectInt GetCenteredFitRect(Vector2Int source, RectInt target)
    {
        if (source.x <= 0 || source.y <= 0 || target.width <= 0 || target.height <= 0)
            return target;
        float sourceAspect = (float)source.x / source.y;
        float targetAspect = (float)target.width / target.height;
        if (sourceAspect > targetAspect)
        {
            int height = Mathf.Max(1, Mathf.RoundToInt(target.width / sourceAspect));
            return new RectInt(
                target.x,
                target.y + (target.height - height) / 2,
                target.width,
                height
            );
        }
        int width = Mathf.Max(1, Mathf.RoundToInt(target.height * sourceAspect));
        return new RectInt(target.x + (target.width - width) / 2, target.y, width, target.height);
    }

    /// <summary>
    /// Finds the source UV rectangle that fills a requested output aspect ratio without stretching.
    /// </summary>
    /// <param name="source">The complete source canvas dimensions.</param>
    /// <param name="target">The requested output dimensions.</param>
    /// <returns>A centered normalized UV rectangle.</returns>
    internal static Rect GetCenteredUvRect(Vector2Int source, Vector2Int target)
    {
        if (source.x <= 0 || source.y <= 0 || target.x <= 0 || target.y <= 0)
            return new Rect(0, 0, 1, 1);
        float sourceAspect = (float)source.x / source.y;
        float targetAspect = (float)target.x / target.y;
        if (sourceAspect > targetAspect)
        {
            float width = targetAspect / sourceAspect;
            return new Rect((1 - width) * 0.5f, 0, width, 1);
        }
        float height = sourceAspect / targetAspect;
        return new Rect(0, (1 - height) * 0.5f, 1, height);
    }

    /// <summary>
    /// Keeps an aspect-constrained crop inside its source while applying position and zoom.
    /// </summary>
    /// <param name="source">The source dimensions.</param>
    /// <param name="target">The required output dimensions.</param>
    /// <param name="center">The requested normalized crop center.</param>
    /// <param name="zoom">The magnification relative to the largest fitting crop.</param>
    /// <returns>The bounded normalized source rectangle.</returns>
    internal static Rect GetUvRect(Vector2Int source, Vector2Int target, Vector2 center, float zoom)
    {
        Rect crop = GetCenteredUvRect(source, target);
        Vector2 size = crop.size / Mathf.Max(1, zoom);
        return new Rect(
            Mathf.Clamp(center.x - size.x * 0.5f, 0, 1 - size.x),
            Mathf.Clamp(center.y - size.y * 0.5f, 0, 1 - size.y),
            size.x,
            size.y
        );
    }
}
