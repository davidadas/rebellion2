public static class RectTransformTools
{
    [UnityEditor.MenuItem("Tools/UI/Convert To Relative Anchors")]
    private static void ConvertToAnchors()
    {
        if (!(UnityEditor.Selection.activeTransform is UnityEngine.RectTransform rect))
            return;

        UnityEngine.RectTransform parent = rect.parent as UnityEngine.RectTransform;
        if (parent == null)
            return;

        UnityEngine.Vector2 newMin = new UnityEngine.Vector2(
            rect.offsetMin.x / parent.rect.width + rect.anchorMin.x,
            rect.offsetMin.y / parent.rect.height + rect.anchorMin.y
        );

        UnityEngine.Vector2 newMax = new UnityEngine.Vector2(
            rect.offsetMax.x / parent.rect.width + rect.anchorMax.x,
            rect.offsetMax.y / parent.rect.height + rect.anchorMax.y
        );

        rect.anchorMin = newMin;
        rect.anchorMax = newMax;

        rect.offsetMin = UnityEngine.Vector2.zero;
        rect.offsetMax = UnityEngine.Vector2.zero;
    }
}
