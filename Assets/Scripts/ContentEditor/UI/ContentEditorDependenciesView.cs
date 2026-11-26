using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents package dependencies as pinned, removable rows using the shared public catalogue.
/// </summary>
public sealed class ContentEditorDependenciesView : MonoBehaviour
{
    [SerializeField]
    private Button choose;

    [SerializeField]
    private ScrollAreaLayout scroll;

    [SerializeField]
    private ContentEditorDependencyRowView rowTemplate;

    [SerializeField]
    private TMP_Text empty;

    private ThunderstorePackage[] catalog = Array.Empty<ThunderstorePackage>();
    private readonly List<string> references = new();
    private ListView<ContentEditorDependencyRowView> list;
    private bool bound;
    private bool busy;
    internal event Action ChooseRequested;
    internal event Action Changed;

    /// <summary>
    /// Returns the exact version pins to save and export.
    /// </summary>
    /// <returns>The current dependency references.</returns>
    internal string[] Read() => references.ToArray();

    /// <summary>
    /// Loads a draft without changing its pinned versions or discarding unavailable packages.
    /// </summary>
    /// <param name="values">The saved references.</param>
    internal void Show(IEnumerable<string> values)
    {
        Bind();
        references.Clear();
        references.AddRange(values.Distinct(StringComparer.Ordinal));
        Render();
    }

    /// <summary>
    /// Shares available package versions without changing the selected pins.
    /// </summary>
    /// <param name="packages">The cached or refreshed public catalogue.</param>
    internal void SetCatalog(ThunderstorePackage[] packages)
    {
        Bind();
        catalog = packages;
        Render();
    }

    /// <summary>
    /// Maps searchable package choices for the application's shared selection dialog.
    /// </summary>
    /// <returns>The catalogue's package identities and readable names.</returns>
    internal IReadOnlyDictionary<string, string> GetChoices() =>
        catalog.ToDictionary(
            package => package.full_name,
            package =>
                package.name.Replace('_', ' ')
                + " — "
                + package.owner
                + (package.is_deprecated ? " (deprecated)" : string.Empty),
            StringComparer.Ordinal
        );

    /// <summary>
    /// Selects current package identities independently of their version choices.
    /// </summary>
    /// <returns>The package keys used by the shared selection dialog.</returns>
    internal IReadOnlyDictionary<string, int> GetSelection() =>
        references
            .Select(GetPackageID)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(id => id, _ => 1, StringComparer.Ordinal);

    /// <summary>
    /// Applies package selection while preserving existing pins and choosing latest versions for additions.
    /// </summary>
    /// <param name="packages">The accepted package identities.</param>
    internal void ApplySelection(IEnumerable<string> packages)
    {
        List<string> next = new();
        foreach (string id in packages)
        {
            string existing = references.FirstOrDefault(value => GetPackageID(value) == id);
            if (existing != null)
                next.Add(existing);
            else
            {
                ThunderstorePackage listing = catalog.FirstOrDefault(package =>
                    package.full_name == id
                );
                if (listing?.Latest == null)
                    throw new ArgumentException(
                        "The selected package is no longer available. Refresh the catalogue and try again."
                    );
                next.Add(id + "-" + listing.Latest.version_number);
            }
        }
        references.Clear();
        references.AddRange(next);
        Render();
        Changed?.Invoke();
    }

    /// <summary>
    /// Splits the exact version pin from its package identity without requiring manual formatting.
    /// </summary>
    /// <param name="reference">The saved dependency reference.</param>
    /// <returns>The package identity, retaining malformed legacy values for explicit removal.</returns>
    internal static string GetPackageID(string reference)
    {
        int separator = reference.LastIndexOf('-');
        return separator < 0 ? reference : reference[..separator];
    }

    /// <summary>
    /// Prevents dependency changes during publishing or catalogue loading.
    /// </summary>
    /// <param name="value">Whether an owning operation is active.</param>
    internal void SetBusy(bool value)
    {
        busy = value;
        choose.interactable = !busy;
        foreach (ContentEditorDependencyRowView row in list.Rows)
        foreach (Selectable control in row.GetComponentsInChildren<Selectable>(true))
            control.interactable = !busy;
        if (!busy)
            Render();
    }

    /// <summary>
    /// Connects authored controls and responsive list layout once.
    /// </summary>
    private void Bind()
    {
        if (bound)
            return;
        if (choose == null || scroll == null || rowTemplate == null || empty == null)
            throw new MissingReferenceException(name + " has incomplete dependency controls.");
        list = new ListView<ContentEditorDependencyRowView>(
            scroll.ScrollArea,
            rowTemplate,
            "Dependency"
        );
        choose.onClick.AddListener(RequestChoices);
        scroll.WidthChanged += Render;
        bound = true;
    }

    /// <summary>
    /// Requests the public catalogue before showing package choices.
    /// </summary>
    private void RequestChoices() => ChooseRequested?.Invoke();

    /// <summary>
    /// Reuses dependency rows at the current scroll width.
    /// </summary>
    private void Render()
    {
        if (!bound)
            return;
        int width = Mathf.Max(360, Mathf.FloorToInt(scroll.ScrollArea.ViewportWidth));
        list.Render(
            references,
            references.Count * 58,
            38,
            false,
            58,
            (row, reference, index) =>
            {
                row.SetLayout(index * 58, width);
                row.Render(
                    reference,
                    catalog.FirstOrDefault(package => package.full_name == GetPackageID(reference)),
                    Change,
                    Remove
                );
                if (busy)
                    foreach (Selectable control in row.GetComponentsInChildren<Selectable>(true))
                        control.interactable = false;
            }
        );
        empty.gameObject.SetActive(references.Count == 0);
    }

    /// <summary>
    /// Replaces one package's pin with the chosen published version.
    /// </summary>
    /// <param name="previous">The previous pin.</param>
    /// <param name="next">The selected pin.</param>
    private void Change(string previous, string next)
    {
        int index = references.IndexOf(previous);
        if (index < 0)
            return;
        string id = GetPackageID(previous);
        references.RemoveAll(value => GetPackageID(value) == id);
        references.Insert(Mathf.Min(index, references.Count), next);
        Render();
        Changed?.Invoke();
    }

    /// <summary>
    /// Removes an explicitly selected dependency without changing other version pins.
    /// </summary>
    /// <param name="reference">The exact pin to remove.</param>
    private void Remove(string reference)
    {
        references.Remove(reference);
        Render();
        Changed?.Invoke();
    }

    /// <summary>
    /// Releases owned UI subscriptions.
    /// </summary>
    private void OnDestroy()
    {
        if (!bound)
            return;
        list.Clear();
        choose.onClick.RemoveListener(RequestChoices);
        scroll.WidthChanged -= Render;
    }
}
