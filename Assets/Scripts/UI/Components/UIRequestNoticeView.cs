using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents a compact notice with optional authored accept and decline controls.
/// </summary>
public sealed class UIRequestNoticeView : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI messageText;

    [SerializeField]
    private Button acceptButton;

    [SerializeField]
    private Button declineButton;

    public event Action<bool> Responded;

    /// <summary>
    /// Binds the two semantic response controls when the authored notice is created.
    /// </summary>
    private void Awake()
    {
        messageText.overflowMode = TextOverflowModes.Ellipsis;
        acceptButton.onClick.AddListener(Accept);
        declineButton.onClick.AddListener(Decline);
    }

    /// <summary>
    /// Releases the response controls when the notice is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        acceptButton.onClick.RemoveListener(Accept);
        declineButton.onClick.RemoveListener(Decline);
    }

    /// <summary>
    /// Displays the message and exposes response controls only for an actionable request.
    /// </summary>
    /// <param name="message">The notice text, or empty to hide the notice.</param>
    /// <param name="color">The faction's text color.</param>
    /// <param name="canRespond">Whether both response controls should be visible.</param>
    public void Render(string message, Color color, bool canRespond)
    {
        messageText.text = message ?? string.Empty;
        messageText.color = color;
        acceptButton.gameObject.SetActive(canRespond);
        declineButton.gameObject.SetActive(canRespond);
        gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    /// <summary>
    /// Accepts the request currently owned by the presenting controller.
    /// </summary>
    private void Accept() => Responded?.Invoke(true);

    /// <summary>
    /// Declines the request currently owned by the presenting controller.
    /// </summary>
    private void Decline() => Responded?.Invoke(false);
}
