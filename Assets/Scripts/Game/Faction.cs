using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Faction : GameNode
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
    ///
    /// </summary>
    /// <param name="message"></param>
    public void AddMessage(Message message)
    {
        Messages[message.Type].Add(message);
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public override GameNode[] GetChildNodes()
    {
        // Leaf node.
        return new GameNode[] { };
    }
}
