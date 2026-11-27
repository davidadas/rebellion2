using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static MenuPrefabBuilder;

/// <summary>
/// Generates the shared sortable row prefab for lists with a name and version.
/// </summary>
internal static class SortableRowPrefabBuilder
{
    private const string _prefabPath = "Assets/Prefabs/UI/Common/SortableRow.prefab";

    /// <summary>
    /// Builds the row's single shared hierarchy and binds its controls.
    /// </summary>
    internal static void Rebuild()
    {
        GameObject root = new GameObject(
            "SortableRow",
            typeof(RectTransform),
            typeof(Image),
            typeof(SortableRowView)
        );
        try
        {
            SortableRowView row = root.GetComponent<SortableRowView>();
            row.targetGraphic = root.GetComponent<Image>();
            ColorBlock colors = row.colors;
            colors.disabledColor = colors.normalColor;
            row.colors = colors;
            GameObject gripObject = new GameObject(
                "ReorderGrip",
                typeof(RectTransform),
                typeof(CardReorderGrip)
            );
            gripObject.transform.SetParent(row.transform, false);
            CardReorderGrip grip = gripObject.GetComponent<CardReorderGrip>();
            grip.raycastTarget = false;
            AssignReference(row, "grip", grip);

            TextMeshProUGUI title = CreateCaption(
                row.transform,
                "Name",
                14,
                TextAlignmentOptions.MidlineLeft
            );
            TextMeshProUGUI version = CreateCaption(
                row.transform,
                "Version",
                12,
                TextAlignmentOptions.MidlineRight
            );
            TextMeshProUGUI details = CreateCaption(
                row.transform,
                "Details",
                11,
                TextAlignmentOptions.TopLeft
            );
            details.textWrappingMode = TextWrappingModes.Normal;
            details.gameObject.SetActive(false);
            AssignReference(row, "title", title);
            AssignReference(row, "version", version);
            AssignReference(row, "details", details);

            GameObject trailing = new GameObject("TrailingControl", typeof(RectTransform));
            trailing.transform.SetParent(row.transform, false);
            AssignReference(row, "trailing", trailing.GetComponent<RectTransform>());
            row.Place(0, 0, 320);
            Directory.CreateDirectory(Path.GetDirectoryName(_prefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, _prefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Instantiates the canonical sortable row in a host list.
    /// </summary>
    /// <param name="parent">The list content.</param>
    /// <returns>The prefab instance available for styling and binding.</returns>
    internal static SortableRowView Create(Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        return root.GetComponent<SortableRowView>();
    }

    /// <summary>
    /// Creates a caption with a fixed alignment in the bar's layout.
    /// </summary>
    /// <param name="parent">The sortable row.</param>
    /// <param name="name">The caption's object name.</param>
    /// <param name="size">The font size in source units.</param>
    /// <param name="alignment">The text alignment for this column.</param>
    /// <returns>The configured caption.</returns>
    private static TextMeshProUGUI CreateCaption(
        Transform parent,
        string name,
        int size,
        TextAlignmentOptions alignment
    )
    {
        TextMeshProUGUI caption = CreateTextLabel(name, parent);
        caption.text = string.Empty;
        caption.color = Color.white;
        caption.fontSize = size;
        caption.alignment = alignment;
        caption.overflowMode = TextOverflowModes.Ellipsis;
        caption.richText = false;
        return caption;
    }
}
