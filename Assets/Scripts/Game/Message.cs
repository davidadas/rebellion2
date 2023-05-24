using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MessageType
{
    Conflict,
    Mission,
    PopularSupport,
    Resource,
}

public class Message : GameLeaf
{
    public MessageType Type;
    public string Text;
    public bool Read = false;

    /// <summary>
    ///
    /// </summary>
    public Message() { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="type"></param>
    /// <param name="text"></param>
    public Message(MessageType type, string text)
    {
        Type = type;
        Text = text;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public string GetText()
    {
        Read = false;
        return Text;
    }
}
