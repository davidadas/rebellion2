using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reuses row instances and scroll geometry independently of row-specific interaction.
/// </summary>
/// <typeparam name="TRowView">The authored row component.</typeparam>
public sealed class ListView<TRowView>
    where TRowView : MonoBehaviour
{
    private readonly List<TRowView> rows = new();
    private readonly ScrollAreaView scroll;
    private readonly ScrollAreaLayout layout;
    private readonly TRowView template;
    private readonly string namePrefix;
    private readonly Action<TRowView, int> created;
    private readonly Action<TRowView> removed;

    public IReadOnlyList<TRowView> Rows => rows;

    /// <summary>
    /// Connects an authored list to its row lifecycle callbacks.
    /// </summary>
    /// <param name="scroll">The list viewport.</param>
    /// <param name="template">The reusable row template.</param>
    /// <param name="namePrefix">The runtime row-name prefix.</param>
    /// <param name="created">Binds a new row once.</param>
    /// <param name="removed">Releases a row's subscriptions.</param>
    public ListView(
        ScrollAreaView scroll,
        TRowView template,
        string namePrefix,
        Action<TRowView, int> created = null,
        Action<TRowView> removed = null
    )
    {
        this.scroll = scroll ?? throw new ArgumentNullException(nameof(scroll));
        layout = scroll.GetComponent<ScrollAreaLayout>();
        this.template = template ?? throw new ArgumentNullException(nameof(template));
        this.namePrefix = string.IsNullOrEmpty(namePrefix) ? typeof(TRowView).Name : namePrefix;
        this.created = created;
        this.removed = removed;
    }

    /// <summary>
    /// Rebinds visible rows and hides unused instances.
    /// </summary>
    /// <typeparam name="T">The projected row data.</typeparam>
    /// <param name="items">The visible data.</param>
    /// <param name="contentHeight">The scrollable content height.</param>
    /// <param name="scrollStep">The wheel-scroll distance.</param>
    /// <param name="resetScroll">Whether to return to the top.</param>
    /// <param name="rowPitch">The distance between row origins.</param>
    /// <param name="render">Applies row-specific content and layout.</param>
    public void Render<T>(
        IReadOnlyList<T> items,
        int contentHeight,
        int scrollStep,
        bool resetScroll,
        int rowPitch,
        Action<TRowView, T, int> render
    )
    {
        items ??= Array.Empty<T>();
        scroll.gameObject.SetActive(true);
        if (layout)
            layout.SetContentHeight(contentHeight, resetScroll);
        scroll.SetContentHeight(contentHeight, scrollStep, resetScroll);
        for (int index = 0; index < items.Count; index++)
        {
            if (index == rows.Count)
            {
                TRowView row = UnityEngine.Object.Instantiate(template, scroll.ContentRoot);
                row.name = namePrefix + index;
                rows.Add(row);
                created?.Invoke(row, index);
            }
            TRowView current = rows[index];
            current.gameObject.SetActive(true);
            UILayout.SetSourceRect(
                (RectTransform)current.transform,
                0,
                index * rowPitch,
                Mathf.RoundToInt(scroll.ViewportWidth),
                rowPitch
            );
            render(current, items[index], index);
        }
        HideFrom(items.Count);
    }

    /// <summary>
    /// Hides all pooled rows.
    /// </summary>
    public void Hide() => HideFrom(0);

    /// <summary>
    /// Releases subscriptions and destroys owned rows.
    /// </summary>
    public void Clear()
    {
        foreach (TRowView row in rows)
        {
            if (row == null)
                continue;
            removed?.Invoke(row);
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(row.gameObject);
            else
                UnityEngine.Object.DestroyImmediate(row.gameObject);
        }
        rows.Clear();
    }

    /// <summary>
    /// Hides rows beyond the visible data.
    /// </summary>
    /// <param name="index">The first unused row.</param>
    private void HideFrom(int index)
    {
        for (int item = index; item < rows.Count; item++)
            rows[item].gameObject.SetActive(false);
    }
}
