using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

/// <summary>
/// Keeps saved artwork addresses valid after a pack reorganizes its image files.
/// </summary>
public static class ContentArtworkAliases
{
    public const string FileName = "artwork-aliases.xml";

    /// <summary>
    /// Reads a pack layer's image-only redirects and rejects ambiguous or unsafe entries.
    /// </summary>
    /// <param name="root">The physical pack layer.</param>
    /// <returns>The legacy-to-current image addresses owned by the layer.</returns>
    public static Dictionary<string, string> Load(string root)
    {
        string path = Path.Combine(root, FileName);
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path))
            return aliases;
        using XmlReader reader = XmlReader.Create(
            path,
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }
        );
        XElement xml = XElement.Load(reader);
        if (xml.Name != "ArtworkAliases")
            throw new InvalidDataException("The artwork address map has an invalid root.");
        foreach (XElement entry in xml.Elements())
        {
            string source = entry.Attribute("From")?.Value;
            string target = entry.Attribute("To")?.Value;
            if (
                entry.Name != "Image"
                || !IsImageAddress(source)
                || !IsImageAddress(target)
                || !aliases.TryAdd(source, target)
            )
                throw new InvalidDataException(
                    "The artwork address map contains an invalid or duplicate image address."
                );
        }
        Validate(aliases);
        return aliases;
    }

    /// <summary>
    /// Rejects unsafe image addresses and cycles before reading or publishing a map.
    /// </summary>
    /// <param name="aliases">The complete pack-local image address map.</param>
    private static void Validate(IReadOnlyDictionary<string, string> aliases)
    {
        foreach (var alias in aliases)
            if (!IsImageAddress(alias.Key) || !IsImageAddress(alias.Value))
                throw new InvalidDataException(
                    "The artwork address map contains an unsafe image address."
                );
        foreach (string source in aliases.Keys)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            string current = source;
            while (aliases.TryGetValue(current, out string next))
            {
                // An identity entry retains a shared image at its existing address.
                if (next == current)
                    break;
                if (!visited.Add(current))
                    throw new InvalidDataException("The artwork address map contains a cycle.");
                current = next;
            }
        }
    }

    /// <summary>
    /// Serializes a validated map in stable address order.
    /// </summary>
    /// <param name="aliases">The legacy-to-current addresses.</param>
    /// <returns>The complete XML file contents.</returns>
    public static string Serialize(IReadOnlyDictionary<string, string> aliases)
    {
        Validate(aliases);
        return new XElement(
            "ArtworkAliases",
            aliases
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new XElement(
                    "Image",
                    new XAttribute("From", pair.Key),
                    new XAttribute("To", pair.Value)
                ))
        ).ToString();
    }

    /// <summary>
    /// Checks that an alias is a pack-relative image address without directory traversal.
    /// </summary>
    /// <param name="address">The authored image address.</param>
    /// <returns>Whether the address can identify an image safely.</returns>
    private static bool IsImageAddress(string address)
    {
        if (
            string.IsNullOrWhiteSpace(address)
            || !address.StartsWith("Pack/", StringComparison.Ordinal)
            || address.Contains('\\')
            || address.Split('/').Any(part => part is "" or "." or ".." || part.Contains(':'))
        )
            return false;
        string extension = Path.GetExtension(address);
        return extension.Length == 0
            || new[] { ".png", ".jpg", ".jpeg" }.Contains(
                extension,
                StringComparer.OrdinalIgnoreCase
            );
    }
}
