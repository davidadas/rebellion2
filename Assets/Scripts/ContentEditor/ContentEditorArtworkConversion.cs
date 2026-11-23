using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Organizes older editor artwork without deleting source files or invalidating saved addresses.
/// </summary>
internal static class ContentEditorArtworkConversion
{
    private static readonly string[] _extensions = { ".png", ".jpg", ".jpeg" };

    /// <summary>
    /// Plans and checks all destinations before copying images and updating the working documents.
    /// </summary>
    /// <param name="project">The writable pack being saved.</param>
    /// <returns>The author-facing conversion summary.</returns>
    internal static string Convert(ContentPackEditorProject project)
    {
        Dictionary<string, string> aliases = ContentArtworkAliases.Load(project.PackRootPath);
        ContentFileResolver resolver = ContentPackLoader.CreateFolderResolver(
            project.ContentRootPath,
            project.PackRootPath,
            project.Definition
        );
        ContentPackEditorField[] fields = project
            .GetCatalogs()
            .SelectMany(catalog => catalog.Records)
            .SelectMany(record => record.GetFields())
            .Where(field => ContentEditorArtworkPaths.GetAddress(field) != null)
            .ToArray();
        var candidates =
            new List<(
                ContentPackEditorField Field,
                string Source,
                string Address,
                string Target
            )>();
        foreach (
            ContentPackEditorField field in fields.Where(field =>
                ContentEditorArtworkPaths.IsLegacy(field.Value)
            )
        )
        {
            string source =
                resolver.ResolveFile(field.Value, _extensions)
                ?? throw new FileNotFoundException(
                    $"Artwork for {field.Record.Name} / {field.Caption} is missing: {field.Value}"
                );
            if (!ContentEditorPaths.Contains(project.PackRootPath, source))
                continue;
            ContentEditorPaths.VerifyWritePath(project, source);
            string address = ContentEditorArtworkPaths.GetAddress(field);
            string target = Path.GetFullPath(
                address[5..] + Path.GetExtension(source).ToLowerInvariant(),
                project.PackRootPath
            );
            ContentEditorPaths.VerifyWritePath(project, target);
            candidates.Add((field, source, address, target));
        }
        var warnings = new List<string>();
        var shared = candidates
            .GroupBy(item => item.Source, StringComparer.OrdinalIgnoreCase)
            .Where(group =>
                group
                    .Select(item => item.Address)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count() > 1
            )
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
            if (
                aliases.TryGetValue(candidate.Field.Value, out string retained)
                && retained == candidate.Field.Value
            )
                shared.Add(candidate.Source);
        foreach (string source in shared)
            warnings.Add(
                "Kept shared artwork at "
                    + Path.GetRelativePath(project.PackRootPath, source).Replace('\\', '/')
                    + "; its unit slots have different destinations."
            );
        var edits = candidates.Where(item => !shared.Contains(item.Source)).ToArray();
        var destinations = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var edit in edits)
        {
            byte[] bytes = File.ReadAllBytes(edit.Source);
            foreach (string extension in _extensions)
            {
                string other = Path.GetFullPath(
                    edit.Address[5..] + extension,
                    project.PackRootPath
                );
                if (File.Exists(other))
                    CheckDestination(project, destinations, other, bytes);
            }
            CheckDestination(project, destinations, edit.Target, bytes);
            string recipe = ContentPackEditorStorage.ReadGeneratedLayers(project, edit.Field);
            if (recipe != null)
            {
                CheckDestination(
                    project,
                    destinations,
                    edit.Target + ".layers.json",
                    Encoding.UTF8.GetBytes(recipe)
                );
                CheckDestination(
                    project,
                    destinations,
                    edit.Target + ".layers.hash",
                    Encoding.UTF8.GetBytes(
                        ContentPackEditorStorage.GetArtworkRevision(bytes, recipe)
                    )
                );
            }
        }
        foreach (var candidate in candidates.Where(candidate => shared.Contains(candidate.Source)))
        {
            if (
                aliases.TryGetValue(candidate.Field.Value, out string previous)
                && previous != candidate.Field.Value
            )
                throw new InvalidDataException(
                    "Shared artwork already has a different address mapping: "
                        + candidate.Field.Value
                );
            aliases[candidate.Field.Value] = candidate.Field.Value;
        }
        if (edits.Length == 0)
        {
            if (shared.Count > 0)
                project.WriteAsset(
                    Path.Combine(project.PackRootPath, ContentArtworkAliases.FileName),
                    Encoding.UTF8.GetBytes(ContentArtworkAliases.Serialize(aliases))
                );
            return string.Join(" ", warnings);
        }
        foreach (var edit in edits)
        {
            AddAlias(aliases, edit.Field.Value, edit.Address);
            string sourceAddress =
                "Pack/"
                + Path.GetRelativePath(project.PackRootPath, edit.Source).Replace('\\', '/');
            AddAlias(aliases, sourceAddress, edit.Address);
            foreach (string revision in FindPreviousRevisions(project, edit.Field))
                AddAlias(aliases, revision, edit.Address);
        }
        string aliasText = ContentArtworkAliases.Serialize(aliases);
        string backup = Path.Combine(
            project.PackRootPath,
            ContentEditorAssetChange.EditorDirectory,
            "Backups",
            Guid.NewGuid().ToString("N")
        );
        foreach (
            ContentPackEditorDocument document in edits
                .Select(item => item.Field.Document)
                .Distinct()
        )
        {
            string target = Path.GetFullPath(document.Address[5..], backup);
            ContentEditorPaths.VerifyWritePath(project, target);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            ContentPackEditorStorage.WriteBytesAtomically(
                target,
                Encoding.UTF8.GetBytes(document.AuthoredText)
            );
        }
        string aliasPath = Path.Combine(project.PackRootPath, ContentArtworkAliases.FileName);
        if (File.Exists(aliasPath))
            File.Copy(aliasPath, Path.Combine(backup, ContentArtworkAliases.FileName));
        using (project.BeginEdit("Organize unit artwork"))
        {
            foreach (var destination in destinations)
                project.WriteAsset(destination.Key, destination.Value);
            project.WriteAsset(aliasPath, Encoding.UTF8.GetBytes(aliasText));
            foreach (var edit in edits)
                project.SetField(edit.Field, edit.Address);
        }
        string report =
            $"Organized {edits.Length} artwork assignments. Original images were retained. "
            + string.Join(" ", warnings)
            + " Backup: "
            + Path.GetRelativePath(project.PackRootPath, backup).Replace('\\', '/');
        ContentPackEditorStorage.WriteBytesAtomically(
            Path.Combine(backup, "report.txt"),
            Encoding.UTF8.GetBytes(report)
        );
        return report;
    }

    /// <summary>
    /// Preserves a unique older address when its artwork is replaced before the pack is saved.
    /// </summary>
    /// <param name="project">The pack owning the old address.</param>
    /// <param name="field">The artwork being replaced.</param>
    /// <param name="address">The new stable slot address.</param>
    internal static void PreserveAddress(
        ContentPackEditorProject project,
        ContentPackEditorField field,
        string address
    )
    {
        if (
            string.IsNullOrWhiteSpace(field.Value)
            || field.Value == address
            || !field.Value.StartsWith("Pack/", StringComparison.Ordinal)
        )
            return;
        int consumers = project
            .GetCatalogs()
            .SelectMany(catalog => catalog.Records)
            .SelectMany(record => record.GetFields())
            .Count(candidate => candidate.IsMedia && candidate.Values.Contains(field.Value));
        Dictionary<string, string> aliases = ContentArtworkAliases.Load(project.PackRootPath);
        if (aliases.TryGetValue(field.Value, out string retained) && retained == field.Value)
            return;
        if (consumers > 1)
        {
            if (aliases.ContainsKey(field.Value))
                return;
            aliases[field.Value] = field.Value;
            project.WriteAsset(
                Path.Combine(project.PackRootPath, ContentArtworkAliases.FileName),
                Encoding.UTF8.GetBytes(ContentArtworkAliases.Serialize(aliases))
            );
            return;
        }
        AddAlias(aliases, field.Value, address);
        foreach (string revision in FindPreviousRevisions(project, field))
            AddAlias(aliases, revision, address);
        project.WriteAsset(
            Path.Combine(project.PackRootPath, ContentArtworkAliases.FileName),
            Encoding.UTF8.GetBytes(ContentArtworkAliases.Serialize(aliases))
        );
    }

    /// <summary>
    /// Rejects differing images assigned to the same destination before conversion writes anything.
    /// </summary>
    /// <param name="project">The pack owning the proposed destination.</param>
    /// <param name="destinations">The checked file writes.</param>
    /// <param name="target">The proposed file.</param>
    /// <param name="bytes">The bytes that must be preserved.</param>
    private static void CheckDestination(
        ContentPackEditorProject project,
        IDictionary<string, byte[]> destinations,
        string target,
        byte[] bytes
    )
    {
        ContentEditorPaths.VerifyWritePath(project, target);
        if (
            destinations.TryGetValue(target, out byte[] pending)
                && !pending.AsSpan().SequenceEqual(bytes)
            || File.Exists(target) && !File.ReadAllBytes(target).AsSpan().SequenceEqual(bytes)
        )
            throw new IOException(
                "Artwork conversion would replace a different file: "
                    + Path.GetRelativePath(project.PackRootPath, target)
            );
        destinations[target] = bytes;
    }

    /// <summary>
    /// Retains an unambiguous image redirect without changing an existing mapping's ownership.
    /// </summary>
    /// <param name="aliases">The pack's address map.</param>
    /// <param name="source">The previous image address.</param>
    /// <param name="target">The stable image slot.</param>
    private static void AddAlias(IDictionary<string, string> aliases, string source, string target)
    {
        if (source == target)
            return;
        if (aliases.TryGetValue(source, out string retained) && retained == source)
            return;
        if (aliases.TryGetValue(source, out string existing) && existing != target)
            throw new InvalidDataException(
                "An older artwork address already belongs to a different slot: " + source
            );
        aliases[source] = target;
    }

    /// <summary>
    /// Finds immutable outputs previously written for this exact unit and image member.
    /// </summary>
    /// <param name="project">The writable project.</param>
    /// <param name="field">The unit image slot.</param>
    /// <returns>The previous generated addresses that saved games may still reference.</returns>
    private static IEnumerable<string> FindPreviousRevisions(
        ContentPackEditorProject project,
        ContentPackEditorField field
    )
    {
        string folder =
            "Pack/Assets/Generated/"
            + ContentPackEditorStorage.GetGeneratedPathSegment(field.Record.Kind)
            + "/"
            + ContentPackEditorStorage.GetGeneratedPathSegment(field.Record.ID);
        string directory = Path.GetFullPath(folder[5..], project.PackRootPath);
        ContentEditorPaths.VerifyWritePath(project, directory);
        if (!Directory.Exists(directory))
            yield break;
        string name = ContentPackEditorStorage.GetGeneratedPathSegment(
            ContentPackEditorStorage.GetGeneratedMemberName(field.MemberName)
        );
        foreach (string file in Directory.EnumerateFiles(directory, name + "*.png"))
            if (
                Regex.IsMatch(
                    Path.GetFileName(file),
                    "^" + Regex.Escape(name) + "(?:-[0-9a-f]{24})?\\.png$"
                )
            )
                yield return folder + "/" + Path.GetFileName(file);
    }
}
