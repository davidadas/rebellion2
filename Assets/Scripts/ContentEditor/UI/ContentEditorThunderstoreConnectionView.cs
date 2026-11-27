using System;
using System.Diagnostics.CodeAnalysis;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents user-initiated Thunderstore connection setup independently of local projects.
/// </summary>
public sealed class ContentEditorThunderstoreConnectionView : MonoBehaviour, ICancelable
{
    [SerializeField]
    private Button _connectButton;

    [SerializeField]
    private Button _disconnectButton;

    [SerializeField]
    private Button _tokenHelpButton;

    [SerializeField]
    private Button _connectionCloseButton;

    [SerializeField]
    private TMP_InputField _token;

    [SerializeField]
    private TextMeshProUGUI _connectionStatus;

    [SerializeField]
    private RectTransform _panel;

    [SerializeField]
    private TMP_Text _heading;

    [SerializeField]
    private GameObject _setup;

    [SerializeField]
    private GameObject _connected;

    [SerializeField]
    private TMP_Text _connectedTeams;

    private bool bound;
    private bool busy;
    internal bool IsOpen => gameObject.activeSelf;
    internal event Action<string> ConnectRequested;
    internal event Action DisconnectRequested;
    internal event Action TokenHelpRequested;
    internal event Action CancelRequested;

    /// <summary>
    /// Connects the authored controls when the dialog first becomes active.
    /// </summary>
    private void Awake() => Bind();

    /// <summary>
    /// Keeps account controls inside a resized window.
    /// </summary>
    [SuppressMessage("Roslynator", "RCS1213", Justification = "Invoked by Unity.")]
    private void OnRectTransformDimensionsChange()
    {
        if (_panel != null)
            UILayout.FitPanel(_panel);
    }

    /// <summary>
    /// Connects authored controls once for runtime and preview presentation.
    /// </summary>
    private void Bind()
    {
        if (bound)
            return;
        if (
            _connectButton == null
            || _disconnectButton == null
            || _tokenHelpButton == null
            || _connectionCloseButton == null
            || _token == null
            || _connectionStatus == null
            || _panel == null
            || _heading == null
            || _setup == null
            || _connected == null
            || _connectedTeams == null
        )
            throw new MissingReferenceException(
                name + " has incomplete Thunderstore connection controls."
            );
        _connectButton.onClick.AddListener(Connect);
        _disconnectButton.onClick.AddListener(Disconnect);
        _tokenHelpButton.onClick.AddListener(OpenTokenHelp);
        _connectionCloseButton.onClick.AddListener(Close);
        bound = true;
    }

    /// <summary>
    /// Opens account setup without opening or modifying a content pack.
    /// </summary>
    internal void Show()
    {
        Bind();
        ShowStatus(string.Empty);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    /// <summary>
    /// Opens account controls for setup or management after verifying the connection.
    /// </summary>
    /// <param name="teams">The teams verified for this connection.</param>
    /// <param name="manageAccount">Whether the request started from an already connected account.</param>
    internal void ShowAfterVerification(string[] teams, bool manageAccount)
    {
        bool connected = teams.Length > 0;
        if (connected && !manageAccount)
            return;
        Show();
        RenderConnection(teams);
        if (!connected)
            ShowStatus("Paste a token to connect this device.");
    }

    /// <summary>
    /// Clears the masked field and its undo history when dismissing account setup.
    /// </summary>
    internal void Hide()
    {
        ClearToken();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Shows current team access and whether the saved credential may be removed.
    /// </summary>
    /// <param name="teams">The currently authenticated teams.</param>
    /// <param name="canForgetSavedToken">Whether an unsuccessful restore may have retained a token.</param>
    internal void RenderConnection(string[] teams, bool canForgetSavedToken = false)
    {
        Bind();
        bool connected = teams.Length > 0;
        _connectedTeams.text = "Connected as " + string.Join(", ", teams);
        RenderLayout(connected);
        _disconnectButton.gameObject.SetActive(connected || canForgetSavedToken);
        if (connected)
        {
            ClearToken();
            ShowStatus(string.Empty);
        }
    }

    /// <summary>
    /// Shows a compact account summary when connected and the full setup guide only when needed.
    /// </summary>
    /// <param name="connected">Whether the account is currently connected.</param>
    private void RenderLayout(bool connected)
    {
        _setup.SetActive(!connected);
        _connected.SetActive(connected);
        _connectButton.gameObject.SetActive(!connected);
        _heading.text = connected ? "Thunderstore" : "Connect Thunderstore";
        _heading.fontSize = connected ? 22 : 24;
        if (connected)
        {
            RenderAccountLayout();
            return;
        }

        _panel.sizeDelta = new Vector2(1040, 740);
        UILayout.FitPanel(_panel);
        UILayout.SetSourceRect(_heading.rectTransform, 28, 20, 984, 36);
        UILayout.SetSourceRect(_connectionStatus.rectTransform, 28, 656, 680, 60);
        UILayout.SetSourceRect((RectTransform)_connectionCloseButton.transform, 744, 680, 120, 36);
        UILayout.SetSourceRect((RectTransform)_disconnectButton.transform, 876, 604, 136, 32);
    }

    /// <summary>
    /// Fits the connected account and any operation message into a compact, padded dialog.
    /// </summary>
    private void RenderAccountLayout()
    {
        int accountHeight = Mathf.Max(
            28,
            Mathf.CeilToInt(
                _connectedTeams
                    .GetPreferredValues(_connectedTeams.text, 432, float.PositiveInfinity)
                    .y
            )
        );
        UILayout.SetSourceRect(_heading.rectTransform, 24, 20, 432, 28);
        UILayout.SetSourceRect((RectTransform)_connected.transform, 24, 64, 432, accountHeight);
        UILayout.SetSourceRect(_connectedTeams.rectTransform, 0, 0, 432, accountHeight);
        int footerTop = 64 + accountHeight + 24;
        if (!string.IsNullOrEmpty(_connectionStatus.text))
        {
            int statusHeight = Mathf.Max(
                36,
                Mathf.CeilToInt(
                    _connectionStatus
                        .GetPreferredValues(_connectionStatus.text, 432, float.PositiveInfinity)
                        .y
                )
            );
            UILayout.SetSourceRect(
                _connectionStatus.rectTransform,
                24,
                64 + accountHeight + 12,
                432,
                statusHeight
            );
            footerTop += statusHeight + 12;
        }
        _panel.sizeDelta = new Vector2(480, footerTop + 60);
        UILayout.FitPanel(_panel);
        UILayout.SetSourceRect(
            (RectTransform)_connectionCloseButton.transform,
            336,
            footerTop,
            120,
            36
        );
        UILayout.SetSourceRect((RectTransform)_disconnectButton.transform, 200, footerTop, 120, 36);
    }

    /// <summary>
    /// Keeps account errors inside the visible connection workflow.
    /// </summary>
    /// <param name="message">The operation result or required action.</param>
    /// <param name="isError">Whether the operation failed.</param>
    internal void ShowStatus(string message, bool isError = false)
    {
        _connectionStatus.text = message;
        _connectionStatus
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(isError ? ContentEditorColorRole.Error : ContentEditorColorRole.Text);
        if (_connected.activeSelf)
            RenderAccountLayout();
    }

    /// <summary>
    /// Protects credentials and connection actions while keeping cancellation available.
    /// </summary>
    /// <param name="value">Whether an account operation is active.</param>
    internal void SetBusy(bool value)
    {
        busy = value;
        _connectButton.interactable = !busy;
        _disconnectButton.interactable = !busy;
        _token.interactable = !busy;
        _connectionCloseButton.GetComponentInChildren<TMP_Text>(true).text = busy
            ? "Cancel"
            : "Close";
    }

    /// <summary>
    /// Dismisses this dialog before the workspace handles Escape.
    /// </summary>
    /// <returns>Whether the dialog consumed cancellation.</returns>
    public bool TryCancel()
    {
        if (!IsOpen)
            return false;
        Close();
        return true;
    }

    /// <summary>
    /// Requests authentication with the masked value without persisting it in the view.
    /// </summary>
    private void Connect() => ConnectRequested?.Invoke(_token.text);

    /// <summary>
    /// Requests removal of the protected credential through its application owner.
    /// </summary>
    private void Disconnect() => DisconnectRequested?.Invoke();

    /// <summary>
    /// Opens the token-creation instructions on Thunderstore.
    /// </summary>
    private void OpenTokenHelp() => TokenHelpRequested?.Invoke();

    /// <summary>
    /// Cancels the active account operation or closes the account dialog.
    /// </summary>
    private void Close()
    {
        if (busy)
            CancelRequested?.Invoke();
        else
            Hide();
    }

    /// <summary>
    /// Releases sensitive input and its local undo history.
    /// </summary>
    private void ClearToken()
    {
        _token.SetTextWithoutNotify(string.Empty);
        _token.GetComponent<TextInputHistory>()?.ClearHistory();
    }

    /// <summary>
    /// Releases the view's control subscriptions and masked input.
    /// </summary>
    private void OnDestroy()
    {
        if (!bound)
            return;
        _connectButton.onClick.RemoveListener(Connect);
        _disconnectButton.onClick.RemoveListener(Disconnect);
        _tokenHelpButton.onClick.RemoveListener(OpenTokenHelp);
        _connectionCloseButton.onClick.RemoveListener(Close);
        ClearToken();
    }
}
