using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the generated tactical-scene root and installs it in the TacticalView scene.
/// </summary>
public static class TacticalViewPrefabBuilder
{
    private const string _prefabPath = "Assets/Prefabs/UI/TacticalView/TacticalViewRoot.prefab";
    private const string _scenePath = "Assets/Scenes/TacticalView.unity";
    private const string _sceneInstanceName = "TacticalViewRoot";

    /// <summary>
    /// Rebuilds the tactical root prefab and generated scene.
    /// </summary>
    public static void Rebuild()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_prefabPath));
        GameObject root = new GameObject(_sceneInstanceName);
        try
        {
            TacticalViewController controller = root.AddComponent<TacticalViewController>();

            GameObject mapRoot = new GameObject("MapRoot", typeof(TacticalMapRenderer));
            mapRoot.transform.SetParent(root.transform, false);

            GameObject battleRoot = new GameObject("BattleRoot");
            battleRoot.transform.SetParent(root.transform, false);

            GameObject cameraObject = new GameObject(
                "BattleCamera",
                typeof(Camera),
                typeof(AudioListener),
                typeof(TacticalViewCameraController)
            );
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(root.transform, false);

            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("battleCamera").objectReferenceValue =
                cameraObject.GetComponent<Camera>();
            serializedController.FindProperty("battleRoot").objectReferenceValue =
                battleRoot.transform;
            serializedController.FindProperty("mapRoot").objectReferenceValue = mapRoot.transform;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, _prefabPath);
            if (prefab == null)
                throw new IOException($"Could not generate tactical prefab: {_prefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        SceneBuilder.Build(_scenePath, _prefabPath, _sceneInstanceName);
    }
}
