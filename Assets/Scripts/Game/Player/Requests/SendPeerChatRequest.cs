namespace Rebellion.Game.PlayerRequests
{
    /// <summary>
    /// Sends unchanged multiline text to the other human participant's durable chat messages.
    /// </summary>
    public sealed class SendPeerChatRequest : PlayerRequest
    {
        public override PlayerRequestKind Kind => PlayerRequestKind.SendPeerChat;
        public string Text { get; set; }
    }
}
