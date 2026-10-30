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
    /// <returns>The authored neutral presentation.</returns>
    internal static FactionTheme GetNeutralTheme(ContentPackEditorProject project)
    {
        return GetTheme(project, project.Definition.NeutralThemePath);
    }

    /// <summary>
    /// Resolves preview themes from the pack's faction manifests, including inherited factions.
    /// </summary>
    /// <param name="project">The working pack.</param>
    /// <returns>The faction names and their current presentation data.</returns>
    internal static IReadOnlyList<(string Name, FactionTheme Theme)> GetFactionThemes(
        ContentPackEditorProject project
    )
    {
        return project
            .GetCatalogs()
            .Where(catalog =>
                project.Definition.FactionPaths.Any(path =>
                    catalog.Document.Address == "Pack/" + path.Replace('\\', '/')
                )
            )
            .Select(catalog => catalog.DocumentRecord)
            .Where(record => !string.IsNullOrEmpty(record.Element.Element("ThemePath")?.Value))
            .Select(record =>
                (record.Name, GetTheme(project, record.Element.Element("ThemePath").Value))
            )
            .ToArray();
    }

    /// <summary>
    /// Reads one theme from the effective authored document without opening a game session.
    /// </summary>
    /// <param name="project">The working pack.</param>
    /// <param name="path">The pack-relative theme path.</param>
    /// <returns>The current theme.</returns>
    private static FactionTheme GetTheme(ContentPackEditorProject project, string path)
    {
        string address = "Pack/" + path;
        ContentPackEditorRecord record = project
            .GetCatalogs()
            .Single(catalog =>
                string.Equals(catalog.Document.Address, address, StringComparison.Ordinal)
            )
            .Records.First();
        using StringReader reader = new(record.Element.ToString());
        return (FactionTheme)
            new GameSerializer(
                typeof(FactionTheme),
                new GameSerializerSettings { RootName = "FactionTheme" }
            ).Deserialize(reader);
    }
}
