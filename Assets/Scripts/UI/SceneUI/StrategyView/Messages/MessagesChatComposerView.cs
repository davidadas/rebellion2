using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders a multiline player-message editor within the Messages window.
/// </summary>
public sealed class MessagesChatComposerView : MonoBehaviour
{
    [SerializeField]
    private TMP_InputField draftInput;

    [SerializeField]
    private Button sendButton;

    [SerializeField]
    private Button cancelButton;

    [SerializeField]
    private TMP_Text statusText;

    private bool canSend;
    private bool initialized;
    private bool sending;

    /// <summary>
    /// Raised when the player changes the unmodified draft.
    /// </summary>
    public event Action<string> DraftChanged;

    /// <summary>
    /// Raised when the player submits a nonempty draft.
    /// </summary>
    public event Action SendRequested;

    /// <summary>
    /// Raised when the player discards the draft and returns to the index.
    /// </summary>
    public event Action CancelRequested;

    /// <summary>
    /// Displays the current draft without resetting the caret during routine window refreshes.
    /// </summary>
    /// <param name="data">The projected composer state.</param>
    public void Render(MessagesChatComposerRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        EnsureInitialized();
        bool opening = !gameObject.activeSelf;
        canSend = data.CanSend;
        sending = data.Sending;
        if (draftInput.text != data.Draft)
            draftInput.SetTextWithoutNotify(data.Draft);
        draftInput.readOnly = sending;
        statusText.text = data.Status;
        UpdateSendAvailability();
        gameObject.SetActive(true);
        if (opening)
            draftInput.ActivateInputField();
    }

    /// <summary>
    /// Hides the editor without submitting or mutating the controller-owned draft.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Binds the authored controls once.
    /// </summary>
    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// Releases control subscriptions when the view is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (!initialized)
            return;
        draftInput.onValueChanged.RemoveListener(HandleDraftChanged);
        sendButton.onClick.RemoveListener(HandleSendRequested);
        cancelButton.onClick.RemoveListener(HandleCancelRequested);
    }

    /// <summary>
    /// Verifies editor references and binds each control exactly once.
    /// </summary>
    private void EnsureInitialized()
    {
        if (initialized)
            return;
        if (draftInput == null || sendButton == null || cancelButton == null || statusText == null)
            throw new MissingReferenceException($"{name} is missing a chat composer control.");

        draftInput.lineType = TMP_InputField.LineType.MultiLineNewline;
        draftInput.characterLimit = 0;
        draftInput.richText = false;
        draftInput.restoreOriginalTextOnEscape = false;
        draftInput.onValueChanged.AddListener(HandleDraftChanged);
        sendButton.onClick.AddListener(HandleSendRequested);
        cancelButton.onClick.AddListener(HandleCancelRequested);
        initialized = true;
    }

    /// <summary>
    /// Preserves entered whitespace while updating the submission control.
    /// </summary>
    /// <param name="rawText">The current multiline input.</param>
    private void HandleDraftChanged(string rawText)
    {
        UpdateSendAvailability();
        DraftChanged?.Invoke(rawText);
    }

    /// <summary>
    /// Enables submission only when the session is available and the draft is nonempty.
    /// </summary>
    private void UpdateSendAvailability()
    {
        sendButton.interactable = canSend && !sending && draftInput.text.Length != 0;
    }

    /// <summary>
    /// Emits a submission request only while the visible control is enabled.
    /// </summary>
    private void HandleSendRequested()
    {
        if (sendButton.interactable)
            SendRequested?.Invoke();
    }

    /// <summary>
    /// Emits the semantic cancellation request.
    /// </summary>
    private void HandleCancelRequested()
    {
        CancelRequested?.Invoke();
    }
}
