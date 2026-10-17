using System;
using System.Globalization;
using System.IO;
using Rebellion.Util.Serialization;

namespace Rebellion.Generation
{
    /// <summary>
    /// Converts launch-specific generation configurations to and from save-safe XML text.
    /// </summary>
    public static class GameGenerationConfigCodec
    {
        /// <summary>
        /// Serializes a complete generation configuration for storage in a game summary.
        /// </summary>
        /// <param name="config">The configuration to serialize.</param>
        /// <returns>The serialized generation configuration.</returns>
        public static string Serialize(GameGenerationConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            GameSerializer serializer = new GameSerializer(typeof(GameGenerationConfig));
            using StringWriter writer = new StringWriter(CultureInfo.InvariantCulture);
            serializer.Serialize(writer, config);
            return writer.ToString();
        }

        /// <summary>
        /// Deserializes a complete launch-specific generation configuration.
        /// </summary>
        /// <param name="xml">The serialized configuration.</param>
        /// <returns>The restored generation configuration.</returns>
        public static GameGenerationConfig Deserialize(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
                throw new ArgumentException(
                    "Generation configuration XML is required.",
                    nameof(xml)
                );

            GameSerializer serializer = new GameSerializer(typeof(GameGenerationConfig));
            using StringReader reader = new StringReader(xml);
            return (GameGenerationConfig)serializer.Deserialize(reader);
        }
    }
}
