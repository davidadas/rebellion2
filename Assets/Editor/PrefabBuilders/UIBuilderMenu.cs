using UnityEditor;

/// <summary>
/// The only Unity menu surface for rebuilding generated UI.
/// </summary>
public static class UIBuilderMenu
{
    [MenuItem("Rebellion/UI/Build All", false, 0)]
    public static void BuildAll()
    {
        UIAuthoringGuard.EnsureEditMode();
        MainMenuPrefabBuilder.Rebuild();
        SaveMenuPrefabBuilder.Rebuild();
        StrategyViewPrefabBuilder.Rebuild();
        SaveAndRefresh();
    }

    [MenuItem("Rebellion/UI/Build Main Menu", false, 20)]
    public static void BuildMainMenu()
    {
        UIAuthoringGuard.EnsureEditMode();
        MainMenuPrefabBuilder.Rebuild();
        SaveAndRefresh();
    }

    [MenuItem("Rebellion/UI/Build Save Game", false, 21)]
    public static void BuildSaveGame()
    {
        UIAuthoringGuard.EnsureEditMode();
        SaveMenuPrefabBuilder.Rebuild();
        SaveAndRefresh();
    }

    [MenuItem("Rebellion/UI/Build Strategy", false, 22)]
    public static void BuildStrategy()
    {
        UIAuthoringGuard.EnsureEditMode();
        StrategyViewPrefabBuilder.Rebuild();
        SaveAndRefresh();
    }

    private static void SaveAndRefresh()
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
