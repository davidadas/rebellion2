using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns scoped XML drafts, text history and scrolling without applying project changes.
/// </summary>
public sealed class ContentEditorXmlView : MonoBehaviour
{
    [SerializeField]
    private TMP_InputField input;

    [SerializeField]
    private TMP_Dropdown scopeDropdown;

    [SerializeField]
    private Button applyButton;

    [SerializeField]
    private Button discardButton;

    private IReadOnlyList<ContentEditorXmlScope> scopes = Array.Empty<ContentEditorXmlScope>();
    private bool bound;
    private float headerOffset = 136;
    public event Action ApplyRequested;
    public event Action DiscardRequested;
    public event Action<int> ScopeRequested;
    public int ScopeIndex { get; private set; }
    public int ScopeCount => scopes.Count;
    public ContentEditorXmlScope Scope => scopes.ElementAtOrDefault(ScopeIndex);
    public string Text => input.text;
    public bool HasDraft => gameObject.activeSelf && input.text != (Scope?.Text ?? string.Empty);
    public bool HasOpenChoice => scopeDropdown.IsExpanded;
    public TextInputHistory PendingTextHistory =>
        HasDraft ? input.GetComponent<TextInputHistory>() : null;

    /// <summary>
    /// Connects authored controls before the first draft is displayed.
    /// </summary>
    private void Awake() => BindControls();

    /// <summary>
    /// Verifies references and connects one listener per control.
    /// </summary>
    private void BindControls()
    {
        if (bound)
            return;
        if (input == null || scopeDropdown == null || applyButton == null || discardButton == null)
            throw new MissingReferenceException(name + " has incomplete XML editor references.");
        bound = true;
        applyButton.onClick.AddListener(RequestApply);
        discardButton.onClick.AddListener(RequestDiscard);
        scopeDropdown.onValueChanged.AddListener(RequestScope);
        input.onValueChanged.AddListener(HandleDraftChanged);
    }

    /// <summary>
    /// Releases authored control subscriptions.
    /// </summary>
    private void OnDestroy()
    {
        if (!bound)
            return;
        applyButton.onClick.RemoveListener(RequestApply);
        discardButton.onClick.RemoveListener(RequestDiscard);
        scopeDropdown.onValueChanged.RemoveListener(RequestScope);
        input.onValueChanged.RemoveListener(HandleDraftChanged);
    }

    /// <summary>
    /// Replaces the editable scopes after the workspace accepts a selection change.
    /// </summary>
    /// <param name="record">The selected definition.</param>
    /// <param name="fields">The fields represented by a combined inspector page.</param>
    public void Bind(ContentPackEditorRecord record, IReadOnlyList<ContentPackEditorField> fields)
    {
        BindControls();
        scopes = ContentEditorXmlScope.Create(record, fields);
        ScopeIndex = 0;
        scopeDropdown.ClearOptions();
        scopeDropdown.AddOptions(scopes.Select(scope => scope.Caption).ToList());
        scopeDropdown.SetValueWithoutNotify(0);
        ResetDraft();
        Hide();
    }

    /// <summary>
    /// Shows the draft with a toolbar only when several blocks are available.
    /// </summary>
    /// <param name="eventConfiguration">Whether XML is the object's primary editor.</param>
    public void Show(bool eventConfiguration = false)
    {
        scopeDropdown.gameObject.SetActive(!eventConfiguration && scopes.Count > 1);
        ((RectTransform)input.transform).offsetMax = new Vector2(
            0,
            eventConfiguration ? -88 : -(headerOffset + (scopes.Count > 1 ? 42 : 0))
        );
        input.gameObject.SetActive(true);
        applyButton.gameObject.SetActive(true);
        discardButton.gameObject.SetActive(true);
        gameObject.SetActive(true);
        ResetDraft();
    }

    /// <summary>
    /// Keeps the editable block below all rows of the inspector's tab bar.
    /// </summary>
    /// <param name="offset">The bottom of the inspector header.</param>
    public void SetHeaderOffset(float offset)
    {
        headerOffset = offset;
        RectTransform selector = (RectTransform)scopeDropdown.transform;
        selector.offsetMin = new Vector2(0, -offset - 32);
        selector.offsetMax = new Vector2(0, -offset);
        if (gameObject.activeSelf)
            ((RectTransform)input.transform).offsetMax = new Vector2(
                0,
                -(offset + (scopes.Count > 1 ? 42 : 0))
            );
    }

    /// <summary>
    /// Enables draft actions only while an edited block differs from its accepted contents.
    /// </summary>
    /// <param name="text">The current XML draft.</param>
    private void HandleDraftChanged(string text)
    {
        bool changed = text != (Scope?.Text ?? string.Empty);
        applyButton.interactable = changed;
        discardButton.interactable = changed;
    }

    /// <summary>
    /// Hides the editing surface without changing its committed XML.
    /// </summary>
    public void Hide() => gameObject.SetActive(false);

    /// <summary>
    /// Releases project-owned scopes before their project is closed.
    /// </summary>
    public void Clear()
    {
        scopes = Array.Empty<ContentEditorXmlScope>();
        ScopeIndex = 0;
        scopeDropdown.ClearOptions();
        ResetDraft();
        Hide();
    }

    /// <summary>
    /// Restores the committed block and resets only that block's draft history.
    /// </summary>
    public void ResetDraft()
    {
        input.SetTextWithoutNotify(Scope?.Text ?? string.Empty);
        HandleDraftChanged(input.text);
        input.GetComponent<TextInputHistory>()?.ClearHistory();
        if (!gameObject.activeInHierarchy)
            return;
        input.textComponent.ForceMeshUpdate();
        input.verticalScrollbar.value = 0;
        input.verticalScrollbar.onValueChanged.Invoke(0);
    }

    /// <summary>
    /// Selects a block after pending changes have been handled by the workspace.
    /// </summary>
    /// <param name="index">The selected block.</param>
    public void SelectScope(int index)
    {
        if (index < 0 || index >= scopes.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        ScopeIndex = index;
        scopeDropdown.SetValueWithoutNotify(index);
        ResetDraft();
    }

    /// <summary>
    /// Requests applying a draft through the workspace's project boundary.
    /// </summary>
    private void RequestApply() => ApplyRequested?.Invoke();

    /// <summary>
    /// Requests confirmation before discarding a draft.
    /// </summary>
    private void RequestDiscard() => DiscardRequested?.Invoke();

    /// <summary>
    /// Requests a context change while preserving the currently committed selection.
    /// </summary>
    /// <param name="index">The proposed block.</param>
    private void RequestScope(int index)
    {
        scopeDropdown.SetValueWithoutNotify(ScopeIndex);
        ScopeRequested?.Invoke(index);
    }
}
