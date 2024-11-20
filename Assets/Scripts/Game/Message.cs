using System.Collections;
using System.Collections.Generic;

public enum MessageType
{
    Conflict,
    Mission,
    PopularSupport,
    Resource,
}

public class Message : BaseGameEntity
{
    public MessageType Type;
    public string Text;
    public bool Read = false;

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public Message() { }

    /// <summary>
    /// Constructor for creating a new message.
    /// </summary>
    /// <param name="type">The type of message.</param>
    /// <param name="text">The text of the message.</param>
    public Message(MessageType type, string text)
    {
        Type = type;
        Text = text;
    }

    /// <summary>
    /// Returns the type of the message.
    /// </summary>
    /// <returns>The text of the message.</returns>
    public string GetText()
    {
        Read = false;
        return Text;
    }
}
