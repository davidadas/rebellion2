using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents one pack validation failure with navigation to its authored field.
/// </summary>
public sealed class ContentEditorValidationIssueView : MonoBehaviour
{
    [SerializeField]
    private Button openButton;

    [SerializeField]
    private TMP_Text message;

    private ContentEditorValidationIssue issue;
    private Action<ContentEditorValidationIssue> selected;
    private bool bound;

    /// <summary>
    /// Binds the authored navigation control when the row is loaded.
    /// </summary>
    private void Awake() => Bind();

    /// <summary>
    /// Removes the row-owned navigation listener.
    /// </summary>
    private void OnDestroy()
    {
        if (bound)
            openButton.onClick.RemoveListener(Select);
    }

    /// <summary>
    /// Displays one validation issue without retaining callbacks from a previous result.
    /// </summary>
    /// <param name="value">The issue to present.</param>
    /// <param name="selection">The action invoked when its field is requested.</param>
    internal void Render(
        ContentEditorValidationIssue value,
        Action<ContentEditorValidationIssue> selection
    )
    {
        Bind();
        issue = value ?? throw new ArgumentNullException(nameof(value));
        selected = selection ?? throw new ArgumentNullException(nameof(selection));
        message.text = value.Message;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Connects required authored references exactly once.
    /// </summary>
    private void Bind()
    {
        if (bound)
            return;
        if (openButton == null || message == null)
            throw new MissingReferenceException(
                name + " has incomplete validation issue references."
            );
        openButton.onClick.AddListener(Select);
        bound = true;
    }

    /// <summary>
    /// Requests navigation to this row's authored field.
    /// </summary>
    private void Select() => selected?.Invoke(issue);
}
