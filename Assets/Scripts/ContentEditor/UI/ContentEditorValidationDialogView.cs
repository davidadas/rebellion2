using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents every structural validation failure from one pack-wide pass.
/// </summary>
public sealed class ContentEditorValidationDialogView : MonoBehaviour, ICancelable
{
    [SerializeField]
    private TMP_Text heading;

    [SerializeField]
    private TMP_Text summary;

    [SerializeField]
    private ScrollAreaLayout scroll;

    [SerializeField]
    private ContentEditorValidationIssueView rowTemplate;

    [SerializeField]
    private Button closeButton;

    private readonly List<ContentEditorValidationIssueView> rows = new();
    private Action<ContentEditorValidationIssue> selected;
    private bool bound;

    public bool IsOpen => gameObject.activeSelf;

    /// <summary>
    /// Binds the authored close control when the dialog is loaded.
    /// </summary>
    private void Awake() => Bind();

    /// <summary>
    /// Removes the dialog-owned close listener.
    /// </summary>
    private void OnDestroy()
    {
        if (bound)
            closeButton.onClick.RemoveListener(Hide);
    }

    /// <summary>
    /// Opens a scrollable validation report with one navigable row per rejected field.
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
        Bind();
        selected = selection ?? throw new ArgumentNullException(nameof(selection));
        heading.text =
            issues.Count == 1 ? "1 validation error" : $"{issues.Count} validation errors";
        summary.text = "Select an error to open the field that needs attention.";
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
                index * 76,
                Mathf.FloorToInt(scroll.ScrollArea.ViewportWidth),
                76
            );
            row.Render(issues[index], Select);
        }
        scroll.SetContentHeight(issues.Count * 76, true);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        closeButton.Select();
    }

    /// <summary>
    /// Dismisses the report and releases its selection callback.
    /// </summary>
    public void Hide()
    {
        selected = null;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Dismisses an open report when the application routes cancellation to it.
    /// </summary>
    /// <returns>Whether an open report was dismissed.</returns>
    public bool TryCancel()
    {
        if (!IsOpen)
            return false;
        Hide();
        return true;
    }

    /// <summary>
    /// Connects required authored references exactly once.
    /// </summary>
    private void Bind()
    {
        if (bound)
            return;
        if (
            heading == null
            || summary == null
            || scroll == null
            || rowTemplate == null
            || closeButton == null
        )
            throw new MissingReferenceException(
                name + " has incomplete validation dialog references."
            );
        closeButton.onClick.AddListener(Hide);
        bound = true;
    }

    /// <summary>
    /// Closes the report before forwarding its selected issue.
    /// </summary>
    /// <param name="issue">The selected validation issue.</param>
    private void Select(ContentEditorValidationIssue issue)
    {
        Action<ContentEditorValidationIssue> callback = selected;
        Hide();
        callback?.Invoke(issue);
    }
}
