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

    [PersistableMember(Name = nameof(BasePackVersion))]
    private string _basePackVersion;

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

    /// <summary>
    /// Gets the exact content-pack version this mod extends.
    /// </summary>
    public string BasePackVersion => _basePackVersion;
}

/// <summary>
/// Describes a content mod that could not be discovered safely.
/// </summary>
public sealed class ContentModIssue
{
    /// <summary>
    /// Gets the best available stable identifier, falling back to the mod folder name.
    /// </summary>
    public string ID { get; }

    /// <summary>
    /// Gets the best available player-facing name, falling back to the mod folder name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the reason the mod could not be loaded.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Creates a failed content-mod discovery result.
    /// </summary>
    /// <param name="id">The best available mod identifier.</param>
    /// <param name="displayName">The best available player-facing name.</param>
    /// <param name="message">The discovery failure.</param>
    public ContentModIssue(string id, string displayName, string message)
    {
        ID = id ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Message = message ?? string.Empty;
    }
}
