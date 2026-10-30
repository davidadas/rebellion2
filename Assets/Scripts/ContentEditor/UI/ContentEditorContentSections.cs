using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Missions;

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
    private static readonly IReadOnlyDictionary<string, string> _missionCaptions =
        ContentPackEditorFieldSchema.MissionIDs.ToDictionary(
            mission => mission,
            ContentPackEditorField.FormatName,
            StringComparer.Ordinal
        );

    /// <summary>
    /// Separates faction authoring tasks while retaining their existing XML documents.
    /// </summary>
    /// <param name="field">The faction field or associated reference article field.</param>
    /// <returns>The inspector tab containing the field.</returns>
    internal static string FactionSection(ContentPackEditorField field) =>
        field.Record.Kind == "EncyclopediaEntry" ? "Encyclopedia"
        : IsFactionArtworkField(field) ? "Artwork"
        : field.Record.Element.Element("FactionDataPath") != null ? "Advanced"
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

    /// <summary>
    /// Identifies mission settings without moving AI planning or unrelated combat settings.
    /// </summary>
    /// <param name="field">The projected rules property.</param>
    /// <returns>Whether the field is part of mission execution rules.</returns>
    internal static bool IsMissionField(ContentPackEditorField field)
    {
        return field.Group is "Research" or "Assassination" or "Espionage"
            || field.Group == "ProbabilityTables" && field.SectionPath.Contains("Mission")
            || field.Group == "Uprising"
                && (
                    field.MemberName.StartsWith("Subdue", StringComparison.Ordinal)
                    || field.MemberName is "InciteMissionSupportShift" or "MissionLeadershipDivisor"
                )
            || field.Group == "Jedi"
                && field.MemberName
                    is "TrainingCatchUpPercent"
                        or "ForceGrowthPerMission"
                        or "MissionParticipantEncounterMinimum"
                        or "MissionDefenderEncounterMinimum"
            || field.Group == "DifficultyModifiers"
                && (
                    field.MemberName.StartsWith("Mission", StringComparison.Ordinal)
                    || field.SectionPath.Any(section =>
                        section.StartsWith("Mission", StringComparison.Ordinal)
                    )
                );
    }

    /// <summary>
    /// Finds the mission owning one rule, leaving shared mission tables in their own section.
    /// </summary>
    /// <param name="field">The projected mission rule.</param>
    /// <returns>The mission identifier, or null for shared mission rules.</returns>
    internal static string MissionFor(ContentPackEditorField field)
    {
        if (field.Group == "Uprising")
            return field.MemberName.StartsWith("Subdue", StringComparison.Ordinal)
                    ? MissionTypeIDs.SubdueUprising
                : field.MemberName == "InciteMissionSupportShift" ? MissionTypeIDs.InciteUprising
                : null;
        if (field.Group == "Jedi" && field.MemberName == "TrainingCatchUpPercent")
            return MissionTypeIDs.JediTraining;
        return _missionCaptions
            .FirstOrDefault(mission =>
                field.Group == mission.Key
                || field.SectionPath.Contains(mission.Value)
                || field.Group == "ProbabilityTables" && field.MemberName == mission.Key
            )
            .Key;
    }

    /// <summary>
    /// Groups shared game rules into focused authoring pages.
    /// </summary>
    /// <param name="field">The rule property.</param>
    /// <returns>The readable category.</returns>
    internal static string RuleCategory(ContentPackEditorField field) =>
        field.Group switch
        {
            "DifficultyModifiers" => "Difficulty",
            "ProbabilityTables" => field.SectionPath.Contains("Mission")
                ? "Shared mission rules"
                : "Uprisings",
            "Jedi" or "DuelResolution" => "Force and duels",
            "SupportShift" => "Popular support",
            "Captive" => "Prisoners",
            "Planet" => "Planets",
            "Blockade" => "Blockades",
            "Uprising" => "Uprisings",
            _ => ContentPackEditorField.FormatName(field.Group),
        };
}
