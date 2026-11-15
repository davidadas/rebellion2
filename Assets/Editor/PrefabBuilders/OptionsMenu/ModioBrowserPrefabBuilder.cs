using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MenuControlPrefabBuilder;
using static MenuPrefabBuilder;

/// <summary>
/// Authors the community browser using the Options menu's established controls.
/// </summary>
public static class ModioBrowserPrefabBuilder
{
    /// <summary>
    /// Adds the browser panel beside the installed-mod controls.
    /// </summary>
    /// <param name="parent">The existing Mods page.</param>
    /// <param name="accent">The Options heading color.</param>
    /// <param name="textDim">The Options secondary text color.</param>
    /// <returns>The authored browser view.</returns>
    internal static ModioBrowserView Build(RectTransform parent, Color accent, Color textDim)
    {
        RectTransform panel = CreateLayer("CommunityModsPanel", parent)
            .GetComponent<RectTransform>();
        FillParent(panel);
        ModioBrowserView view = panel.gameObject.AddComponent<ModioBrowserView>();
        CreateOptionsSectionHeader(panel, "CommunityModsHeader", "Community mods", 16, accent);
        Button back = CreateMenuActionButton(
            panel,
            "BackToInstalledMods",
            "Installed mods",
            250,
            Color.white,
            118
        );
        SetSourceRect((RectTransform)back.transform, 250, 10, 118, 22);
        TMP_InputField search = CreateTextInputField(
            "ModSearch",
            panel,
            string.Empty,
            20,
            46,
            252,
            22
        );
        Button searchButton = CreateMenuActionButton(
            panel,
            "SearchMods",
            "Search",
            280,
            Color.white,
            88
        );
        SetSourceRect((RectTransform)searchButton.transform, 280, 46, 88, 22);
        ScrollAreaView scroll = CreateScrollAreaView(
            panel,
            "CommunityModsScrollArea",
            20,
            80,
            348,
            204,
            0,
            0,
            330,
            204,
            334,
            0,
            12,
            204,
            out RectTransform content
        );
        ModioCatalogRowView row = BuildRow(content, textDim);
        row.gameObject.SetActive(false);
        Button previous = CreateMenuActionButton(
            panel,
            "PreviousModsPage",
            "Previous",
            20,
            Color.white,
            90
        );
        SetSourceRect((RectTransform)previous.transform, 20, 294, 90, 22);
        Button next = CreateMenuActionButton(panel, "NextModsPage", "Next", 278, Color.white, 90);
        SetSourceRect((RectTransform)next.transform, 278, 294, 90, 22);
        TextMeshProUGUI status = CreateTextLabel("CommunityModsStatus", panel);
        status.fontSize = 10;
        status.color = textDim;
        status.textWrappingMode = TextWrappingModes.Normal;
        SetSourceRect(status.rectTransform, 20, 322, 348, 35);
        AssignReference(view, "_searchField", search);
        AssignReference(view, "_searchButton", searchButton);
        AssignReference(view, "_backButton", back);
        AssignReference(view, "_previousButton", previous);
        AssignReference(view, "_nextButton", next);
        AssignReference(view, "_scrollArea", scroll);
        AssignReference(view, "_rowTemplate", row);
        AssignReference(view, "_statusText", status);
        panel.gameObject.SetActive(false);
        return view;
    }

    /// <summary>
    /// Creates a reusable catalog row with bounded text, overflow tooltips, and an install action.
    /// </summary>
    /// <param name="parent">The browser scroll content.</param>
    /// <param name="textDim">The Options secondary text color.</param>
    /// <returns>The authored catalog row.</returns>
    private static ModioCatalogRowView BuildRow(RectTransform parent, Color textDim)
    {
        RectTransform root = CreateLayer("CommunityModRowTemplate", parent)
            .GetComponent<RectTransform>();
        SetSourceRect(root, 0, 0, 330, 44);
        ModioCatalogRowView row = root.gameObject.AddComponent<ModioCatalogRowView>();
        TextMeshProUGUI title = BuildLabel(root, "Name", 0, 236, Color.white, 11);
        TextMeshProUGUI summary = BuildLabel(root, "Summary", 23, 330, textDim, 10);
        Button install = CreateMenuActionButton(
            root,
            "InstallMod",
            "Install",
            244,
            Color.white,
            86
        );
        SetSourceRect((RectTransform)install.transform, 244, 0, 86, 20);
        AssignReference(row, "_nameText", title);
        AssignReference(row, "_summaryText", summary);
        AssignReference(row, "_installButton", install);
        AssignReference(row, "_actionText", install.GetComponentInChildren<TextMeshProUGUI>(true));
        return row;
    }

    /// <summary>
    /// Adds a fixed-size label with the existing full-text hover presentation.
    /// </summary>
    /// <param name="parent">The catalog row.</param>
    /// <param name="name">The authored label name.</param>
    /// <param name="y">The source-space top position.</param>
    /// <param name="width">The source-space label width.</param>
    /// <param name="color">The text color.</param>
    /// <param name="fontSize">The fixed text size.</param>
    /// <returns>The bounded text label.</returns>
    private static TextMeshProUGUI BuildLabel(
        RectTransform parent,
        string name,
        int y,
        int width,
        Color color,
        int fontSize
    )
    {
        RectTransform surface = CreateLayer(name + "Surface", parent).GetComponent<RectTransform>();
        SetSourceRect(surface, 0, y, width, 16);
        Image image = surface.gameObject.AddComponent<Image>();
        image.color = Color.clear;
        TextMeshProUGUI label = CreateTextLabel(name, surface);
        label.fontSize = fontSize;
        label.richText = false;
        label.color = color;
        SetSourceRect(label.rectTransform, 0, 0, width, 16);
        OverflowTextTooltip tooltip = label.gameObject.AddComponent<OverflowTextTooltip>();
        tooltip.Configure(label);
        surface.gameObject.AddComponent<OverflowTextTooltipTrigger>().Configure(tooltip);
        return label;
    }
}
