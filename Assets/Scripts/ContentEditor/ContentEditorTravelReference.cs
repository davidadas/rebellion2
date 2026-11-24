using System.Linq;
using System.Xml.Linq;

/// <summary>
/// Resolves artwork references used to generate travel effects for a unit.
/// </summary>
internal static class ContentEditorTravelReference
{
    private static readonly string[] _imageExtensions = { ".png", ".jpg", ".jpeg" };

    /// <summary>
    /// Looks up a unit's installed travel and display pair even in a standalone working copy.
    /// </summary>
    /// <param name="project">The working pack containing the unit.</param>
    /// <param name="record">The unit whose artwork is missing.</param>
    /// <param name="travelMember">The travel slot requested by the editor.</param>
    /// <param name="displayMember">The normal artwork slot paired with that travel size.</param>
    /// <returns>The installed artwork pair, if the installed pack defines one.</returns>
    internal static ContentEditorTravelPattern ResolveUnitPattern(
        ContentPackEditorProject project,
        ContentPackEditorRecord record,
        string travelMember,
        string displayMember
    )
    {
        ContentFileResolver reference = ResolveReference(project);
        string address = record.Catalog.Document.Address;
        if (reference?.ResolveFile(address) == null)
            return default;
        var source = XElement.Parse(ContentXmlOverrides.Load(reference, address));
        XElement unit = ContentXmlOverrides.FindEquivalent(record.Element, source);
        string travel = unit?.Element(travelMember)?.Value;
        string display = unit?.Element(displayMember)?.Value;
        if (string.IsNullOrWhiteSpace(travel) || string.IsNullOrWhiteSpace(display))
            return default;
        string image = reference.ResolveFile(travel, _imageExtensions);
        string subject = reference.ResolveFile(display, _imageExtensions);
        return image == null || subject == null
            ? default
            : new ContentEditorTravelPattern(image, subject);
    }

    /// <summary>
    /// Finds the installed pack with the declared base or standalone identity.
    /// </summary>
    /// <param name="project">The working pack whose reference is needed.</param>
    /// <returns>The relevant installed resolver, if one exists.</returns>
    private static ContentFileResolver ResolveReference(ContentPackEditorProject project)
    {
        string id = string.IsNullOrWhiteSpace(project.Definition.BasePackID)
            ? project.Definition.ID
            : project.Definition.BasePackID;
        return ContentPackLoader.DiscoverPacks(project.ContentRootPath).Any(pack => pack.ID == id)
            ? ContentPackLoader.CreatePackResolver(project.ContentRootPath, id)
            : null;
    }
}
