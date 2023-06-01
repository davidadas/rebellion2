using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Serialization;
using UnityEngine;

[Serializable]
public abstract class GameNode
{
    private string _instanceId;

    // Set the InstaceID property.
    // This is a unique ID set for each instance of an object.
    [CloneIgnore]
    public string InstanceID
    {
        get
        {
            if (_instanceId == null)
            {
                _instanceId = Guid.NewGuid().ToString().Replace("-", "");
            }
            return _instanceId;
        }
        set
        {
            // Set only once.
            if (_instanceId == null)
            {
                _instanceId = value;
            }
        }
    }
    public string GameID { get; set; }

    // Game Info
    public string DisplayName { get; set; }
    public string Description { get; set; }

    // Parent Info
    [CloneIgnore]
    public string ParentGameID { get; set; }
    protected GameNode ParentNode;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public GameNode() { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="parentNode"></param>
    public void SetParent(GameNode parentNode)
    {
        ParentNode = parentNode;
        ParentGameID = parentNode.GameID;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public GameNode GetParent()
    {
        return ParentNode;
    }

    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="instanceId"></param>
    /// <returns></returns>
    public T FindNodeByInstanceID<T>(string instanceId)
        where T : GameNode
    {
        GameNode[] nodes = new GameNode[] { this };

        return searchByInstanceId<T>(nodes, instanceId);
    }

    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public T[] FindNodesByGameID<T>(string gameId)
        where T : GameNode
    {
        GameNode[] nodes = new GameNode[] { this };
        List<T> results = searchByGameId<T>(nodes, gameId);

        return results.ToArray();
    }

    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="nodes"></param>
    /// <param name="instanceId"></param>
    /// <returns></returns>
    private T searchByInstanceId<T>(GameNode[] nodes, string instanceId)
        where T : GameNode
    {
        foreach (GameNode node in nodes)
        {
            if (node.InstanceID == instanceId)
            {
                return (T)node;
            }

            T result = searchByInstanceId<T>(node.GetChildNodes(), instanceId);

            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="nodes"></param>
    /// <param name="gameId"></param>
    /// <param name="results"></param>
    /// <returns></returns>
    private List<T> searchByGameId<T>(
        GameNode[] nodes,
        string gameId,
        List<T> results = null
    )
        where T : GameNode
    {
        if (results == null)
        {
            results = new List<T>();
        }

        foreach (GameNode node in nodes)
        {
            if (node.GameID == gameId)
            {
                results.Add((T)node);
            }

            searchByGameId(node.GetChildNodes(), gameId, results);
        }

        return results;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="childNodes"></param>
    public void Attach(params GameNode[] childNodes)
    {
        foreach(GameNode childNode in childNodes)
        {
            AddChildNode(childNode);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="childNode"></param>
    protected abstract void AddChildNode(GameNode childNode);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="childNode"></param>
    protected abstract void RemoveChildNode(GameNode childNode);

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public abstract GameNode[] GetChildNodes();
}
