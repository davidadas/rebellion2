using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Reuses authored row templates to render and navigate a scrollable selectable list.
/// </summary>
/// <typeparam name="TRowView">The authored row-view type.</typeparam>
/// <typeparam name="TRowData">The projected row-data type.</typeparam>
public sealed class SelectableListView<TRowView, TRowData>
    where TRowView : SelectableListRowView
{
    private readonly List<TRowView> rowViews = new List<TRowView>();
    private readonly ScrollAreaView scrollArea;
    private readonly TRowView rowTemplate;
    private readonly string rowNamePrefix;
    private readonly Action<TRowView, PointerEventData> rowSelected;
    private readonly Action<TRowView, PointerEventData> rowActivated;
    private readonly Action<TRowView, PointerEventData> rowContextRequested;
    private readonly Func<bool> canNavigate;
    private readonly Transform navigationScope;

    /// <summary>
    /// Creates a reusable list renderer around an authored scroll area and row template.
    /// </summary>
    /// <param name="scrollArea">The authored scroll area.</param>
    /// <param name="rowTemplate">The inactive authored row template.</param>
    /// <param name="rowNamePrefix">The runtime row name prefix.</param>
    /// <param name="rowSelected">Handles row selection.</param>
    /// <param name="rowActivated">Handles row activation.</param>
    /// <param name="rowContextRequested">Handles row context requests.</param>
    /// <param name="canNavigate">Determines whether keyboard navigation is currently allowed.</param>
    /// <param name="navigationScope">The selection scope used when restoring keyboard focus.</param>
    public SelectableListView(
        ScrollAreaView scrollArea,
        TRowView rowTemplate,
        string rowNamePrefix,
        Action<TRowView, PointerEventData> rowSelected,
        Action<TRowView, PointerEventData> rowActivated,
        Action<TRowView, PointerEventData> rowContextRequested = null,
        Func<bool> canNavigate = null,
        Transform navigationScope = null
    )
    {
        this.scrollArea = scrollArea ?? throw new ArgumentNullException(nameof(scrollArea));
        this.rowTemplate = rowTemplate ?? throw new ArgumentNullException(nameof(rowTemplate));
        this.rowNamePrefix = string.IsNullOrEmpty(rowNamePrefix)
            ? typeof(TRowView).Name
            : rowNamePrefix;
        this.rowSelected = rowSelected;
        this.rowActivated = rowActivated;
        this.rowContextRequested = rowContextRequested;
        this.canNavigate = canNavigate;
        this.navigationScope = navigationScope;
    }

    /// <summary>
    /// Renders projected rows, reusing existing row instances and preserving scroll position.
    /// </summary>
    /// <param name="rows">The projected rows.</param>
    /// <param name="contentHeight">The complete content height.</param>
    /// <param name="scrollStep">The distance moved by one scroll action.</param>
    /// <param name="resetScroll">Whether to return the list to its top.</param>
    /// <param name="rowPitch">The vertical distance between row origins.</param>
    /// <param name="renderRow">Applies one projected row to one row view.</param>
    /// <param name="isSelected">Determines which row should be revealed for navigation.</param>
    public void Render(
        IReadOnlyList<TRowData> rows,
        int contentHeight,
        int scrollStep,
        bool resetScroll,
        int rowPitch,
        Action<TRowView, TRowData, int> renderRow,
        Func<TRowData, int, bool> isSelected = null
    )
    {
        IReadOnlyList<TRowData> safeRows = rows ?? Array.Empty<TRowData>();
        scrollArea.gameObject.SetActive(true);
        scrollArea.SetContentHeight(contentHeight, scrollStep, resetScroll);

        for (int i = 0; i < safeRows.Count; i++)
        {
            TRowView rowView = GetRowView(i);
            SetTopLeftRect(
                rowView.transform as RectTransform,
                0,
                i * rowPitch,
                Mathf.RoundToInt(scrollArea.ViewportWidth),
                rowPitch
            );
            renderRow(rowView, safeRows[i], i);
        }

        HideRowsFrom(safeRows.Count);
        RevealAndFocusSelectedRow(safeRows, rowPitch, isSelected);
    }

    /// <summary>
    /// Hides all instantiated rows without releasing them.
    /// </summary>
    public void Hide()
    {
        HideRowsFrom(0);
    }

    /// <summary>
    /// Unbinds and destroys all instantiated rows.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < rowViews.Count; i++)
        {
            TRowView row = rowViews[i];
            if (row == null)
                continue;

            row.Selected -= HandleRowSelected;
            row.Activated -= HandleRowActivated;
            row.ContextRequested -= HandleRowContextRequested;
            UnityEngine.Object.Destroy(row.gameObject);
        }

        rowViews.Clear();
    }

    /// <summary>
    /// Gets or creates the row view at a display index.
    /// </summary>
    /// <param name="index">The zero-based display index.</param>
    /// <returns>The reusable row view.</returns>
    private TRowView GetRowView(int index)
    {
        while (rowViews.Count <= index)
        {
            TRowView row = UnityEngine.Object.Instantiate(rowTemplate, scrollArea.ContentRoot);
            row.name = $"{rowNamePrefix}{rowViews.Count}";
            row.Selected += HandleRowSelected;
            row.Activated += HandleRowActivated;
            row.ContextRequested += HandleRowContextRequested;
            row.SetNavigationGate(CanNavigate);
            rowViews.Add(row);
        }

        return rowViews[index];
    }

    /// <summary>
    /// Hides every instantiated row at or after an index.
    /// </summary>
    /// <param name="index">The first row to hide.</param>
    private void HideRowsFrom(int index)
    {
        for (int i = index; i < rowViews.Count; i++)
            rowViews[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Forwards a base-row selection through the typed list callback.
    /// </summary>
    /// <param name="row">The selected row.</param>
    /// <param name="eventData">The pointer or navigation event.</param>
    private void HandleRowSelected(SelectableListRowView row, PointerEventData eventData)
    {
        if (row is TRowView typedRow)
            rowSelected?.Invoke(typedRow, eventData);
    }

    /// <summary>
    /// Forwards a base-row activation through the typed list callback.
    /// </summary>
    /// <param name="row">The activated row.</param>
    /// <param name="eventData">The pointer or navigation event.</param>
    private void HandleRowActivated(SelectableListRowView row, PointerEventData eventData)
    {
        if (row is TRowView typedRow)
            rowActivated?.Invoke(typedRow, eventData);
    }

    /// <summary>
    /// Forwards a base-row context request through the typed list callback.
    /// </summary>
    /// <param name="row">The requesting row.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleRowContextRequested(SelectableListRowView row, PointerEventData eventData)
    {
        if (row is TRowView typedRow)
            rowContextRequested?.Invoke(typedRow, eventData);
    }

    /// <summary>
    /// Reveals the selected row and restores keyboard focus within the configured scope.
    /// </summary>
    /// <param name="rows">The projected rows.</param>
    /// <param name="rowPitch">The vertical distance between row origins.</param>
    /// <param name="isSelected">Determines whether a projected row is selected.</param>
    private void RevealAndFocusSelectedRow(
        IReadOnlyList<TRowData> rows,
        int rowPitch,
        Func<TRowData, int, bool> isSelected
    )
    {
        if (isSelected == null)
            return;

        if (!CanNavigate())
            return;

        int count = Math.Min(rows.Count, rowViews.Count);
        for (int i = 0; i < count; i++)
        {
            if (!isSelected(rows[i], i))
                continue;

            TRowView row = rowViews[i];
            if (row.gameObject.activeInHierarchy)
            {
                scrollArea.RevealContentRect(i * rowPitch, rowPitch);
                SelectableListRowView.FocusRowForNavigation(navigationScope, true, row);
            }
            return;
        }
    }

    /// <summary>
    /// Evaluates the optional keyboard-navigation gate.
    /// </summary>
    /// <returns>True when keyboard navigation is permitted.</returns>
    private bool CanNavigate()
    {
        return canNavigate?.Invoke() != false;
    }

    /// <summary>
    /// Applies fixed top-left row geometry inside the content root.
    /// </summary>
    /// <param name="rectTransform">The row transform.</param>
    /// <param name="x">The left coordinate.</param>
    /// <param name="y">The top coordinate.</param>
    /// <param name="width">The row width.</param>
    /// <param name="height">The row height.</param>
    private static void SetTopLeftRect(
        RectTransform rectTransform,
        int x,
        int y,
        int width,
        int height
    )
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = new Vector2(x, -y);
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }
}
