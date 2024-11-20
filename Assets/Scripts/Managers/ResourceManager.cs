using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using UnityEngine;

/// <summary>
///
/// </summary>
public interface IResourceManager
{
    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T GetConfig<T>()
        where T : IConfig;

    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T[] GetGameData<T>()
        where T : BaseGameEntity;
}

/// <summary>
/// The ResourceManagerImpl class is responsible for managing the retrieval of
/// configuration data and scene node data from files in the Resources directory.
/// </summary>
class ResourceManagerImpl : IResourceManager
{
    /// <summary>
    /// Retrieves the configuration data of type T.
    /// </summary>
    /// <typeparam name="T">The type of the configuration data.</typeparam>
    /// <returns>The configuration data of type T.</returns>
    public T GetConfig<T>()
        where T : IConfig
    {
        string configName = typeof(T).ToString();
        string filePath = Path.Combine("Configs", configName);

        string json = Resources.Load<TextAsset>(filePath).text;
        T config = JsonUtility.FromJson<T>(json);

        return config;
    }

    /// <summary>
    /// Retrieves an array of scene node data of type T.
    /// </summary>
    /// <typeparam name="T">The type of the scene node data.</typeparam>
    /// <returns>An array of scene node data of type T.</returns>
    public T[] GetGameData<T>()
        where T : BaseGameEntity
    {
        // Pluralize the type name to form the file path.
        string pluralizedType = $"{typeof(T).Name}s"; // Example: PlanetSystem -> PlanetSystems
        string filePath = Path.Combine("Data", pluralizedType);

        // Load the XML data from the Resources directory.
        TextAsset gameXml = Resources.Load<TextAsset>(filePath);

        // Initialize DataContractSerializer with a custom root name if needed.
        var settings = new GameSerializerSettings { RootName = pluralizedType };
        GameSerializer serializer = new GameSerializer(typeof(T[]), settings);

        // Deserialize the result.
        using (MemoryStream stream = new MemoryStream(gameXml.bytes))
        {
            T[] gameData = serializer.Deserialize(stream) as T[];
            // Immediately serialize the game data back to XML
            using (MemoryStream outputStream = new MemoryStream())
            {
                serializer.Serialize(outputStream, gameData);
                outputStream.Position = 0; // Reset the position to read the stream from the beginning
                string serializedXml = new StreamReader(outputStream).ReadToEnd();
            }
            return gameData;
        }
    }
}

/// <summary>
/// The ResourceManager class is a singleton class that provides a single point of access to the ResourceManagerImpl class.
/// </summary>
public class ResourceManager
{
    private static IResourceManager resourceManager = new ResourceManagerImpl();

    /// <summary>
    ///
    /// </summary>
    public static IResourceManager Instance
    {
        get { return resourceManager; }
    }
}
