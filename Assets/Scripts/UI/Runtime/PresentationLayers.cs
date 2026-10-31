/// <summary>
/// Reserves separate render layers for UI presentations that coexist during scene transitions.
/// Cameras and lights use the same assignments as their presentation's renderers.
/// </summary>
public static class PresentationLayers
{
    // The loading shuttle, its exhaust, and its isolated camera and lights.
    public const int LoadingScreen = 26;

    // The main-menu planetary backdrop and its isolated camera and sun.
    public const int MainMenuPlanet = 31;
}
