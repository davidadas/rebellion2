/// <summary>
/// Provides active-pack data to Unity authoring tools.
/// </summary>
public static class ContentPackEditor
{
    private static EditorContentAssetSource assets;

    public static IContentAssetSource Assets => assets ??= new EditorContentAssetSource();

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
}
