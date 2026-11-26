using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Combines community browsing filters with attached category checkbox dropdowns.
/// </summary>
public sealed class ContentEditorModFiltersView : MonoBehaviour
{
    [SerializeField]
    private TMP_Dropdown section;

    [SerializeField]
    private TMP_Dropdown installation;

    [SerializeField]
    private MultiSelectDropdown includeCategories;

    [SerializeField]
    private MultiSelectDropdown excludeCategories;

    [SerializeField]
    private UICheckboxView showDeprecated;

    [SerializeField]
    private UICheckboxView showNsfw;

    [SerializeField]
    private TMP_Text sectionHeading;

    [SerializeField]
    private TMP_Text installationHeading;

    [SerializeField]
    private TMP_Text includedHeading;

    [SerializeField]
    private TMP_Text excludedHeading;

    [SerializeField]
    private TMP_Text categoryHelp;

    [SerializeField]
    private Button clearButton;

    private readonly Dictionary<string, string> categories = new(StringComparer.OrdinalIgnoreCase);
    private bool bound;
    internal event Action Changed;
    internal int Height => Mathf.CeilToInt(((RectTransform)transform).rect.height);
    internal int ActiveCount
    {
        get
        {
            Bind();
            return includeCategories.SelectedValues.Count
                + excludeCategories.SelectedValues.Count
                + (SelectedSection != null ? 1 : 0)
                + (SelectedInstallation != ContentEditorModFilter.All ? 1 : 0)
                + (showDeprecated.IsChecked ? 1 : 0)
                + (showNsfw.IsChecked ? 1 : 0);
        }
    }

    private ContentEditorModFilter SelectedInstallation =>
        ((DropdownOption<ContentEditorModFilter>)installation.options[installation.value]).Value;
    private ThunderstorePackageSection SelectedSection =>
        ((DropdownOption<ThunderstorePackageSection>)section.options[section.value]).Value;

    /// <summary>
    /// Connects filter interactions once all authored references are available.
    /// </summary>
    private void Awake() => Bind();

    /// <summary>
    /// Connects controls for both runtime use and authoring previews.
    /// </summary>
    private void Bind()
    {
        if (bound)
            return;
        if (
            section == null
            || installation == null
            || includeCategories == null
            || excludeCategories == null
            || showDeprecated == null
            || showNsfw == null
        )
            throw new MissingReferenceException(name + " has incomplete catalogue filters.");
        section.options = new List<TMP_Dropdown.OptionData>
        {
            new DropdownOption<ThunderstorePackageSection>(null, "All sections"),
        };
        installation.options = new List<TMP_Dropdown.OptionData>
        {
            new DropdownOption<ContentEditorModFilter>(ContentEditorModFilter.All, "All mods"),
            new DropdownOption<ContentEditorModFilter>(
                ContentEditorModFilter.NotInstalled,
                "Not installed"
            ),
            new DropdownOption<ContentEditorModFilter>(
                ContentEditorModFilter.Installed,
                "Installed"
            ),
            new DropdownOption<ContentEditorModFilter>(
                ContentEditorModFilter.Updates,
                "Updates available"
            ),
            new DropdownOption<ContentEditorModFilter>(
                ContentEditorModFilter.Local,
                "Local content"
            ),
        };
        section.RefreshShownValue();
        installation.RefreshShownValue();
        section.onValueChanged.AddListener(ChangeSelection);
        installation.onValueChanged.AddListener(ChangeSelection);
        includeCategories.Changed += ChangeIncluded;
        excludeCategories.Changed += ChangeExcluded;
        showDeprecated.ValueChanged += ChangeVisibility;
        showNsfw.ValueChanged += ChangeVisibility;
        bound = true;
    }

    /// <summary>
    /// Shares the available width between filters while keeping each label above its control.
    /// </summary>
    /// <param name="width">The logical width of the browsing page.</param>
    internal void SetWidth(int width)
    {
        var fields = new[]
        {
            (Heading: sectionHeading, Control: section.transform),
            (Heading: installationHeading, Control: installation.transform),
            (Heading: includedHeading, Control: includeCategories.transform),
            (Heading: excludedHeading, Control: excludeCategories.transform),
        };
        int columns = width < 900 ? 2 : 4;
        int columnWidth = Mathf.Max(1, (width - 32 - (columns - 1) * 16) / columns);
        for (int index = 0; index < fields.Length; index++)
        {
            int left = 16 + index % columns * (columnWidth + 16);
            int top = 16 + index / columns * 88;
            UILayout.SetSourceRect(fields[index].Heading.rectTransform, left, top, columnWidth, 20);
            UILayout.SetSourceRect(
                (RectTransform)fields[index].Control,
                left,
                top + 28,
                columnWidth,
                44
            );
        }
        int footer = 16 + Mathf.CeilToInt(fields.Length / (float)columns) * 88;
        int actionTop = columns == 4 ? footer : footer + 40;
        UILayout.SetSourceRect(
            (RectTransform)showDeprecated.transform,
            16,
            footer + (columns == 4 ? 10 : 0),
            columnWidth,
            24
        );
        UILayout.SetSourceRect(
            (RectTransform)showNsfw.transform,
            32 + columnWidth,
            footer + (columns == 4 ? 10 : 0),
            columnWidth,
            24
        );
        int helpLeft = columns == 4 ? 48 + 2 * columnWidth : 16;
        UILayout.SetSourceRect(
            categoryHelp.rectTransform,
            helpLeft,
            actionTop + 12,
            Mathf.Max(1, width - helpLeft - 208),
            20
        );
        UILayout.SetSourceRect(
            (RectTransform)clearButton.transform,
            width - 192,
            actionTop,
            176,
            44
        );
        ((RectTransform)transform).SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            actionTop + 60
        );
    }

    /// <summary>
    /// Reads a snapshot for local filtering without mutating the selection draft.
    /// </summary>
    /// <returns>The combined category, section, visibility, and installation choices.</returns>
    internal ContentEditorModFilterOptions Read()
    {
        Bind();
        ThunderstorePackageSection selectedSection = SelectedSection;
        return new ContentEditorModFilterOptions
        {
            IncludedCategories = includeCategories.SelectedValues.ToArray(),
            ExcludedCategories = excludeCategories.SelectedValues.ToArray(),
            SectionPackages =
                selectedSection == null
                    ? null
                    : new HashSet<string>(selectedSection.packages, StringComparer.Ordinal),
            Installation = SelectedInstallation,
            ShowDeprecated = showDeprecated.IsChecked,
            ShowNsfw = showNsfw.IsChecked,
        };
    }

    /// <summary>
    /// Refreshes public filter choices while retaining the user's selections.
    /// </summary>
    /// <param name="metadata">The cached or refreshed community definitions.</param>
    /// <param name="catalogCategories">Category names available in the package snapshot.</param>
    internal void SetOptions(
        ThunderstoreCommunityFilters metadata,
        IEnumerable<string> catalogCategories
    )
    {
        Bind();
        string selected = SelectedSection?.uuid;
        section.options = new[]
        {
            new DropdownOption<ThunderstorePackageSection>(null, "All sections"),
        }
            .Concat(
                (metadata?.sections ?? Array.Empty<ThunderstorePackageSection>()).Select(
                    item => new DropdownOption<ThunderstorePackageSection>(item, item.name)
                )
            )
            .Cast<TMP_Dropdown.OptionData>()
            .ToList();
        section.SetValueWithoutNotify(
            Mathf.Max(
                0,
                section.options.FindIndex(option =>
                    ((DropdownOption<ThunderstorePackageSection>)option).Value?.uuid == selected
                )
            )
        );
        section.RefreshShownValue();
        categories.Clear();
        categories.Add(string.Empty, "Uncategorized");
        foreach (
            string name in (
                metadata?.package_categories ?? Array.Empty<ThunderstorePackageCategory>()
            )
                .Select(item => item.name)
                .Concat(catalogCategories)
                .Where(name => !string.IsNullOrWhiteSpace(name))
        )
            categories[name] = name;
        RenderCategoryLabels();
    }

    /// <summary>
    /// Prevents browsing changes while an installation owns the catalogue controls.
    /// </summary>
    /// <param name="value">Whether the filter controls accept input.</param>
    internal void SetInteractable(bool value)
    {
        section.interactable = installation.interactable = value;
        includeCategories.SetInteractable(value);
        excludeCategories.SetInteractable(value);
        showDeprecated.GetComponent<Toggle>().interactable = value;
        showNsfw.GetComponent<Toggle>().interactable = value;
    }

    /// <summary>
    /// Clears all filtering criteria while retaining the current sort order.
    /// </summary>
    internal void Clear()
    {
        Bind();
        includeCategories.ClearWithoutNotify();
        excludeCategories.ClearWithoutNotify();
        section.SetValueWithoutNotify(0);
        installation.SetValueWithoutNotify(
            installation.options.FindIndex(option =>
                ((DropdownOption<ContentEditorModFilter>)option).Value == ContentEditorModFilter.All
            )
        );
        showDeprecated.SetIsCheckedWithoutNotify(false);
        showNsfw.SetIsCheckedWithoutNotify(false);
        RenderCategoryLabels();
        Changed?.Invoke();
    }

    /// <summary>
    /// Closes an expanded category list or single-choice filter before its panel is dismissed.
    /// </summary>
    /// <returns>Whether an expanded selector was closed.</returns>
    internal bool TryCancel()
    {
        if (includeCategories.TryCancel() || excludeCategories.TryCancel())
            return true;
        foreach (TMP_Dropdown dropdown in new[] { section, installation })
            if (dropdown.IsExpanded)
            {
                dropdown.Hide();
                return true;
            }
        return false;
    }

    /// <summary>
    /// Applies checked inclusions and removes conflicting exclusions.
    /// </summary>
    private void ChangeIncluded()
    {
        excludeCategories.DeselectWithoutNotify(includeCategories.SelectedValues);
        Changed?.Invoke();
    }

    /// <summary>
    /// Applies checked exclusions and removes conflicting inclusions.
    /// </summary>
    private void ChangeExcluded()
    {
        includeCategories.DeselectWithoutNotify(excludeCategories.SelectedValues);
        Changed?.Invoke();
    }

    /// <summary>
    /// Refreshes the category fields and any expanded checkbox lists.
    /// </summary>
    private void RenderCategoryLabels()
    {
        includeCategories.SetOptions(categories, "Any category");
        excludeCategories.SetOptions(categories, "No exclusions");
    }

    /// <summary>
    /// Applies one single-choice section or installation selection.
    /// </summary>
    /// <param name="index">The selected option index.</param>
    private void ChangeSelection(int index) => Changed?.Invoke();

    /// <summary>
    /// Applies an independent visibility toggle.
    /// </summary>
    /// <param name="value">The selected visibility state.</param>
    private void ChangeVisibility(bool value) => Changed?.Invoke();

    /// <summary>
    /// Releases filter-owned event subscriptions.
    /// </summary>
    private void OnDestroy()
    {
        if (!bound)
            return;
        section.onValueChanged.RemoveListener(ChangeSelection);
        installation.onValueChanged.RemoveListener(ChangeSelection);
        includeCategories.Changed -= ChangeIncluded;
        excludeCategories.Changed -= ChangeExcluded;
        showDeprecated.ValueChanged -= ChangeVisibility;
        showNsfw.ValueChanged -= ChangeVisibility;
    }
}
