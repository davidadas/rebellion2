/// <summary>
/// Contains the immutable presentation of one player-message draft.
/// </summary>
public sealed class MessagesChatComposerRenderData
{
    public string Draft { get; }

    public bool CanSend { get; }

    public bool Sending { get; }

    public string Status { get; }

    /// <summary>
    /// Creates the presentation of a controller-owned draft.
    /// </summary>
    /// <param name="draft">The unmodified multiline draft.</param>
    /// <param name="canSend">Whether the current session can accept player chat.</param>
    /// <param name="sending">Whether a submission is awaiting acceptance.</param>
    /// <param name="status">The optional submission status.</param>
    public MessagesChatComposerRenderData(string draft, bool canSend, bool sending, string status)
    {
        Draft = draft ?? string.Empty;
        CanSend = canSend;
        Sending = sending;
        Status = status ?? string.Empty;
    }
}
