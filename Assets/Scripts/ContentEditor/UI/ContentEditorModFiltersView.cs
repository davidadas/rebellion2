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
    private ContentEditorMultiSelectDropdown includeCategories;

    [SerializeField]
    private ContentEditorMultiSelectDropdown excludeCategories;

    [SerializeField]
    private UICheckboxView showDeprecated;

    [SerializeField]
    private UICheckboxView showNsfw;

    [SerializeField]
    private TMP_Text[] headings;

    [SerializeField]
    private TMP_Text categoryHelp;

    private readonly HashSet<string> included = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> excluded = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> categories = new(StringComparer.OrdinalIgnoreCase);
    private ThunderstorePackageSection[] sections = Array.Empty<ThunderstorePackageSection>();
    private bool bound;
    internal event Action Changed;
    internal int ActiveCount =>
        included.Count
        + excluded.Count
        + (section.value > 0 ? 1 : 0)
        + (installation.value > 0 ? 1 : 0)
        + (showDeprecated.IsChecked ? 1 : 0)
        + (showNsfw.IsChecked ? 1 : 0);

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
        RectTransform[] controls =
        {
            (RectTransform)section.transform,
            (RectTransform)installation.transform,
            (RectTransform)includeCategories.transform,
            (RectTransform)excludeCategories.transform,
        };
        int left = 16;
        int[] weights = { 240, 240, 280, 280 };
        for (int index = 0; index < controls.Length; index++)
        {
            int columnWidth = Mathf.FloorToInt((width - 80) * weights[index] / 1040f);
            UILayout.SetSourceRect(headings[index].rectTransform, left, 16, columnWidth, 20);
            UILayout.SetSourceRect(controls[index], left, 44, columnWidth, 44);
            if (index == 0 || index == 1)
                UILayout.SetSourceRect(
                    (RectTransform)(index == 0 ? showDeprecated : showNsfw).transform,
                    left,
                    128,
                    columnWidth,
                    24
                );
            if (index == 2)
                UILayout.SetSourceRect(
                    categoryHelp.rectTransform,
                    left,
                    100,
                    width - left - 16,
                    20
                );
            left += columnWidth + 16;
        }
    }

    /// <summary>
    /// Reads a snapshot for local filtering without mutating the selection draft.
    /// </summary>
    /// <returns>The combined category, section, visibility, and installation choices.</returns>
    internal ContentEditorModFilterOptions Read()
    {
        Bind();
        ThunderstorePackageSection selectedSection = sections.ElementAtOrDefault(section.value - 1);
        return new ContentEditorModFilterOptions
        {
            IncludedCategories = included.ToArray(),
            ExcludedCategories = excluded.ToArray(),
            SectionPackages =
                selectedSection == null
                    ? null
                    : new HashSet<string>(selectedSection.packages, StringComparer.Ordinal),
            Installation = (ContentEditorModFilter)installation.value,
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
        string selected = sections.ElementAtOrDefault(section.value - 1)?.uuid;
        sections = metadata?.sections ?? Array.Empty<ThunderstorePackageSection>();
        section.ClearOptions();
        section.AddOptions(
            new[] { "All sections" }.Concat(sections.Select(item => item.name)).ToList()
        );
        section.SetValueWithoutNotify(
            Mathf.Max(0, Array.FindIndex(sections, item => item.uuid == selected) + 1)
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
        included.Clear();
        excluded.Clear();
        section.SetValueWithoutNotify(0);
        installation.SetValueWithoutNotify(0);
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
    /// <param name="values">The selected category keys.</param>
    private void ChangeIncluded(string[] values)
    {
        included.Clear();
        included.UnionWith(values);
        excluded.ExceptWith(included);
        RenderCategoryLabels();
        Changed?.Invoke();
    }

    /// <summary>
    /// Applies checked exclusions and removes conflicting inclusions.
    /// </summary>
    /// <param name="values">The selected category keys.</param>
    private void ChangeExcluded(string[] values)
    {
        excluded.Clear();
        excluded.UnionWith(values);
        included.ExceptWith(excluded);
        RenderCategoryLabels();
        Changed?.Invoke();
    }

    /// <summary>
    /// Refreshes the category fields and any expanded checkbox lists.
    /// </summary>
    private void RenderCategoryLabels()
    {
        includeCategories.Render(categories, included, "Any category");
        excludeCategories.Render(categories, excluded, "No exclusions");
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
