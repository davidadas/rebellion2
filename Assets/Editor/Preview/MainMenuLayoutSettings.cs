using UnityEditor;
using UnityEngine;

/// <summary>
/// Persists authored main-menu placements independently of generated prefabs.
/// </summary>
[FilePath("ProjectSettings/MainMenuLayout.asset", FilePathAttribute.Location.ProjectFolder)]
public sealed class MainMenuLayoutSettings : ScriptableSingleton<MainMenuLayoutSettings>
{
    public static readonly Rect DefaultMultiplayer = new Rect(
        0.1046875f,
        0.07916667f,
        0.0796875f,
        0.12708333f
    );

    public static readonly UIQuadrilateral MultiplayerSurface = new UIQuadrilateral(
        new Vector2(12f / 51f, 0),
        new Vector2(0, 42f / 61f),
        new Vector2(40f / 51f, 1),
        new Vector2(1, 22f / 61f)
    );

    [SerializeField]
    private Rect _multiplayer = DefaultMultiplayer;

    [SerializeField]
    private float _multiplayerRotation;

    [SerializeField]
    private UIQuadrilateral _multiplayerCorners = UIQuadrilateral.Unit;

    public Rect Multiplayer => _multiplayer;
    public float MultiplayerRotation => _multiplayerRotation;
    public UIQuadrilateral MultiplayerCorners => _multiplayerCorners;

    /// <summary>
    /// Saves the chosen normalized control rectangle, rotation, and projected corners.
    /// </summary>
    /// <param name="bounds">The authored rectangle.</param>
    /// <param name="rotation">The counterclockwise rotation in degrees.</param>
    /// <param name="corners">The texture corners inside the authored rectangle.</param>
    public void SetMultiplayer(Rect bounds, float rotation, UIQuadrilateral corners)
    {
        _multiplayer = bounds;
        _multiplayerRotation = rotation;
        _multiplayerCorners = corners;
        Save(true);
    }

    /// <summary>
    /// Applies the shared placement and interactive surface to a menu image.
    /// </summary>
    /// <param name="image">The projected control image.</param>
    /// <param name="bounds">The normalized anchor rectangle.</param>
    /// <param name="rotation">The counterclockwise rotation in degrees.</param>
    /// <param name="corners">The texture corners inside the rectangle.</param>
    public static void ApplyPlacement(
        PerspectiveImage image,
        Rect bounds,
        float rotation,
        UIQuadrilateral corners
    )
    {
        RectTransform rect = image.rectTransform;
        rect.anchorMin = bounds.min;
        rect.anchorMax = bounds.max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localRotation = Quaternion.Euler(0, 0, rotation);
        image.Configure(corners, MultiplayerSurface.Transform(corners.ToMatrix()));
    }
}
