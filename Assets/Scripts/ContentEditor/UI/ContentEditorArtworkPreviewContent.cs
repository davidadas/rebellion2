using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

/// <summary>
/// Resolves representative authored records and their real pack artwork for window previews.
/// </summary>
internal sealed class ContentEditorArtworkPreviewContent
{
    private static readonly string[] _factionAssignmentMembers =
    {
        "OwnerInstanceID",
        "RecruitingFactionInstanceIDs",
        "ManufacturingFactionInstanceIDs",
    };

    private readonly ContentAssets assets;
    private readonly Dictionary<Texture, ContentPackEditorField> fieldsByTexture = new();
    private readonly string factionDirectory;
    private readonly string factionInstanceID;
    private readonly IReadOnlyList<ContentPackEditorRecord> records;

    /// <summary>
    /// Captures the working project's current records for one faction-themed preview pass.
    /// </summary>
    /// <param name="project">The working pack whose unsaved records are being previewed.</param>
    /// <param name="theme">The selected faction theme.</param>
    /// <param name="contentAssets">The working pack's resolved textures.</param>
    internal ContentEditorArtworkPreviewContent(
        ContentPackEditorProject project,
        FactionTheme theme,
        ContentAssets contentAssets
    )
    {
        if (project == null)
            throw new ArgumentNullException(nameof(project));
        if (theme == null)
            throw new ArgumentNullException(nameof(theme));
        assets = contentAssets ?? throw new ArgumentNullException(nameof(contentAssets));
        factionInstanceID = theme.FactionInstanceID;
        records = project.GetCatalogs().SelectMany(catalog => catalog.Records).ToArray();
        factionDirectory = records
            .Where(record => record.Kind == "Faction")
            .Where(record => record.Element.Element("ID")?.Value == factionInstanceID)
            .Select(record => record.Catalog.Faction)
            .FirstOrDefault(value => !string.Equals(value, "Shared", StringComparison.Ordinal));
    }

    /// <summary>
    /// Gets faction-appropriate records of one kind in stable authored order.
    /// </summary>
    /// <param name="kind">The serialized record kind.</param>
    /// <param name="maximumCount">The maximum number of records to return.</param>
    /// <returns>Representative records backed by their configured textures.</returns>
    internal IReadOnlyList<ContentEditorArtworkPreviewEntity> GetEntities(
        string kind,
        int maximumCount
    )
    {
        if (string.IsNullOrWhiteSpace(kind))
            throw new ArgumentException("A record kind is required.", nameof(kind));
        if (maximumCount < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumCount));

        return records
            .Where(record => string.Equals(record.Kind, kind, StringComparison.Ordinal))
            .Where(HasDisplayArtwork)
            .Select(record => new { Record = record, Affinity = GetFactionAffinity(record) })
            .Where(item => item.Affinity >= 0)
            .OrderBy(item => item.Affinity)
            .ThenBy(item => item.Record.Catalog.Document.Address, StringComparer.Ordinal)
            .ThenBy(item => item.Record.Name, StringComparer.Ordinal)
            .Take(maximumCount)
            .Select(item => CreateEntity(item.Record))
            .ToArray();
    }

    /// <summary>
    /// Gets the first faction-appropriate record from the requested kinds.
    /// </summary>
    /// <param name="kinds">Record kinds in preference order.</param>
    /// <returns>The first available record, or an empty placeholder.</returns>
    internal ContentEditorArtworkPreviewEntity GetFirst(params string[] kinds)
    {
        foreach (string kind in kinds ?? Array.Empty<string>())
        {
            ContentEditorArtworkPreviewEntity entity = GetEntities(kind, 1).FirstOrDefault();
            if (entity != null)
                return entity;
        }

        return ContentEditorArtworkPreviewEntity.Empty;
    }

    /// <summary>
    /// Gets every authored planet sector in the working pack's stable catalog order.
    /// </summary>
    /// <returns>The complete effective galaxy definition.</returns>
    internal IReadOnlyList<ContentPackEditorRecord> GetPlanetSectors()
    {
        return records
            .Where(record => string.Equals(record.Kind, "PlanetSector", StringComparison.Ordinal))
            .ToArray();
    }

    /// <summary>
    /// Finds the exact authored field that supplied one representative rendered texture.
    /// </summary>
    /// <param name="texture">The rendered texture.</param>
    /// <returns>The supplying field, or null for non-record artwork.</returns>
    internal ContentPackEditorField FindField(Texture texture) =>
        texture != null && fieldsByTexture.TryGetValue(texture, out ContentPackEditorField field)
            ? field
            : null;

    /// <summary>
    /// Determines how closely one record belongs to the selected faction.
    /// </summary>
    /// <param name="record">The authored record.</param>
    /// <returns>A lower preferred score, or -1 when the record belongs to another faction.</returns>
    private int GetFactionAffinity(ContentPackEditorRecord record)
    {
        string catalogFaction = record.Catalog.Faction;
        if (!string.Equals(catalogFaction, "Shared", StringComparison.Ordinal))
        {
            return
                string.IsNullOrEmpty(factionDirectory)
                || string.Equals(catalogFaction, factionDirectory, StringComparison.Ordinal)
                ? 0
                : -1;
        }

        string[] assignments = _factionAssignmentMembers
            .Select(member => record.Element.Element(member))
            .Where(element => element != null)
            .SelectMany(GetAssignmentValues)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (assignments.Length == 0)
            return 2;
        return assignments.Contains(factionInstanceID, StringComparer.Ordinal) ? 1 : -1;
    }

    /// <summary>
    /// Reads scalar and collection-shaped faction assignments from one member.
    /// </summary>
    /// <param name="element">The assignment member.</param>
    /// <returns>Its authored faction identifiers.</returns>
    private static IEnumerable<string> GetAssignmentValues(XElement element)
    {
        return element.HasElements
            ? element.Elements().Select(child => child.Value)
            : new[] { element.Value };
    }

    /// <summary>
    /// Reports whether one record supplies artwork that can be shown in a gameplay window.
    /// </summary>
    /// <param name="record">The authored record.</param>
    /// <returns>True when at least one supported display member has a value.</returns>
    private static bool HasDisplayArtwork(ContentPackEditorRecord record)
    {
        return new[]
        {
            "SmallDisplayImagePath",
            "DisplayImagePath",
            "MessageImagePath",
            "EncyclopediaImagePath",
        }.Any(member => !string.IsNullOrWhiteSpace(record.Element.Element(member)?.Value));
    }

    /// <summary>
    /// Resolves one editable record into the textures required by the preview renderers.
    /// </summary>
    /// <param name="record">The authored record.</param>
    /// <returns>The record's current presentation.</returns>
    private ContentEditorArtworkPreviewEntity CreateEntity(ContentPackEditorRecord record)
    {
        Texture2D display = Resolve(record, "DisplayImagePath");
        Texture2D small = Resolve(record, "SmallDisplayImagePath") ?? display;
        return new ContentEditorArtworkPreviewEntity(
            record.Name,
            small,
            display ?? small,
            Resolve(record, "MessageImagePath") ?? display ?? small,
            Resolve(record, "EncyclopediaImagePath") ?? display ?? small,
            record.Element.Element("EncyclopediaDescription")?.Value
        );
    }

    /// <summary>
    /// Resolves one optional image member from an authored record.
    /// </summary>
    /// <param name="record">The editable record.</param>
    /// <param name="member">The image-path member.</param>
    /// <returns>The configured texture, or null when the member is empty.</returns>
    private Texture2D Resolve(ContentPackEditorRecord record, string member)
    {
        ContentPackEditorField field = record
            .GetFields()
            .FirstOrDefault(candidate => candidate.MemberName == member);
        string address = field?.Value;
        if (string.IsNullOrWhiteSpace(address))
            return null;
        Texture2D texture = assets.GetTexture(field.GetMediaAddress(address));
        if (texture != null && !fieldsByTexture.ContainsKey(texture))
            fieldsByTexture.Add(texture, field);
        return texture;
    }
}

/// <summary>
/// Contains the real authored presentation of one representative preview entity.
/// </summary>
internal sealed class ContentEditorArtworkPreviewEntity
{
    internal static readonly ContentEditorArtworkPreviewEntity Empty = new(
        string.Empty,
        null,
        null,
        null,
        null,
        string.Empty
    );

    internal string Name { get; }

    internal Texture2D SmallTexture { get; }

    internal Texture2D DisplayTexture { get; }

    internal Texture2D MessageTexture { get; }

    internal Texture2D EncyclopediaTexture { get; }

    internal string EncyclopediaDescription { get; }

    /// <summary>
    /// Creates one immutable authored-entity presentation.
    /// </summary>
    /// <param name="name">The configured display name.</param>
    /// <param name="smallTexture">The compact gameplay texture.</param>
    /// <param name="displayTexture">The regular gameplay texture.</param>
    /// <param name="messageTexture">The message texture.</param>
    /// <param name="encyclopediaTexture">The encyclopedia texture.</param>
    /// <param name="encyclopediaDescription">The encyclopedia description.</param>
    internal ContentEditorArtworkPreviewEntity(
        string name,
        Texture2D smallTexture,
        Texture2D displayTexture,
        Texture2D messageTexture,
        Texture2D encyclopediaTexture,
        string encyclopediaDescription
    )
    {
        Name = name ?? string.Empty;
        SmallTexture = smallTexture;
        DisplayTexture = displayTexture;
        MessageTexture = messageTexture;
        EncyclopediaTexture = encyclopediaTexture;
        EncyclopediaDescription = encyclopediaDescription ?? string.Empty;
    }
}
