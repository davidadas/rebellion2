using System;
using System.IO;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Defines a unified interface for loading all game resources including
/// configs, game data, audio, and video.
/// </summary>
public interface IResourceManager
{
    /// <summary>
    /// Loads and deserializes a strongly-typed configuration object
    /// from the Resources/Configs folder using the custom XML serializer.
    /// </summary>
    /// <typeparam name="T">
    /// The configuration type to load.
    /// </typeparam>
    /// <returns>
    /// A deserialized instance of the configuration object.
    /// </returns>
    T GetConfig<T>()
        where T : class;

    /// <summary>
    /// Loads and deserializes all game data entities of type T
    /// from the Resources/Data folder.
    /// </summary>
    /// <typeparam name="T">
    /// The entity type to load.
    /// </typeparam>
    /// <returns>
    /// An array of deserialized game entities.
    /// </returns>
    T[] GetGameData<T>()
        where T : BaseGameEntity;

    /// <summary>
    /// Loads a single AudioClip from the specified Resources path.
    /// </summary>
    AudioClip GetAudio(string path);

    /// <summary>
    /// Loads multiple AudioClips from the specified resource paths.
    /// </summary>
    AudioClip[] GetAudioSet(string[] paths);

    /// <summary>
    /// Loads a single VideoClip from the specified Resources path.
    /// </summary>
    VideoClip GetVideo(string path);

    /// <summary>
    /// Loads all VideoClips in a specified Resources folder.
    /// </summary>
    VideoClip[] GetVideoGroup(string folderPath);

    /// <summary>
    /// Loads a Sprite from the specified Resources path.
    /// </summary>
    Sprite GetSprite(string path);
}

/// <summary>
/// Concrete implementation of IResourceManager using Unity's
/// Resources system and the custom XML serializer.
/// </summary>
internal class ResourceManagerImpl : IResourceManager
{
    /// <summary>
    /// Loads and deserializes an XML configuration file located
    /// in Resources/Configs using the custom GameSerializer.
    /// The file name must match the type name.
    /// </summary>
    public T GetConfig<T>()
        where T : class
    {
        string typeName = typeof(T).Name;
        string filePath = Path.Combine("Configs", typeName);

        TextAsset asset = Resources.Load<TextAsset>(filePath);

        if (asset == null)
        {
            throw new Exception($"Config not found at: {filePath}");
        }

        var settings = new GameSerializerSettings { RootName = typeName };

        GameSerializer serializer = new GameSerializer(typeof(T), settings);

        using MemoryStream stream = new MemoryStream(asset.bytes);
        object result = serializer.Deserialize(stream);

        if (result == null)
        {
            throw new Exception($"Failed to deserialize config: {typeName}");
        }

        return result as T;
    }

    /// <summary>
    /// Loads and deserializes XML game data from Resources/Data.
    /// The file name must match the pluralized entity type name.
    /// </summary>
    public T[] GetGameData<T>()
        where T : BaseGameEntity
    {
        string pluralizedType = $"{typeof(T).Name}s";
        string filePath = Path.Combine("Data", pluralizedType);

        TextAsset asset = Resources.Load<TextAsset>(filePath);

        if (asset == null)
        {
            throw new Exception($"Game data not found at: {filePath}");
        }

        var settings = new GameSerializerSettings { RootName = pluralizedType };

        GameSerializer serializer = new GameSerializer(typeof(T[]), settings);

        using MemoryStream stream = new MemoryStream(asset.bytes);
        object result = serializer.Deserialize(stream);

        if (result == null)
        {
            throw new Exception($"Failed to deserialize game data: {pluralizedType}");
        }

        return result as T[];
    }

    /// <summary>
    /// Loads a single AudioClip from Resources.
    /// Throws if the asset cannot be found.
    /// </summary>
    public AudioClip GetAudio(string path)
    {
        return LoadResource<AudioClip>(path, "Audio not found at: ");
    }

    /// <summary>
    /// Loads a specific set of AudioClips from the provided resource paths.
    /// </summary>
    public AudioClip[] GetAudioSet(string[] paths)
    {
        if (paths == null || paths.Length == 0)
        {
            throw new Exception("Audio set paths cannot be null or empty.");
        }

        AudioClip[] clips = new AudioClip[paths.Length];

        for (int i = 0; i < paths.Length; i++)
        {
            clips[i] = LoadResource<AudioClip>(paths[i], "Audio not found at: ");
        }

        return clips;
    }

    /// <summary>
    /// Loads a single VideoClip from Resources.
    /// Throws if the asset cannot be found.
    /// </summary>
    public VideoClip GetVideo(string path)
    {
        return LoadResource<VideoClip>(path, "Video not found at: ");
    }

    /// <summary>
    /// Loads all VideoClips located within a Resources folder.
    /// Throws if none are found.
    /// </summary>
    public VideoClip[] GetVideoGroup(string folderPath)
    {
        return LoadResourceGroup<VideoClip>(folderPath, "No videos found in folder: ");
    }

    /// <summary>
    /// Loads a Sprite from Resources.
    /// Throws if the asset cannot be found.
    /// </summary>
    public Sprite GetSprite(string path)
    {
        return LoadResource<Sprite>(path, "Sprite not found at: ");
    }

    /// <summary>
    /// Helper method to load a single Unity resource of type T.
    /// Throws if the resource does not exist.
    /// </summary>
    private T LoadResource<T>(string path, string errorPrefix)
        where T : UnityEngine.Object
    {
        T resource = Resources.Load<T>(path);

        if (resource == null)
        {
            throw new Exception(errorPrefix + path);
        }

        return resource;
    }

    /// <summary>
    /// Helper method to load all Unity resources of type T
    /// from a Resources folder.
    /// Throws if no resources are found.
    /// </summary>
    private T[] LoadResourceGroup<T>(string folderPath, string errorPrefix)
        where T : UnityEngine.Object
    {
        T[] resources = Resources.LoadAll<T>(folderPath);

        if (resources == null || resources.Length == 0)
        {
            throw new Exception(errorPrefix + folderPath);
        }

        return resources;
    }
}

/// <summary>
///
/// </summary>
public static class ResourceManager
{
    private static readonly IResourceManager instance = new ResourceManagerImpl();

    public static IResourceManager Instance
    {
        get { return instance; }
    }
}
