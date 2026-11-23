using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ContentEditorControlBuilder;

/// <summary>
/// Authors artwork-led community browsing using the toolkit's shared controls and palettes.
/// </summary>
internal static class ContentEditorModManagerBuilder
{
    /// <summary>
    /// Creates navigation, discovery controls, package cards, and the selected-package panel.
    /// </summary>
    /// <param name="parent">The toolkit canvas.</param>
    /// <returns>The authored manager screen.</returns>
    internal static ContentEditorModManagerView Build(Transform parent)
    {
        Image overlay = Surface("ModManager", parent, new Color(0, 0, 0, 0.8f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;
        ContentEditorModManagerView view =
            overlay.gameObject.AddComponent<ContentEditorModManagerView>();
        Image panel = Surface("Panel", overlay.transform, ContentEditorColorRole.Background);
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1460, 800);
        Assign(view, "panel", rect);
        Image sidebar = Surface("Navigation", rect, ContentEditorColorRole.Panel);
        Rect(sidebar.rectTransform, 0, 0, 220, 800);
        Image brand = Surface("BrandAccent", rect, ContentEditorColorRole.Accent);
        Rect(brand.rectTransform, 24, 32, 40, 4);
        Caption(rect, "Brand", "Rebellion 2", 24, 48, 176, 24, 14, ContentEditorColorRole.Accent);
        Caption(rect, "Title", "Mod manager", 24, 76, 176, 36, 24);
        ActionButton(view, rect, "browseButton", "Discover", 24, 146, 176, true);
        ActionButton(view, rect, "installedButton", "Installed", 24, 198, 176, true);
        foreach (string name in new[] { "browseMarker", "installedMarker" })
        {
            Image marker = Surface(name, rect, ContentEditorColorRole.Accent);
            Rect(marker.rectTransform, 0, name == "browseMarker" ? 146 : 198, 4, 36);
            Assign(view, name, marker);
        }
        Caption(
            rect,
            "CategoryLabel",
            "Category",
            24,
            276,
            176,
            22,
            13,
            ContentEditorColorRole.Muted
        );
        Selector(view, rect, "category", 24, 304, 176, new[] { "All categories", "Uncategorized" });
        Caption(
            rect,
            "InstallationLabel",
            "Installation",
            24,
            362,
            176,
            22,
            13,
            ContentEditorColorRole.Muted
        );
        Selector(
            view,
            rect,
            "installation",
            24,
            390,
            176,
            new[]
            {
                "All packages",
                "Not installed",
                "Installed",
                "Updates available",
                "Local content",
                "Deprecated",
            }
        );
        ActionButton(view, rect, "resetButton", "Clear filters", 24, 446, 176, true);
        TextMeshProUGUI notice = Caption(
            rect,
            "ContentNotice",
            "Close the game before changing mods. Selections apply when the game next loads content.",
            24,
            552,
            176,
            80,
            13,
            ContentEditorColorRole.Muted
        );
        notice.textWrappingMode = TextWrappingModes.Normal;
        notice.alignment = TextAlignmentOptions.TopLeft;
        Caption(
            rect,
            "Source",
            "Powered by Thunderstore",
            24,
            652,
            176,
            22,
            12,
            ContentEditorColorRole.Muted
        );
        ActionButton(view, rect, "importButton", "Import ZIP…", 24, 688, 176);
        ActionButton(view, rect, "closeButton", "Close", 24, 744, 176, true);

        TextMeshProUGUI title = Caption(
            rect,
            "DiscoverHeading",
            "Discover community mods",
            248,
            28,
            620,
            44,
            30
        );
        title.fontStyle = FontStyles.Bold;
        Assign(view, "pageTitle", title);
        TextMeshProUGUI subtitle = Caption(
            rect,
            "Subtitle",
            "Browse the Rebellion 2 community on Thunderstore.",
            248,
            76,
            718,
            24,
            15,
            ContentEditorColorRole.Muted
        );
        Assign(view, "pageSubtitle", subtitle);
        ActionButton(view, rect, "refreshButton", "Refresh", 870, 32, 96, true);
        TMP_InputField search = Input("SearchPackages", rect, "Search mods, creators, or keywords");
        Rect((RectTransform)search.transform, 248, 122, 640, 44);
        search.textComponent.fontSize = 17;
        ((TMP_Text)search.placeholder).fontSize = 17;
        Assign(view, "search", search);
        ActionButton(view, rect, "clearSearchButton", "Clear", 900, 126, 66, true);
        Assign(
            view,
            "resultCount",
            Caption(
                rect,
                "ResultCount",
                string.Empty,
                248,
                188,
                390,
                24,
                14,
                ContentEditorColorRole.Muted
            )
        );
        Selector(
            view,
            rect,
            "sorting",
            710,
            182,
            256,
            new[] { "Best match", "Most downloaded", "Recently updated", "Newest", "Name (A–Z)" }
        );
        ScrollAreaLayout packages = Scroll("Packages", rect);
        Rect((RectTransform)packages.transform, 248, 230, 718, 486);
        Assign(view, "packages", packages);
        Assign(view, "rowTemplate", BuildCard(packages.ScrollArea.ContentRoot));
        TextMeshProUGUI empty = Caption(
            rect,
            "EmptyResults",
            string.Empty,
            286,
            340,
            640,
            180,
            20,
            ContentEditorColorRole.Muted
        );
        empty.alignment = TextAlignmentOptions.Center;
        empty.textWrappingMode = TextWrappingModes.Normal;
        empty.gameObject.SetActive(false);
        Assign(view, "emptyResults", empty);
        TextMeshProUGUI status = Caption(
            rect,
            "ManagerStatus",
            string.Empty,
            248,
            750,
            718,
            38,
            14,
            ContentEditorColorRole.Muted
        );
        status.alignment = TextAlignmentOptions.TopLeft;
        status.textWrappingMode = TextWrappingModes.Normal;
        Assign(view, "status", status);

        Image detailPanel = Surface("DetailPanel", rect, ContentEditorColorRole.Panel);
        Rect(detailPanel.rectTransform, 998, 0, 462, 800);
        Caption(
            rect,
            "DetailsHeading",
            "Package details",
            1030,
            22,
            398,
            22,
            13,
            ContentEditorColorRole.Muted
        );
        RectTransform detailContent = Layer("DetailContent", rect);
        Stretch(detailContent);
        Assign(view, "detailContent", detailContent.gameObject);
        TextMeshProUGUI emptyDetail = Caption(
            rect,
            "EmptyDetails",
            "Choose a package\n\nDetails and available versions appear here.",
            1030,
            300,
            398,
            150,
            18,
            ContentEditorColorRole.Muted
        );
        emptyDetail.textWrappingMode = TextWrappingModes.Normal;
        emptyDetail.alignment = TextAlignmentOptions.Center;
        Assign(view, "emptyDetails", emptyDetail);
        Image cover = Surface("Cover", detailContent, ContentEditorColorRole.Background);
        Rect(cover.rectTransform, 1030, 64, 128, 128);
        TextMeshProUGUI initials = Caption(
            cover.transform,
            "IconInitials",
            string.Empty,
            0,
            0,
            128,
            128,
            40,
            ContentEditorColorRole.Accent
        );
        initials.alignment = TextAlignmentOptions.Center;
        Assign(view, "iconInitials", initials);
        RawImage icon = Layer("PackageIcon", cover.transform).gameObject.AddComponent<RawImage>();
        Stretch(icon.rectTransform);
        icon.raycastTarget = false;
        icon.enabled = false;
        Assign(view, "icon", icon);
        Assign(
            view,
            "popularity",
            Caption(
                detailContent,
                "DownloadCount",
                string.Empty,
                1182,
                70,
                246,
                48,
                38,
                ContentEditorColorRole.Accent
            )
        );
        Caption(
            detailContent,
            "DownloadsLabel",
            "downloads",
            1182,
            122,
            246,
            22,
            14,
            ContentEditorColorRole.Muted
        );
        Assign(
            view,
            "packageState",
            Caption(rect, "PackageState", string.Empty, 1182, 160, 246, 24, 15)
        );
        TextMeshProUGUI heading = Caption(
            detailContent,
            "PackageName",
            string.Empty,
            1030,
            220,
            398,
            40,
            27
        );
        heading.fontStyle = FontStyles.Bold;
        Assign(view, "heading", heading);
        Assign(
            view,
            "creator",
            Caption(
                detailContent,
                "Creator",
                string.Empty,
                1030,
                268,
                398,
                24,
                15,
                ContentEditorColorRole.Accent
            )
        );
        Assign(
            view,
            "updated",
            Caption(
                detailContent,
                "Updated",
                string.Empty,
                1030,
                298,
                398,
                22,
                13,
                ContentEditorColorRole.Muted
            )
        );
        Assign(
            view,
            "categories",
            Caption(
                detailContent,
                "Categories",
                string.Empty,
                1030,
                328,
                398,
                24,
                13,
                ContentEditorColorRole.Muted
            )
        );
        ScrollAreaLayout details = Scroll("PackageDetails", detailContent);
        Rect((RectTransform)details.transform, 1030, 368, 398, 188);
        TextMeshProUGUI description = Caption(
            details.ScrollArea.ContentRoot,
            "Description",
            string.Empty,
            0,
            0,
            374,
            188,
            16
        );
        description.alignment = TextAlignmentOptions.TopLeft;
        description.textWrappingMode = TextWrappingModes.Normal;
        description.overflowMode = TextOverflowModes.Overflow;
        Assign(view, "details", details);
        Assign(view, "detailText", description);
        Caption(
            detailContent,
            "ReleaseLabel",
            "Version",
            1030,
            574,
            398,
            20,
            13,
            ContentEditorColorRole.Muted
        );
        Selector(view, detailContent, "versions", 1030, 600, 398, new[] { "Select a release" });
        Button install = ActionButton(
            view,
            detailContent,
            "installButton",
            "Install package",
            1030,
            656,
            398
        );
        ((RectTransform)install.transform).sizeDelta = new Vector2(398, 44);
        BindColor(install.GetComponent<Image>(), ContentEditorColorRole.Accent, install);
        BindColor(install.GetComponentInChildren<TMP_Text>(), ContentEditorColorRole.Background);
        ActionButton(view, detailContent, "activateButton", "Use content pack", 1030, 712, 192);
        ActionButton(view, detailContent, "uninstallButton", "Uninstall", 1236, 712, 192);
        ActionButton(
            view,
            detailContent,
            "listingButton",
            "View on Thunderstore",
            1030,
            760,
            224,
            true
        );
        ActionButton(view, detailContent, "folderButton", "Open folder", 1278, 760, 150, true);
        overlay.gameObject.SetActive(false);
        return view;
    }

    /// <summary>
    /// Authors an artwork-led card whose selection and content can be rebound without reconstruction.
    /// </summary>
    /// <param name="parent">The shared scroll content root.</param>
    /// <returns>The inactive card template.</returns>
    private static ContentEditorModCardView BuildCard(Transform parent)
    {
        Button button = Button("PackageCard", parent, string.Empty);
        Object.DestroyImmediate(button.GetComponentInChildren<TMP_Text>().gameObject);
        ContentEditorModCardView card = button.gameObject.AddComponent<ContentEditorModCardView>();
        Assign(card, "button", button);
        Image stripe = Surface("Selected", button.transform, ContentEditorColorRole.Accent);
        Rect(stripe.rectTransform, 0, 0, 4, 156);
        Assign(card, "selectionStripe", stripe);
        Image cover = Surface("Cover", button.transform, ContentEditorColorRole.Background);
        Rect(cover.rectTransform, 16, 16, 96, 96);
        TextMeshProUGUI initials = Caption(
            cover.transform,
            "Initials",
            string.Empty,
            0,
            0,
            96,
            96,
            32,
            ContentEditorColorRole.Accent
        );
        initials.alignment = TextAlignmentOptions.Center;
        Assign(card, "initials", initials);
        RawImage image = Layer("Artwork", cover.transform).gameObject.AddComponent<RawImage>();
        Stretch(image.rectTransform);
        image.raycastTarget = false;
        image.enabled = false;
        Assign(card, "artwork", image);
        TextMeshProUGUI name = Caption(
            button.transform,
            "Name",
            string.Empty,
            132,
            12,
            382,
            30,
            21
        );
        name.fontStyle = FontStyles.Bold;
        Assign(card, "caption", name);
        Assign(
            card,
            "author",
            Caption(
                button.transform,
                "Author",
                string.Empty,
                132,
                44,
                530,
                20,
                13,
                ContentEditorColorRole.Muted
            )
        );
        TextMeshProUGUI description = Caption(
            button.transform,
            "Summary",
            string.Empty,
            132,
            72,
            530,
            44,
            15
        );
        description.textWrappingMode = TextWrappingModes.Normal;
        description.maxVisibleLines = 2;
        Assign(card, "description", description);
        Assign(
            card,
            "metadata",
            Caption(
                button.transform,
                "Metadata",
                string.Empty,
                16,
                126,
                646,
                20,
                12,
                ContentEditorColorRole.Muted
            )
        );
        TextMeshProUGUI state = Caption(
            button.transform,
            "State",
            string.Empty,
            522,
            17,
            140,
            22,
            12,
            ContentEditorColorRole.Muted
        );
        state.alignment = TextAlignmentOptions.MidlineRight;
        Assign(card, "state", state);
        button.gameObject.SetActive(false);
        return card;
    }

    /// <summary>
    /// Creates a palette-aware label at an explicit source position.
    /// </summary>
    /// <param name="parent">The owning panel.</param>
    /// <param name="name">The authored control name.</param>
    /// <param name="text">The visible caption.</param>
    /// <param name="left">The horizontal position.</param>
    /// <param name="top">The vertical position.</param>
    /// <param name="width">The available width.</param>
    /// <param name="height">The available height.</param>
    /// <param name="size">The font size.</param>
    /// <param name="role">The caption's palette role.</param>
    /// <returns>The authored caption.</returns>
    private static TextMeshProUGUI Caption(
        Transform parent,
        string name,
        string text,
        int left,
        int top,
        int width,
        int height,
        int size,
        ContentEditorColorRole role = ContentEditorColorRole.Text
    )
    {
        TextMeshProUGUI label = Label(name, parent, text, size);
        label.GetComponent<ContentEditorColorBinding>().SetRole(role);
        Rect(label.rectTransform, left, top, width, height);
        return label;
    }

    /// <summary>
    /// Authors a shared dropdown with explicit options and a visible current value.
    /// </summary>
    /// <param name="view">The owning manager.</param>
    /// <param name="parent">The owning panel.</param>
    /// <param name="field">The serialized control field.</param>
    /// <param name="left">The horizontal position.</param>
    /// <param name="top">The vertical position.</param>
    /// <param name="width">The control width.</param>
    /// <param name="options">The visible choices in enum order.</param>
    private static void Selector(
        ContentEditorModManagerView view,
        Transform parent,
        string field,
        int left,
        int top,
        int width,
        string[] options
    )
    {
        TMP_Dropdown dropdown = Dropdown(field, parent);
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(options));
        Rect((RectTransform)dropdown.transform, left, top, width, 36);
        Assign(view, field, dropdown);
    }

    /// <summary>
    /// Authors and binds one manager action using the shared button style.
    /// </summary>
    /// <param name="view">The owning screen.</param>
    /// <param name="parent">The panel transform.</param>
    /// <param name="field">The serialized control field.</param>
    /// <param name="label">The visible action.</param>
    /// <param name="left">The horizontal position.</param>
    /// <param name="top">The vertical position.</param>
    /// <param name="width">The control width.</param>
    /// <param name="plain">Whether this is a text action.</param>
    /// <returns>The authored action button.</returns>
    private static Button ActionButton(
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
        if (plain && left < 220)
            button.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
        Assign(view, field, button);
        return button;
    }
}
