using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Projects authored records into form choices and workspace navigation targets.
/// </summary>
internal static class ContentPackEditorProjector
{
    /// <summary>
    /// Derives the visible inspector page and tab from one requested selection.
    /// </summary>
    /// <param name="record">The requested definition.</param>
    /// <param name="page">Its logical navigation page.</param>
    /// <param name="sector">The map context to display.</param>
    /// <param name="section">The previously selected inspector tab.</param>
    /// <returns>The complete read-only selection presentation.</returns>
    internal static ContentPackEditorSelection BuildSelection(
        ContentPackEditorRecord record,
        ContentEditorTreeNode page,
        ContentPackEditorRecord sector,
        string section
    )
    {
        ContentEditorTreeNode visiblePage =
            page?.Fields != null || page?.Catalog?.IsMap == true && page.Record == null
                ? page
                : null;
        if (
            section is "Advanced" or "Rules"
            || page?.Fields != null
            || section == "Encyclopedia" && !ContentEditorContentSections.HasEncyclopedia(record)
        )
            section = "General";
        if (page?.MissionID != null && page.RelatedRecords.Count == 0)
            section = "Rules";
        if (page?.Key == ContentEditorContentSections.NeutralThemePageKey)
            section = page.Fields.Any(field =>
                ContentEditorContentSections.IsFactionInterfaceField(field, neutral: true)
            )
                ? "Interfaces"
                : "Colors";
        if (record?.Catalog.IsMap == true)
            section = "General";
        return new ContentPackEditorSelection(record, visiblePage, sector, section);
    }

    /// <summary>
    /// Resolves one field's display choices without giving its control a mutable project.
    /// </summary>
    /// <param name="project">The source authoring project.</param>
    /// <param name="field">The field being presented.</param>
    /// <returns>The field presentation and its read-only directory query.</returns>
    /// <param name="referenceLabels">An optional set of choices supplied by the editing context.</param>
    /// <param name="assets">The optional store owning thumbnails for shared image choices.</param>
    internal static ContentPackEditorFieldRenderData BuildField(
        ContentPackEditorProject project,
        ContentPackEditorField field,
        IReadOnlyDictionary<string, string> referenceLabels = null,
        ContentAssets assets = null
    )
    {
        IReadOnlyDictionary<string, string> images = ContentEditorPlanetArtwork.IsImageField(field)
            ? ContentEditorPlanetArtwork.GetChoices(project, field)
            : null;
        referenceLabels ??= project.GetReferenceOptions(field);
        if (
            referenceLabels == null
            && field.Record.Kind == "Starfighter"
            && field.MemberName == "Role"
        )
        {
            Dictionary<string, string> roles = new() { [string.Empty] = "Unclassified" };
            foreach (string role in field.Choices)
                roles[role] = ContentPackEditorField.FormatName(role);
            referenceLabels = roles;
        }
        return new ContentPackEditorFieldRenderData(
            field,
            referenceLabels,
            () => project.GetMediaFiles(field),
            images,
            ContentEditorPlanetArtwork.IsImageField(field)
                && ContentEditorPlanetArtwork.GetReplacementPath(project, field) != null,
            images == null || assets == null
                ? null
                : images.Keys.ToDictionary(
                    address => address,
                    address => GetMediaImage(assets, address)
                )
        );
    }

    /// <summary>
    /// Loads an optional thumbnail while keeping an invalid existing address repairable.
    /// </summary>
    /// <param name="assets">The owner of loaded sprites and textures.</param>
    /// <param name="address">The selected or available artwork address.</param>
    /// <returns>The shared sprite, or null for an empty, missing, or invalid address.</returns>
    private static UnityEngine.Sprite GetMediaImage(ContentAssets assets, string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;
        try
        {
            return assets.GetSprite(address);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Finds the sector containing a planet without modifying the source catalog.
    /// </summary>
    /// <param name="record">The selected authored record.</param>
    /// <returns>The containing sector, or null for a non-planet record.</returns>
    internal static ContentPackEditorRecord GetParentSector(ContentPackEditorRecord record) =>
        record?.Kind == "Planet"
            ? record.Catalog.Records.FirstOrDefault(sector =>
                sector.Planets.Any(planet => planet.Element == record.Element)
            )
            : null;
}
