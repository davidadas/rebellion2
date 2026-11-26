using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ContentEditorControlBuilder;

/// <summary>
/// Authors dependency selection using named package rows and published-version dropdowns.
/// </summary>
internal static class ContentEditorDependenciesBuilder
{
    /// <summary>
    /// Creates the release page's dependency list without a raw manifest editor.
    /// </summary>
    /// <param name="parent">The release stage.</param>
    /// <returns>The authored dependency presentation.</returns>
    internal static ContentEditorDependenciesView Build(Transform parent)
    {
        RectTransform root = Layer("Dependencies", parent);
        Rect(root, 0, 100, 650, 164);
        ContentEditorDependenciesView view =
            root.gameObject.AddComponent<ContentEditorDependenciesView>();
        Rect(Label("Heading", root, "Dependencies (optional)", 15).rectTransform, 0, 6, 440, 28);
        Button choose = Button("ChooseDependencies", root, "Choose packages…");
        Rect((RectTransform)choose.transform, 470, 0, 180, 34);
        Assign(view, "choose", choose);
        ScrollAreaLayout scroll = Scroll("SelectedDependencies", root);
        Rect((RectTransform)scroll.transform, 0, 42, 650, 116);
        Assign(view, "scroll", scroll);
        TMP_Text empty = Label(
            "Empty",
            root,
            "No dependencies. Add packages if your pack needs them.",
            14,
            true
        );
        Rect(empty.rectTransform, 12, 52, 600, 28);
        Assign(view, "empty", empty);
        Image row = Surface(
            "DependencyTemplate",
            scroll.ScrollArea.ContentRoot,
            ContentEditorColorRole.Input
        );
        Rect(row.rectTransform, 0, 0, 626, 52);
        ContentEditorDependencyRowView item =
            row.gameObject.AddComponent<ContentEditorDependencyRowView>();
        TMP_Text name = Label("PackageName", row.transform, string.Empty, 15);
        name.overflowMode = TextOverflowModes.Ellipsis;
        Assign(item, "packageName", name);
        TMP_Text author = Label("Author", row.transform, string.Empty, 13, true);
        author.overflowMode = TextOverflowModes.Ellipsis;
        Assign(item, "author", author);
        TMP_Dropdown version = Dropdown("DependencyVersion", row.transform);
        version.template.anchoredPosition = Vector2.zero;
        Assign(item, "version", version);
        Button remove = PlainButton("RemoveDependency", row.transform, "Remove");
        Assign(item, "remove", remove);
        Assign(view, "rowTemplate", item);
        row.gameObject.SetActive(false);
        return view;
    }
}
