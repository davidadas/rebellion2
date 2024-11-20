/// <summary>
///
/// </summary>
public interface IGameEntity
{
    public string InstanceID { get; set; }
    public string TypeID { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public string GetInstanceID();

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public string GetTypeID();

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public string GetDisplayName();
}
