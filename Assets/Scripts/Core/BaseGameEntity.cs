using System;
using System.Collections.Generic;
using ObjectExtensions;

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
    ///
    /// </summary>
    /// <returns></returns>
    public string GetInstanceID()
    {
        return InstanceID;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public string GetTypeID()
    {
        return TypeID;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public string GetDisplayName()
    {
        return DisplayName;
    }
}
