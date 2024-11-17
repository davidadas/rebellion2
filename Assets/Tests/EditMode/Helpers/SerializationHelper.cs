using System.IO;

public static class SerializationHelper
{
    public static string Serialize<T>(T obj)
    {
        GameSerializer serializer = new GameSerializer(typeof(T));
        using (StringWriter writer = new StringWriter())
        {
            serializer.Serialize(writer, obj);
            return writer.ToString();
        }
    }

    public static string Serialize<T>(T obj, GameSerializerSettings settings)
    {
        GameSerializer serializer = new GameSerializer(typeof(T), settings);
        using (StringWriter writer = new StringWriter())
        {
            serializer.Serialize(writer, obj);
            return writer.ToString();
        }
    }

    public static T Deserialize<T>(string xml)
    {
        GameSerializer serializer = new GameSerializer(typeof(T));
        using (StringReader reader = new StringReader(xml))
        {
            return (T)serializer.Deserialize(reader);
        }
    }

    public static T Deserialize<T>(string xml, GameSerializerSettings settings)
    {
        GameSerializer serializer = new GameSerializer(typeof(T), settings);
        using (StringReader reader = new StringReader(xml))
        {
            return (T)serializer.Deserialize(reader);
        }
    }
}
