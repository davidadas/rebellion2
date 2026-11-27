using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;

/// <summary>
/// The role of an installed or authored package.
/// </summary>
public enum ContentPackageType
{
    Unknown,
    GamePack,
    Mod,
}

/// <summary>
/// Identifies the shared package manifest and its compatible legacy forms.
/// </summary>
public static class ContentPackageManifest
{
    public const string FileName = "pack.xml";
    public const string RootName = "ContentPackage";
    internal const string LegacyPackRoot = "ContentPack";
    private const string _legacyModFileName = "mod.xml";
    private const string _typeAttribute = "Type";

    /// <summary>
    /// Checks whether a folder contains a package manifest candidate.
    /// </summary>
    /// <param name="folder">The direct package folder.</param>
    /// <returns>Whether either supported manifest filename exists.</returns>
    public static bool Exists(string folder) =>
        File.Exists(Path.Combine(folder, FileName))
        || File.Exists(Path.Combine(folder, _legacyModFileName));

    /// <summary>
    /// Finds the manifest owned by one package folder.
    /// </summary>
    /// <param name="folder">The direct package folder.</param>
    /// <param name="legacyPreference">The existing loader role to preserve when both legacy files are present.</param>
    /// <returns>The single package manifest path.</returns>
    public static string Find(
        string folder,
        ContentPackageType legacyPreference = ContentPackageType.Unknown
    )
    {
        string manifest = Path.Combine(folder, FileName);
        string legacyMod = Path.Combine(folder, _legacyModFileName);
        if (File.Exists(manifest))
        {
            if (File.Exists(legacyMod))
                return legacyPreference switch
                {
                    ContentPackageType.GamePack => manifest,
                    ContentPackageType.Mod => legacyMod,
                    _ => throw new InvalidDataException(
                        "Choose a folder with a single package manifest."
                    ),
                };
            return manifest;
        }
        if (File.Exists(legacyMod))
            return legacyMod;
        throw new FileNotFoundException("Package manifest not found in " + folder + ".");
    }

    /// <summary>
    /// Reads the package role declared by a manifest or its legacy root.
    /// </summary>
    /// <param name="path">The absolute manifest path.</param>
    /// <returns>The role, or unknown when the shared manifest has no recognized type.</returns>
    public static ContentPackageType ReadType(string path)
    {
        using XmlReader reader = XmlReader.Create(path);
        reader.MoveToContent();
        if (
            string.Equals(
                Path.GetFileName(path),
                _legacyModFileName,
                StringComparison.OrdinalIgnoreCase
            )
            && reader.Name != nameof(ContentModDefinition)
        )
            throw new InvalidDataException("Use pack.xml for the shared package manifest.");
        return ReadType(reader.Name, reader.GetAttribute(_typeAttribute));
    }

    /// <summary>
    /// Reads a manifest role from its root name and type attribute.
    /// </summary>
    /// <param name="root">The root element name.</param>
    /// <param name="type">The optional type attribute value.</param>
    /// <returns>The package role.</returns>
    internal static ContentPackageType ReadType(string root, string type) =>
        root switch
        {
            LegacyPackRoot => ContentPackageType.GamePack,
            nameof(ContentModDefinition) => ContentPackageType.Mod,
            RootName => type switch
            {
                nameof(ContentPackageType.GamePack) or "Era" => ContentPackageType.GamePack,
                nameof(ContentPackageType.Mod) => ContentPackageType.Mod,
                _ => ContentPackageType.Unknown,
            },
            _ => throw new InvalidDataException(
                "Unrecognized package manifest root '" + root + "'."
            ),
        };

    /// <summary>
    /// Stamps the shared root and role on a serialized package manifest.
    /// </summary>
    /// <param name="root">The serialized manifest root.</param>
    /// <param name="type">The selected package role.</param>
    public static void SetType(XElement root, ContentPackageType type)
    {
        if (type is not (ContentPackageType.GamePack or ContentPackageType.Mod))
            throw new ArgumentOutOfRangeException(nameof(type));
        root.Name = RootName;
        root.SetAttributeValue(_typeAttribute, type.ToString());
    }

    /// <summary>
    /// Makes a typed game pack readable by existing campaign-override logic.
    /// </summary>
    /// <param name="root">The authored game pack manifest.</param>
    /// <returns>The same XML element using the existing campaign root.</returns>
    internal static XElement AsCampaignRoot(XElement root)
    {
        if (root.Name != RootName)
            return root;
        if (
            ReadType(RootName, (string)root.Attribute(_typeAttribute))
            != ContentPackageType.GamePack
        )
            throw new InvalidDataException("The package manifest must declare Type=\"GamePack\".");
        root.Name = LegacyPackRoot;
        root.Attribute(_typeAttribute)?.Remove();
        return root;
    }
}
