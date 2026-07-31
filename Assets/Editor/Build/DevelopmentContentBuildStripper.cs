using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Removes editor-preview references to Assets/Content from the temporary scene copies Unity
/// serializes into a player. Runtime controllers replace these previews from installation Content.
/// </summary>
public sealed class DevelopmentContentBuildStripper : IProcessSceneWithReport
{
    private const string _developmentContentRoot = "Assets/Content/";

    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null)
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                StripPreviewReferences(component);
        }
    }

    private static void StripPreviewReferences(Component component)
    {
        if (component == null)
            return;

        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        bool changed = false;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.propertyType != SerializedPropertyType.ObjectReference)
                continue;

            Object value = property.objectReferenceValue;
            if (value == null)
                continue;

            string assetPath = AssetDatabase.GetAssetPath(value);
            if (!assetPath.StartsWith(_developmentContentRoot, System.StringComparison.Ordinal))
                continue;

            property.objectReferenceValue = null;
            changed = true;
        }

        if (changed)
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
