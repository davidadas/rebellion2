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
    /// Reflows plain text and reveals the newest conversation line through the shared scroll control.
    /// </summary>
    /// <param name="text">The already formatted conversation transcript.</param>
    public void SetTranscript(string text)
    {
        transcript.text = text;
        float height = Mathf.Max(scrollArea.ViewportRoot.rect.height, transcript.preferredHeight);
        transcript.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        scrollArea.SetContentHeight(height, 24, false);
        scrollArea.RevealContentRect(Mathf.Max(0, height - 24), 24);
    }
}
