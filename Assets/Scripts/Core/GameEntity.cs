using System;
using System.Collections.Generic;
using ObjectExtensions;

[PersistableObject]
public class GameEntity
{
    private string _instanceId;
    private string _ownerInstanceId;

    [CloneIgnore]
    public string InstanceID
    {
        get => _instanceId ??= Guid.NewGuid().ToString().Replace("-", "");
        set => _instanceId ??= value;
    }

    public string TypeID { get; set; }

    // Owner Info
    public string DisplayName { get; set; }
    public string Description { get; set; }

    [CloneIgnore]
    public string OwnerInstanceID
    {
        get => _ownerInstanceId;
        set => SetOwnerInstanceID(value);
    }
    public List<string> AllowedOwnerInstanceIDs { get; set; }

    /// <summary>
    /// Sets the owner type id. If the ID is not in the allowed list, throws an exception.
    /// </summary>
    /// <param name="value">The owner type id to set.</param>
    /// <exception cref="ArgumentException">Thrown when the owner type id is invalid.</exception>
    private void SetOwnerInstanceID(string value)
    {
        if (
            AllowedOwnerInstanceIDs == null
            || AllowedOwnerInstanceIDs.Count == 0
            || AllowedOwnerInstanceIDs.Contains(value)
        )
        {
            _ownerInstanceId = value;
        }
        else
        {
            throw new ArgumentException(
                $"Invalid owner type id \"{value}\" for object \"{DisplayName}\"."
            );
        }
    }
}
