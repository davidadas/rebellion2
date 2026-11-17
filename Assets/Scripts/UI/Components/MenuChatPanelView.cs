using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the reusable menu conversation surface, transcript scrolling, and message-entry geometry.
/// </summary>
public sealed class MenuChatPanelView : MonoBehaviour
{
    [SerializeField]
    private TMP_Text heading;

    [SerializeField]
    private TMP_Text transcript;

    [SerializeField]
    private ScrollAreaView scrollArea;

    [SerializeField]
    private TMP_InputField messageInput;

    [SerializeField]
    private Button sendButton;

    public TMP_Text Transcript => transcript;
    public ScrollAreaView ScrollArea => scrollArea;
    public TMP_InputField MessageInput => messageInput;
    public Button SendButton => sendButton;

    /// <summary>
    /// Updates the conversation heading without changing its layout or transcript.
    /// </summary>
    /// <param name="title">The current conversation title and connection information.</param>
    public void SetTitle(string title)
    {
        heading.text = title;
    }

    /// <summary>
    /// Fits the same conversation controls to a browser, lobby, or another menu host.
    /// </summary>
    /// <param name="title">The conversation heading.</param>
    /// <param name="width">The panel width.</param>
    /// <param name="height">The panel height.</param>
    public void SetLayout(string title, int width, int height)
    {
        heading.text = title;
        UILayout.SetSourceRect(heading.rectTransform, 13, 9, width - 26, 20);
        UILayout.SetSourceRect(
            (RectTransform)scrollArea.transform,
            13,
            33,
            width - 26,
            height - 78
        );
        scrollArea.SetLayout(
            Vector2Int.zero,
            new Vector2Int(width - 43, height - 78),
            new Vector2Int(width - 40, 0),
            new Vector2Int(13, height - 78)
        );
        UILayout.SetSourceRect(transcript.rectTransform, 0, 0, width - 48, height - 78);
        RectTransform entry = (RectTransform)messageInput.transform;
        UILayout.SetSourceRect(entry, 18, height - 33, width - 137, 20);
        RectTransform inputFrame = (RectTransform)transform.Find("InputFrame");
        UILayout.SetSourceRect(inputFrame, 13, height - 36, width - 127, 26);
        UILayout.SetSourceRect(
            (RectTransform)sendButton.transform,
            width - 101,
            height - 36,
            88,
            26
        );
    }

    /// <summary>
    /// Reflows plain text while following the bottom or preserving the player's reading position.
    /// </summary>
    /// <param name="text">The already formatted conversation transcript.</param>
    public void SetTranscript(string text)
    {
        bool followBottom = scrollArea.IsAtBottom;
        transcript.text = text ?? string.Empty;
        float height = Mathf.Max(
            scrollArea.ViewportHeight,
            transcript
                .GetPreferredValues(
                    transcript.text,
                    transcript.rectTransform.rect.width,
                    Mathf.Infinity
                )
                .y
        );
        transcript.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        scrollArea.SetContentHeight(height, 24, false, preserveScrollOffset: true);
        if (followBottom)
            ScrollToBottom();
    }

    /// <summary>
    /// Reveals the newest message and cancels queued scrolling after a local send.
    /// </summary>
    public void ScrollToBottom()
    {
        scrollArea.VerticalNormalizedPosition = 0f;
    }
}
