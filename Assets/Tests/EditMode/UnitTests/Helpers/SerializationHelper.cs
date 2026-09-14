using System.IO;
using Rebellion.Util.Serialization;

public static class SerializationHelper
{
    /// <summary>
    /// Serializes the requested operation.
    /// </summary>
    /// <param name="obj">The obj.</param>
    /// <typeparam name="T">The t type.</typeparam>
    /// <returns>The result of serialize.</returns>
    public static string Serialize<T>(T obj)
    {
        GameSerializer serializer = new GameSerializer(typeof(T));
        using (StringWriter writer = new StringWriter())
        {
            serializer.Serialize(writer, obj);
            return writer.ToString();
        }
    }

    /// <summary>
    /// Serializes the requested operation.
    /// </summary>
    /// <param name="obj">The obj.</param>
    /// <param name="settings">The settings.</param>
    /// <typeparam name="T">The t type.</typeparam>
    /// <returns>The result of serialize.</returns>
    public static string Serialize<T>(T obj, GameSerializerSettings settings)
    {
        GameSerializer serializer = new GameSerializer(typeof(T), settings);
        using (StringWriter writer = new StringWriter())
        {
            serializer.Serialize(writer, obj);
            return writer.ToString();
        }
    }

    /// <summary>
    /// Deserializes the requested operation.
    /// </summary>
    /// <param name="xml">The xml.</param>
    /// <typeparam name="T">The t type.</typeparam>
    /// <returns>The result of deserialize.</returns>
    public static T Deserialize<T>(string xml)
    {
        GameSerializer serializer = new GameSerializer(typeof(T));
        using (StringReader reader = new StringReader(xml))
        {
            return (T)serializer.Deserialize(reader);
        }
    }

    /// <summary>
    /// Deserializes the requested operation.
    /// </summary>
    /// <param name="xml">The xml.</param>
    /// <param name="settings">The settings.</param>
    /// <typeparam name="T">The t type.</typeparam>
    /// <returns>The result of deserialize.</returns>
    public static T Deserialize<T>(string xml, GameSerializerSettings settings)
    {
        GameSerializer serializer = new GameSerializer(typeof(T), settings);
        using (StringReader reader = new StringReader(xml))
        {
            return (T)serializer.Deserialize(reader);
        }
    }
}
