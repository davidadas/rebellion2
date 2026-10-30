using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Projects related authored content into task-oriented inspector and navigation sections.
/// </summary>
internal static class ContentEditorContentSections
{
    internal static readonly string[] GenerationSections =
    {
        "Galaxy",
        "Officers",
        "Buildings",
        "Units",
        "Resources",
        "Support",
        "Balance",
    };

    /// <summary>
    /// Separates faction authoring tasks while retaining their existing XML documents.
    /// </summary>
    /// <param name="field">The faction field or associated reference article field.</param>
    /// <returns>The inspector tab containing the field.</returns>
    internal static string FactionSection(ContentPackEditorField field) =>
        field.Record.Kind == "EncyclopediaEntry" ? "Encyclopedia"
        : IsFactionArtworkField(field) ? "Artwork"
        : field.Record.Element.Element("FactionDataPath") != null ? "Advanced"
        : field.MemberName == "DisallowedMissionTypeIDs" ? "Rules"
        : field.Group switch
        {
            "Settings" => "Rules",
            "ShipNamePools" => "Ship names",
            "Messages" => "Messages",
            _ => "General",
        };

    /// <summary>
    /// Identifies the faction's unit icons without exposing the full interface configuration.
    /// </summary>
    /// <param name="field">A theme property.</param>
    /// <returns>Whether the property is an editable faction icon.</returns>
    internal static bool IsFactionArtworkField(ContentPackEditorField field) =>
        field.Record.Kind == "FactionTheme"
        && (
            field.XmlElement.Ancestors().Any(element => element.Name.LocalName == "UnitTileIcons")
            || field.MemberName == "SavedGameSlotIconImagePath"
        );

    /// <summary>
    /// Projects only unit-icon properties instead of flattening the entire faction interface.
    /// </summary>
    /// <param name="theme">The effective faction theme.</param>
    /// <returns>The icon fields backed by their existing XML elements.</returns>
    internal static IReadOnlyList<ContentPackEditorField> FactionArtworkFields(
        ContentPackEditorRecord theme
    )
    {
        if (theme == null)
            return Array.Empty<ContentPackEditorField>();
        IEnumerable<System.Xml.Linq.XElement> elements =
            theme.Element.Element("PlanetOverlayTheme")?.Element("UnitTileIcons")?.Elements()
            ?? Enumerable.Empty<System.Xml.Linq.XElement>();
        System.Xml.Linq.XElement saveIcon = theme.Element.Element("SavedGameSlotIconImagePath");
        if (saveIcon != null)
            elements = elements.Append(saveIcon);
        return elements
            .Select(element => new ContentPackEditorField(
                theme,
                element,
                ContentPackEditorField.FormatName(element.Name.LocalName)
            ))
            .ToArray();
    }

    /// <summary>
    /// Assigns generation settings to their authoring task without changing their XML layout.
    /// </summary>
    /// <param name="field">The generated-world setting.</param>
    /// <returns>The inspector tab that owns it.</returns>
    internal static string GenerationSection(ContentPackEditorField field) =>
        field.Group switch
        {
            "GalaxyClassification" => "Galaxy",
            "Officers" => "Officers",
            "FacilityGeneration" => "Buildings",
            "UnitDeployment" => "Units",
            "PlanetResources" => "Resources",
            "PlanetSupport" => "Support",
            _ => "Balance",
        };

    /// <summary>
    /// Identifies entities whose encyclopedia article is generated from their definition.
    /// </summary>
    /// <param name="record">The selected authored definition.</param>
    /// <returns>Whether the entity supports an encyclopedia inspector.</returns>
    internal static bool HasEncyclopedia(ContentPackEditorRecord record)
    {
        return record?.Kind
            is "Officer"
                or "SpecialForce"
                or "Regiment"
                or "CapitalShip"
                or "Starfighter"
                or "Building"
                or "Planet";
    }

    /// <summary>
    /// Finds the effective article, honoring embedded entity data before standalone fallback data.
    /// </summary>
    /// <param name="record">The entity being edited.</param>
    /// <param name="catalogs">The working project's catalogs.</param>
    /// <returns>The editable fields used by the runtime encyclopedia.</returns>
    internal static IReadOnlyList<ContentPackEditorField> EncyclopediaFields(
        ContentPackEditorRecord record,
        IReadOnlyList<ContentPackEditorCatalog> catalogs
    )
    {
        bool embedded =
            !string.IsNullOrEmpty(record.Element.Element("EncyclopediaImagePath")?.Value)
            || !string.IsNullOrEmpty(record.Element.Element("EncyclopediaDescription")?.Value)
            || record.Element.Element("EncyclopediaStats")?.HasElements == true;
        ContentPackEditorRecord article = embedded
            ? null
            : catalogs
                .SelectMany(catalog => catalog.Records)
                .FirstOrDefault(candidate =>
                    candidate.Kind == "EncyclopediaEntry" && candidate.ID == record.ID
                );
        if (article != null)
            return article.GetFields().Where(field => field.Group != "TypeID").ToArray();
        List<ContentPackEditorField> fields = record
            .GetFields()
            .Where(IsEncyclopediaField)
            .ToList();
        foreach (string member in new[] { "EncyclopediaImagePath", "EncyclopediaDescription" })
            if (!fields.Any(field => field.Group == member))
                fields.Add(record.GetField(member));
        return fields;
    }

    /// <summary>
    /// Keeps encyclopedia-only properties out of the entity's general and media forms.
    /// </summary>
    /// <param name="field">The authored property.</param>
    /// <returns>Whether the property belongs to the encyclopedia tab.</returns>
    internal static bool IsEncyclopediaField(ContentPackEditorField field)
    {
        return field.Group.StartsWith("Encyclopedia", StringComparison.Ordinal);
    }
}
