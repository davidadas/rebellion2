using UnityEngine;

/// <summary>
/// Calculates non-destructive centered cover crops for artwork workflow previews.
/// </summary>
internal static class ContentEditorArtworkCrop
{
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
}
