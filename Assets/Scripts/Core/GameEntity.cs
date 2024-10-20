using System;
using System.Collections.Generic;
using System.Xml.Serialization;

[Serializable]
public class GameEntity
{
    private string _instanceId;
    private string _ownerTypeID;

    /// <summary>
    /// InstanceID is a unique identifier for the object. If not set, it will be generated automatically.
    /// Its primar
    /// </summary>
    [CloneIgnore]
    public string InstanceID
    {
        get => _instanceId ??= Guid.NewGuid().ToString().Replace("-", "");
        set => _instanceId ??= value;
    }

    /// <summary>
    /// TypeID is a non-unique identifier for specific types of objects, such as starfighters, ships, regiments, etc.
    ///
    /// </summary>
    public string TypeID { get; set; }

    // Owner Info
    public string DisplayName { get; set; }
    public string Description { get; set; }
    [CloneIgnore]
    public string OwnerTypeID
    {
        get => _ownerTypeID;
        set => SetOwnerTypeID(value);
    }
    public List<string> AllowedOwnerTypeIDs { get; set; }

    /// <summary>
    /// Sets the owner type id. If the ID is not in the allowed list, throws an exception.
    /// </summary>
    /// <param name="value">The owner type id to set.</param>
    /// <exception cref="ArgumentException">Thrown when the owner type id is invalid.</exception>
    private void SetOwnerTypeID(string value)
    {
        if (AllowedOwnerTypeIDs == null || AllowedOwnerTypeIDs.Count == 0 || AllowedOwnerTypeIDs.Contains(value))
        {
            _ownerTypeID = value;
        }
        else
        {
            throw new ArgumentException($"Invalid owner type id: {value}");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public SceneNode GetShallowCopy()
    {
        return (SceneNode)MemberwiseClone();
    }
}
