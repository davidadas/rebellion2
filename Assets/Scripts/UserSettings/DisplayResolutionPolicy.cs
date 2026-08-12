using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the supported display resolutions.
/// </summary>
internal static class DisplayResolutionPolicy
{
    private static readonly Vector2Int[] _commonFallbacks =
    {
        new Vector2Int(3840, 2160),
        new Vector2Int(2560, 1440),
        new Vector2Int(1920, 1080),
        new Vector2Int(1600, 900),
        new Vector2Int(1366, 768),
        new Vector2Int(1280, 720),
        new Vector2Int(1024, 576),
        new Vector2Int(960, 540),
        new Vector2Int(640, 360),
    };

    /// <summary>
    /// Checks whether a resolution uses a 16:9 aspect ratio.
    /// </summary>
    internal static bool IsSixteenByNine(int width, int height)
    {
        return width > 0 && height > 0 && Math.Abs((long)width * 9L - (long)height * 16L) <= 8L;
    }

    /// <summary>
    /// Returns the available 16:9 resolutions.
    /// </summary>
    internal static List<Vector2Int> GetSupportedResolutions()
    {
        List<Vector2Int> supported = new List<Vector2Int>();
        foreach (Resolution resolution in Screen.resolutions)
        {
            if (!IsSixteenByNine(resolution.width, resolution.height))
                continue;

            Vector2Int size = new Vector2Int(resolution.width, resolution.height);
            if (!supported.Contains(size))
                supported.Add(size);
        }

        supported.Sort(CompareBySize);
        if (supported.Count == 0)
            supported.Add(CreateFallback(Screen.width, Screen.height));

        return supported;
    }

    /// <summary>
    /// Returns the supported resolution that best matches the requested size.
    /// </summary>
    internal static Vector2Int Resolve(
        IReadOnlyList<Vector2Int> supported,
        int requestedWidth,
        int requestedHeight,
        int fallbackWidth,
        int fallbackHeight
    )
    {
        int targetWidth = requestedWidth > 0 ? requestedWidth : fallbackWidth;
        int targetHeight = requestedHeight > 0 ? requestedHeight : fallbackHeight;
        if (supported == null || supported.Count == 0)
            return CreateFallback(targetWidth, targetHeight);

        Vector2Int bestFit = default;
        long bestArea = -1;
        Vector2Int smallest = default;
        long smallestArea = long.MaxValue;
        foreach (Vector2Int candidate in supported)
        {
            if (!IsSixteenByNine(candidate.x, candidate.y))
                continue;
            if (candidate.x == requestedWidth && candidate.y == requestedHeight)
                return candidate;

            long area = (long)candidate.x * candidate.y;
            if (area < smallestArea)
            {
                smallest = candidate;
                smallestArea = area;
            }

            if (candidate.x <= targetWidth && candidate.y <= targetHeight && area > bestArea)
            {
                bestFit = candidate;
                bestArea = area;
            }
        }

        if (bestArea >= 0)
            return bestFit;
        return smallestArea < long.MaxValue ? smallest : CreateFallback(targetWidth, targetHeight);
    }

    private static int CompareBySize(Vector2Int left, Vector2Int right)
    {
        int widthComparison = left.x.CompareTo(right.x);
        return widthComparison != 0 ? widthComparison : left.y.CompareTo(right.y);
    }

    private static Vector2Int CreateFallback(int targetWidth, int targetHeight)
    {
        if (targetWidth <= 0 || targetHeight <= 0)
            return new Vector2Int(1920, 1080);

        foreach (Vector2Int candidate in _commonFallbacks)
        {
            if (candidate.x <= targetWidth && candidate.y <= targetHeight)
                return candidate;
        }

        int scale = Math.Max(1, Math.Min(targetWidth / 16, targetHeight / 9));
        return new Vector2Int(scale * 16, scale * 9);
    }
}
