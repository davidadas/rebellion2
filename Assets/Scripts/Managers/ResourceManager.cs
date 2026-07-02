using System;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

/// <summary>
/// Loads all game resources including configs, game data, audio, video, and sprites.
/// </summary>
public static class ResourceManager
{
    /// <summary>
    /// Loads and deserializes a strongly-typed configuration object
    /// from the Resources/Configs folder using the custom XML serializer.
    /// If a matching schema file exists at Configs/{TypeName}Schema.xml,
    /// it is applied for inline validation during deserialization.
    /// </summary>
    public static T GetConfig<T>()
        where T : class
    {
        string typeName = typeof(T).Name;
        string filePath = Path.Combine("Configs", typeName);

        UnityEngine.TextAsset asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>(filePath);
        if (asset == null)
            throw new Exception($"Config not found at: {filePath}");

        GameSerializerSettings settings = new GameSerializerSettings { RootName = typeName };

        UnityEngine.TextAsset schemaAsset = UnityEngine.Resources.Load<UnityEngine.TextAsset>(
            Path.Combine("Configs", $"{typeName}Schema")
        );
        if (schemaAsset != null)
        {
            XmlSchemaSet schemas = new XmlSchemaSet();
            schemas.Add(null, XmlReader.Create(new StringReader(schemaAsset.text)));
            settings.Schemas = schemas;
        }

        GameSerializer serializer = new GameSerializer(typeof(T), settings);

        using MemoryStream stream = new MemoryStream(asset.bytes);
        object result = serializer.Deserialize(stream);

        if (result == null)
            throw new Exception($"Failed to deserialize config: {typeName}");

        return (T)result;
    }

    /// <summary>
    /// Loads and deserializes all game data entities of type T
    /// from the Resources/Data folder.
    /// </summary>
    public static T[] GetEntityData<T>()
        where T : BaseGameEntity
    {
        string typeName = typeof(T).Name;
        string pluralizedType = typeName.EndsWith("s") ? typeName : $"{typeName}s";
        string filePath = Path.Combine("Data", pluralizedType);

        UnityEngine.TextAsset asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>(filePath);

        if (asset == null)
        {
            throw new Exception($"Game data not found at: {filePath}");
        }

        GameSerializerSettings settings = new GameSerializerSettings { RootName = pluralizedType };
        GameSerializer serializer = new GameSerializer(typeof(T[]), settings);

        using MemoryStream stream = new MemoryStream(asset.bytes);
        object result = serializer.Deserialize(stream);

        if (result == null)
        {
            throw new Exception($"Failed to deserialize game data: {pluralizedType}");
        }

        return (T[])result;
    }

    /// <summary>
    /// Loads and deserializes a strongly typed data object from the Resources/Data folder.
    /// </summary>
    /// <typeparam name="T">The data object type to load.</typeparam>
    /// <returns>The loaded data object.</returns>
    public static T GetData<T>()
        where T : class
    {
        string typeName = typeof(T).Name;
        string filePath = Path.Combine("Data", typeName);

        UnityEngine.TextAsset asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>(filePath);
        if (asset == null)
            throw new Exception($"Data not found at: {filePath}");

        GameSerializerSettings settings = new GameSerializerSettings { RootName = typeName };
        GameSerializer serializer = new GameSerializer(typeof(T), settings);

        using MemoryStream stream = new MemoryStream(asset.bytes);
        object result = serializer.Deserialize(stream);

        if (result == null)
            throw new Exception($"Failed to deserialize data: {typeName}");

        return (T)result;
    }

    /// <summary>
    /// Loads a single AudioClip from the specified Resources path.
    /// </summary>
    /// <param name="path">Resources path to the audio file.</param>
    /// <returns>The loaded AudioClip.</returns>
    public static UnityEngine.AudioClip GetAudio(string path)
    {
        return LoadResource<UnityEngine.AudioClip>(path, "Audio not found at: ");
    }

    /// <summary>
    /// Loads multiple AudioClips from the specified resource paths.
    /// </summary>
    /// <param name="paths">Array of Resources paths to load.</param>
    /// <returns>Array of loaded AudioClips.</returns>
    public static UnityEngine.AudioClip[] GetAudioSet(string[] paths)
    {
        if (paths == null || paths.Length == 0)
        {
            throw new Exception("Audio set paths cannot be null or empty.");
        }

        UnityEngine.AudioClip[] clips = new UnityEngine.AudioClip[paths.Length];

        for (int i = 0; i < paths.Length; i++)
        {
            clips[i] = LoadResource<UnityEngine.AudioClip>(paths[i], "Audio not found at: ");
        }

        return clips;
    }

    /// <summary>
    /// Loads a single VideoClip from the specified Resources path.
    /// </summary>
    /// <param name="path">Resources path to the video file.</param>
    /// <returns>The loaded VideoClip.</returns>
    public static UnityEngine.Video.VideoClip GetVideo(string path)
    {
        return LoadResource<UnityEngine.Video.VideoClip>(path, "Video not found at: ");
    }

    /// <summary>
    /// Loads all VideoClips in a specified Resources folder.
    /// </summary>
    /// <param name="folderPath">Resources folder path containing video files.</param>
    /// <returns>Array of loaded VideoClips.</returns>
    public static UnityEngine.Video.VideoClip[] GetVideoGroup(string folderPath)
    {
        return LoadResourceGroup<UnityEngine.Video.VideoClip>(
            folderPath,
            "No videos found in folder: "
        );
    }

    /// <summary>
    /// Loads a Sprite from the specified Resources path.
    /// </summary>
    /// <param name="path">Resources path to the sprite.</param>
    /// <returns>The loaded Sprite.</returns>
    public static UnityEngine.Sprite GetSprite(string path)
    {
        return LoadResource<UnityEngine.Sprite>(path, "Sprite not found at: ");
    }

    private static T LoadResource<T>(string path, string errorPrefix)
        where T : UnityEngine.Object
    {
        T resource = string.IsNullOrEmpty(path) ? null : UnityEngine.Resources.Load<T>(path);

        if (resource == null)
        {
            throw new Exception(errorPrefix + path);
        }

        return resource;
    }

    private static T[] LoadResourceGroup<T>(string folderPath, string errorPrefix)
        where T : UnityEngine.Object
    {
        T[] resources = UnityEngine.Resources.LoadAll<T>(folderPath);

        if (resources == null || resources.Length == 0)
        {
            throw new Exception(errorPrefix + folderPath);
        }

        return resources;
    }
}
