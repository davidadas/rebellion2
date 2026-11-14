using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Presents bundled legal documents for agreement acceptance, multiplayer entry, or later reading.
/// </summary>
public sealed class LegalDocumentsView : MonoBehaviour, ICancelable
{
    [SerializeField]
    private TextAsset _agreementText;

    [SerializeField]
    private TextAsset _privacyNoticeText;

    [SerializeField]
    private TMP_Text _heading;

    [SerializeField]
    private TMP_Text _body;

    [SerializeField]
    private TMP_Text _status;

    [SerializeField]
    private ScrollAreaView _scrollArea;

    [SerializeField]
    private Button _agreementButton;

    [SerializeField]
    private Button _privacyButton;

    [SerializeField]
    private Button _acceptButton;

    [SerializeField]
    private Button _closeButton;

    [SerializeField]
    private TMP_Text _closeLabel;

    private PlayerAgreement _agreement;
    private GameObject _previousSelection;
    private DocumentMode _mode;
    private bool _agreementAccepted;
    private bool _privacyAccepted;
    private bool _showingPrivacy;

    private enum DocumentMode
    {
        ReadOnly,
        Agreement,
        Multiplayer,
    }

    public bool IsVisible => gameObject.activeSelf;
    public PlayerAgreement Agreement =>
        _agreement ??= new PlayerAgreement(_agreementText.text, _privacyNoticeText.text);

    public event Action Accepted;
    public event Action QuitRequested;
    public event Action MultiplayerAccepted;
    public event Action Closed;

    /// <summary>
    /// Binds document navigation and explicit acceptance controls while the panel is visible.
    /// </summary>
    private void OnEnable()
    {
        _agreementButton.onClick.AddListener(ShowAgreement);
        _privacyButton.onClick.AddListener(ShowPrivacyNotice);
        _acceptButton.onClick.AddListener(HandleAccepted);
        _closeButton.onClick.AddListener(HandleClosed);
    }

    /// <summary>
    /// Removes listeners and restores the previous menu selection when the panel closes.
    /// </summary>
    private void OnDisable()
    {
        _agreementButton.onClick.RemoveListener(ShowAgreement);
        _privacyButton.onClick.RemoveListener(ShowPrivacyNotice);
        _acceptButton.onClick.RemoveListener(HandleAccepted);
        _closeButton.onClick.RemoveListener(HandleClosed);
        EventSystem.current?.SetSelectedGameObject(_previousSelection);
        _previousSelection = null;
    }

    /// <summary>
    /// Keeps keyboard and gamepad selection within the active document overlay.
    /// </summary>
    private void LateUpdate()
    {
        EventSystem events = EventSystem.current;
        if (events == null)
            return;
        GameObject selected = events.currentSelectedGameObject;
        if (selected?.transform.IsChildOf(transform) != true)
            events.SetSelectedGameObject(_agreementButton.gameObject);
    }

    /// <summary>
    /// Opens the documents without treating opening or reading them as acceptance.
    /// </summary>
    /// <param name="requireAcceptance">Whether the player must accept or quit.</param>
    public void Show(bool requireAcceptance)
    {
        Show(requireAcceptance ? DocumentMode.Agreement : DocumentMode.ReadOnly);
    }

    /// <summary>
    /// Opens the multiplayer entry prompt with separate agreement to both documents.
    /// </summary>
    public void ShowMultiplayerAgreement()
    {
        Show(DocumentMode.Multiplayer);
    }

    /// <summary>
    /// Configures the shared document reader for its current entry point.
    /// </summary>
    /// <param name="mode">The document presentation and available actions.</param>
    private void Show(DocumentMode mode)
    {
        if (!IsVisible)
            _previousSelection = EventSystem.current?.currentSelectedGameObject;
        _mode = mode;
        _agreementAccepted = false;
        _privacyAccepted = false;
        gameObject.SetActive(true);
        _acceptButton.gameObject.SetActive(mode != DocumentMode.ReadOnly);
        _closeLabel.text = mode switch
        {
            DocumentMode.Agreement => "QUIT",
            DocumentMode.Multiplayer => "BACK",
            _ => "CLOSE",
        };
        _status.text =
            mode == DocumentMode.ReadOnly ? string.Empty : "Read each document and select Agree.";
        ShowAgreement();
        EventSystem.current?.SetSelectedGameObject(_agreementButton.gameObject);
    }

    /// <summary>
    /// Removes the document overlay after acceptance is saved or reading is finished.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Keeps a failed acceptance visible so the player can retry or quit.
    /// </summary>
    /// <param name="message">The local persistence failure to display.</param>
    public void ShowError(string message)
    {
        _status.text = message;
    }

    /// <summary>
    /// Closes a reading session and consumes cancellation without bypassing required acceptance.
    /// </summary>
    /// <returns>Whether the document panel consumed cancellation.</returns>
    public bool TryCancel()
    {
        if (!IsVisible)
            return false;
        if (_mode != DocumentMode.Agreement)
            HandleClosed();
        return true;
    }

    /// <summary>
    /// Displays the agreement and resets scrolling to its beginning.
    /// </summary>
    private void ShowAgreement()
    {
        ShowDocument("PLAYER AGREEMENT", Agreement.Text, false);
    }

    /// <summary>
    /// Displays the privacy notice without changing any optional consent choice.
    /// </summary>
    private void ShowPrivacyNotice()
    {
        ShowDocument("PRIVACY NOTICE", Agreement.PrivacyNotice, true);
    }

    /// <summary>
    /// Sizes the shared scroll area to the selected document's wrapped text.
    /// </summary>
    /// <param name="heading">The document heading.</param>
    /// <param name="text">The complete document text.</param>
    /// <param name="privacy">Whether the privacy notice is selected.</param>
    private void ShowDocument(string heading, string text, bool privacy)
    {
        _showingPrivacy = privacy;
        bool agreed = privacy ? _privacyAccepted : _agreementAccepted;
        bool bothAgreed = _agreementAccepted && _privacyAccepted;
        _acceptButton.interactable = !agreed || bothAgreed;
        _acceptButton.GetComponentInChildren<TMP_Text>(true).text =
            agreed && !bothAgreed ? "AGREED" : "AGREE";
        _heading.text = heading;
        _body.text = text;
        float width = _scrollArea.ViewportWidth;
        float height = _body.GetPreferredValues(text, width, 0f).y;
        _body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        _body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        _scrollArea.SetContentHeight(height, _body.fontSize * 3f, true);
        Color active = new Color(0.373f, 0.659f, 0.925f);
        Color inactive = new Color(0.875f, 0.910f, 0.941f);
        _agreementButton.GetComponentInChildren<TMP_Text>().color = privacy ? inactive : active;
        _privacyButton.GetComponentInChildren<TMP_Text>().color = privacy ? active : inactive;
    }

    /// <summary>
    /// Requires a separate agreement to each page before completing the current entry flow.
    /// </summary>
    private void HandleAccepted()
    {
        if (!IsVisible || _mode == DocumentMode.ReadOnly)
            return;
        if (_showingPrivacy)
            _privacyAccepted = true;
        else
            _agreementAccepted = true;
        if (!_agreementAccepted)
        {
            ShowAgreement();
            return;
        }
        if (!_privacyAccepted)
        {
            ShowPrivacyNotice();
            return;
        }
        if (_mode == DocumentMode.Agreement)
            Accepted?.Invoke();
        else if (_mode == DocumentMode.Multiplayer)
            MultiplayerAccepted?.Invoke();
    }

    /// <summary>
    /// Quits when acceptance is required and reports closure of reading or multiplayer entry.
    /// </summary>
    private void HandleClosed()
    {
        if (!IsVisible)
            return;
        if (_mode == DocumentMode.Agreement)
            QuitRequested?.Invoke();
        else
        {
            Hide();
            Closed?.Invoke();
        }
    }
}
