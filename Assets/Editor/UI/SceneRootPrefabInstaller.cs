using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneRootPrefabInstaller
{
    public static void InstallRootPrefabInScene(
        string scenePath,
        string prefabPath,
        string rootName,
        string parentPath
    )
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            throw new FileNotFoundException(prefabPath);

        Transform parent = string.IsNullOrEmpty(parentPath)
            ? null
            : FindRequiredTransform(scene, parentPath);
        GameObject existing = FindChild(scene, parent, rootName);
        int siblingIndex = existing == null ? -1 : existing.transform.GetSiblingIndex();

        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = rootName;

        if (parent != null)
            instance.transform.SetParent(parent, false);

        if (instance.transform is RectTransform rect)
            FillParent(rect);

        if (siblingIndex >= 0)
            instance.transform.SetSiblingIndex(siblingIndex);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static Transform FindRequiredTransform(Scene scene, string path)
    {
        Transform transform = FindTransform(scene, path);
        if (transform == null)
            throw new MissingReferenceException(path + " not found in scene.");

        return transform;
    }

    private static GameObject FindChild(Scene scene, Transform parent, string name)
    {
        if (parent == null)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                    return root;
            }

            return null;
        }

        Transform child = parent.Find(name);
        return child == null ? null : child.gameObject;
    }

    private static Transform FindTransform(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        if (parts.Length == 0)
            return null;

        Transform current = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == parts[0])
            {
                current = root.transform;
                break;
            }
        }

        for (int i = 1; i < parts.Length && current != null; i++)
            current = current.Find(parts[i]);

        return current;
    }

    private static void FillParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }
}
