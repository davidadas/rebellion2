using System;
using System.Collections.Generic;
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

    [PersistableMember(Name = nameof(Requires))]
    private ContentModRequirement[] _requires;

    [PersistableMember(Name = nameof(LoadAfter))]
    [PersistableCollectionItem(Name = "ModID")]
    private string[] _loadAfter;

    [PersistableMember(Name = nameof(LoadBefore))]
    [PersistableCollectionItem(Name = "ModID")]
    private string[] _loadBefore;

    [PersistableMember(Name = nameof(IncompatibleWith))]
    [PersistableCollectionItem(Name = "ModID")]
    private string[] _incompatibleWith;

    [PersistableMember(Name = nameof(ExtendsPack))]
    private bool _extendsPack;

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

    public IReadOnlyList<ContentModRequirement> Requires =>
        _requires ?? Array.Empty<ContentModRequirement>();

    public IReadOnlyList<string> LoadAfter => _loadAfter ?? Array.Empty<string>();

    public IReadOnlyList<string> LoadBefore => _loadBefore ?? Array.Empty<string>();

    public IReadOnlyList<string> IncompatibleWith => _incompatibleWith ?? Array.Empty<string>();

    public bool ExtendsPack => _extendsPack;

    /// <summary>
    /// Creates the manifest for a mod authored against an installed pack.
    /// </summary>
    /// <param name="id">The stable identifier for the new mod.</param>
    /// <param name="version">Its initial version.</param>
    /// <param name="displayName">Its name in the editor and game.</param>
    /// <param name="basePack">The installed baseline.</param>
    /// <returns>The new manifest.</returns>
    public static ContentModDefinition Create(
        string id,
        string version,
        string displayName,
        ContentPackDefinition basePack
    ) =>
        new ContentModDefinition
        {
            _id = id,
            _version = version,
            _displayName = displayName,
            _basePackId = basePack.ID,
            _basePackVersion = basePack.Version,
            _extendsPack = true,
        };
}

/// <summary>
/// Declares an installed mod that must load first, optionally at an exact version.
/// </summary>
[PersistableObject(Name = "Mod")]
public sealed class ContentModRequirement
{
    [PersistableMember(Name = nameof(ID))]
    private string _id;

    [PersistableMember(Name = nameof(Version))]
    private string _version;

    public string ID => _id;

    public string Version => _version;
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

    internal bool BlocksSelection { get; }

    internal bool IsDuplicate { get; }

    /// <summary>
    /// Creates a failed content-mod discovery result.
    /// </summary>
    /// <param name="id">The best available mod identifier.</param>
    /// <param name="displayName">The best available player-facing name.</param>
    /// <param name="message">The discovery failure.</param>
    /// <param name="blocksSelection">Whether the requested selection must be corrected.</param>
    /// <param name="isDuplicate">Whether the ID refers to multiple installations.</param>
    public ContentModIssue(
        string id,
        string displayName,
        string message,
        bool blocksSelection = false,
        bool isDuplicate = false
    )
    {
        ID = id ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Message = message ?? string.Empty;
        BlocksSelection = blocksSelection;
        IsDuplicate = isDuplicate;
    }
}
