using UnityEngine;

/// <summary>
/// Launches the player in exclusive fullscreen by default. The video-settings system does not yet
/// apply a fullscreen preference at runtime, so this guarantees fullscreen regardless of the
/// platform's remembered window state. It runs only in the built player, leaving the editor Game
/// view untouched.
/// </summary>
public static class DisplayBootstrap
{
    /// <summary>
    /// Applies the default fullscreen mode as the player starts, before any scene loads.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyDefaultDisplayMode()
    {
#if !UNITY_EDITOR
        // Borderless (FullScreenWindow) leaves the Windows taskbar visible over the game; exclusive
        // fullscreen takes over the whole display. Use the desktop resolution (not the panel's
        // native, which may be a wider cinema mode) so 16:9 content fills without bars.
        Resolution desktop = Screen.currentResolution;
        Screen.SetResolution(desktop.width, desktop.height, FullScreenMode.ExclusiveFullScreen);
#endif
    }
}
