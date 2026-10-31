using System;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;

namespace Rebellion.Util.Serialization
{
    /// <summary>
    /// Represents settings for the game serializer.
    /// </summary>
    public class GameSerializerSettings
    {
        /// <summary>
        /// Overrides the written root element name.
        /// </summary>
        public string RootName { get; set; }

        /// <summary>
        /// Optionally reads persisted member values without invoking their usual accessors.
        /// Null uses the member's normal field or property access.
        /// </summary>
        public Func<MemberInfo, object, object> MemberValueProvider { get; set; }

        /// <summary>
        /// Controls formatting whitespace between serialized XML elements.
        /// </summary>
        public bool Idendent { get; set; } = true;

        /// <summary>
        /// Controls line-break encoding within serialized text and attribute values.
        /// </summary>
        public NewLineHandling NewLineHandling { get; set; } = NewLineHandling.Replace;

        public bool IgnoreComments { get; set; } = true;
        public bool IgnoreWhitespace { get; set; } = true;

        /// <summary>
        /// Whether deserialization skips XML elements that have no matching member.
        /// </summary>
        public bool IgnoreUnknownElements { get; set; }

        /// <summary>
        /// Receives the containing type and element name whenever an unknown element is skipped.
        /// </summary>
        public Action<Type, string> UnknownElementSkipped { get; set; }

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
            return new XmlWriterSettings
            {
                Indent = Idendent,
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling,
            };
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
                        throw new XmlSchemaValidationException(args.Message, args.Exception);
                };
            }

            return settings;
        }
    }
}
