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

    public string ID => _id;

    public string Version => _version;

    public string DisplayName => _displayName;

    public string BasePackID => _basePackId;
}
