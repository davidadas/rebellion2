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
        Image background = Surface("ModManager", parent, ContentEditorColorRole.Background);
        Stretch(background.rectTransform, 0, 76, 0, 40);
        background.raycastTarget = true;
        ContentEditorModManagerView view =
            background.gameObject.AddComponent<ContentEditorModManagerView>();
        RectTransform rect = Layer("Workspace", background.transform);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1120, 816);
        Assign(view, "panel", rect);
        TextMeshProUGUI title = Caption(rect, "Title", "Library", 0, 32, 640, 48, 36);
        title.fontStyle = FontStyles.Bold;
        Assign(view, "pageTitle", title);
        RectTransform actions = ActionRow(rect, 32, 48);
        ActionButton(view, actions, "refreshButton", "Refresh", 0, 0, 128, true);
        StyleSecondary(
            ActionButton(view, actions, "importButton", "Install from file…", 0, 0, 208)
        );
        Button cancel = ActionButton(
            view,
            actions,
            "closeButton",
            "Cancel operation",
            0,
            0,
            208,
            true
        );
        cancel.gameObject.SetActive(false);
        RectTransform defaults = Layer("DefaultPack", rect);
        Rect(defaults, 0, 112, 1120, 72);
        RectTransform description = Layer("Description", defaults);
        Rect(description, 0, 0, 600, 72);
        Assign(view, "defaultPackDescription", description);
        Caption(description, "DefaultLabel", "Default content pack", 0, 0, 600, 28, 18);
        Caption(
            description,
            "DefaultHelp",
            "Used when starting the game.",
            0,
            32,
            600,
            24,
            14,
            ContentEditorColorRole.Muted
        );
        foreach (TMP_Text label in description.GetComponentsInChildren<TMP_Text>())
        {
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMax = new Vector2(0, label.rectTransform.offsetMax.y);
        }
        Selector(view, defaults, "activePack", 680, 0, 440, new[] { "No content packs installed" });
        Assign(view, "defaultPackControls", defaults.gameObject);
        TMP_InputField search = Input(
            "SearchPackages",
            rect,
            "Search mods by name, creator, or description"
        );
        Rect((RectTransform)search.transform, 0, 112, 672, 44);
        Vector2 inset = search.textViewport.offsetMax;
        inset.x = -44;
        search.textViewport.offsetMax = inset;
        search.textComponent.fontSize = 16;
        ((TMP_Text)search.placeholder).fontSize = 16;
        Assign(view, "search", search);
        ActionButton(view, rect, "clearSearchButton", "×", 624, 112, 44, true);
        StyleSecondary(ActionButton(view, rect, "filterButton", "Filters", 688, 112, 112));
        Selector(
            view,
            rect,
            "sorting",
            816,
            112,
            304,
            new[]
            {
                "Best match",
                "Most downloaded",
                "Recently updated",
                "Newest",
                "Name (A–Z)",
                "Most liked",
            }
        );
        Assign(
            view,
            "resultCount",
            Caption(
                rect,
                "ResultCount",
                string.Empty,
                0,
                280,
                1120,
                24,
                13,
                ContentEditorColorRole.Muted
            )
        );
        ScrollAreaLayout packages = Scroll("Packages", rect, 8, 8);
        Stretch((RectTransform)packages.transform, 0, 248, -16, 0);
        Assign(view, "packages", packages);
        Assign(view, "rowTemplate", BuildCard(packages.ScrollArea.ContentRoot));
        TextMeshProUGUI empty = Caption(
            rect,
            "EmptyResults",
            string.Empty,
            0,
            384,
            1120,
            160,
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
            0,
            752,
            1120,
            48,
            14,
            ContentEditorColorRole.Muted
        );
        status.textWrappingMode = TextWrappingModes.Normal;
        status.rectTransform.anchorMin = Vector2.zero;
        status.rectTransform.anchorMax = Vector2.right;
        status.rectTransform.pivot = Vector2.zero;
        status.rectTransform.offsetMin = Vector2.zero;
        status.rectTransform.offsetMax = new Vector2(0, 48);
        status.gameObject.SetActive(false);
        Assign(view, "status", status);
        BuildFilters(rect, view);
        BuildDetails(background.transform, view);
        background.gameObject.SetActive(false);
        return view;
    }

    /// <summary>
    /// Expands filter choices in the page without covering the result list.
    /// </summary>
    /// <param name="parent">The browse workspace.</param>
    /// <param name="view">The owning manager.</param>
    private static void BuildFilters(Transform parent, ContentEditorModManagerView view)
    {
        Image strip = Surface("FilterPanel", parent, ContentEditorColorRole.Panel);
        Rect(strip.rectTransform, 0, 176, 1120, 176);
        Assign(view, "filterPanel", strip.gameObject);
        ContentEditorModFiltersView filters =
            strip.gameObject.AddComponent<ContentEditorModFiltersView>();
        Assign(view, "filters", filters);
        List<TMP_Text> headings = new();
        foreach (
            (string, int, int) column in new[]
            {
                ("Section", 16, 240),
                ("Installation", 272, 240),
                ("Include categories", 528, 280),
                ("Exclude categories", 824, 280),
            }
        )
            headings.Add(
                Caption(
                    strip.transform,
                    column.Item1 + "Label",
                    column.Item1,
                    column.Item2,
                    16,
                    column.Item3,
                    20,
                    13,
                    ContentEditorColorRole.Muted
                )
            );
        Assign(filters, "headings", headings.ToArray());
        Selector(filters, strip.transform, "section", 16, 44, 240, new[] { "All sections" });
        Selector(
            filters,
            strip.transform,
            "installation",
            272,
            44,
            240,
            new[] { "All mods", "Not installed", "Installed", "Updates available", "Local content" }
        );
        Assign(
            filters,
            "includeCategories",
            CategoryPicker(strip.transform, "IncludeCategories", "Any category", 528)
        );
        Assign(
            filters,
            "excludeCategories",
            CategoryPicker(strip.transform, "ExcludeCategories", "No exclusions", 824)
        );
        TMP_Text help = Caption(
            strip.transform,
            "CategoryHelp",
            "Match any included category; exclusions take precedence.",
            528,
            100,
            576,
            20,
            12,
            ContentEditorColorRole.Muted
        );
        help.textWrappingMode = TextWrappingModes.NoWrap;
        Assign(filters, "categoryHelp", help);
        foreach (
            (string, string, int) toggle in new[]
            {
                ("showDeprecated", "Show deprecated", 16),
                ("showNsfw", "Show NSFW", 272),
            }
        )
        {
            UICheckboxView checkbox = Checkbox(strip.transform);
            checkbox.name = toggle.Item1;
            Rect((RectTransform)checkbox.transform, toggle.Item3, 128, 224, 24);
            TMP_Text label = checkbox.GetComponentInChildren<TMP_Text>(true);
            label.text = toggle.Item2;
            label.fontSize = 14;
            Rect(label.rectTransform, 32, 0, 192, 24);
            checkbox.SetIsCheckedWithoutNotify(false);
            Assign(filters, toggle.Item1, checkbox);
        }
        Button clear = ActionButton(
            view,
            strip.transform,
            "resetButton",
            "Clear filters",
            928,
            118,
            176,
            true
        );
        UILayout.SetTopRightRect((RectTransform)clear.transform, 16, 118, 176, 44);
        TMP_Text clearLabel = clear.GetComponentInChildren<TMP_Text>();
        clearLabel.alignment = TextAlignmentOptions.MidlineRight;
        Stretch(clearLabel.rectTransform);
        strip.gameObject.SetActive(false);
    }

    /// <summary>
    /// Authors an attached multi-select dropdown using compact checkbox rows.
    /// </summary>
    /// <param name="parent">The filter strip.</param>
    /// <param name="name">The control identity.</param>
    /// <param name="caption">The empty-selection label.</param>
    /// <param name="left">The field's left position.</param>
    /// <returns>The checkbox dropdown.</returns>
    private static ContentEditorMultiSelectDropdown CategoryPicker(
        Transform parent,
        string name,
        string caption,
        int left
    )
    {
        Button picker = Button(name, parent, caption);
        Rect((RectTransform)picker.transform, left, 44, 280, 44);
        Outline outline = picker.gameObject.AddComponent<Outline>();
        outline.effectDistance = new Vector2(1, -1);
        outline.useGraphicAlpha = false;
        BindColor(picker.targetGraphic, ContentEditorColorRole.Input, picker);
        TMP_Text label = picker.GetComponentInChildren<TMP_Text>();
        BindColor(label, ContentEditorColorRole.Text);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        Stretch(label.rectTransform, 12, 0, 40, 0);
        AddDropdownArrow(picker.transform);
        ContentEditorMultiSelectDropdown dropdown =
            picker.gameObject.AddComponent<ContentEditorMultiSelectDropdown>();
        Image popup = Surface("CategoryOptions", picker.transform, ContentEditorColorRole.Border);
        Rect(popup.rectTransform, 0, 48, 280, 304);
        popup.raycastTarget = true;
        Canvas canvas = popup.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 30000;
        popup.gameObject.AddComponent<GraphicRaycaster>();
        Image interior = Surface("Interior", popup.transform, ContentEditorColorRole.Panel);
        Stretch(interior.rectTransform, 1, 1, 1, 1);
        ScrollAreaLayout scroll = Scroll("Choices", popup.transform, 8, 8);
        Rect((RectTransform)scroll.transform, 8, 8, 264, 256);
        UICheckboxView row = Checkbox(scroll.ScrollArea.ContentRoot);
        row.name = "CategoryTemplate";
        Rect((RectTransform)row.transform, 0, 0, 248, 32);
        Rect((RectTransform)row.transform.Find("Frame"), 0, 5, 22, 22);
        TMP_Text rowLabel = row.GetComponentInChildren<TMP_Text>(true);
        rowLabel.fontSize = 14;
        rowLabel.textWrappingMode = TextWrappingModes.NoWrap;
        rowLabel.overflowMode = TextOverflowModes.Ellipsis;
        row.gameObject.SetActive(false);
        Button clear = PlainButton("ClearSelection", popup.transform, "Clear selection");
        Rect((RectTransform)clear.transform, 8, 268, 264, 28);
        TMP_Text clearLabel = clear.GetComponentInChildren<TMP_Text>();
        clearLabel.fontSize = 13;
        clearLabel.alignment = TextAlignmentOptions.MidlineLeft;
        Assign(dropdown, "control", picker);
        Assign(dropdown, "caption", label);
        Assign(dropdown, "popup", popup.rectTransform);
        Assign(dropdown, "scroll", scroll);
        Assign(dropdown, "rowTemplate", row);
        Assign(dropdown, "clear", clear);
        popup.gameObject.SetActive(false);
        return dropdown;
    }

    /// <summary>
    /// Creates focused package details without repeating them beside every browse result.
    /// </summary>
    /// <param name="parent">The owning overlay.</param>
    /// <param name="view">The manager whose actions operate on the selected package.</param>
    private static void BuildDetails(Transform parent, ContentEditorModManagerView view)
    {
        Image overlay = Surface("PackageDetailsOverlay", parent, new Color(0, 0, 0, 0.72f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;
        Assign(view, "detailOverlay", overlay.gameObject);
        RectTransform rect = Surface(
            "PackageDetailsPanel",
            overlay.transform,
            ContentEditorColorRole.Panel
        ).rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(760, 640);
        Assign(view, "detailPanel", rect);
        Button close = ActionButton(view, rect, "detailCloseButton", "Close", 640, 28, 88, true);
        TMP_Text closeLabel = close.GetComponentInChildren<TMP_Text>();
        closeLabel.alignment = TextAlignmentOptions.MidlineRight;
        Stretch(closeLabel.rectTransform);
        TextMeshProUGUI empty = Caption(
            rect,
            "EmptyDetails",
            "This mod is no longer available.",
            32,
            200,
            696,
            100,
            18,
            ContentEditorColorRole.Muted
        );
        empty.alignment = TextAlignmentOptions.Center;
        Assign(view, "emptyDetails", empty);
        RectTransform content = Layer("DetailContent", rect);
        Stretch(content);
        Assign(view, "detailContent", content.gameObject);
        Image cover = Surface("Cover", content, ContentEditorColorRole.Background);
        Rect(cover.rectTransform, 32, 32, 96, 96);
        TextMeshProUGUI initials = Caption(
            cover.transform,
            "IconInitials",
            string.Empty,
            0,
            0,
            96,
            96,
            28,
            ContentEditorColorRole.Accent
        );
        initials.alignment = TextAlignmentOptions.Center;
        Assign(view, "iconInitials", initials);
        RawImage icon = Layer("PackageIcon", cover.transform).gameObject.AddComponent<RawImage>();
        Stretch(icon.rectTransform);
        icon.raycastTarget = false;
        icon.enabled = false;
        Assign(view, "icon", icon);
        TextMeshProUGUI heading = Caption(
            content,
            "PackageName",
            string.Empty,
            152,
            32,
            464,
            36,
            28
        );
        heading.fontStyle = FontStyles.Bold;
        Assign(view, "heading", heading);
        Assign(
            view,
            "creator",
            Caption(
                content,
                "Creator",
                string.Empty,
                152,
                72,
                464,
                24,
                14,
                ContentEditorColorRole.Muted
            )
        );
        Assign(
            view,
            "packageState",
            Caption(
                content,
                "PackageState",
                string.Empty,
                152,
                104,
                464,
                24,
                13,
                ContentEditorColorRole.Accent
            )
        );
        Assign(
            view,
            "popularity",
            Caption(
                content,
                "Popularity",
                string.Empty,
                32,
                144,
                440,
                24,
                13,
                ContentEditorColorRole.Muted
            )
        );
        TextMeshProUGUI updated = Caption(
            content,
            "Updated",
            string.Empty,
            488,
            144,
            240,
            24,
            13,
            ContentEditorColorRole.Muted
        );
        updated.alignment = TextAlignmentOptions.MidlineRight;
        Assign(view, "updated", updated);
        Assign(
            view,
            "categories",
            Caption(
                content,
                "Categories",
                string.Empty,
                32,
                176,
                696,
                24,
                13,
                ContentEditorColorRole.Muted
            )
        );
        ScrollAreaLayout details = Scroll("PackageDetails", content, 8, 8);
        Rect((RectTransform)details.transform, 32, 208, 712, 112);
        TextMeshProUGUI description = Caption(
            details.ScrollArea.ContentRoot,
            "Description",
            string.Empty,
            0,
            0,
            696,
            112,
            16
        );
        description.textWrappingMode = TextWrappingModes.Normal;
        description.alignment = TextAlignmentOptions.TopLeft;
        description.overflowMode = TextOverflowModes.Overflow;
        Assign(view, "details", details);
        Assign(view, "detailText", description);
        TextMeshProUGUI metadata = Caption(
            content,
            "ReleaseMetadata",
            string.Empty,
            32,
            336,
            696,
            24,
            13,
            ContentEditorColorRole.Muted
        );
        metadata.textWrappingMode = TextWrappingModes.Normal;
        Assign(view, "releaseMetadata", metadata);
        TextMeshProUGUI requirement = Caption(
            content,
            "Requirement",
            string.Empty,
            32,
            352,
            696,
            48,
            14,
            ContentEditorColorRole.Error
        );
        requirement.textWrappingMode = TextWrappingModes.Normal;
        requirement.gameObject.SetActive(false);
        Assign(view, "requirement", requirement);
        TMP_Dropdown versions = Selector(
            view,
            content,
            "versions",
            32,
            376,
            184,
            new[] { "Select a release" }
        );
        Caption(
            versions.transform,
            "VersionLabel",
            "Version",
            0,
            -28,
            184,
            20,
            13,
            ContentEditorColorRole.Muted
        );
        Button install = ActionButton(view, content, "installButton", "Install", 232, 376, 496);
        Image installInset = Surface("Inset", install.transform, ContentEditorColorRole.Panel);
        Stretch(installInset.rectTransform, 1, 1, 1, 1);
        installInset.transform.SetAsFirstSibling();
        installInset.raycastTarget = false;
        Assign(view, "installInset", installInset);
        BindColor(install.GetComponent<Image>(), ContentEditorColorRole.Accent, install);
        BindColor(install.GetComponentInChildren<TMP_Text>(), ContentEditorColorRole.Background);
        Button activate = ActionButton(
            view,
            content,
            "activateButton",
            "Set as default",
            32,
            376,
            280
        );
        BindColor(activate.GetComponent<Image>(), ContentEditorColorRole.Accent, activate);
        BindColor(activate.GetComponentInChildren<TMP_Text>(), ContentEditorColorRole.Background);
        Button uninstall = ActionButton(
            view,
            content,
            "uninstallButton",
            "Uninstall",
            32,
            600,
            100,
            true
        );
        Button listing = ActionButton(
            view,
            content,
            "listingButton",
            "Thunderstore page",
            32,
            512,
            184,
            true
        );
        Button folder = ActionButton(
            view,
            content,
            "folderButton",
            "Open folder",
            232,
            512,
            200,
            true
        );
        foreach (Button secondary in new[] { uninstall, listing, folder })
        {
            TMP_Text label = secondary.GetComponentInChildren<TMP_Text>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            Stretch(label.rectTransform);
        }
        uninstall.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.MidlineRight;
        BindColor(
            uninstall.GetComponentInChildren<TMP_Text>(),
            ContentEditorColorRole.Error,
            uninstall
        );
        TextMeshProUGUI status = Caption(
            rect,
            "DetailStatus",
            string.Empty,
            32,
            576,
            696,
            48,
            14,
            ContentEditorColorRole.Muted
        );
        status.textWrappingMode = TextWrappingModes.Normal;
        Assign(view, "detailStatus", status);
        overlay.gameObject.SetActive(false);
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
        Image cover = Surface("Cover", button.transform, ContentEditorColorRole.Background);
        Rect(cover.rectTransform, 24, 24, 96, 96);
        TextMeshProUGUI initials = Caption(
            cover.transform,
            "Initials",
            string.Empty,
            0,
            0,
            96,
            96,
            28,
            ContentEditorColorRole.Accent
        );
        initials.alignment = TextAlignmentOptions.Center;
        Assign(card, "initials", initials);
        RawImage artwork = Layer("Artwork", cover.transform).gameObject.AddComponent<RawImage>();
        Stretch(artwork.rectTransform);
        artwork.raycastTarget = false;
        artwork.enabled = false;
        Assign(card, "artwork", artwork);
        TextMeshProUGUI name = Caption(
            button.transform,
            "Name",
            string.Empty,
            144,
            24,
            680,
            28,
            22
        );
        name.fontStyle = FontStyles.Bold;
        Assign(card, "caption", name);
        TextMeshProUGUI author = Caption(
            button.transform,
            "Author",
            string.Empty,
            144,
            56,
            680,
            20,
            13,
            ContentEditorColorRole.Muted
        );
        Assign(card, "author", author);
        TextMeshProUGUI description = Caption(
            button.transform,
            "Summary",
            string.Empty,
            144,
            80,
            680,
            40,
            15
        );
        description.textWrappingMode = TextWrappingModes.Normal;
        description.alignment = TextAlignmentOptions.TopLeft;
        description.maxVisibleLines = 2;
        Assign(card, "description", description);
        TextMeshProUGUI popularity = Caption(
            button.transform,
            "Popularity",
            string.Empty,
            0,
            80,
            152,
            40,
            13,
            ContentEditorColorRole.Muted
        );
        popularity.rectTransform.anchorMin =
            popularity.rectTransform.anchorMax =
            popularity.rectTransform.pivot =
                Vector2.one;
        popularity.rectTransform.anchoredPosition = new Vector2(-24, -80);
        popularity.alignment = TextAlignmentOptions.TopRight;
        Assign(card, "popularity", popularity);
        foreach (TMP_Text label in new TMP_Text[] { name, author, description })
        {
            RectTransform textRect = label.rectTransform;
            Vector2 minimum = textRect.offsetMin;
            Vector2 maximum = textRect.offsetMax;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = minimum;
            textRect.offsetMax = new Vector2(-216, maximum.y);
        }
        Button action = Button("PrimaryAction", button.transform, "Install");
        RectTransform actionRect = (RectTransform)action.transform;
        actionRect.anchorMin = actionRect.anchorMax = actionRect.pivot = Vector2.one;
        actionRect.anchoredPosition = new Vector2(-24, -24);
        actionRect.sizeDelta = new Vector2(152, 44);
        BindColor(action.GetComponent<Image>(), ContentEditorColorRole.Accent, action);
        BindColor(action.GetComponentInChildren<TMP_Text>(), ContentEditorColorRole.Background);
        Image inset = Surface("Inset", action.transform, ContentEditorColorRole.Panel);
        Stretch(inset.rectTransform, 1, 1, 1, 1);
        inset.transform.SetAsFirstSibling();
        inset.raycastTarget = false;
        Assign(card, "actionInset", inset);
        Assign(card, "primaryAction", action);
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
    /// <returns>The authored selector.</returns>
    private static TMP_Dropdown Selector(
        Object view,
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
        Rect((RectTransform)dropdown.transform, left, top, width, 44);
        dropdown.captionText.alignment = TextAlignmentOptions.MidlineLeft;
        dropdown.template.anchoredPosition = Vector2.zero;
        BindColor(dropdown.template.GetComponent<Image>(), ContentEditorColorRole.Border);
        Assign(view, field, dropdown);
        return dropdown;
    }

    /// <summary>
    /// Gives page utilities a quiet outlined treatment beneath the installation action.
    /// </summary>
    /// <param name="button">The utility action to style.</param>
    private static void StyleSecondary(Button button)
    {
        BindColor(button.GetComponent<Image>(), ContentEditorColorRole.Divider, button);
        BindColor(button.GetComponentInChildren<TMP_Text>(), ContentEditorColorRole.Text, button);
        Image inset = Surface("Inset", button.transform, ContentEditorColorRole.Background);
        Stretch(inset.rectTransform, 1, 1, 1, 1);
        inset.transform.SetAsFirstSibling();
        inset.raycastTarget = false;
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
        Rect((RectTransform)button.transform, left, top, width, 44);
        Assign(view, field, button);
        return button;
    }
}
