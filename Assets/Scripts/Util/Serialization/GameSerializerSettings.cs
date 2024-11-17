using System.Xml;

/// <summary>
/// Represents settings for the game serializer.
/// </summary>
public class GameSerializerSettings
{
    public string RootName { get; set; }
    public bool Idendent { get; set; } = true;
    public bool IgnoreComments { get; set; } = true;
    public bool IgnoreWhitespace { get; set; } = true;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public GameSerializerSettings() { }

    /// <summary>
    /// Creates a new instance of the <see cref="XmlWriterSettings"/> class
    /// using the settings provided.
    /// </summary>
    /// <returns>A new instance of the <see cref="XmlWriterSettings"/> class.</returns>
    public XmlWriterSettings CreateWriterSettings()
    {
        return new XmlWriterSettings { Indent = true };
    }

    /// <summary>
    /// Creates a new instance of the <see cref="XmlReaderSettings"/> class
    /// using the settings provided.
    /// </summary>
    /// <returns>A new instance of the <see cref="XmlReaderSettings"/> class.</returns>
    public XmlReaderSettings CreateReaderSettings()
    {
        return new XmlReaderSettings
        {
            IgnoreComments = IgnoreComments,
            IgnoreWhitespace = IgnoreWhitespace,
        };
    }
}
