using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

/// <summary>
/// Recovers authored travel artwork and assignments from the working pack's installed reference.
/// </summary>
internal static class ContentEditorTravelRestoration
{
    private static readonly string[] _imageExtensions = { ".png", ".jpg", ".jpeg" };

    /// <summary>
    /// Restores missing travel files and cleared assignments from the installed reference pack.
    /// </summary>
    /// <param name="project">The working pack to restore.</param>
    /// <returns>The number of image files added and the number of image references restored.</returns>
    internal static (int Files, int References) Restore(ContentPackEditorProject project)
    {
        ContentFileResolver reference =
            ResolveReference(project)
            ?? throw new InvalidOperationException(
                "This pack has no installed reference for restoring travel artwork."
            );
        ContentFileResolver working = ContentPackLoader.CreateFolderResolver(
            project.ContentRootPath,
            project.PackRootPath,
            project.Definition
        );
        var files = reference
            .EnumerateFileAddresses("Pack/")
            .Where(address => IsTravelFileName(address) && IsImageFile(address))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var edits = new List<(ContentPackEditorDocument Document, XDocument Xml)>();
        int references = 0;
        foreach (ContentPackEditorDocument document in project.Documents)
        {
            if (
                document.Address == "Pack/pack.xml"
                || reference.ResolveFile(document.Address) == null
            )
                continue;
            XDocument source = XDocument.Parse(
                ContentXmlOverrides.Load(reference, document.Address)
            );
            XElement[] travelFields = source.Descendants().Where(IsTravelReference).ToArray();
            if (travelFields.Length == 0)
                continue;

            XDocument edited = XDocument.Parse(document.Text, LoadOptions.PreserveWhitespace);
            int restored = 0;
            foreach (XElement field in travelFields)
            {
                string sourceImage = reference.ResolveFile(field.Value.Trim(), _imageExtensions);
                if (sourceImage == null || !IsImageFile(sourceImage))
                    continue;
                string relative = reference
                    .PackRootPaths.Select(root => Path.GetRelativePath(root, sourceImage))
                    .First(path =>
                        !Path.IsPathRooted(path) && !path.StartsWith("..", StringComparison.Ordinal)
                    );
                files.Add("Pack/" + relative.Replace('\\', '/'));

                XElement target = ContentXmlOverrides.FindEquivalent(field, edited.Root);
                if (!string.IsNullOrWhiteSpace(target?.Value))
                    continue;
                if (target != null)
                    target.Value = field.Value;
                else if (
                    ContentXmlOverrides.FindEquivalent(field.Parent, edited.Root) is XElement owner
                )
                    owner.Add(new XElement(field));
                else
                    continue;
                restored++;
            }
            if (restored == 0)
                continue;
            edits.Add((document, edited));
            references += restored;
        }

        int copied = 0;
        foreach (string address in files.OrderBy(path => path, StringComparer.Ordinal))
        {
            if (working.ResolveFile(address) != null)
                continue;
            string source = reference.ResolveFile(address);
            string destination = Path.GetFullPath(address[5..], project.PackRootPath);
            CopyMissingFile(project, source, destination);
            copied++;
        }
        using (project.BeginEdit("Restore missing travel artwork"))
            foreach (var edit in edits)
                project.SetDocumentText(
                    edit.Document,
                    edit.Xml.ToString(SaveOptions.DisableFormatting)
                );
        return (copied, references);
    }

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

    /// <summary>
    /// Identifies authored travel imagery, including nested theme references.
    /// </summary>
    /// <param name="field">The authored XML value.</param>
    /// <returns>Whether the value identifies a travel image in the pack.</returns>
    private static bool IsTravelReference(XElement field)
    {
        if (field.HasElements)
            return false;
        string address = field.Value.Trim();
        return address.StartsWith("Pack/", StringComparison.Ordinal)
            && (
                field.Name.LocalName
                    is "InTransitSmallImagePath"
                        or "InTransitImagePath"
                        or "BattleResultInTransitImagePath"
                        or "EnrouteBackgroundImagePath"
                        or "PersonnelEnrouteBackgroundImagePath"
                        or "FleetBannerEnrouteImagePath"
                        or "FleetListEnrouteIconImagePath"
                        or "EnrouteImagePath"
                || IsTravelFileName(address)
            );
    }

    /// <summary>
    /// Identifies transit imagery whose file name describes its gameplay state.
    /// </summary>
    /// <param name="address">The media address or file name.</param>
    /// <returns>Whether the file name denotes travel artwork.</returns>
    private static bool IsTravelFileName(string address)
    {
        string name = Path.GetFileNameWithoutExtension(address);
        return name.Contains("in-transit", StringComparison.OrdinalIgnoreCase)
            || name.Contains("enroute", StringComparison.OrdinalIgnoreCase)
            || name.Contains("hyperspace", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks the file extensions accepted by the artwork importer.
    /// </summary>
    /// <param name="path">The resolved media filename.</param>
    /// <returns>Whether it is a supported image.</returns>
    private static bool IsImageFile(string path) =>
        _imageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Installs the reference image without overwriting a file already present in the working pack.
    /// </summary>
    /// <param name="project">The owner of the write.</param>
    /// <param name="source">The installed image.</param>
    /// <param name="destination">The path reserved for the missing local image.</param>
    private static void CopyMissingFile(
        ContentPackEditorProject project,
        string source,
        string destination
    )
    {
        ContentEditorPaths.VerifyWritePath(project, destination);
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.Copy(source, temporary);
            File.Move(temporary, destination);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
