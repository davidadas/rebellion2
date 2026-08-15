using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Maintains generated scenes containing one self-contained application root prefab.
/// </summary>
public static class SceneBuilder
{
    /// <summary>
    /// Ensures a generated scene contains the requested root prefab without changing editor focus.
    /// </summary>
    /// <param name="scenePath">The generated scene asset path.</param>
    /// <param name="prefabPath">The self-contained scene-root prefab path.</param>
    /// <param name="instanceName">The root instance name.</param>
    /// <param name="configureScene">Optional scene-specific configuration applied before saving.</param>
    public static void Build(
        string scenePath,
        string prefabPath,
        string instanceName,
        Action configureScene = null
    )
    {
        string sceneDirectory = Path.GetDirectoryName(scenePath);
        if (string.IsNullOrEmpty(sceneDirectory))
            throw new IOException($"Scene path has no directory: {scenePath}");
        Directory.CreateDirectory(sceneDirectory);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            throw new FileNotFoundException(prefabPath);

        Scene scene = FindOpenScene(scenePath);
        Scene activeScene = SceneManager.GetActiveScene();
        bool reusesBatchScene =
            !scene.IsValid()
            && Application.isBatchMode
            && SceneManager.sceneCount == 1
            && activeScene.IsValid()
            && activeScene.isLoaded
            && string.IsNullOrEmpty(activeScene.path);
        bool openedByBuilder = !scene.IsValid() && !reusesBatchScene;
        if (reusesBatchScene)
            scene = activeScene;
        Object[] selection = Selection.objects;
        try
        {
            if (openedByBuilder)
            {
                scene = File.Exists(scenePath)
                    ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            }

            GameObject namedRoot = FindRoot(scene, instanceName);
            GameObject instance = FindPrefabRoot(scene, prefab) ?? namedRoot;
            if (namedRoot != null && namedRoot != instance)
                Object.DestroyImmediate(namedRoot);
            GameObject source =
                instance == null ? null : PrefabUtility.GetCorrespondingObjectFromSource(instance);
            if (source != prefab)
            {
                if (instance != null)
                    Object.DestroyImmediate(instance);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            }

            instance.name = instanceName;
            ResetRootTransform(instance.transform);
            RemoveOtherRoots(scene, instance);
            SceneManager.SetActiveScene(scene);
            configureScene?.Invoke();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
                throw new IOException($"Could not generate scene: {scenePath}");
        }
        finally
        {
            Selection.objects = selection;
            if (activeScene.IsValid() && activeScene.isLoaded)
                SceneManager.SetActiveScene(activeScene);
            if (openedByBuilder && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Removes stale scene roots because generated scenes are owned entirely by one root prefab.
    /// </summary>
    /// <param name="scene">The generated scene being updated.</param>
    /// <param name="instance">The scene-root prefab instance to retain.</param>
    private static void RemoveOtherRoots(Scene scene, GameObject instance)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root != instance)
                Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Finds an already open scene by project-relative path.
    /// </summary>
    private static Scene FindOpenScene(string scenePath)
    {
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (scene.path == scenePath)
                return scene;
        }
        return default;
    }

    /// <summary>
    /// Finds a named root object in a scene.
    /// </summary>
    private static GameObject FindRoot(Scene scene, string instanceName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == instanceName)
                return root;
        }
        return null;
    }

    /// <summary>
    /// Finds an existing scene-root instance of a generated prefab even when it was renamed.
    /// </summary>
    private static GameObject FindPrefabRoot(Scene scene, GameObject prefab)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (PrefabUtility.GetCorrespondingObjectFromSource(root) == prefab)
                return root;
        }
        return null;
    }

    /// <summary>
    /// Removes residual placement state from a scene-root prefab instance.
    /// </summary>
    private static void ResetRootTransform(Transform transform)
    {
        transform.SetParent(null, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        if (transform is RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
