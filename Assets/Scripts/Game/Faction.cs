using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 
/// </summary>
public class Faction : LeafNode
{
    public SerializableDictionary<MessageType, List<Message>> Messages = new SerializableDictionary<
        MessageType,
        List<Message>
    >()
    {
        { MessageType.Conflict, new List<Message>() },
        { MessageType.Mission, new List<Message>() },
        { MessageType.PopularSupport, new List<Message>() },
        { MessageType.Resource, new List<Message>() }
    };
    public int ResearchLevel;
    public string HQGameID;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public Faction() { }

    /// <summary>
    /// Adds a message to the Messages dictionary based on its type.
    /// </summary>
    /// <param name="message">The message to add.</param>
    public void AddMessage(Message message)
    {
        Messages[message.Type].Add(message);
    }
}
