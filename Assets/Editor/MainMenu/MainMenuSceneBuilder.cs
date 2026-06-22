using System.IO;
using UnityEditor;

public static class MainMenuSceneBuilder
{
    private const string _scenePath = "Assets/Scenes/MainMenu.unity";
    private const string _prefabPath = "Assets/Prefabs/UI/MainMenu/MainMenuRoot.prefab";
    private const string _sceneInstanceName = "MainMenuRoot";

    [MenuItem("Rebellion/Main Menu/Install Main Menu Root Prefab In Scene")]
    public static void InstallMainMenuRootPrefabInScene()
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(_prefabPath) == null)
            throw new FileNotFoundException(_prefabPath);

        SceneRootPrefabInstaller.InstallRootPrefabInScene(
            _scenePath,
            _prefabPath,
            _sceneInstanceName,
            null
        );
    }
}
