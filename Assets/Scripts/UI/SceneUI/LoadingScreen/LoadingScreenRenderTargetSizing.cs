using UnityEngine;

/// <summary>
/// Defines the resolution policy for an aspect-correct loading-screen render target.
/// </summary>
internal static class LoadingScreenRenderTargetSizing
{
    private const int _referenceHeight = 720;
    private const int _maximumWidth = 1920;

    /// <summary>
    /// Calculates a bounded render-target size with the same aspect ratio as the display.
    /// </summary>
    /// <param name="displayWidth">The current display width.</param>
    /// <param name="displayHeight">The current display height.</param>
    /// <returns>The width and height of the loading-screen render target.</returns>
    internal static Vector2Int Calculate(int displayWidth, int displayHeight)
    {
        int safeWidth = Mathf.Max(displayWidth, 1);
        int safeHeight = Mathf.Max(displayHeight, 1);
        float displayAspect = (float)safeWidth / safeHeight;
        int renderWidth = Mathf.Max(1, Mathf.RoundToInt(_referenceHeight * displayAspect));
        int renderHeight = _referenceHeight;
        if (renderWidth <= _maximumWidth)
            return new Vector2Int(renderWidth, renderHeight);

        renderWidth = _maximumWidth;
        renderHeight = Mathf.Max(1, Mathf.RoundToInt(renderWidth / displayAspect));
        return new Vector2Int(renderWidth, renderHeight);
    }
}
