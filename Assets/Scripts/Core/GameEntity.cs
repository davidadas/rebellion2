using System;
using System.Collections.Generic;
using System.Xml.Serialization;

[Serializable]
public class GameEntity
{
    private string _instanceId;
    private string _ownerGameID;

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
    /// GameID is a non-unique identifier for specific types of objects, such as starfighters, ships, regiments, etc.
    ///
    /// </summary>
    public string GameID { get; set; }

    // Owner Info
    public string DisplayName { get; set; }
    public string Description { get; set; }
    [CloneIgnore]
    public string OwnerGameID
    {
        get => _ownerGameID;
        set => SetOwnerGameID(value);
    }
    public List<string> AllowedOwnerGameIDs { get; set; }

    /// <summary>
    /// Sets the owner game ID. If the ID is not in the allowed list, throws an exception.
    /// </summary>
    /// <param name="value">The owner game ID to set.</param>
    /// <exception cref="ArgumentException">Thrown when the owner game ID is invalid.</exception>
    private void SetOwnerGameID(string value)
    {
        if (AllowedOwnerGameIDs == null || AllowedOwnerGameIDs.Count == 0 || AllowedOwnerGameIDs.Contains(value))
        {
            _ownerGameID = value;
        }
        else
        {
            throw new ArgumentException($"Invalid owner game ID: {value}");
        }
    }
}
