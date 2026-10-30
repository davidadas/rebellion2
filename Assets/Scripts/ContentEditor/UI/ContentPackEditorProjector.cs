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
    internal static ContentPackEditorFieldRenderData BuildField(
        ContentPackEditorProject project,
        ContentPackEditorField field
    ) =>
        new ContentPackEditorFieldRenderData(
            field,
            project.GetReferenceOptions(field),
            () => project.GetMediaFiles(field)
        );

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
