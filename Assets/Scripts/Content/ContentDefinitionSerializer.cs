using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using PersistableReadAliasAttribute = Rebellion.Util.Serialization.PersistableReadAliasAttribute;

/// <summary>
/// Reads and writes the plain XML definitions that describe content packs.
/// </summary>
public static class ContentDefinitionSerializer
{
    private static readonly string _legacyThemePathElement = typeof(ContentPackDefinition)
        .GetProperty(nameof(ContentPackDefinition.ThemePaths))
        .GetCustomAttribute<PersistableReadAliasAttribute>()
        .Name;

    /// <summary>
    /// Checks whether an authored manifest still stores its Neutral theme in the legacy field.
    /// </summary>
    /// <param name="root">The authored manifest or sparse overlay.</param>
    /// <returns>Whether the deprecated reference is present.</returns>
    public static bool HasLegacyThemePath(XElement root) =>
        root.Element(_legacyThemePathElement) != null;

    /// <summary>
    /// Writes the shared theme list and removes the deprecated scalar from an authored manifest.
    /// </summary>
    /// <param name="root">The authored pack manifest.</param>
    /// <param name="paths">The effective theme paths to preserve.</param>
    /// <returns>Whether the manifest changed.</returns>
    public static bool SetThemePaths(XElement root, IEnumerable<string> paths)
    {
        XElement updated = new(
            nameof(ContentPackDefinition.ThemePaths),
            paths.Select(path => new XElement("Path", path))
        );
        XElement current = root.Element(updated.Name);
        XElement legacy = root.Element(_legacyThemePathElement);
        if (legacy == null && XNode.DeepEquals(current, updated))
            return false;
        legacy?.Remove();
        if (current == null)
            root.Add(updated);
        else
            current.ReplaceWith(updated);
        return true;
    }

    /// <summary>
    /// Reads an edited definition before committing it to disk.
    /// </summary>
    /// <typeparam name="T">The expected definition type.</typeparam>
    /// <param name="text">The working XML definition.</param>
    /// <returns>The deserialized definition.</returns>
    public static T DeserializeText<T>(string text)
        where T : class
    {
        using StringReader reader = new StringReader(text);
        using XmlReader xml = XmlReader.Create(reader);
        return Read<T>(xml);
    }

    /// <summary>
    /// Reads one XML definition from disk.
    /// </summary>
    /// <typeparam name="T">The expected definition type.</typeparam>
    /// <param name="filePath">The absolute XML file path.</param>
    /// <returns>The deserialized definition.</returns>
    public static T Deserialize<T>(string filePath)
        where T : class
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Content definition not found: {filePath}");

        using FileStream stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );
        using XmlReader reader = XmlReader.Create(stream);
        return Read<T>(reader);
    }

    /// <summary>
    /// Reads a definition using its existing model and remembers the shared era manifest format.
    /// </summary>
    /// <typeparam name="T">The expected definition type.</typeparam>
    /// <param name="reader">The XML source.</param>
    /// <returns>The parsed definition.</returns>
    private static T Read<T>(XmlReader reader)
        where T : class
    {
        var serializer = new XmlSerializer(typeof(T));
        reader.MoveToContent();
        object result;
        if (typeof(T) == typeof(ContentPackDefinition))
        {
            bool usesPackageManifest = reader.Name == ContentPackageManifest.RootName;
            XElement root;
            try
            {
                root = (XElement)XNode.ReadFrom(reader);
            }
            catch (XmlException exception)
            {
                throw new InvalidOperationException(
                    "Failed to deserialize the content definition.",
                    exception
                );
            }
            root = ContentPackageManifest.AsCampaignRoot(root);
            var legacyThemePath = (string)root.Element(_legacyThemePathElement);
            using XmlReader normalized = root.CreateReader();
            result = serializer.Deserialize(normalized);
            if (result is ContentPackDefinition era)
            {
                era.UsesPackageManifest = usesPackageManifest;
                era.LegacyNeutralThemePath = legacyThemePath;
            }
        }
        else
            result = serializer.Deserialize(reader);
        return result as T
            ?? throw new InvalidDataException("Failed to deserialize the content definition.");
    }

    /// <summary>
    /// Writes one XML definition without serializer namespace noise.
    /// </summary>
    /// <typeparam name="T">The definition type.</typeparam>
    /// <param name="filePath">The absolute destination path.</param>
    /// <param name="definition">The definition to write.</param>
    /// <param name="usePackageManifest">Whether a new era should use the shared package manifest.</param>
    public static void Serialize<T>(string filePath, T definition, bool usePackageManifest = false)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("A destination path is required.", nameof(filePath));
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        string directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (string.IsNullOrWhiteSpace(directory))
            throw new IOException($"Content definition path has no directory: {filePath}");
        Directory.CreateDirectory(directory);

        string temporaryPath = filePath + ".tmp";
        XmlWriterSettings settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
        };
        XmlSerializerNamespaces namespaces = new XmlSerializerNamespaces();
        namespaces.Add(string.Empty, string.Empty);
        XmlSerializer serializer = new XmlSerializer(typeof(T));
        try
        {
            using (XmlWriter writer = XmlWriter.Create(temporaryPath, settings))
            {
                if (
                    definition is ContentPackDefinition era
                    && (
                        usePackageManifest
                        || era.UsesPackageManifest
                        || era.LegacyNeutralThemePath != null
                    )
                )
                {
                    XDocument document = new();
                    using (XmlWriter buffer = document.CreateWriter())
                        serializer.Serialize(buffer, definition, namespaces);
                    if (usePackageManifest || era.UsesPackageManifest)
                        ContentPackageManifest.SetType(document.Root, ContentPackageType.Era);
                    if (!string.IsNullOrWhiteSpace(era.LegacyNeutralThemePath))
                        SetThemePaths(
                            document.Root,
                            era.ThemePaths.Append(era.LegacyNeutralThemePath)
                                .Distinct(StringComparer.Ordinal)
                        );
                    document.Save(writer);
                }
                else
                    serializer.Serialize(writer, definition, namespaces);
            }
            ReplaceFile(temporaryPath, filePath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    /// <summary>
    /// Moves a completed temporary file into place without exposing a partial definition.
    /// </summary>
    /// <param name="temporaryPath">The complete temporary file.</param>
    /// <param name="destinationPath">The final definition path.</param>
    private static void ReplaceFile(string temporaryPath, string destinationPath)
    {
        if (File.Exists(destinationPath))
            File.Replace(temporaryPath, destinationPath, null);
        else
            File.Move(temporaryPath, destinationPath);
    }
}
