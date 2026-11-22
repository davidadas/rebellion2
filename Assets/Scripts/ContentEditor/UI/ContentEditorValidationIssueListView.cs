using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presents pack validation failures inside the existing operation notice.
/// </summary>
public sealed class ContentEditorValidationIssueListView : MonoBehaviour
{
    private const int _maximumVisibleRows = 3;
    private const int _rowHeight = 72;

    [SerializeField]
    private ScrollAreaLayout scroll;

    [SerializeField]
    private ContentEditorValidationIssueView rowTemplate;

    private readonly List<ContentEditorValidationIssueView> rows = new();
    private Action<ContentEditorValidationIssue> selected;

    internal bool IsExpanded => gameObject.activeSelf;
    internal float VisibleHeight => ((RectTransform)transform).rect.height;

    /// <summary>
    /// Shows every validation issue while limiting the notice to three visible rows.
    /// </summary>
    /// <param name="issues">The rejected authored fields.</param>
    /// <param name="selection">The action invoked when an issue is selected.</param>
    internal void Show(
        IReadOnlyList<ContentEditorValidationIssue> issues,
        Action<ContentEditorValidationIssue> selection
    )
    {
        if (issues == null)
            throw new ArgumentNullException(nameof(issues));
        if (issues.Count == 0)
            throw new ArgumentException(
                "At least one validation issue is required.",
                nameof(issues)
            );
        if (scroll == null || rowTemplate == null)
            throw new MissingReferenceException(
                name + " has incomplete validation list references."
            );

        selected = selection ?? throw new ArgumentNullException(nameof(selection));
        gameObject.SetActive(true);
        RectTransform listTransform = (RectTransform)transform;
        int visibleRows = Mathf.Min(_maximumVisibleRows, issues.Count);
        listTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            visibleRows * _rowHeight
        );
        listTransform.ForceUpdateRectTransforms();
        ((RectTransform)scroll.transform).ForceUpdateRectTransforms();
        scroll.SetContentHeight(issues.Count * _rowHeight, true);

        foreach (ContentEditorValidationIssueView row in rows)
            row.gameObject.SetActive(false);
        for (int index = 0; index < issues.Count; index++)
        {
            if (rows.Count <= index)
                rows.Add(Instantiate(rowTemplate, scroll.ScrollArea.ContentRoot));
            ContentEditorValidationIssueView row = rows[index];
            UILayout.SetSourceRect(
                (RectTransform)row.transform,
                0,
                index * _rowHeight,
                Mathf.FloorToInt(scroll.ScrollArea.ViewportWidth),
                _rowHeight
            );
            row.Render(issues[index], Select);
        }
    }

    /// <summary>
    /// Hides the validation rows and releases their selection callback.
    /// </summary>
    internal void Hide()
    {
        selected = null;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Expands or collapses the rendered validation rows without discarding their navigation state.
    /// </summary>
    /// <param name="expanded">Whether the rendered validation rows should be visible.</param>
    internal void SetExpanded(bool expanded)
    {
        if (expanded && selected == null)
            throw new InvalidOperationException(
                "Validation issues must be rendered before expansion."
            );
        gameObject.SetActive(expanded);
    }

    /// <summary>
    /// Forwards the selected issue without dismissing the remaining validation results.
    /// </summary>
    /// <param name="issue">The selected validation issue.</param>
    private void Select(ContentEditorValidationIssue issue)
    {
        selected?.Invoke(issue);
    }
}
