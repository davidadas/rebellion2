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

    // Graph Info
    [CloneIgnore]
    public string ParentGameID { get; set; }

    /// <summary>
    /// Default constructor.
    /// </summary>
    public GameNode() { }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public abstract GameNode[] GetChildNodes();
}
