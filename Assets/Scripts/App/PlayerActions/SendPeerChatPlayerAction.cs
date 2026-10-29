namespace Rebellion.App.PlayerActions
{
    /// <summary>
    /// Sends unchanged multiline text to the other human participant's durable chat messages.
    /// </summary>
    public sealed class SendPeerChatPlayerAction : PlayerAction
    {
        public override PlayerActionKind Kind => PlayerActionKind.SendPeerChat;
        public string Text { get; set; }
    }
}
