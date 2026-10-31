using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Provides consistent selection, addition, and removal for a named collection.
/// </summary>
public sealed class CollectionEditorView : MonoBehaviour
{
    [SerializeField]
    private TMP_Text heading;

    [SerializeField]
    private TMP_Dropdown entries;

    [SerializeField]
    private Button add;

    [SerializeField]
    private Button remove;
    private bool bound;
    public event Action<int> Selected;
    public event Action AddRequested;
    public event Action RemoveRequested;
    public bool HasOpenChoice => entries.IsExpanded;

    /// <summary>
    /// Connects authored controls once.
    /// </summary>
    private void Awake() => Bind();

    /// <summary>
    /// Verifies reusable collection controls before presentation.
    /// </summary>
    private void Bind()
    {
        if (bound)
            return;
        if (heading == null || entries == null || add == null || remove == null)
            throw new MissingReferenceException(name + " has incomplete collection references.");
        entries.onValueChanged.AddListener(Select);
        add.onClick.AddListener(RequestAdd);
        remove.onClick.AddListener(RequestRemove);
        bound = true;
    }

    /// <summary>
    /// Releases control listeners.
    /// </summary>
    private void OnDestroy()
    {
        if (!bound)
            return;
        entries.onValueChanged.RemoveListener(Select);
        add.onClick.RemoveListener(RequestAdd);
        remove.onClick.RemoveListener(RequestRemove);
    }

    /// <summary>
    /// Displays one collection with complete-width controls on separate rows.
    /// </summary>
    /// <param name="title">The collection's purpose.</param>
    /// <param name="names">The entry captions.</param>
    /// <param name="selected">The selected entry index.</param>
    /// <param name="addCaption">The specific creation action.</param>
    public void Render(string title, IReadOnlyList<string> names, int selected, string addCaption)
    {
        Bind();
        heading.text = title;
        add.GetComponentInChildren<TMP_Text>().text = addCaption;
        entries.ClearOptions();
        entries.AddOptions(names.Count == 0 ? new List<string> { "No entries" } : names.ToList());
        entries.SetValueWithoutNotify(Math.Max(0, selected));
        entries.RefreshShownValue();
        entries.interactable = remove.interactable = names.Count > 0;
        RectTransform rect = (RectTransform)transform;
        UILayout.SetSourceRect(
            heading.rectTransform,
            4,
            0,
            Mathf.RoundToInt(rect.rect.width - 8),
            32
        );
        UILayout.SetSourceRect(
            (RectTransform)entries.transform,
            4,
            38,
            Mathf.RoundToInt(rect.rect.width - 8),
            34
        );
        UILayout.SetSourceRect(
            (RectTransform)add.transform,
            4,
            80,
            Mathf.Min(220, Mathf.RoundToInt(rect.rect.width - 116)),
            32
        );
        UILayout.SetSourceRect(
            (RectTransform)remove.transform,
            Mathf.RoundToInt(rect.rect.width - 108),
            80,
            100,
            32
        );
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Publishes a selection intent.
    /// </summary>
    /// <param name="index">The selected entry.</param>
    private void Select(int index) => Selected?.Invoke(index);

    /// <summary>
    /// Publishes a collection creation intent.
    /// </summary>
    private void RequestAdd() => AddRequested?.Invoke();

    /// <summary>
    /// Publishes a collection removal intent.
    /// </summary>
    private void RequestRemove() => RemoveRequested?.Invoke();

    /// <summary>
    /// Releases the previous collection binding before this pooled control is reused.
    /// </summary>
    public void Clear()
    {
        Selected = null;
        AddRequested = null;
        RemoveRequested = null;
        gameObject.SetActive(false);
    }
}
