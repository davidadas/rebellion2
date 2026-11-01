using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

/// <summary>
/// Reads and writes the plain XML definitions that describe content packs.
/// </summary>
public static class ContentDefinitionSerializer
{
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
        return new XmlSerializer(typeof(T)).Deserialize(reader) as T
            ?? throw new InvalidDataException(
                "Failed to deserialize the edited content definition."
            );
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

        XmlSerializer serializer = new XmlSerializer(typeof(T));
        using FileStream stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );
        return serializer.Deserialize(stream) as T
            ?? throw new InvalidDataException(
                $"Failed to deserialize content definition: {filePath}"
            );
    }

    /// <summary>
    /// Writes one XML definition without serializer namespace noise.
    /// </summary>
    /// <typeparam name="T">The definition type.</typeparam>
    /// <param name="filePath">The absolute destination path.</param>
    /// <param name="definition">The definition to write.</param>
    public static void Serialize<T>(string filePath, T definition)
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
                serializer.Serialize(writer, definition, namespaces);
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
