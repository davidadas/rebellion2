using Rebellion.Game;
using Rebellion.Game.Encyclopedia;

internal static class TestContent
{
    private static ContentAssets assets;
    private static ContentPack pack;

    internal static ContentPack Pack => pack ??= ContentPackLoader.OpenActive();

    internal static ContentAssets Assets =>
        assets ??= new ContentAssets(Pack.ContentRootPath, Pack.PackRootPath);

    internal static GameDataCatalog Data => Pack.GameData;

    internal static FactionThemeLibrary CreateThemeLibrary()
    {
        return new FactionThemeLibrary(Data.FactionThemes);
    }

    internal static UIContext CreateUIContext(
        GameRoot game,
        FactionThemeLibrary themeLibrary,
        EncyclopediaCatalog encyclopediaCatalog
    )
    {
        return new UIContext(game, themeLibrary, encyclopediaCatalog, Assets.GetTexture);
    }

    internal static GameManager CreateGameManager(GameRoot game)
    {
        return new GameManager(game, Data);
    }
}
