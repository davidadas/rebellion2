using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ContentEditorControlBuilder;

/// <summary>
/// Authors local project and connected-team package lists inside the projects overview.
/// </summary>
internal static class ContentEditorProjectsBuilder
{
    /// <summary>
    /// Creates paired project lists using the existing scroll and button controls.
    /// </summary>
    /// <param name="parent">The projects overview's scaled workspace.</param>
    /// <returns>The projects list presentation.</returns>
    internal static ContentEditorProjectsView Build(Transform parent)
    {
        RectTransform root = Layer("ProjectLists", parent);
        Rect(root, 0, 152, 1120, 540);
        ContentEditorProjectsView view = root.gameObject.AddComponent<ContentEditorProjectsView>();
        BuildList(view, root, "Local", "Local projects", 0);
        BuildList(view, root, "Published", "Published packages", 576);
        RectTransform publishedHeader = Layer("PublishedHeader", root);
        Rect(publishedHeader, 576, 0, 544, 36);
        Assign(view, "publishedHeader", publishedHeader);
        RectTransform actions = ActionRow(publishedHeader, 0, 36);
        UICheckboxView showDeprecated = Checkbox(actions);
        showDeprecated.name = "ShowDeprecated";
        Rect((RectTransform)showDeprecated.transform, 0, 0, 192, 24);
        TMP_Text caption = showDeprecated.GetComponentInChildren<TMP_Text>(true);
        caption.text = "Show deprecated";
        caption.fontSize = 14;
        Stretch(caption.rectTransform, 32, 0, 0, 0);
        showDeprecated.SetIsCheckedWithoutNotify(false);
        showDeprecated.gameObject.SetActive(false);
        Assign(view, "showDeprecated", showDeprecated);
        Button refresh = PlainButton("RefreshProjects", actions, "Refresh");
        Rect((RectTransform)refresh.transform, 0, 0, 104, 36);
        Assign(view, "refresh", refresh);
        refresh.gameObject.SetActive(false);
        Button connect = PlainButton("ConnectProjects", actions, "Connect Thunderstore…");
        Rect((RectTransform)connect.transform, 0, 0, 192, 36);
        Assign(view, "connect", connect);
        return view;
    }

    /// <summary>
    /// Authors one column and its reusable metadata row.
    /// </summary>
    /// <param name="view">The paired-list owner.</param>
    /// <param name="parent">The list workspace.</param>
    /// <param name="name">The authored column identity.</param>
    /// <param name="heading">The readable column title.</param>
    /// <param name="left">The column's horizontal position.</param>
    private static void BuildList(
        ContentEditorProjectsView view,
        Transform parent,
        string name,
        string heading,
        int left
    )
    {
        TMP_Text title = Label(name + "Heading", parent, heading, 20);
        Rect(title.rectTransform, left, 0, name == "Published" ? 216 : 336, 36);
        Assign(view, name == "Local" ? "localHeading" : "publishedHeading", title);
        TMP_Text status = Label(
            name + "Status",
            parent,
            name == "Local"
                ? "Projects you create or open appear here."
                : "Connect Thunderstore to see your team's published packages.",
            14,
            true
        );
        Rect(status.rectTransform, left, 44, name == "Local" ? 544 : 336, 40);
        status.textWrappingMode = TextWrappingModes.Normal;
        status.richText = false;
        Assign(view, name == "Local" ? "localStatus" : "publishedStatus", status);
        ScrollAreaLayout scroll = Scroll(name + "Projects", parent, 8, 8);
        Rect((RectTransform)scroll.transform, left, 100, 560, 440);
        Assign(view, name == "Local" ? "localScroll" : "publishedScroll", scroll);
        Image card = Surface(
            name + "ProjectTemplate",
            scroll.ScrollArea.ContentRoot,
            ContentEditorColorRole.Panel
        );
        Rect(card.rectTransform, 0, 0, 544, ContentEditorProjectRowView.Height);
        ContentEditorProjectRowView row =
            card.gameObject.AddComponent<ContentEditorProjectRowView>();
        foreach ((string, int) entry in new[] { ("title", 17), ("summary", 13), ("detail", 12) })
        {
            TMP_Text label = Label(
                entry.Item1,
                card.transform,
                string.Empty,
                entry.Item2,
                entry.Item1 != "title"
            );
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.richText = false;
            Assign(row, entry.Item1, label);
        }
        Assign(row, "primary", Button("OpenProject", card.transform, "Open"));
        Assign(row, "secondary", PlainButton("ProjectAction", card.transform, "Publish…"));
        Assign(view, name == "Local" ? "localTemplate" : "publishedTemplate", row);
        card.gameObject.SetActive(false);
    }
}
