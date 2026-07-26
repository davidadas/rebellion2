using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using InvalidOperationException = System.InvalidOperationException;

/// <summary>
/// Installs an authored root prefab into a Unity scene while preserving its prior sibling order.
/// </summary>
public static class SceneRootPrefabInstaller
{
    /// <summary>
    /// Replaces one named scene object with an instance of the requested root prefab.
    /// </summary>
    /// <param name="scenePath">The project-relative scene asset path.</param>
    /// <param name="prefabPath">The project-relative prefab asset path.</param>
    /// <param name="rootName">The scene name assigned to the installed prefab instance.</param>
    /// <param name="parentPath">The optional slash-delimited scene parent path.</param>
    public static void InstallRootPrefabInScene(
        string scenePath,
        string prefabPath,
        string rootName,
        string parentPath
    )
    {
        UIAuthoringGuard.EnsureEditMode();
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

    /// <summary>
    /// Resolves a required transform by scene path.
    /// </summary>
    /// <param name="scene">The scene to search.</param>
    /// <param name="path">The slash-delimited transform path.</param>
    /// <returns>The resolved transform.</returns>
    private static Transform FindRequiredTransform(Scene scene, string path)
    {
        Transform transform = FindTransform(scene, path);
        if (transform == null)
            throw new MissingReferenceException(path + " not found in scene.");

        return transform;
    }

    /// <summary>
    /// Finds one direct child under a transform or among the scene roots.
    /// </summary>
    /// <param name="scene">The scene to search.</param>
    /// <param name="parent">The optional parent transform.</param>
    /// <param name="name">The required direct-child name.</param>
    /// <returns>The matching object, or null when none exists.</returns>
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

    /// <summary>
    /// Resolves a slash-delimited transform path from the scene roots.
    /// </summary>
    /// <param name="scene">The scene to search.</param>
    /// <param name="path">The slash-delimited transform path.</param>
    /// <returns>The matching transform, or null when the path is absent.</returns>
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

    /// <summary>
    /// Stretches an installed root across its authored parent without residual transform state.
    /// </summary>
    /// <param name="rect">The installed root transform.</param>
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

/// <summary>
/// Protects UI prefab and scene authoring commands from running against live Play mode objects.
/// </summary>
public static class UIAuthoringGuard
{
    /// <summary>
    /// Verifies that an authoring command is running in Edit mode before it mutates assets.
    /// </summary>
    public static void EnsureEditMode()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        throw new InvalidOperationException(
            "UI authoring commands are unavailable while the editor is in Play mode."
        );
    }
}
