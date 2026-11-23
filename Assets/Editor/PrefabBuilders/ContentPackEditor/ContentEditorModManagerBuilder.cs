using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ContentEditorControlBuilder;

/// <summary>
/// Authors the toolkit's community browser and installed-package manager with shared controls.
/// </summary>
internal static class ContentEditorModManagerBuilder
{
    /// <summary>
    /// Creates the manager independently of the currently open project.
    /// </summary>
    /// <param name="parent">The toolkit canvas.</param>
    /// <returns>The authored manager screen.</returns>
    internal static ContentEditorModManagerView Build(Transform parent)
    {
        Image overlay = Surface("ModManager", parent, new Color(0, 0, 0, 0.7f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;
        ContentEditorModManagerView view =
            overlay.gameObject.AddComponent<ContentEditorModManagerView>();
        Image panel = Surface("Panel", overlay.transform, ContentEditorColorRole.Panel);
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1080, 740);
        Rect(Label("Title", rect, "Mod manager", 26).rectTransform, 28, 18, 690, 42);
        Rect(
            Label(
                "Subtitle",
                rect,
                "Discover community content on Thunderstore",
                15,
                true
            ).rectTransform,
            28,
            62,
            800,
            24
        );
        ActionButton(view, rect, "browseButton", "Browse", 28, 102, 116, true);
        ActionButton(view, rect, "installedButton", "Installed", 154, 102, 116, true);
        ActionButton(view, rect, "refreshButton", "Refresh", 810, 102, 110);
        ActionButton(view, rect, "importButton", "Import ZIP…", 930, 102, 122);
        TMP_InputField search = Input("SearchPackages", rect, "Search packages or authors");
        Rect((RectTransform)search.transform, 28, 152, 400, 38);
        Assign(view, "search", search);
        ScrollAreaLayout packages = Scroll("Packages", rect);
        Rect((RectTransform)packages.transform, 28, 206, 400, 414);
        Assign(view, "packages", packages);
        Button row = Button("PackageRow", packages.ScrollArea.ContentRoot, string.Empty);
        TextMeshProUGUI rowLabel = row.GetComponentInChildren<TextMeshProUGUI>(true);
        rowLabel.alignment = TextAlignmentOptions.MidlineLeft;
        rowLabel.textWrappingMode = TextWrappingModes.NoWrap;
        Stretch(rowLabel.rectTransform, 14, 8, 14, 8);
        rowLabel.lineSpacing = 10;
        row.gameObject.SetActive(false);
        Assign(view, "rowTemplate", row);
        Image divider = Surface("Divider", rect, ContentEditorColorRole.Divider);
        Rect(divider.rectTransform, 448, 152, 1, 468);
        RawImage icon = Layer("PackageIcon", rect).gameObject.AddComponent<RawImage>();
        Rect(icon.rectTransform, 476, 152, 88, 88);
        icon.raycastTarget = false;
        Assign(view, "icon", icon);
        TextMeshProUGUI heading = Label("PackageName", rect, "Community mods", 22);
        Rect(heading.rectTransform, 584, 152, 468, 40);
        Assign(view, "heading", heading);
        TMP_Dropdown versions = Dropdown("Release", rect);
        Rect((RectTransform)versions.transform, 584, 204, 272, 36);
        Assign(view, "versions", versions);
        ScrollAreaLayout details = Scroll("PackageDetails", rect);
        Rect((RectTransform)details.transform, 476, 260, 576, 250);
        TextMeshProUGUI text = Label(
            "Description",
            details.ScrollArea.ContentRoot,
            string.Empty,
            16
        );
        Rect(text.rectTransform, 0, 0, 496, 250);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.TopLeft;
        Assign(view, "details", details);
        Assign(view, "detailText", text);
        ActionButton(view, rect, "installButton", "Install", 476, 532, 182);
        ActionButton(view, rect, "activateButton", "Use content pack", 670, 532, 182);
        ActionButton(view, rect, "uninstallButton", "Uninstall", 864, 532, 188);
        ActionButton(view, rect, "listingButton", "View on Thunderstore", 476, 584, 266, true);
        ActionButton(view, rect, "folderButton", "Open folder", 758, 584, 150, true);
        TextMeshProUGUI status = Label("ManagerStatus", rect, string.Empty, 14, true);
        Rect(status.rectTransform, 28, 642, 1024, 44);
        status.textWrappingMode = TextWrappingModes.Normal;
        status.alignment = TextAlignmentOptions.TopLeft;
        Assign(view, "status", status);
        ActionButton(view, rect, "closeButton", "Close", 930, 690, 122);
        overlay.gameObject.SetActive(false);
        return view;
    }

    /// <summary>
    /// Authors and binds one manager action using the existing button style.
    /// </summary>
    /// <param name="view">The owning screen.</param>
    /// <param name="parent">The panel transform.</param>
    /// <param name="field">The serialized control field.</param>
    /// <param name="label">The visible action.</param>
    /// <param name="left">The horizontal position.</param>
    /// <param name="top">The vertical position.</param>
    /// <param name="width">The control width.</param>
    /// <param name="plain">Whether this is a text action.</param>
    private static void ActionButton(
        ContentEditorModManagerView view,
        Transform parent,
        string field,
        string label,
        int left,
        int top,
        int width,
        bool plain = false
    )
    {
        Button button = plain ? PlainButton(field, parent, label) : Button(field, parent, label);
        Rect((RectTransform)button.transform, left, top, width, 36);
        Assign(view, field, button);
    }
}
