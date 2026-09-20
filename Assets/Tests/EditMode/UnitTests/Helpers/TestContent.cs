using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Simulation;

internal static class TestContent
{
    private static ContentAssets _assets;
    private static ContentPack _pack;

    internal static ContentPack Pack => _pack ??= ContentPackLoader.OpenActive();

    internal static ContentAssets Assets =>
        _assets ??= new ContentAssets(Pack.ContentRootPath, Pack.PackRootPath);

    internal static GameDataCatalog Data => Pack.GameData;

    /// <summary>
    /// Creates theme library.
    /// </summary>
    /// <returns>The created theme library.</returns>
    internal static FactionThemeLibrary CreateThemeLibrary()
    {
        return new FactionThemeLibrary(Data.FactionThemes);
    }

    /// <summary>
    /// Creates ui context.
    /// </summary>
    /// <param name="game">The game.</param>
    /// <param name="themeLibrary">The theme library.</param>
    /// <param name="encyclopediaCatalog">The encyclopedia catalog.</param>
    /// <returns>The created ui context.</returns>
    internal static UIContext CreateUIContext(
        GameRoot game,
        FactionThemeLibrary themeLibrary,
        EncyclopediaCatalog encyclopediaCatalog
    )
    {
        return new UIContext(game, themeLibrary, encyclopediaCatalog, Assets.GetTexture);
    }

    /// <summary>
    /// Creates game manager.
    /// </summary>
    /// <param name="game">The game.</param>
    /// <returns>The created game manager.</returns>
    internal static GameSession CreateGameSession(GameRoot game)
    {
        return GameSessionFactory.Create(game, Data);
    }
}
