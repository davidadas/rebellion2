using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Rebellion.Game.Combat;

/// <summary>
/// Loads battle-map definitions from external map packages.
/// </summary>
internal static class BattleMapLoader
{
    private const int _supportedFormatVersion = 1;
    private const string _manifestEntryName = "map.xml";

    /// <summary>
    /// Loads one battle map from an rmap package.
    /// </summary>
    /// <param name="filePath">The absolute rmap package path.</param>
    /// <returns>The loaded battle-map definition.</returns>
    internal static BattleMap Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("An rmap file path is required.", nameof(filePath));
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Battle map not found: {filePath}", filePath);

        using FileStream stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );
        using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);
        ZipArchiveEntry manifestEntry = archive.GetEntry(_manifestEntryName);
        if (manifestEntry == null)
            throw new InvalidDataException("The battle-map package is missing map.xml.");

        using Stream manifestStream = manifestEntry.Open();
        XDocument document = XDocument.Load(manifestStream, LoadOptions.None);
        return DeserializeManifest(document);
    }

    /// <summary>
    /// Deserializes the gameplay data from a battle-map package manifest.
    /// </summary>
    /// <param name="document">The package manifest document.</param>
    /// <returns>The deserialized battle map.</returns>
    private static BattleMap DeserializeManifest(XDocument document)
    {
        XElement root = document.Root;
        if (root == null || root.Name != "RebellionMap")
            throw new InvalidDataException("The battle-map manifest requires a RebellionMap root.");

        int formatVersion = ReadRequiredIntAttribute(root, "FormatVersion");
        if (formatVersion != _supportedFormatVersion)
        {
            throw new InvalidDataException(
                $"Unsupported battle-map format version {formatVersion}; expected {_supportedFormatVersion}."
            );
        }

        XElement spaceMap = GetRequiredElement(root, "SpaceMap");
        BattleMap battleMap = new BattleMap
        {
            InstanceID = GetRequiredElementValue(spaceMap, "ID"),
            Kind = BattleKind.Space,
            PlayableBounds = ReadBounds(GetRequiredElement(spaceMap, "PlayableVolume")),
        };

        XElement spawnVolumes = GetRequiredElement(spaceMap, "SpawnVolumes");
        foreach (XElement spawnVolume in spawnVolumes.Elements("SpawnVolume"))
        {
            string purpose = GetRequiredAttributeValue(spawnVolume, "Purpose");
            if (!string.Equals(purpose, "InitialDeployment", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Unsupported battle-map spawn purpose '{purpose}'."
                );
            }

            battleMap
                .GetDeploymentRegions()
                .Add(
                    new BattleMapDeploymentRegion
                    {
                        ParticipantSlotID = GetRequiredAttributeValue(
                            spawnVolume,
                            "ParticipantSlotID"
                        ),
                        Bounds = ReadBounds(spawnVolume),
                    }
                );
        }

        return battleMap;
    }

    /// <summary>
    /// Converts a center-and-size volume into axis-aligned map bounds.
    /// </summary>
    /// <param name="volume">The volume element containing Center and Size vectors.</param>
    /// <returns>The equivalent axis-aligned bounds.</returns>
    private static BattleMapBounds ReadBounds(XElement volume)
    {
        XElement center = GetRequiredElement(volume, "Center");
        XElement size = GetRequiredElement(volume, "Size");
        float centerX = ReadRequiredFloatAttribute(center, "X");
        float centerY = ReadRequiredFloatAttribute(center, "Y");
        float centerZ = ReadRequiredFloatAttribute(center, "Z");
        float sizeX = ReadRequiredFloatAttribute(size, "X");
        float sizeY = ReadRequiredFloatAttribute(size, "Y");
        float sizeZ = ReadRequiredFloatAttribute(size, "Z");
        if (sizeX < 0f || sizeY < 0f || sizeZ < 0f)
            throw new InvalidDataException("Battle-map volume sizes cannot be negative.");

        return new BattleMapBounds
        {
            MinimumX = centerX - sizeX / 2f,
            MaximumX = centerX + sizeX / 2f,
            MinimumY = centerY - sizeY / 2f,
            MaximumY = centerY + sizeY / 2f,
            MinimumZ = centerZ - sizeZ / 2f,
            MaximumZ = centerZ + sizeZ / 2f,
        };
    }

    /// <summary>
    /// Returns the single required child element with the specified name.
    /// </summary>
    /// <param name="parent">The parent element.</param>
    /// <param name="name">The required child-element name.</param>
    /// <returns>The required child element.</returns>
    private static XElement GetRequiredElement(XElement parent, string name)
    {
        XElement[] elements = parent.Elements(name).ToArray();
        if (elements.Length != 1)
        {
            throw new InvalidDataException(
                $"Element '{parent.Name}' requires exactly one '{name}' child."
            );
        }

        return elements[0];
    }

    /// <summary>
    /// Returns a required nonblank child-element value.
    /// </summary>
    /// <param name="parent">The parent element.</param>
    /// <param name="name">The required child-element name.</param>
    /// <returns>The required value.</returns>
    private static string GetRequiredElementValue(XElement parent, string name)
    {
        string value = GetRequiredElement(parent, name).Value;
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"Element '{name}' cannot be blank.");
        return value;
    }

    /// <summary>
    /// Returns a required nonblank attribute value.
    /// </summary>
    /// <param name="element">The element containing the attribute.</param>
    /// <param name="name">The required attribute name.</param>
    /// <returns>The required value.</returns>
    private static string GetRequiredAttributeValue(XElement element, string name)
    {
        string value = element.Attribute(name)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"Element '{element.Name}' requires attribute '{name}'."
            );
        }

        return value;
    }

    /// <summary>
    /// Reads a required invariant integer attribute.
    /// </summary>
    /// <param name="element">The element containing the attribute.</param>
    /// <param name="name">The required attribute name.</param>
    /// <returns>The parsed integer.</returns>
    private static int ReadRequiredIntAttribute(XElement element, string name)
    {
        string value = GetRequiredAttributeValue(element, name);
        if (
            !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
        )
        {
            throw new InvalidDataException(
                $"Attribute '{name}' on element '{element.Name}' must be an integer."
            );
        }

        return result;
    }

    /// <summary>
    /// Reads a required invariant floating-point attribute.
    /// </summary>
    /// <param name="element">The element containing the attribute.</param>
    /// <param name="name">The required attribute name.</param>
    /// <returns>The parsed floating-point value.</returns>
    private static float ReadRequiredFloatAttribute(XElement element, string name)
    {
        string value = GetRequiredAttributeValue(element, name);
        if (
            !float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float result
            )
        )
        {
            throw new InvalidDataException(
                $"Attribute '{name}' on element '{element.Name}' must be a number."
            );
        }

        return result;
    }
}
