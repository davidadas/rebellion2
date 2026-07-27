/// <summary>
/// Provides active-pack data to Unity authoring tools.
/// </summary>
public static class ContentPackEditor
{
    /// <summary>
    /// Loads the active pack's typed game-data catalog for editor authoring.
    /// </summary>
    /// <returns>The active content pack's game data.</returns>
    public static GameDataCatalog LoadGameData()
    {
        return ContentPackLoader.OpenActive().GameData;
    }
}
