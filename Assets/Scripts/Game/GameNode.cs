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
    public string InstanceID
    {
        get
        {
            if (_instanceId == null)
            {
                _instanceId = Guid.NewGuid().ToString();
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
    public string DisplayName { get; set; }
    public string Description = "";
    protected GameNode ParentNode = null;
    protected GameNode ReferenceNode = null;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public GameNode() { }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public abstract GameNode[] GetChildNodes();

    /// <summary>
    ///
    /// </summary>
    /// <param name="parentNode"></param>
    public void SetParent(GameNode parentNode)
    {
        ParentNode = parentNode;
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
    /// <param name="referenceNode"></param>
    public void SetReferenceNode(GameNode referenceNode)
    {
        ReferenceNode = referenceNode;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public GameNode GetReferenceNode()
    {
        return ReferenceNode;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="property"></param>
    /// <param name="expectedValue"></param>
    /// <returns></returns>
    public GameNode FindNodeByProperty(string property, object expectedValue)
    {
        GameNode[] self = new GameNode[] { this };

        return getChildNodeByProperty(property, expectedValue, self);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="property"></param>
    /// <param name="expectedValue"></param>
    /// <returns></returns>
    public GameNode[] FindNodesByProperty(string property, object expectedValue)
    {
        GameNode[] self = new GameNode[] { this };
        List<GameNode> results = new List<GameNode>();
        List<GameNode> children = getChildNodesByProperty(property, expectedValue, self, results);

        return children.ToArray();
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="expectedType"></param>
    /// <returns></returns>
    public GameNode[] FindNodesByType(Type expectedType)
    {
        GameNode[] self = new GameNode[] { this };
        List<GameNode> results = new List<GameNode>();
        List<GameNode> children = getChildNodesByType(expectedType, self, results);

        return children.ToArray();
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="property"></param>
    /// <param name="expectedValue"></param>
    /// <param name="nodes"></param>
    /// <returns></returns>
    private GameNode getChildNodeByProperty(string property, object expectedValue, GameNode[] nodes)
    {
        foreach (GameNode childNode in nodes)
        {
            PropertyInfo propertyInfo = childNode.GetType().GetProperty(property);
            if (propertyInfo != null && propertyInfo.GetValue(childNode, null) == expectedValue)
            {
                return childNode;
            }

            return getChildNodeByProperty(property, expectedValue, childNode.GetChildNodes());
        }

        return null;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="property"></param>
    /// <param name="expectedValue"></param>
    /// <param name="nodes"></param>
    /// <param name="results"></param>
    /// <returns></returns>
    private List<GameNode> getChildNodesByProperty(
        string property,
        object expectedValue,
        GameNode[] nodes,
        List<GameNode> results
    )
    {
        foreach (GameNode childNode in nodes)
        {
            PropertyInfo propertyInfo = childNode.GetType().GetProperty(property);
            if (propertyInfo != null && propertyInfo.GetValue(childNode, null) == expectedValue)
            {
                results.Add(childNode);
            }
            results = getChildNodesByProperty(
                property,
                expectedValue,
                childNode.GetChildNodes(),
                results
            );
        }

        return results;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="expectedType"></param>
    /// <param name="nodes"></param>
    /// <param name="results"></param>
    /// <returns></returns>
    private List<GameNode> getChildNodesByType(
        Type expectedType,
        GameNode[] nodes,
        List<GameNode> results
    )
    {
        foreach (GameNode childNode in nodes)
        {
            bool isClass = childNode.GetType() == expectedType;
            bool isSubclass = childNode.GetType().IsSubclassOf(expectedType);

            if (isClass || isSubclass)
            {
                results.Add(childNode);
            }
            results = getChildNodesByType(expectedType, childNode.GetChildNodes(), results);
        }

        return results;
    }
}
