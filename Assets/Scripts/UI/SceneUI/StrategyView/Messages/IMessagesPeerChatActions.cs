using System.Threading.Tasks;

/// <summary>
/// Submits player chat through the active multiplayer session without exposing transport to Messages.
/// </summary>
public interface IMessagesPeerChatActions
{
    /// <summary>
    /// Gets whether the current player can submit a message to the other player.
    /// </summary>
    bool CanSendPeerChat { get; }

    /// <summary>
    /// Submits the entered text without changing its whitespace or creating sender history.
    /// </summary>
    /// <param name="rawText">The unmodified multiline message.</param>
    /// <returns>True when the session accepted the submission.</returns>
    Task<bool> SendPeerChatAsync(string rawText);
}
