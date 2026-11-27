using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

/// <summary>
/// Explains writes that active mods make to the same content address.
/// </summary>
internal static class ContentModOverrides
{
    /// <summary>
    /// Compares only files reached through two or more mods.
    /// </summary>
    /// <param name="mods">The active manifests in load order.</param>
    /// <param name="roots">The parallel content roots.</param>
    /// <returns>Warnings per mod that is overridden.</returns>
    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> Describe(
        IReadOnlyList<ContentModDefinition> mods,
        IReadOnlyList<string> roots
    )
    {
        var warnings = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (mods.Count < 2)
            return new Dictionary<string, IReadOnlyList<string>>();

        var groups = new SortedDictionary<string, List<(ContentModDefinition Mod, string File)>>(
            StringComparer.Ordinal
        );
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };
        for (int index = 0; index < mods.Count; index++)
            foreach (string scope in new[] { "Application", "Pack" })
            {
                string directory = Path.Combine(roots[index], scope);
                if (!Directory.Exists(directory))
                    continue;
                foreach (string file in Directory.EnumerateFiles(directory, "*", options))
                {
                    string address = Path.GetRelativePath(roots[index], file).Replace('\\', '/');
                    if (address == "Pack/pack.xml" && !mods[index].ExtendsPack)
                        continue;
                    if (!groups.TryGetValue(address, out var entries))
                    {
                        entries = new List<(ContentModDefinition, string)>();
                        groups.Add(address, entries);
                    }
                    entries.Add((mods[index], file));
                }
            }
        foreach (var group in groups.Where(group => group.Value.Count > 1))
        {
            List<(ContentModDefinition Mod, string File)> layers = group.Value;
            for (int index = 0; index < layers.Count - 1; index++)
            {
                var earlier = layers[index];
                var winners = new Dictionary<string, (string ModName, string Value)>(
                    StringComparer.Ordinal
                );
                for (int laterIndex = layers.Count - 1; laterIndex > index; laterIndex--)
                {
                    var later = layers[laterIndex];
                    foreach (var edit in DescribeOverlap(group.Key, earlier.File, later.File))
                        winners.TryAdd(edit.Path, (later.Mod.DisplayName, edit.Value));
                    if (winners.ContainsKey(group.Key))
                        break;
                }
                if (winners.Count == 0)
                    continue;
                if (!warnings.TryGetValue(earlier.Mod.ID, out List<string> messages))
                {
                    messages = new List<string>();
                    warnings.Add(earlier.Mod.ID, messages);
                }
                messages.AddRange(
                    winners.Select(winner =>
                        winner.Value.Value == null
                            ? $"{winner.Value.ModName} overrides {winner.Key}."
                            : $"{winner.Value.ModName} sets {DescribeSetting(group.Key, winner.Key)} to {winner.Value.Value}."
                    )
                );
            }
        }
        return warnings.ToDictionary(
            item => item.Key,
            item => (IReadOnlyList<string>)item.Value,
            StringComparer.Ordinal
        );
    }

    /// <summary>
    /// Displays a setting by its closest record ID and its path within that record.
    /// </summary>
    /// <param name="address">The shared XML file.</param>
    /// <param name="editPath">The file address followed by the authored setting path.</param>
    /// <returns>The setting label shown in the Mods menu.</returns>
    private static string DescribeSetting(string address, string editPath)
    {
        string path = editPath[address.Length..];
        int recordEnd = path.LastIndexOf(']');
        int recordStart = recordEnd < 0 ? -1 : path.LastIndexOf('[', recordEnd);
        if (recordStart < 0)
            return Path.GetFileNameWithoutExtension(address)
                + path[path.IndexOf('/', 1)..].Replace("/", " / ");

        string id = Uri.UnescapeDataString(path[(recordStart + 1)..recordEnd]);
        return id + path[(recordEnd + 1)..].Replace("/", " / ");
    }

    /// <summary>
    /// Finds whole-file or sparse-setting overlaps for one address.
    /// </summary>
    /// <param name="address">The address shared by both mods.</param>
    /// <param name="earlier">The lower priority file.</param>
    /// <param name="later">The higher priority file.</param>
    /// <returns>The addresses or settings superseded by the later file.</returns>
    private static IReadOnlyList<(string Path, string Value)> DescribeOverlap(
        string address,
        string earlier,
        string later
    )
    {
        if (!address.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            return new[] { (address, (string)null) };

        try
        {
            if (address == "Pack/" + ContentArtworkAliases.FileName)
            {
                var lowerAliases = ContentArtworkAliases.Load(Path.GetDirectoryName(earlier));
                var higherAliases = ContentArtworkAliases.Load(Path.GetDirectoryName(later));
                return lowerAliases
                    .Where(alias =>
                        higherAliases.TryGetValue(alias.Key, out string target)
                        && target != alias.Value
                    )
                    .Select(alias => (alias.Key, (string)null))
                    .ToArray();
            }
            string higher = File.ReadAllText(later);
            if (!ContentXmlOverrides.IsOverlay(higher))
                return new[] { (address, (string)null) };
            string lower = File.ReadAllText(earlier);
            return ContentXmlOverrides
                .GetOverlappingSettings(lower, higher)
                .Select(edit => (address + edit.Path, edit.Value))
                .ToArray();
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or InvalidDataException
                        or UnauthorizedAccessException
                        or XmlException
                        or FormatException
            )
        {
            return new[] { (address, (string)null) };
        }
    }
}
