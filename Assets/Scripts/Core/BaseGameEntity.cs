using System;
using System.Collections.Generic;
using ObjectExtensions;

/// <summary>
/// Base implementation of the <see cref="IGameEntity"/> interface.
/// </summary>
[PersistableObject]
public class BaseGameEntity : IGameEntity
{
    private string _instanceId;

    [CloneIgnore]
    public string InstanceID
    {
        get => _instanceId ??= Guid.NewGuid().ToString().Replace("-", "");
        set => _instanceId ??= value;
    }

    public string TypeID { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }

    /// <summary>
    /// Returns the instance ID of the entity.
    /// </summary>
    /// <returns>The instance ID of the entity.</returns>
    public string GetInstanceID()
    {
        return InstanceID;
    }

    /// <summary>
    /// Returns the TypeID of the entity.
    /// </summary>
    /// <returns>The TypeID of the entity.</returns>
    public string GetTypeID()
    {
        return TypeID;
    }

    /// <summary>
    /// Returns the DisplayName of the entity.
    /// </summary>
    /// <returns></returns>
    public string GetDisplayName()
    {
        return DisplayName;
    }
}
