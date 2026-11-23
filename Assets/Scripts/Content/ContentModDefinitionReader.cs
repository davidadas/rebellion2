using System.IO;
using Rebellion.Util.Serialization;

/// <summary>
/// Reads native add-on manifests for discovery and package installation.
/// </summary>
public static class ContentModDefinitionReader
{
    /// <summary>
    /// Reads the authored add-on manifest without applying compatibility filtering.
    /// </summary>
    /// <param name="path">The manifest path.</param>
    /// <returns>The authored add-on definition.</returns>
    public static ContentModDefinition Read(string path)
    {
        GameSerializer serializer = new GameSerializer(
            typeof(ContentModDefinition),
            new GameSerializerSettings { RootName = nameof(ContentModDefinition) }
        );
        using FileStream stream = File.OpenRead(path);
        return serializer.Deserialize(stream) as ContentModDefinition
            ?? throw new InvalidDataException("Failed to deserialize content mod: " + path);
    }
}
