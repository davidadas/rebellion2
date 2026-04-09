using System.Xml;
using System.Xml.Schema;

namespace Rebellion.Util.Serialization
{
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
        /// Optional schema set for inline validation during deserialization.
        /// When set, the XmlReader validates against the schema as it reads.
        /// </summary>
        public XmlSchemaSet Schemas { get; set; }

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
            return new XmlWriterSettings { Indent = true, NewLineChars = "\n" };
        }

        /// <summary>
        /// Creates a new instance of the <see cref="XmlReaderSettings"/> class
        /// using the settings provided.
        /// </summary>
        /// <returns>A new instance of the <see cref="XmlReaderSettings"/> class.</returns>
        public XmlReaderSettings CreateReaderSettings()
        {
            XmlReaderSettings settings = new XmlReaderSettings
            {
                IgnoreComments = IgnoreComments,
                IgnoreWhitespace = IgnoreWhitespace,
            };

            if (Schemas != null)
            {
                settings.Schemas = Schemas;
                settings.ValidationType = ValidationType.Schema;
                settings.ValidationEventHandler += (_, args) =>
                {
                    if (args.Severity == XmlSeverityType.Error)
                        throw new XmlSchemaValidationException(args.Message);
                };
            }

            return settings;
        }
    }
}
