using System;
using System.Collections.Generic;
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
    private const string _assetEntryPrefix = "assets/";
    private const int _maximumEntryCount = 256;
    private const long _maximumAssetBytes = 64L * 1024L * 1024L;
    private const long _maximumTotalAssetBytes = 256L * 1024L * 1024L;

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
        ValidateEntries(archive);
        ZipArchiveEntry manifestEntry = archive.GetEntry(_manifestEntryName);
        if (manifestEntry == null)
            throw new InvalidDataException("The battle-map package is missing map.xml.");

        using Stream manifestStream = manifestEntry.Open();
        XDocument document = XDocument.Load(manifestStream, LoadOptions.None);
        BattleMap battleMap = DeserializeManifest(document);
        LoadEmbeddedAssets(archive, battleMap);
        return battleMap;
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
            Environment = ReadEnvironment(GetRequiredElement(spaceMap, "Environment")),
        };

        XElement objects = GetRequiredElement(spaceMap, "Objects");
        foreach (XElement planet in objects.Elements("Planet"))
            battleMap.GetPlanets().Add(ReadPlanet(planet));
        foreach (XElement starfield in objects.Elements("Starfield"))
            battleMap.GetStarfields().Add(ReadStarfield(starfield));
        foreach (XElement debrisField in objects.Elements("DebrisField"))
            battleMap.GetDebrisFields().Add(ReadDebrisField(debrisField));

        XElement lights = GetRequiredElement(spaceMap, "Lights");
        foreach (XElement directionalLight in lights.Elements("DirectionalLight"))
            battleMap.GetDirectionalLights().Add(ReadDirectionalLight(directionalLight));

        XElement cameraStarts = GetRequiredElement(spaceMap, "CameraStarts");
        foreach (XElement cameraStart in cameraStarts.Elements("CameraStart"))
        {
            float fieldOfView = ReadRequiredFloatElement(cameraStart, "FieldOfView");
            if (fieldOfView <= 0f || fieldOfView >= 180f)
            {
                throw new InvalidDataException(
                    "Battle-map camera field of view must be greater than zero and less than 180."
                );
            }

            battleMap
                .GetCameraStarts()
                .Add(
                    new BattleMapCameraStart
                    {
                        ParticipantSlotID = GetRequiredAttributeValue(
                            cameraStart,
                            "ParticipantSlotID"
                        ),
                        Position = ReadVector(GetRequiredElement(cameraStart, "Position")),
                        Rotation = ReadVector(GetRequiredElement(cameraStart, "Rotation")),
                        FieldOfView = fieldOfView,
                    }
                );
        }

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
                        Facing = ReadVector(GetRequiredElement(spawnVolume, "Facing")),
                    }
                );
        }

        return battleMap;
    }

    /// <summary>
    /// Validates package entry names, uniqueness, and count before reading package data.
    /// </summary>
    /// <param name="archive">The open map archive.</param>
    private static void ValidateEntries(ZipArchive archive)
    {
        if (archive.Entries.Count > _maximumEntryCount)
            throw new InvalidDataException("The battle-map package contains too many entries.");

        HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string path = NormalizeArchivePath(entry.FullName);
            if (!names.Add(path))
                throw new InvalidDataException($"Duplicate battle-map package entry '{path}'.");
            if (
                !string.Equals(path, _manifestEntryName, StringComparison.Ordinal)
                && !path.StartsWith(_assetEntryPrefix, StringComparison.Ordinal)
            )
            {
                throw new InvalidDataException($"Unknown battle-map package entry '{path}'.");
            }
        }
    }

    /// <summary>
    /// Loads the bounded asset payload stored alongside a map manifest.
    /// </summary>
    /// <param name="archive">The open map archive.</param>
    /// <param name="battleMap">The map that owns the assets.</param>
    private static void LoadEmbeddedAssets(ZipArchive archive, BattleMap battleMap)
    {
        long totalBytes = 0L;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string path = NormalizeArchivePath(entry.FullName);
            if (!path.StartsWith(_assetEntryPrefix, StringComparison.Ordinal))
                continue;
            if (entry.Length > _maximumAssetBytes)
            {
                throw new InvalidDataException($"Battle-map package asset '{path}' is too large.");
            }

            totalBytes += entry.Length;
            if (totalBytes > _maximumTotalAssetBytes)
                throw new InvalidDataException(
                    "The battle-map package contains too much asset data."
                );
            using Stream source = entry.Open();
            using MemoryStream destination = new MemoryStream((int)entry.Length);
            source.CopyTo(destination);
            battleMap.AddEmbeddedAsset(
                path.Substring(_assetEntryPrefix.Length),
                destination.ToArray()
            );
        }
    }

    /// <summary>
    /// Normalizes and validates one relative path inside a map archive.
    /// </summary>
    /// <param name="path">The archive path.</param>
    /// <returns>The normalized safe path.</returns>
    private static string NormalizeArchivePath(string path)
    {
        if (
            string.IsNullOrWhiteSpace(path)
            || path.IndexOf('\\') >= 0
            || path.StartsWith("/", StringComparison.Ordinal)
        )
        {
            throw new InvalidDataException($"Unsafe battle-map package entry '{path}'.");
        }

        string[] segments = path.Split('/');
        if (
            segments.Any(segment =>
                string.IsNullOrEmpty(segment)
                || string.Equals(segment, ".", StringComparison.Ordinal)
                || string.Equals(segment, "..", StringComparison.Ordinal)
            )
        )
        {
            throw new InvalidDataException($"Unsafe battle-map package entry '{path}'.");
        }

        return string.Join("/", segments);
    }

    /// <summary>
    /// Reads the scene-wide colors authored for a battle map.
    /// </summary>
    /// <param name="element">The Environment element.</param>
    /// <returns>The parsed environment.</returns>
    private static BattleMapEnvironment ReadEnvironment(XElement element)
    {
        return new BattleMapEnvironment
        {
            BackgroundColor = ReadColor(GetRequiredElement(element, "BackgroundColor")),
            AmbientSkyColor = ReadColor(GetRequiredElement(element, "AmbientSkyColor")),
            AmbientEquatorColor = ReadColor(GetRequiredElement(element, "AmbientEquatorColor")),
            AmbientGroundColor = ReadColor(GetRequiredElement(element, "AmbientGroundColor")),
        };
    }

    /// <summary>
    /// Reads one authored planet object.
    /// </summary>
    /// <param name="element">The Planet element.</param>
    /// <returns>The parsed planet.</returns>
    private static BattleMapPlanet ReadPlanet(XElement element)
    {
        float diameter = ReadRequiredFloatAttribute(element, "Diameter");
        float atmosphereRadiusRatio = ReadRequiredFloatAttribute(element, "AtmosphereRadiusRatio");
        if (diameter <= 0f)
            throw new InvalidDataException("Battle-map planet diameter must be positive.");
        if (atmosphereRadiusRatio <= 1f)
        {
            throw new InvalidDataException(
                "Battle-map planet atmosphere radius ratio must exceed one."
            );
        }

        return new BattleMapPlanet
        {
            InstanceID = GetRequiredAttributeValue(element, "ID"),
            ModelAssetPath = GetRequiredAttributeValue(element, "ModelAssetPath"),
            CloudTextureAssetPath = GetRequiredAttributeValue(element, "CloudTextureAssetPath"),
            Diameter = diameter,
            AtmosphereRadiusRatio = atmosphereRadiusRatio,
            CloudRotationSpeed = ReadRequiredFloatAttribute(element, "CloudRotationSpeed"),
            Position = ReadVector(GetRequiredElement(element, "Position")),
            Rotation = ReadVector(GetRequiredElement(element, "Rotation")),
            SunDirection = ReadVector(GetRequiredElement(element, "SunDirection")),
        };
    }

    /// <summary>
    /// Reads one authored directional light.
    /// </summary>
    /// <param name="element">The DirectionalLight element.</param>
    /// <returns>The parsed light.</returns>
    private static BattleMapDirectionalLight ReadDirectionalLight(XElement element)
    {
        float intensity = ReadRequiredFloatAttribute(element, "Intensity");
        if (intensity < 0f)
            throw new InvalidDataException("Battle-map light intensity cannot be negative.");

        return new BattleMapDirectionalLight
        {
            InstanceID = GetRequiredAttributeValue(element, "ID"),
            Intensity = intensity,
            CastShadows = ReadRequiredBoolAttribute(element, "CastShadows"),
            IsSun = ReadRequiredBoolAttribute(element, "IsSun"),
            Rotation = ReadVector(GetRequiredElement(element, "Rotation")),
            Color = ReadColor(GetRequiredElement(element, "Color")),
        };
    }

    /// <summary>
    /// Reads one deterministic camera-relative starfield.
    /// </summary>
    /// <param name="element">The Starfield element.</param>
    /// <returns>The parsed starfield.</returns>
    private static BattleMapStarfield ReadStarfield(XElement element)
    {
        BattleMapStarfield starfield = new BattleMapStarfield
        {
            InstanceID = GetRequiredAttributeValue(element, "ID"),
            Count = ReadRequiredIntAttribute(element, "Count"),
            Seed = ReadRequiredIntAttribute(element, "Seed"),
            InnerRadius = ReadRequiredFloatAttribute(element, "InnerRadius"),
            OuterRadius = ReadRequiredFloatAttribute(element, "OuterRadius"),
            MinimumSize = ReadRequiredFloatAttribute(element, "MinimumSize"),
            MaximumSize = ReadRequiredFloatAttribute(element, "MaximumSize"),
            MinimumBrightness = ReadRequiredFloatAttribute(element, "MinimumBrightness"),
            MaximumBrightness = ReadRequiredFloatAttribute(element, "MaximumBrightness"),
            SecondaryColorProbability = ReadRequiredFloatAttribute(
                element,
                "SecondaryColorProbability"
            ),
            PrimaryColor = ReadColor(GetRequiredElement(element, "PrimaryColor")),
            SecondaryColor = ReadColor(GetRequiredElement(element, "SecondaryColor")),
        };
        if (starfield.Count <= 0)
            throw new InvalidDataException("Battle-map starfield count must be positive.");
        if (starfield.InnerRadius <= 0f || starfield.OuterRadius < starfield.InnerRadius)
        {
            throw new InvalidDataException(
                "Battle-map starfield radii must define a positive outward range."
            );
        }
        if (starfield.MinimumSize <= 0f || starfield.MaximumSize < starfield.MinimumSize)
        {
            throw new InvalidDataException(
                "Battle-map starfield sizes must define a positive ascending range."
            );
        }
        if (
            starfield.MinimumBrightness < 0f
            || starfield.MaximumBrightness < starfield.MinimumBrightness
        )
        {
            throw new InvalidDataException(
                "Battle-map starfield brightness must define a nonnegative ascending range."
            );
        }
        if (starfield.SecondaryColorProbability < 0f || starfield.SecondaryColorProbability > 1f)
        {
            throw new InvalidDataException(
                "Battle-map starfield secondary color probability must be between zero and one."
            );
        }

        return starfield;
    }

    /// <summary>
    /// Reads one deterministic field of embedded debris models.
    /// </summary>
    /// <param name="element">The DebrisField element.</param>
    /// <returns>The parsed debris field.</returns>
    private static BattleMapDebrisField ReadDebrisField(XElement element)
    {
        BattleMapDebrisField debrisField = new BattleMapDebrisField
        {
            InstanceID = GetRequiredAttributeValue(element, "ID"),
            ModelAssetPath = GetRequiredAttributeValue(element, "ModelAssetPath"),
            ShowDistance = ReadRequiredFloatAttribute(element, "ShowDistance"),
            HideDistance = ReadRequiredFloatAttribute(element, "HideDistance"),
            CellCount = ReadRequiredIntAttribute(element, "CellCount"),
            CellNoise = ReadRequiredFloatAttribute(element, "CellNoise"),
            DebrisCountTarget = ReadRequiredFloatAttribute(element, "DebrisCountTarget"),
            MinimumScale = ReadRequiredFloatAttribute(element, "MinimumScale"),
            MaximumScale = ReadRequiredFloatAttribute(element, "MaximumScale"),
            ScaleBias = ReadRequiredFloatAttribute(element, "ScaleBias"),
            RandomRotation = ReadRequiredBoolAttribute(element, "RandomRotation"),
            Seed = ReadRequiredIntAttribute(element, "Seed"),
        };
        if (debrisField.ShowDistance <= 0f || debrisField.HideDistance < debrisField.ShowDistance)
        {
            throw new InvalidDataException(
                "Battle-map debris distances must define a positive outward range."
            );
        }
        if (
            debrisField.CellCount <= 0
            || debrisField.CellNoise < 0f
            || debrisField.CellNoise > 0.5f
        )
        {
            throw new InvalidDataException(
                "Battle-map debris cells require a positive count and noise between zero and one half."
            );
        }
        if (debrisField.DebrisCountTarget <= 0f)
            throw new InvalidDataException("Battle-map debris target count must be positive.");
        if (debrisField.MinimumScale < 0f || debrisField.MaximumScale < debrisField.MinimumScale)
        {
            throw new InvalidDataException(
                "Battle-map debris scales must define a nonnegative ascending range."
            );
        }

        return debrisField;
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
    /// Reads a required three-dimensional vector from element attributes.
    /// </summary>
    /// <param name="element">The element containing X, Y, and Z attributes.</param>
    /// <returns>The parsed vector.</returns>
    private static BattleMapVector3 ReadVector(XElement element)
    {
        return new BattleMapVector3
        {
            X = ReadRequiredFloatAttribute(element, "X"),
            Y = ReadRequiredFloatAttribute(element, "Y"),
            Z = ReadRequiredFloatAttribute(element, "Z"),
        };
    }

    /// <summary>
    /// Reads a required linear color from element attributes.
    /// </summary>
    /// <param name="element">The element containing R, G, B, and A attributes.</param>
    /// <returns>The parsed color.</returns>
    private static BattleMapColor ReadColor(XElement element)
    {
        return new BattleMapColor
        {
            Red = ReadRequiredFloatAttribute(element, "R"),
            Green = ReadRequiredFloatAttribute(element, "G"),
            Blue = ReadRequiredFloatAttribute(element, "B"),
            Alpha = ReadRequiredFloatAttribute(element, "A"),
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
    /// Reads a required Boolean attribute.
    /// </summary>
    /// <param name="element">The element containing the attribute.</param>
    /// <param name="name">The required attribute name.</param>
    /// <returns>The parsed Boolean value.</returns>
    private static bool ReadRequiredBoolAttribute(XElement element, string name)
    {
        string value = GetRequiredAttributeValue(element, name);
        if (!bool.TryParse(value, out bool result))
        {
            throw new InvalidDataException(
                $"Attribute '{name}' on element '{element.Name}' must be a Boolean."
            );
        }

        return result;
    }

    /// <summary>
    /// Reads a required invariant floating-point child-element value.
    /// </summary>
    /// <param name="parent">The parent containing the required child element.</param>
    /// <param name="name">The required child-element name.</param>
    /// <returns>The parsed floating-point value.</returns>
    private static float ReadRequiredFloatElement(XElement parent, string name)
    {
        XElement element = GetRequiredElement(parent, name);
        if (
            !float.TryParse(
                element.Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float result
            )
        )
        {
            throw new InvalidDataException($"Element '{name}' must contain a number.");
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
