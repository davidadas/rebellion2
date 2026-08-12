using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns supported display modes and applies the active video configuration.
/// </summary>
public sealed class DisplayManager
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

    private readonly Func<IReadOnlyList<Vector2Int>> _getAvailableResolutions;
    private readonly Func<Vector2Int> _getNativeResolution;
    private readonly Action<int, int, FullScreenMode> _applyResolution;

    /// <summary>
    /// Creates a display manager backed by Unity's active display APIs.
    /// </summary>
    public DisplayManager()
        : this(GetUnityResolutions, GetUnityNativeResolution, ApplyUnityResolution) { }

    /// <summary>
    /// Creates a display manager backed by supplied display operations.
    /// </summary>
    /// <param name="getAvailableResolutions">Returns the resolutions reported by the display.</param>
    /// <param name="getNativeResolution">Returns the display's native resolution.</param>
    /// <param name="applyResolution">Applies a selected resolution and window mode.</param>
    internal DisplayManager(
        Func<IReadOnlyList<Vector2Int>> getAvailableResolutions,
        Func<Vector2Int> getNativeResolution,
        Action<int, int, FullScreenMode> applyResolution
    )
    {
        _getAvailableResolutions =
            getAvailableResolutions
            ?? throw new ArgumentNullException(nameof(getAvailableResolutions));
        _getNativeResolution =
            getNativeResolution ?? throw new ArgumentNullException(nameof(getNativeResolution));
        _applyResolution =
            applyResolution ?? throw new ArgumentNullException(nameof(applyResolution));
    }

    /// <summary>
    /// Returns the distinct available 16:9 resolutions in ascending size order.
    /// </summary>
    public IReadOnlyList<Vector2Int> GetSupportedResolutions()
    {
        List<Vector2Int> supported = new List<Vector2Int>();
        IReadOnlyList<Vector2Int> available = _getAvailableResolutions();
        if (available != null)
        {
            foreach (Vector2Int resolution in available)
            {
                if (!IsSixteenByNine(resolution.x, resolution.y) || supported.Contains(resolution))
                    continue;

                supported.Add(resolution);
            }
        }

        supported.Sort(CompareBySize);
        if (supported.Count == 0)
        {
            Vector2Int native = _getNativeResolution();
            supported.Add(CreateFallback(native.x, native.y));
        }

        return supported;
    }

    /// <summary>
    /// Resolves the closest supported 16:9 mode for the requested size.
    /// </summary>
    /// <param name="requestedWidth">The requested width, or zero for the native width.</param>
    /// <param name="requestedHeight">The requested height, or zero for the native height.</param>
    /// <returns>The selected supported resolution.</returns>
    public Vector2Int ResolveResolution(int requestedWidth, int requestedHeight)
    {
        Vector2Int native = _getNativeResolution();
        return ResolveResolution(
            GetSupportedResolutions(),
            requestedWidth,
            requestedHeight,
            native.x,
            native.y
        );
    }

    /// <summary>
    /// Resolves and applies the supplied persisted video settings.
    /// </summary>
    /// <param name="settings">The video settings to apply and update with the resolved size.</param>
    /// <returns>The resolution applied to the display.</returns>
    public Vector2Int Apply(UserVideoSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        Vector2Int resolution = ResolveResolution(
            settings.ResolutionWidth,
            settings.ResolutionHeight
        );
        settings.ResolutionWidth = resolution.x;
        settings.ResolutionHeight = resolution.y;
        _applyResolution(resolution.x, resolution.y, (FullScreenMode)settings.FullScreenMode);
        return resolution;
    }

    /// <summary>
    /// Checks whether a resolution has a 16:9 aspect ratio within integer rounding tolerance.
    /// </summary>
    internal static bool IsSixteenByNine(int width, int height)
    {
        return width > 0 && height > 0 && Math.Abs((long)width * 9L - (long)height * 16L) <= 8L;
    }

    /// <summary>
    /// Selects the exact requested mode or the largest supported mode fitting the target display.
    /// </summary>
    internal static Vector2Int ResolveResolution(
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

    /// <summary>
    /// Compares two resolutions by width and then height.
    /// </summary>
    private static int CompareBySize(Vector2Int left, Vector2Int right)
    {
        int widthComparison = left.x.CompareTo(right.x);
        return widthComparison != 0 ? widthComparison : left.y.CompareTo(right.y);
    }

    /// <summary>
    /// Creates a common 16:9 mode fitting inside the supplied bounds.
    /// </summary>
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

    /// <summary>
    /// Reads the distinct resolution sizes reported by Unity.
    /// </summary>
    private static IReadOnlyList<Vector2Int> GetUnityResolutions()
    {
        List<Vector2Int> resolutions = new List<Vector2Int>();
        foreach (Resolution resolution in Screen.resolutions)
            resolutions.Add(new Vector2Int(resolution.width, resolution.height));
        return resolutions;
    }

    /// <summary>
    /// Reads the native resolution of Unity's primary display.
    /// </summary>
    private static Vector2Int GetUnityNativeResolution()
    {
        return new Vector2Int(Display.main.systemWidth, Display.main.systemHeight);
    }

    /// <summary>
    /// Applies a display mode through Unity outside the editor.
    /// </summary>
    private static void ApplyUnityResolution(int width, int height, FullScreenMode mode)
    {
        if (!Application.isEditor)
            Screen.SetResolution(width, height, mode);
    }
}
