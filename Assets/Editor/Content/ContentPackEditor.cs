/// <summary>
/// Provides active-pack data to Unity authoring tools.
/// </summary>
public static class ContentPackEditor
{
    private const string _contentAssetRoot = "Assets/Content/";
    private const string _embeddedPlanetAssetRoot =
        "Assets/Content/Application/MainMenu/Models/Planet/";

    private static EditorContentAssetSource _assets;

    public static IContentAssetSource Assets => _assets ??= new EditorContentAssetSource();

    /// <summary>
    /// Loads the active content pack for editor authoring.
    /// </summary>
    /// <returns>The active content pack.</returns>
    public static ContentPack LoadActivePack()
    {
        return ContentPackLoader.OpenActive();
    }

    /// <summary>
    /// Loads the active pack's typed game-data catalog for editor authoring.
    /// </summary>
    /// <returns>The active content pack's game data.</returns>
    public static GameDataCatalog LoadGameData()
    {
        return LoadActivePack().GameData;
    }

    /// <summary>
    /// Determines whether a Unity asset is external runtime content that must be removed from
    /// generated player assets. The main-menu planet package is the sole native Unity package
    /// beneath the content tree and is compiled into the player.
    /// </summary>
    /// <param name="assetPath">The Unity project-relative asset path.</param>
    /// <returns>True when the asset belongs to external runtime content.</returns>
    public static bool IsExternalContentAssetPath(string assetPath)
    {
        return !string.IsNullOrEmpty(assetPath)
            && assetPath.StartsWith(_contentAssetRoot, System.StringComparison.Ordinal)
            && !assetPath.StartsWith(_embeddedPlanetAssetRoot, System.StringComparison.Ordinal);
    }
}
