using System.Xml.Serialization;
using System;

[Serializable]
public class GameEntity
{
    private string _instanceId;

    // Set the InstaceID property.
    // This is a unique ID set for each node.
    [CloneIgnore]
    public string InstanceID
    {
        get
        {
            // Generate a new instance ID if it is not set.
            if (_instanceId == null)
            {
                _instanceId = Guid.NewGuid().ToString().Replace("-", "");
            }
            return _instanceId;
        }
        set
        {
            // Set the instance ID if it is not set.
            if (_instanceId == null)
            {
                _instanceId = value;
            }
        }
    }
    // Set the GameID property.
    // This is a non-unique ID set for each specific types of objects, such as planets, ships, etc.
    public string GameID { get; set; }

    // Game Info
    public string DisplayName { get; set; }
    public string Description { get; set; }
}