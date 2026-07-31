using UnityEditor;

public static class ApplicationContentPrefabBuilder
{
    [MenuItem("Rebellion/Content/Rebuild Application UI")]
    public static void Rebuild()
    {
        UIAuthoringGuard.EnsureEditMode();
        SaveMenuPrefabBuilder.RebuildAllSaveMenuPrefabs();
        StrategyViewPrefabBuilder.RebuildAllStrategyViewPrefabs();
        MainMenuPrefabBuilder.RebuildMainMenuPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
