using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rebellion.Util.Serialization;

/// <summary>
/// Resolves authored presentation data for previews without creating a simulated campaign.
/// </summary>
internal static class ContentEditorPresentation
{
    /// <summary>
    /// Reads the working pack's effective neutral theme through its normal catalog inheritance.
    /// </summary>
    /// <param name="project">The working content pack.</param>
    /// <returns>The authored neutral presentation, when configured.</returns>
    internal static FactionTheme GetNeutralTheme(ContentPackEditorProject project)
    {
        ContentPackEditorRecord record = GetNeutralThemeRecord(project.GetCatalogs());
        return record == null ? null : GetFactionTheme(record);
    }

    /// <summary>
    /// Finds the editable default theme in the pack's registered theme documents.
    /// </summary>
    /// <param name="catalogs">The current effective content catalogs.</param>
    /// <returns>The configured neutral theme record, when available.</returns>
    internal static ContentPackEditorRecord GetNeutralThemeRecord(
        IReadOnlyList<ContentPackEditorCatalog> catalogs
    ) =>
        catalogs
            .SelectMany(catalog => catalog.Records)
            .FirstOrDefault(record =>
                record.Kind == nameof(FactionTheme)
                && FactionThemes.IsNeutral(record.Element.Element("FactionInstanceID")?.Value)
            );

    /// <summary>
    /// Resolves preview themes from the pack's faction manifests, including inherited factions.
    /// </summary>
    /// <param name="project">The working pack.</param>
    /// <returns>The faction names and their current presentation data.</returns>
    internal static IReadOnlyList<(string Name, FactionTheme Theme)> GetFactionThemes(
        ContentPackEditorProject project
    )
    {
        IReadOnlyList<ContentPackEditorCatalog> catalogs = project.GetCatalogs();
        return catalogs
            .Where(catalog =>
                project.Definition.FactionPaths.Any(path =>
                    catalog.Document.Address == "Pack/" + path.Replace('\\', '/')
                )
            )
            .Select(catalog => catalog.DocumentRecord)
            .Select(record => (record.Name, Theme: GetFactionThemeRecord(catalogs, record)))
            .Where(pair => pair.Theme != null)
            .Select(pair => (pair.Name, GetFactionTheme(pair.Theme)))
            .ToArray();
    }

    /// <summary>
    /// Finds the theme matching a faction manifest, including its legacy explicit path.
    /// </summary>
    /// <param name="catalogs">The current effective content catalogs.</param>
    /// <param name="faction">The faction manifest that owns the theme.</param>
    /// <returns>The corresponding theme record, when configured.</returns>
    internal static ContentPackEditorRecord GetFactionThemeRecord(
        IReadOnlyList<ContentPackEditorCatalog> catalogs,
        ContentPackEditorRecord faction
    )
    {
        string id = faction?.Element.Element("ID")?.Value;
        if (string.IsNullOrEmpty(id))
            return null;
        string legacyPath = faction.Element.Element("ThemePath")?.Value.Replace('\\', '/');
        return catalogs
                .SelectMany(catalog => catalog.Records)
                .FirstOrDefault(record =>
                    record.Kind == nameof(FactionTheme)
                    && record.Element.Element("FactionInstanceID")?.Value == id
                )
            ?? catalogs
                .FirstOrDefault(catalog => catalog.Document.Address == "Pack/" + legacyPath)
                ?.Records.FirstOrDefault(record => record.Kind == nameof(FactionTheme));
    }

    /// <summary>
    /// Reads one effective theme record without constructing a game session.
    /// </summary>
    /// <param name="record">The effective faction-theme record.</param>
    /// <returns>The current authored presentation.</returns>
    internal static FactionTheme GetFactionTheme(ContentPackEditorRecord record)
    {
        if (record == null)
            throw new ArgumentNullException(nameof(record));
        using StringReader reader = new(record.Element.ToString());
        return (FactionTheme)
            new GameSerializer(
                typeof(FactionTheme),
                new GameSerializerSettings { RootName = "FactionTheme" }
            ).Deserialize(reader);
    }
}
