using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Offers reusable planet artwork from the pack's shared folder and existing map references.
/// </summary>
internal static class ContentEditorPlanetArtwork
{
    internal const string PoolAddress = "Pack/Shared/Planets";
    internal static IReadOnlyList<string> Members { get; } =
        new[] { "PlanetIconPath", "EncyclopediaImagePath" };

    /// <summary>
    /// Identifies the two independently authored planet image slots.
    /// </summary>
    /// <param name="field">The selected editor field.</param>
    /// <returns>Whether the field selects a planet icon or encyclopedia image.</returns>
    internal static bool IsImageField(ContentPackEditorField field) =>
        field?.Record.Kind == "Planet"
        && Members.Contains(field.MemberName)
        && (field.Container == null || field.Container == field.Record.Element);

    /// <summary>
    /// Keeps map icons with map properties and article artwork with its encyclopedia entry.
    /// </summary>
    /// <param name="field">The planet image field being placed.</param>
    /// <returns>The inspector section owning this image role.</returns>
    internal static string GetSection(ContentPackEditorField field) =>
        field.MemberName == "PlanetIconPath" ? "General" : "Encyclopedia";

    /// <summary>
    /// Finds the pack-local destination that overrides the selected shared image's resolved file.
    /// </summary>
    /// <param name="project">The current authoring project.</param>
    /// <param name="field">The selected planet image.</param>
    /// <returns>The writable destination, or null for empty, missing, or application artwork.</returns>
    internal static string GetReplacementPath(
        ContentPackEditorProject project,
        ContentPackEditorField field
    )
    {
        project.RequireOwnedRecord(field.Record);
        if (
            !IsImageField(field)
            || string.IsNullOrWhiteSpace(field.Value)
            || !field.Value.StartsWith("Pack/", StringComparison.Ordinal)
        )
            return null;
        ContentFileResolver resolver = ContentPackLoader.CreateFolderResolver(
            project.ContentRootPath,
            project.PackRootPath,
            project.Definition
        );
        string file;
        try
        {
            file = resolver.ResolveFile(field.Value, field.MediaExtensions);
        }
        catch (ArgumentException)
        {
            return null;
        }
        if (file == null)
            return null;
        string owner = resolver.PackRootPaths.FirstOrDefault(root =>
            ContentEditorPaths.Contains(root, file)
        );
        return owner == null
            ? null
            : Path.GetFullPath(Path.GetRelativePath(owner, file), project.PackRootPath);
    }

    /// <summary>
    /// Replaces a shared image in the working pack while leaving inherited files untouched.
    /// </summary>
    /// <param name="project">The pack receiving the replacement.</param>
    /// <param name="field">The planet slot referencing the shared image.</param>
    /// <param name="source">The replacement image file.</param>
    internal static void Replace(
        ContentPackEditorProject project,
        ContentPackEditorField field,
        string source
    )
    {
        string target =
            GetReplacementPath(project, field)
            ?? throw new ArgumentException("Choose an existing pack image.", nameof(field));
        if (!ContentPackEditorStorage.HasMatchingMediaFormat(source, target))
            throw new ArgumentException(
                "Replacing a shared image requires the same file type.",
                nameof(source)
            );
        ContentEditorPaths.VerifyWritePath(project, target);
        if (string.Equals(Path.GetFullPath(source), target, StringComparison.OrdinalIgnoreCase))
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        using (project.BeginEdit("Replace shared planet image"))
            project.CopyAsset(target, source);
    }

    /// <summary>
    /// Lists images assigned to the same planet image role without copying or rewriting them.
    /// </summary>
    /// <param name="project">The current authoring project.</param>
    /// <param name="field">The planet image slot receiving the choices.</param>
    /// <returns>Stored image addresses and unambiguous picker captions.</returns>
    internal static IReadOnlyDictionary<string, string> GetChoices(
        ContentPackEditorProject project,
        ContentPackEditorField field
    )
    {
        project.RequireOwnedRecord(field.Record);
        if (!IsImageField(field))
            throw new ArgumentException("Choose a planet image field.", nameof(field));
        ContentFileResolver resolver = ContentPackLoader.CreateFolderResolver(
            project.ContentRootPath,
            project.PackRootPath,
            project.Definition
        );
        ContentPackEditorCatalog[] maps = project
            .GetCatalogs()
            .Where(catalog => catalog.IsMap)
            .ToArray();
        IEnumerable<string> referenced = maps.SelectMany(catalog => catalog.Records)
            .SelectMany(record => record.Planets)
            .Select(planet => planet.Element.Element(field.MemberName)?.Value)
            .Concat(
                maps.SelectMany(catalog =>
                    catalog.Document.GetInheritedRecordValues("Planet", field.MemberName)
                )
            );
        var choices = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [string.Empty] = "No image",
        };
        var files = new HashSet<string>(StringComparer.Ordinal);
        foreach (
            string address in new[] { field.Value }
                .Concat(referenced)
                .Where(address => !string.IsNullOrWhiteSpace(address))
                .Distinct(StringComparer.Ordinal)
        )
        {
            string file;
            try
            {
                file = resolver.ResolveFile(address, field.MediaExtensions);
            }
            catch (ArgumentException)
            {
                file = null;
            }
            if (file != null && !files.Add(file))
                continue;
            if (file == null && address != field.Value)
                continue;
            string origin =
                file == null ? "missing"
                : ContentEditorPaths.Contains(project.PackRootPath, file) ? "this pack"
                : "inherited";
            choices[address] = address.StartsWith(PoolAddress + "/", StringComparison.Ordinal)
                ? address[(PoolAddress.Length + 1)..] + " (" + origin + ")"
                : address + " (" + origin + ")";
        }
        return choices
            .OrderBy(pair => pair.Key.Length == 0 ? 0 : 1)
            .ThenBy(pair => pair.Value, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }
}
