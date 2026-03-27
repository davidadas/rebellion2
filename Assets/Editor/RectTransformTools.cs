using UnityEditor;
using UnityEngine;

public static class RectTransformTools
{
    [MenuItem("Tools/UI/Convert To Relative Anchors")]
    private static void ConvertToAnchors()
    {
        if (!(Selection.activeTransform is RectTransform rect))
            return;

        RectTransform parent = rect.parent as RectTransform;
        if (parent == null)
            return;

        Vector2 newMin = new Vector2(
            rect.offsetMin.x / parent.rect.width + rect.anchorMin.x,
            rect.offsetMin.y / parent.rect.height + rect.anchorMin.y
        );

        Vector2 newMax = new Vector2(
            rect.offsetMax.x / parent.rect.width + rect.anchorMax.x,
            rect.offsetMax.y / parent.rect.height + rect.anchorMax.y
        );

        rect.anchorMin = newMin;
        rect.anchorMax = newMax;

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
