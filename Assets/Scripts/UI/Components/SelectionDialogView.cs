using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents a searchable multi-selection draft with optional quantities and explicit apply/cancel.
/// </summary>
public sealed class SelectionDialogView : MonoBehaviour, ICancelable
{
    [SerializeField]
    private TMP_Text heading;

    [SerializeField]
    private TMP_Text error;

    [SerializeField]
    private TMP_Text summary;

    [SerializeField]
    private TMP_Text quantityHeading;

    [SerializeField]
    private TMP_InputField search;

    [SerializeField]
    private ScrollAreaLayout scroll;

    [SerializeField]
    private SelectionOptionView rowTemplate;

    [SerializeField]
    private Button apply;

    [SerializeField]
    private Button cancel;
    private readonly List<SelectionOptionView> rows = new();
    private KeyValuePair<string, string>[] options = Array.Empty<KeyValuePair<string, string>>();
    private ItemSelection selection;
    private Action<IReadOnlyDictionary<string, int>> accepted;
    private bool quantities;
    private bool bound;
    public bool IsOpen => gameObject.activeSelf;

    /// <summary>
    /// Connects the authored dialog controls once.
    /// </summary>
    private void Awake() => Bind();

    /// <summary>
    /// Verifies references before direct presentation or runtime activation.
    /// </summary>
    private void Bind()
    {
        if (bound)
            return;
        if (
            error == null
            || heading == null
            || summary == null
            || quantityHeading == null
            || search == null
            || scroll == null
            || rowTemplate == null
            || apply == null
            || cancel == null
        )
            throw new MissingReferenceException(
                name + " has incomplete selection dialog references."
            );
        search.onValueChanged.AddListener(Filter);
        apply.onClick.AddListener(Apply);
        cancel.onClick.AddListener(Hide);
        bound = true;
    }

    /// <summary>
    /// Releases dialog-owned control subscriptions.
    /// </summary>
    private void OnDestroy()
    {
        if (!bound)
            return;
        search.onValueChanged.RemoveListener(Filter);
        apply.onClick.RemoveListener(Apply);
        cancel.onClick.RemoveListener(Hide);
        foreach (SelectionOptionView row in rows)
            row.Changed -= Change;
    }

    /// <summary>
    /// Opens an isolated draft, retaining unknown selected keys so they can be removed.
    /// </summary>
    /// <param name="title">The selection's purpose.</param>
    /// <param name="choices">The available keys and names.</param>
    /// <param name="current">The currently selected quantities.</param>
    /// <param name="confirmed">The callback receiving an accepted draft.</param>
    /// <param name="quantityLabel">The quantity column title, or null for a set selection.</param>
    public void Show(
        string title,
        IReadOnlyDictionary<string, string> choices,
        IReadOnlyDictionary<string, int> current,
        Action<IReadOnlyDictionary<string, int>> confirmed,
        string quantityLabel = null
    )
    {
        Bind();
        heading.text = title;
        error.text = string.Empty;
        selection = new ItemSelection(current);
        accepted = confirmed;
        quantities = quantityLabel != null;
        quantityHeading.text = quantityLabel ?? string.Empty;
        options = choices
            .Concat(
                current
                    .Keys.Where(key => !choices.ContainsKey(key))
                    .Select(key => new KeyValuePair<string, string>(key, "Missing: " + key))
            )
            .OrderBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        search.SetTextWithoutNotify(string.Empty);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        scroll.SetContentHeight(0, true);
        Render();
    }

    /// <summary>
    /// Dismisses the draft without applying its selection.
    /// </summary>
    public void Hide()
    {
        accepted = null;
        selection = null;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Closes this modal when the application routes Escape to it.
    /// </summary>
    /// <returns>Whether an open dialog was dismissed.</returns>
    public bool TryCancel()
    {
        if (!IsOpen)
            return false;
        Hide();
        return true;
    }

    /// <summary>
    /// Filters readable option names and their underlying keys.
    /// </summary>
    /// <param name="text">The entered search.</param>
    private void Filter(string text)
    {
        if (rows.Any(row => row.gameObject.activeSelf && !row.Commit()))
            return;
        Render();
    }

    /// <summary>
    /// Reuses standard checkbox/input rows for matching options.
    /// </summary>
    private void Render()
    {
        foreach (SelectionOptionView row in rows)
            row.gameObject.SetActive(false);
        UILayout.SetSourceRect(
            quantityHeading.rectTransform,
            20 + Mathf.FloorToInt(scroll.ScrollArea.ViewportWidth) - 112,
            104,
            104,
            28
        );
        int index = 0;
        foreach (
            var option in options.Where(option =>
                string.IsNullOrEmpty(search.text)
                || option.Value.IndexOf(search.text, StringComparison.OrdinalIgnoreCase) >= 0
                || option.Key.IndexOf(search.text, StringComparison.OrdinalIgnoreCase) >= 0
            )
        )
        {
            if (rows.Count <= index)
            {
                SelectionOptionView row = Instantiate(rowTemplate, scroll.ScrollArea.ContentRoot);
                row.Changed += Change;
                rows.Add(row);
            }
            SelectionOptionView control = rows[index];
            UILayout.SetSourceRect(
                (RectTransform)control.transform,
                0,
                index * 64,
                Mathf.FloorToInt(scroll.ScrollArea.ViewportWidth),
                64
            );
            control.Render(
                option.Key,
                option.Value,
                selection.Counts.TryGetValue(option.Key, out int amount) ? amount : 0,
                quantities
            );
            index++;
        }
        scroll.SetContentHeight(index * 64, true);
        RefreshSummary();
    }

    /// <summary>
    /// Updates only the local draft as a row is changed.
    /// </summary>
    /// <param name="key">The changed item.</param>
    /// <param name="amount">Its selected quantity.</param>
    private void Change(string key, int amount)
    {
        selection.SetCount(key, amount);
        RefreshSummary();
    }

    /// <summary>
    /// Reports selected types and total quantities without expanding repeated entries.
    /// </summary>
    private void RefreshSummary() =>
        summary.text = quantities
            ? selection.Counts.Count
                + " selected; total "
                + selection.Counts.Values.Sum(value => (long)value)
            : selection.Counts.Count + " selected";

    /// <summary>
    /// Accepts a valid draft once, leaving application validation to the caller.
    /// </summary>
    private void Apply()
    {
        if (rows.Any(row => row.gameObject.activeSelf && !row.Commit()))
            return;
        var values = new Dictionary<string, int>(selection.Counts, StringComparer.Ordinal);
        try
        {
            accepted?.Invoke(values);
            Hide();
        }
        catch (ArgumentException exception)
        {
            error.text = exception.Message;
        }
    }
}
