/// <summary>
/// Provides active-pack data to Unity authoring tools.
/// </summary>
public static class ContentPackEditor
{
    private static EditorContentAssetSource assets;

    public static IContentAssetSource Assets => assets ??= new EditorContentAssetSource();

    /// <summary>
    /// Loads a development-preview texture at its full authored resolution.
    /// </summary>
    /// <param name="address">The content-relative texture address.</param>
    /// <returns>The imported full-resolution texture.</returns>
    public static UnityEngine.Texture2D GetFullSizeTexture(string address)
    {
        assets ??= new EditorContentAssetSource();
        return assets.GetFullSizeTexture(address);
    }

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
