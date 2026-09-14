using Rebellion.Util.Serialization;

/// <summary>
/// Declares a content mod's identity and version.
/// </summary>
[PersistableObject]
public sealed class ContentModDefinition
{
    [PersistableMember(Name = nameof(ID))]
    private string _id;

    [PersistableMember(Name = nameof(Version))]
    private string _version;

    [PersistableMember(Name = nameof(DisplayName))]
    private string _displayName;

    [PersistableMember(Name = nameof(BasePackID))]
    private string _basePackId;

    /// <summary>
    /// Gets the stable mod identifier.
    /// </summary>
    public string ID => _id;

    /// <summary>
    /// Gets the mod version.
    /// </summary>
    public string Version => _version;

    /// <summary>
    /// Gets the player-facing mod name.
    /// </summary>
    public string DisplayName => _displayName;

    /// <summary>
    /// Gets the identifier of the content pack this mod extends.
    /// </summary>
    public string BasePackID => _basePackId;
}
