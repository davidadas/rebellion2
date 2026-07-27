using UnityEditor;

public static class ApplicationContentPrefabBuilder
{
    [MenuItem("Rebellion/Content/Rebuild Application UI")]
    public static void Rebuild()
    {
        UIAuthoringGuard.EnsureEditMode();
        SaveMenuPrefabBuilder.RebuildAllSaveMenuPrefabs();
        StrategyViewPrefabBuilder.RebuildAllStrategyViewPrefabs();
        MainMenuPrefabAuthoring.RebuildMainMenuViewBindings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
